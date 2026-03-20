#pragma warning disable OPENAI001

using System.Text;
using System.Text.Json.Nodes;
using Incursa.OpenAI.Agents;
using OpenAI.Audio;

namespace Incursa.OpenAI.Agents.Tests;

/// <summary>Tests for the OpenAI audio adapter and request mapping.</summary>
public sealed class OpenAiAudioTests
{
    /// <summary>Transcription requests map repo-owned settings onto the upstream SDK options and normalize the result using a real microset fixture.</summary>
    /// <intent>Protect the public audio transcription surface from drifting away from the upstream OpenAI SDK.</intent>
    /// <scenario>LIB-OAI-AUDIO-TRANSCRIBE-001</scenario>
    /// <behavior>Transcription requests preserve model, prompt, language, timestamp choices, and normalized response data.</behavior>
    [Fact]
    public async Task TranscribeAsync_MapsRequestAndNormalizesResponse()
    {
        RecordingSdkAudioClient client = new()
        {
            TranscriptionResult = OpenAIAudioModelFactory.AudioTranscription(
                "en",
                TimeSpan.FromSeconds(3),
                "hello world",
                [
                    OpenAIAudioModelFactory.TranscribedWord("hello", TimeSpan.Zero, TimeSpan.FromMilliseconds(400)),
                ],
                [
                    OpenAIAudioModelFactory.TranscribedSegment(0, 0, TimeSpan.Zero, TimeSpan.FromSeconds(3), "hello world", new ReadOnlyMemory<int>([1, 2]), 0, 0.9f, 0, 0.1f),
                ]),
        };
        OpenAiAudioClient adapter = new(new StubAudioClientFactory(client));
        string fixturePath = GetMicrosetFixturePath();
        byte[] expectedBytes = await File.ReadAllBytesAsync(fixturePath);

        await using FileStream audio = File.OpenRead(fixturePath);
        OpenAiAudioTranscriptionResponse response = await adapter.TranscribeAsync(
            audio,
            Path.GetFileName(fixturePath),
            new OpenAiAudioTranscriptionRequest("gpt-4o-transcribe")
            {
                Language = "en",
                Prompt = "medical dictation",
                IncludeWordTimestamps = true,
                IncludeSegmentTimestamps = true,
                IncludeLogProbabilities = true,
            },
            CancellationToken.None);

        Assert.Equal("gpt-4o-transcribe", client.Model);
        Assert.Equal(Path.GetFileName(fixturePath), client.AudioFileName);
        Assert.Equal("medical dictation", client.TranscriptionOptions?.Prompt);
        Assert.Equal("en", client.TranscriptionOptions?.Language);
        Assert.Equal(AudioTimestampGranularities.Word | AudioTimestampGranularities.Segment, client.TranscriptionOptions?.TimestampGranularities);
        Assert.Equal(AudioTranscriptionIncludes.Default | AudioTranscriptionIncludes.Logprobs, client.TranscriptionOptions?.Includes);
        Assert.Equal(expectedBytes, client.AudioBytes);

        Assert.Equal("hello world", response.Text);
        Assert.Equal("en", response.Language);
        Assert.Equal(TimeSpan.FromSeconds(3), response.Duration);
        Assert.Single(response.Words);
        Assert.Single(response.Segments);
        Assert.Equal(TimeSpan.Zero, response.Words[0].StartTime);
        Assert.Equal(TimeSpan.FromSeconds(3), response.Segments[0].EndTime);
        Assert.Equal("hello world", response.Raw["text"]?.GetValue<string>());
    }

    /// <summary>Speech-generation requests map repo-owned settings onto the upstream SDK options and normalize the binary output.</summary>
    /// <intent>Protect the public speech-generation surface from drifting away from the upstream OpenAI SDK.</intent>
    /// <scenario>LIB-OAI-AUDIO-SPEECH-001</scenario>
    /// <behavior>Speech requests preserve model, voice, format, and tuning options while returning audio bytes and format metadata.</behavior>
    [Fact]
    public async Task GenerateSpeechAsync_MapsRequestAndNormalizesResponse()
    {
        RecordingSdkAudioClient client = new()
        {
            GeneratedSpeech = BinaryData.FromBytes([9, 8, 7]),
        };
        OpenAiAudioClient adapter = new(new StubAudioClientFactory(client));

        OpenAiSpeechGenerationResponse response = await adapter.GenerateSpeechAsync(
            new OpenAiSpeechGenerationRequest("gpt-4o-mini-tts", "Hello there", "alloy")
            {
                Format = "wav",
                Instructions = "Speak clearly.",
                SpeedRatio = 1.25f,
            },
            CancellationToken.None);

        Assert.Equal("gpt-4o-mini-tts", client.Model);
        Assert.Equal("Hello there", client.InputText);
        Assert.Equal("alloy", client.Voice?.ToString());
        Assert.Equal("wav", client.SpeechOptions?.ResponseFormat.ToString());
        Assert.Equal("Speak clearly.", client.SpeechOptions?.Instructions);
        Assert.Equal(1.25f, client.SpeechOptions?.SpeedRatio);

        Assert.Equal("wav", response.Format);
        Assert.Equal("audio/wav", response.ContentType);
        Assert.Equal([9, 8, 7], response.Audio.ToArray());
    }

    /// <summary>Empty audio streams are rejected before any SDK client is created.</summary>
    /// <intent>Prevent pointless uploads and clearer caller failures for empty inputs.</intent>
    /// <scenario>LIB-OAI-AUDIO-TRANSCRIBE-002</scenario>
    /// <behavior>Seekable empty streams fail local validation before network work begins.</behavior>
    [Fact]
    public async Task TranscribeAsync_RejectsEmptyStreamBeforeCreatingSdkClient()
    {
        StubAudioClientFactory factory = new(new RecordingSdkAudioClient());
        OpenAiAudioClient adapter = new(factory);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.TranscribeAsync(new MemoryStream(), "clip.wav", new OpenAiAudioTranscriptionRequest("gpt-4o-transcribe"), CancellationToken.None));

        Assert.Contains("must contain at least one byte", exception.Message);
        Assert.Equal(0, factory.CreateCallCount);
    }

    /// <summary>Oversized seekable streams are rejected before upload.</summary>
    /// <intent>Enforce a configurable local size cap instead of relying on remote failures.</intent>
    /// <scenario>LIB-OAI-AUDIO-TRANSCRIBE-003</scenario>
    /// <behavior>Seekable streams larger than the configured maximum fail local validation before network work begins.</behavior>
    [Fact]
    public async Task TranscribeAsync_RejectsSeekableStreamLargerThanConfiguredMaximum()
    {
        StubAudioClientFactory factory = new(new RecordingSdkAudioClient());
        OpenAiAudioClient adapter = new(factory, new OpenAiAudioValidationOptions
        {
            MaxFileSizeBytes = 12,
        });

        byte[] oversizedWave = CreateWaveBytes(new byte[16]);
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.TranscribeAsync(new MemoryStream(oversizedWave), "clip.wav", new OpenAiAudioTranscriptionRequest("gpt-4o-transcribe"), CancellationToken.None));

        Assert.Contains("exceeds the configured maximum", exception.Message);
        Assert.Equal(0, factory.CreateCallCount);
    }

    /// <summary>Unsupported file extensions are rejected before upload.</summary>
    /// <intent>Block clearly invalid input types at the boundary of the public audio API.</intent>
    /// <scenario>LIB-OAI-AUDIO-TRANSCRIBE-004</scenario>
    /// <behavior>Files outside the configured extension allowlist fail local validation before network work begins.</behavior>
    [Fact]
    public async Task TranscribeAsync_RejectsUnsupportedFileExtension()
    {
        StubAudioClientFactory factory = new(new RecordingSdkAudioClient());
        OpenAiAudioClient adapter = new(factory);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.TranscribeAsync(new MemoryStream([1, 2, 3]), "clip.txt", new OpenAiAudioTranscriptionRequest("gpt-4o-transcribe"), CancellationToken.None));

        Assert.Contains("extension '.txt' is not allowed", exception.Message);
        Assert.Equal(0, factory.CreateCallCount);
    }

    /// <summary>Known audio headers are checked before upload when content sniffing is enabled.</summary>
    /// <intent>Catch mismatched or obviously invalid binary data before it reaches the OpenAI API.</intent>
    /// <scenario>LIB-OAI-AUDIO-TRANSCRIBE-005</scenario>
    /// <behavior>Files whose bytes do not match the declared extension fail local validation before network work begins.</behavior>
    [Fact]
    public async Task TranscribeAsync_RejectsKnownHeaderMismatch()
    {
        StubAudioClientFactory factory = new(new RecordingSdkAudioClient());
        OpenAiAudioClient adapter = new(factory);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.TranscribeAsync(new MemoryStream(Encoding.ASCII.GetBytes("not-wave-data")), "clip.wav", new OpenAiAudioTranscriptionRequest("gpt-4o-transcribe"), CancellationToken.None));

        Assert.Contains("does not match the '.wav' file extension", exception.Message);
        Assert.Equal(0, factory.CreateCallCount);
    }

    /// <summary>Non-seekable streams are still supported after local validation.</summary>
    /// <intent>Keep server-side streaming scenarios viable while still performing empty-input and header checks.</intent>
    /// <scenario>LIB-OAI-AUDIO-TRANSCRIBE-006</scenario>
    /// <behavior>Validation peeks the header from non-seekable streams and replays the consumed bytes to the upstream SDK.</behavior>
    [Fact]
    public async Task TranscribeAsync_AllowsNonSeekableValidAudioAndReplaysPeekedBytes()
    {
        RecordingSdkAudioClient client = new()
        {
            TranscriptionResult = OpenAIAudioModelFactory.AudioTranscription("en", TimeSpan.FromSeconds(1), "ok", [], []),
        };
        StubAudioClientFactory factory = new(client);
        OpenAiAudioClient adapter = new(factory, new OpenAiAudioValidationOptions
        {
            MaxFileSizeBytes = 4,
        });

        byte[] waveBytes = CreateWaveBytes([1, 2, 3, 4, 5]);
        NonSeekableReadStream stream = new(waveBytes);

        OpenAiAudioTranscriptionResponse response = await adapter.TranscribeAsync(
            stream,
            "clip.wav",
            new OpenAiAudioTranscriptionRequest("gpt-4o-transcribe"),
            CancellationToken.None);

        Assert.Equal("ok", response.Text);
        Assert.Equal(waveBytes, client.AudioBytes);
        Assert.Equal(1, factory.CreateCallCount);
    }

    private sealed class StubAudioClientFactory : IOpenAiSdkAudioClientFactory
    {
        private readonly RecordingSdkAudioClient client;

        public StubAudioClientFactory(RecordingSdkAudioClient client)
        {
            this.client = client;
        }

        public int CreateCallCount { get; private set; }

        public IOpenAiSdkAudioClient Create(string model)
        {
            CreateCallCount++;
            client.Model = model;
            return client;
        }
    }

    private sealed class RecordingSdkAudioClient : IOpenAiSdkAudioClient
    {
        public string? Model { get; set; }

        public string? AudioFileName { get; private set; }

        public byte[]? AudioBytes { get; private set; }

        public string? InputText { get; private set; }

        public GeneratedSpeechVoice? Voice { get; private set; }

        public AudioTranscriptionOptions? TranscriptionOptions { get; private set; }

        public SpeechGenerationOptions? SpeechOptions { get; private set; }

        public AudioTranscription TranscriptionResult { get; set; } = OpenAIAudioModelFactory.AudioTranscription("default", null, "en", [], []);

        public BinaryData GeneratedSpeech { get; set; } = BinaryData.FromBytes([1]);

        public Task<AudioTranscription> TranscribeAudioAsync(Stream audio, string audioFileName, AudioTranscriptionOptions options, CancellationToken cancellationToken)
        {
            AudioFileName = audioFileName;
            TranscriptionOptions = options;
            using MemoryStream capture = new();
            audio.CopyTo(capture);
            AudioBytes = capture.ToArray();
            return Task.FromResult(TranscriptionResult);
        }

        public Task<BinaryData> GenerateSpeechAsync(string text, GeneratedSpeechVoice voice, SpeechGenerationOptions options, CancellationToken cancellationToken)
        {
            InputText = text;
            Voice = voice;
            SpeechOptions = options;
            return Task.FromResult(GeneratedSpeech);
        }
    }

    private static byte[] CreateWaveBytes(byte[] payload)
    {
        byte[] header =
        [
            (byte)'R', (byte)'I', (byte)'F', (byte)'F',
            0, 0, 0, 0,
            (byte)'W', (byte)'A', (byte)'V', (byte)'E',
        ];

        byte[] result = new byte[header.Length + payload.Length];
        header.CopyTo(result, 0);
        payload.CopyTo(result, header.Length);
        return result;
    }

    private static string GetMicrosetFixturePath()
        => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Audio", "peoples_speech_microset_sample.flac");

    private sealed class NonSeekableReadStream : Stream
    {
        private readonly MemoryStream inner;

        public NonSeekableReadStream(byte[] bytes)
        {
            inner = new MemoryStream(bytes);
        }

        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
            => inner.Flush();

        public override int Read(byte[] buffer, int offset, int count)
            => inner.Read(buffer, offset, count);

        public override int Read(Span<byte> buffer)
            => inner.Read(buffer);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => inner.ReadAsync(buffer, cancellationToken);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => inner.ReadAsync(buffer, offset, count, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
