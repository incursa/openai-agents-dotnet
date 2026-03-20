#pragma warning disable OPENAI001
#pragma warning disable SCME0001

using OpenAI;
using OpenAI.Audio;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace Incursa.OpenAI.Agents;

/// <summary>
/// Calls the OpenAI audio APIs via the official OpenAI .NET SDK.
/// </summary>
public sealed class OpenAiAudioClient : IOpenAiAudioClient
{
    private const int HeaderProbeLength = 64;
    private readonly IOpenAiSdkAudioClientFactory clientFactory;
    private readonly AudioValidationSettings validationSettings;

    /// <summary>Creates an audio client from the provided configured <see cref="HttpClient"/>.</summary>
    public OpenAiAudioClient(HttpClient httpClient, OpenAiAudioValidationOptions? validationOptions = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        clientFactory = new OpenAiSdkAudioClientFactory(httpClient);
        validationSettings = AudioValidationSettings.Create(validationOptions);
    }

    internal OpenAiAudioClient(IOpenAiSdkAudioClientFactory clientFactory, OpenAiAudioValidationOptions? validationOptions = null)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        this.clientFactory = clientFactory;
        validationSettings = AudioValidationSettings.Create(validationOptions);
    }

    /// <inheritdoc />
    public async Task<OpenAiAudioTranscriptionResponse> TranscribeAsync(
        Stream audio,
        string audioFileName,
        OpenAiAudioTranscriptionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audio);
        ArgumentException.ThrowIfNullOrWhiteSpace(audioFileName);
        ArgumentNullException.ThrowIfNull(request);

        Stream validatedAudio = ValidateAudioInput(audio, audioFileName);
        IOpenAiSdkAudioClient client = clientFactory.Create(request.Model);
        AudioTranscriptionOptions options = CreateTranscriptionOptions(request);

        try
        {
            AudioTranscription transcription = await client.TranscribeAudioAsync(validatedAudio, audioFileName, options, cancellationToken).ConfigureAwait(false);
            return MapTranscription(transcription);
        }
        catch (ClientResultException ex)
        {
            throw CreateDetailedException("transcribe audio", ex);
        }
    }

    /// <inheritdoc />
    public async Task<OpenAiSpeechGenerationResponse> GenerateSpeechAsync(OpenAiSpeechGenerationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        IOpenAiSdkAudioClient client = clientFactory.Create(request.Model);
        SpeechGenerationOptions options = CreateSpeechOptions(request);

        try
        {
            BinaryData audio = await client.GenerateSpeechAsync(request.Input, CreateVoice(request.Voice), options, cancellationToken).ConfigureAwait(false);
            string format = request.Format ?? "mp3";
            return new OpenAiSpeechGenerationResponse(audio, format, GetContentType(format));
        }
        catch (ClientResultException ex)
        {
            throw CreateDetailedException("generate speech", ex);
        }
    }

    private Stream ValidateAudioInput(Stream audio, string audioFileName)
    {
        string extension = GetValidatedExtension(audioFileName);
        return audio.CanSeek
            ? ValidateSeekableAudio(audio, extension)
            : ValidateNonSeekableAudio(audio, extension);
    }

    private string GetValidatedExtension(string audioFileName)
    {
        string extension = Path.GetExtension(audioFileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new InvalidOperationException("Audio file name must include a supported extension.");
        }

        extension = extension.ToLowerInvariant();
        if (!validationSettings.AllowedFileExtensions.Contains(extension))
        {
            throw new InvalidOperationException($"Audio file extension '{extension}' is not allowed. Allowed extensions: {string.Join(", ", validationSettings.AllowedFileExtensions.OrderBy(value => value, StringComparer.Ordinal))}.");
        }

        return extension;
    }

    private Stream ValidateSeekableAudio(Stream audio, string extension)
    {
        long originalPosition = audio.Position;
        long remainingLength = audio.Length - originalPosition;
        if (remainingLength <= 0)
        {
            throw new InvalidOperationException("Audio stream must contain at least one byte.");
        }

        if (validationSettings.MaxFileSizeBytes is long maxFileSizeBytes && remainingLength > maxFileSizeBytes)
        {
            throw new InvalidOperationException($"Audio stream is {remainingLength} bytes, which exceeds the configured maximum of {maxFileSizeBytes} bytes.");
        }

        byte[] header = ReadPrefix(audio, HeaderProbeLength);
        audio.Position = originalPosition;

        ValidateSniffedContent(extension, header);
        return audio;
    }

    private Stream ValidateNonSeekableAudio(Stream audio, string extension)
    {
        byte[] prefix = ReadPrefix(audio, HeaderProbeLength);
        if (prefix.Length == 0)
        {
            throw new InvalidOperationException("Audio stream must contain at least one byte.");
        }

        ValidateSniffedContent(extension, prefix);
        return new PrefixedReadStream(prefix, audio);
    }

    private void ValidateSniffedContent(string extension, byte[] header)
    {
        if (!validationSettings.EnableContentSniffing)
        {
            return;
        }

        if (!MatchesKnownHeader(extension, header))
        {
            throw new InvalidOperationException($"Audio content does not match the '{extension}' file extension.");
        }
    }

    private static AudioTranscriptionOptions CreateTranscriptionOptions(OpenAiAudioTranscriptionRequest request)
    {
        AudioTranscriptionOptions options = new();

        if (!string.IsNullOrWhiteSpace(request.Prompt))
        {
            options.Prompt = request.Prompt;
        }

        if (!string.IsNullOrWhiteSpace(request.Language))
        {
            options.Language = request.Language;
        }

        AudioTimestampGranularities timestampGranularities = AudioTimestampGranularities.Default;
        if (request.IncludeWordTimestamps)
        {
            timestampGranularities |= AudioTimestampGranularities.Word;
        }

        if (request.IncludeSegmentTimestamps)
        {
            timestampGranularities |= AudioTimestampGranularities.Segment;
        }

        options.TimestampGranularities = timestampGranularities;
        options.Includes = request.IncludeLogProbabilities
            ? AudioTranscriptionIncludes.Default | AudioTranscriptionIncludes.Logprobs
            : AudioTranscriptionIncludes.Default;

        return options;
    }

    private static SpeechGenerationOptions CreateSpeechOptions(OpenAiSpeechGenerationRequest request)
    {
        SpeechGenerationOptions options = new();

        if (!string.IsNullOrWhiteSpace(request.Format))
        {
            options.ResponseFormat = CreateFormat(request.Format);
        }

        if (!string.IsNullOrWhiteSpace(request.Instructions))
        {
            options.Instructions = request.Instructions;
        }

        if (request.SpeedRatio is float speedRatio)
        {
            options.SpeedRatio = speedRatio;
        }

        return options;
    }

    private static GeneratedSpeechVoice CreateVoice(string voice)
        => new(voice);

    private static GeneratedSpeechFormat CreateFormat(string format)
        => new(format);

    private static OpenAiAudioTranscriptionResponse MapTranscription(AudioTranscription transcription)
    {
        IReadOnlyList<OpenAiTranscribedWord> words = transcription.Words?
            .Select(word => new OpenAiTranscribedWord(word.StartTime, word.EndTime))
            .ToArray()
            ?? [];

        IReadOnlyList<OpenAiTranscribedSegment> segments = transcription.Segments?
            .Select(segment => new OpenAiTranscribedSegment(
                segment.StartTime,
                segment.EndTime,
                segment.SeekOffset,
                segment.TokenIds.ToArray(),
                segment.AverageLogProbability,
                segment.NoSpeechProbability))
            .ToArray()
            ?? [];

        return new OpenAiAudioTranscriptionResponse(
            transcription.Text,
            transcription.Language,
            transcription.Duration,
            words,
            segments,
            OpenAiSdkSerialization.ToJsonObject(transcription));
    }

    private static byte[] ReadPrefix(Stream audio, int count)
    {
        byte[] buffer = new byte[count];
        int totalRead = 0;
        while (totalRead < count)
        {
            int bytesRead = audio.Read(buffer, totalRead, count - totalRead);
            if (bytesRead == 0)
            {
                break;
            }

            totalRead += bytesRead;
        }

        return totalRead == buffer.Length
            ? buffer
            : buffer[..totalRead];
    }

    private static bool MatchesKnownHeader(string extension, ReadOnlySpan<byte> header)
        => extension switch
        {
            ".flac" => MatchesAscii(header, "fLaC"),
            ".m4a" => MatchesIsoBaseMediaHeader(header),
            ".mp3" => MatchesMp3Header(header),
            ".ogg" => MatchesAscii(header, "OggS"),
            ".wav" => MatchesWaveHeader(header),
            ".webm" => header.Length >= 4 && header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3,
            _ => true,
        };

    private static bool MatchesAscii(ReadOnlySpan<byte> header, string value)
    {
        byte[] expected = Encoding.ASCII.GetBytes(value);
        return header.Length >= expected.Length && header[..expected.Length].SequenceEqual(expected);
    }

    private static bool MatchesIsoBaseMediaHeader(ReadOnlySpan<byte> header)
        => header.Length >= 12
            && header[4] == (byte)'f'
            && header[5] == (byte)'t'
            && header[6] == (byte)'y'
            && header[7] == (byte)'p';

    private static bool MatchesMp3Header(ReadOnlySpan<byte> header)
        => MatchesAscii(header, "ID3")
            || (header.Length >= 2 && header[0] == 0xFF && (header[1] & 0xE0) == 0xE0);

    private static bool MatchesWaveHeader(ReadOnlySpan<byte> header)
    {
        if (header.Length < 12)
        {
            return false;
        }

        bool riff = header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F';
        bool rf64 = header[0] == (byte)'R' && header[1] == (byte)'F' && header[2] == (byte)'6' && header[3] == (byte)'4';
        bool wave = header[8] == (byte)'W' && header[9] == (byte)'A' && header[10] == (byte)'V' && header[11] == (byte)'E';
        return (riff || rf64) && wave;
    }

    private static string GetContentType(string format)
        => format.ToLowerInvariant() switch
        {
            "aac" => "audio/aac",
            "flac" => "audio/flac",
            "opus" => "audio/ogg",
            "pcm" => "audio/pcm",
            "wav" => "audio/wav",
            _ => "audio/mpeg",
        };

    private static Exception CreateDetailedException(string operation, ClientResultException exception)
    {
        string? body = null;
        try
        {
            body = exception.GetRawResponse()?.Content?.ToString();
        }
        catch
        {
        }

        string message = body is null
            ? $"OpenAI audio API failed to {operation}: {exception.Message}"
            : $"OpenAI audio API failed to {operation}: {exception.Message}{Environment.NewLine}Response body: {body}";

        return new InvalidOperationException(message, exception);
    }

    private sealed class AudioValidationSettings
    {
        private AudioValidationSettings(HashSet<string> allowedFileExtensions, long? maxFileSizeBytes, bool enableContentSniffing)
        {
            AllowedFileExtensions = allowedFileExtensions;
            MaxFileSizeBytes = maxFileSizeBytes;
            EnableContentSniffing = enableContentSniffing;
        }

        public HashSet<string> AllowedFileExtensions { get; }

        public long? MaxFileSizeBytes { get; }

        public bool EnableContentSniffing { get; }

        public static AudioValidationSettings Create(OpenAiAudioValidationOptions? options)
        {
            options ??= new OpenAiAudioValidationOptions();
            if (options.MaxFileSizeBytes is <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options.MaxFileSizeBytes), "MaxFileSizeBytes must be greater than zero when specified.");
            }

            IEnumerable<string> configuredExtensions = options.AllowedFileExtensions ?? [];
            HashSet<string> allowedFileExtensions = new(
                configuredExtensions
                    .Where(extension => !string.IsNullOrWhiteSpace(extension))
                    .Select(extension => extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}"),
                StringComparer.OrdinalIgnoreCase);

            if (allowedFileExtensions.Count == 0)
            {
                throw new InvalidOperationException("At least one allowed audio file extension must be configured.");
            }

            return new AudioValidationSettings(allowedFileExtensions, options.MaxFileSizeBytes, options.EnableContentSniffing);
        }
    }

    private sealed class PrefixedReadStream : Stream
    {
        private readonly byte[] prefix;
        private readonly Stream inner;
        private int prefixOffset;

        public PrefixedReadStream(byte[] prefix, Stream inner)
        {
            this.prefix = prefix;
            this.inner = inner;
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
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            return Read(buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            int copied = CopyPrefix(buffer);
            if (copied == buffer.Length)
            {
                return copied;
            }

            return copied + inner.Read(buffer[copied..]);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int copied = CopyPrefix(buffer.Span);
            if (copied == buffer.Length)
            {
                return ValueTask.FromResult(copied);
            }

            return ReadRemainingAsync(buffer, copied, cancellationToken);
        }

        private async ValueTask<int> ReadRemainingAsync(Memory<byte> buffer, int copied, CancellationToken cancellationToken)
            => copied + await inner.ReadAsync(buffer[copied..], cancellationToken).ConfigureAwait(false);

        private int CopyPrefix(Span<byte> destination)
        {
            int remainingPrefix = prefix.Length - prefixOffset;
            if (remainingPrefix <= 0)
            {
                return 0;
            }

            int bytesToCopy = Math.Min(destination.Length, remainingPrefix);
            prefix.AsSpan(prefixOffset, bytesToCopy).CopyTo(destination);
            prefixOffset += bytesToCopy;
            return bytesToCopy;
        }

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

internal interface IOpenAiSdkAudioClientFactory
{
    IOpenAiSdkAudioClient Create(string model);
}

internal interface IOpenAiSdkAudioClient
{
    Task<AudioTranscription> TranscribeAudioAsync(Stream audio, string audioFileName, AudioTranscriptionOptions options, CancellationToken cancellationToken);

    Task<BinaryData> GenerateSpeechAsync(string text, GeneratedSpeechVoice voice, SpeechGenerationOptions options, CancellationToken cancellationToken);
}

internal sealed class OpenAiSdkAudioClientFactory : IOpenAiSdkAudioClientFactory
{
    private readonly HttpClient httpClient;

    public OpenAiSdkAudioClientFactory(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        this.httpClient = httpClient;
    }

    public IOpenAiSdkAudioClient Create(string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        OpenAIClientOptions options = new()
        {
            Endpoint = httpClient.BaseAddress ?? new Uri("https://api.openai.com/v1"),
            Transport = new HttpClientPipelineTransport(httpClient),
        };

        AuthenticationHeaderValue? authorization = httpClient.DefaultRequestHeaders.Authorization;
        if (authorization is null || !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(authorization.Parameter))
        {
            throw new InvalidOperationException("OpenAiAudioClient requires an HttpClient with a bearer Authorization header.");
        }

        return new SdkAudioClientAdapter(new AudioClient(model, new ApiKeyCredential(authorization.Parameter), options));
    }

    private sealed class SdkAudioClientAdapter : IOpenAiSdkAudioClient
    {
        private readonly AudioClient client;

        public SdkAudioClientAdapter(AudioClient client)
        {
            this.client = client;
        }

        public async Task<AudioTranscription> TranscribeAudioAsync(Stream audio, string audioFileName, AudioTranscriptionOptions options, CancellationToken cancellationToken)
            => (await client.TranscribeAudioAsync(audio, audioFileName, options, cancellationToken).ConfigureAwait(false)).Value;

        public async Task<BinaryData> GenerateSpeechAsync(string text, GeneratedSpeechVoice voice, SpeechGenerationOptions options, CancellationToken cancellationToken)
            => (await client.GenerateSpeechAsync(text, voice, options, cancellationToken).ConfigureAwait(false)).Value;
    }
}
