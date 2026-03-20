# Fixture Notice

This directory contains a single extracted audio sample from the MLCommons `peoples_speech` microset:

- File: `peoples_speech_microset_sample.flac`
- Source dataset: https://huggingface.co/datasets/MLCommons/peoples_speech
- Source row id: `07282016HFUUforum_SLASH_07-28-2016_HFUUforum_DOT_mp3_00000.flac`
- Transcript: `i wanted this to share a few things but i'm going to not share as much as i wanted to share because we are starting late i'd like to get this thing going so we all get home at a decent hour this this election is very important to`

The dataset card states that the dataset is licensed for academic and commercial usage under `CC-BY-SA` and `CC-BY 4.0`. This fixture is redistributed only for test purposes and should retain the same upstream attribution and license context.

If the fixture needs to be regenerated, run:

```powershell
python scripts/data/import-peoples-speech-microset-fixture.py --output tests/Incursa.OpenAI.Agents.Tests/Fixtures/Audio/peoples_speech_microset_sample.flac
```
