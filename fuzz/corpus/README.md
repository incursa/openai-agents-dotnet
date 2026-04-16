# Fuzz Corpus

This directory contains the checked-in seed corpus for `Incursa.OpenAI.Agents.Fuzz`.

## Layout

- `openai/` covers the OpenAI Responses branch of the harness.
- `mcp/` covers the streamable MCP branch and the HTTP status variants.

## Seed Format

Each seed is a UTF-8 text file.

- The first character controls harness routing.
- Even first-byte values exercise the OpenAI Responses path.
- Odd first-byte values exercise the MCP path.
- `Q` exercises the request-mapper path.

For OpenAI seeds, an optional second character can force the response-item shape:

- `M` -> message output
- `F` -> function call with valid JSON arguments
- `B` -> function call with malformed arguments
- `R` -> reasoning item
- `A` -> MCP approval request
- `L` -> MCP tool-list item

For request-mapper seeds, the second character selects the request shape:

- `D` -> large finite `double` model-setting serialization
- `H` -> hosted MCP mapping with only a server label
- `U` -> raw `JsonPatch` fallback for uncommon `JsonValue` primitives

For the MCP seeds, the first character also selects the mock HTTP status:

- `1` -> `200 OK`
- `3` -> `401 Unauthorized`
- `5` -> `503 Service Unavailable`
- `7` -> `502 Bad Gateway`

## Run

Execute the corpus with:

```powershell
pwsh -File scripts/quality/run-fuzz-corpus.ps1
```
