# Incursa.OpenAI.Agents.Fuzz

This project contains the SharpFuzz harnesses for parser-facing and wire-facing `Incursa.OpenAI.Agents` code.

## Purpose

- Feed arbitrary byte sequences into the OpenAI Responses item mapping helpers.
- Exercise streamed-response helper methods against malformed and unusual payloads.
- Exercise streamable MCP discovery and tool-call handling against arbitrary response bodies.
- Fail fast on unexpected exceptions while allowing ordinary rejection paths.

## Corpus

Checked-in seed inputs live in [corpus/](corpus/README.md). Run the full corpus with [``scripts/quality/run-fuzz-corpus.ps1``](../scripts/quality/run-fuzz-corpus.ps1).

## Build

```powershell
dotnet build fuzz/Incursa.OpenAI.Agents.Fuzz.csproj -c Release
```

## Tooling

Run `dotnet tool restore` from the repository root to make the SharpFuzz command-line driver available.
