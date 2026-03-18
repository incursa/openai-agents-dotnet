# Codex Translation Notes

- This automation is translating only the behavior introduced in an upstream diff from openai-agents-python.
- Preserve the existing .NET structure and avoid unrelated refactors unless the diff explicitly requires new helpers.
- Tests should only change when upstream behavior changes or new behaviors require coverage.
- Keep modifications minimal and frame them so reviewers can trace them directly to the Python changes.

Include these notes in the Codex prompt to reinforce the existing instructions in AGENTS.md.
