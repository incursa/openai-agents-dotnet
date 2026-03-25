# Incursa.OpenAI.Agents.Storage.S3

AWS S3-backed session storage for `Incursa.OpenAI.Agents`.

Included in this package:
- [`S3AgentSessionStore`](https://github.com/incursa/openai-agents-dotnet/blob/main/src/Incursa.OpenAI.Agents.Storage.S3/S3AgentSessionStore.cs) for durable S3 session persistence with optimistic concurrency
- [`S3AgentSessionStoreOptions`](https://github.com/incursa/openai-agents-dotnet/blob/main/src/Incursa.OpenAI.Agents.Storage.S3/S3AgentSessionStoreOptions.cs) for bucket, prefix, client, and retention configuration
- [`AddS3AgentSessions()`](https://github.com/incursa/openai-agents-dotnet/blob/main/src/Incursa.OpenAI.Agents.Storage.S3/S3AgentSessionServiceCollectionExtensions.cs) for DI registration
