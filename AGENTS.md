# openai-agents-dotnet translation guidance

- This repository hosts the semantic .NET translation of https://github.com/openai/openai-agents-python.
- Treat the upstream Python repo as the source of truth for behavior; port only those behaviors that the upstream diff actually introduces.
- Preserve the established .NET architecture (namespaces, layering, and conventions) already present in this project, and avoid unrelated refactors.
- Update .NET tests only when they are needed to secure new or changed behavior from upstream, and keep those changes scoped to the translated files.
- In Markdown docs, link concrete references such as classes, interfaces, commands, files, folders, schemas, requirements/specs, and generated artifacts to the most specific stable target available instead of leaving them as bare code spans.
- Use repository-relative links for Markdown that lives in the repo. Use absolute URLs only for Markdown that is expected to render outside the repo, such as NuGet or other package-facing documentation.
- When the linked label should stay in code style, put the backticks inside the link text, for example [`Bar`](../path/to/Bar.cs).
- Keep every change minimal and reviewable, with clear linkage to the upstream commit summary and diff
