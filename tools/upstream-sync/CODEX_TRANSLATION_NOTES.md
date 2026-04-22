# Codex Translation Notes

- The upstream Python repository remains the source of truth for included behavior in this .NET translation.
- The upstream JavaScript repository is a supporting signal only; use it to clarify intent or coverage, not to override Python.
- Triage and apply are separate phases. Phase 1 writes only run-local artifacts, and phase 2 applies the tracked repo changes.
- Preserve the existing .NET structure and avoid unrelated refactors unless the upstream delta explicitly requires new helpers.
- Tests should change only when upstream behavior changes or when a translated scenario needs coverage.
- Keep modifications minimal and frame them so reviewers can trace them directly to the upstream commit ranges.
