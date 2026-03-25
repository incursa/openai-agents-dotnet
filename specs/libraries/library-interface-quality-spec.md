# Library Interface Quality Specification

## Meta
- Scope: Cross-library hardening and traceability for the public packages under [``src/``](../../src/)
- Status: Active
- Last Updated: 2026-03-17
- Scope Owner: AI Agents library maintainers

## Purpose
This specification defines the minimum production-readiness contract for the repo:

- public API changes are intentionally tracked
- public behavior is explicitly specified
- tests and quality lanes are traceable to authored scenarios
- coverage, mutation, and Workbench evidence are wired into the repo lifecycle

All requirements use stable scenario IDs (`LIB-*`) and are traceable via [``specs/libraries/library-conformance-matrix.md``](library-conformance-matrix.md).

## Governance Requirements
- `LIB-GOV-SPEC-001`: Every major public domain has an authored specification with stable `LIB-*` identifiers.
- `LIB-GOV-MATRIX-001`: Every authored `LIB-*` identifier appears exactly once in the library conformance matrix.
- `LIB-GOV-API-001`: Every public package has committed `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` baselines enforced by the Microsoft public API analyzer.
- `LIB-GOV-API-002`: Every covered public API scenario maps to both a public API baseline file and at least one automated test artifact.
- `LIB-GOV-TESTDOCS-001`: Test documentation generation and analyzer enforcement are available for the maintained test suite.
- `LIB-GOV-COV-001`: Coverage gate automation exists for each public package.
- `LIB-GOV-MUT-001`: Mutation automation exists for each public package, with thresholds tracked explicitly.
- `LIB-GOV-WORKBENCH-001`: Workbench quality evidence can normalize advisory results and coverage from the maintained contract files.

## Domain Requirements
- `LIB-COMP-API-001`: Agent composition APIs are intentionally public and covered by composition-focused tests.
- `LIB-EXEC-API-001`: Agent execution, run-state, approval, guardrail, and session APIs are intentionally public and covered by runtime tests.
- `LIB-EXT-API-001`: Extensions registration, options, and observability APIs are intentionally public and covered by integration-focused tests.
- `LIB-MCP-API-001`: MCP auth, filtering, transport, and error APIs are intentionally public and covered by MCP-focused tests.
- `LIB-OAI-API-001`: OpenAI Responses adapter and streaming APIs are intentionally public and covered by adapter-focused tests.
