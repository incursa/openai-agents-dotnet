# Benchmarks

This directory contains permanent BenchmarkDotNet suites for `Incursa.OpenAI.Agents`.

## Included Suites

- `OpenAiResponsesMappingBenchmarks`
- `StreamableMcpClientBenchmarks`

## Run

```powershell
dotnet run -c Release --project benchmarks/Incursa.OpenAI.Agents.Benchmarks.csproj -- --job Dry --filter "*OpenAiResponsesMappingBenchmarks*"
dotnet run -c Release --project benchmarks/Incursa.OpenAI.Agents.Benchmarks.csproj -- --job Dry --filter "*StreamableMcpClientBenchmarks*"
```

Use `--filter` to narrow to a subset of benchmarks when iterating locally.
