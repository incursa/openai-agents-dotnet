---
workbench:
  type: doc
  workItems: []
  codeRefs: []
  pathHistory: []
  path: /docs/quality/repo-scope-boundary.md
---

# Repository Scope Boundary

Quality governance in this repo applies to the included .NET surface described in [``docs/parity/manifest.md``](../parity/manifest.md):

- [`Incursa.OpenAI.Agents`](../../src/Incursa.OpenAI.Agents/README.md)
- [`Incursa.OpenAI.Agents.Extensions`](../../src/Incursa.OpenAI.Agents.Extensions/README.md)

The storage adapter packages are separate public release surfaces and are governed through their own public API baselines and integration tests:

- [`Incursa.OpenAI.Agents.Storage.Azure`](../../src/Incursa.OpenAI.Agents.Storage.Azure/README.md)
- [`Incursa.OpenAI.Agents.Storage.S3`](../../src/Incursa.OpenAI.Agents.Storage.S3/README.md)

Out of scope for the conformance matrix:

- sample applications
- excluded Python SDK areas that this repo does not translate
- local developer convenience files that do not affect public library behavior
