# Incursa.OpenAI.Agents.Storage.S3

AWS S3-backed session storage for `Incursa.OpenAI.Agents`.

Included in this package:
- `S3AgentSessionStore` for durable S3 session persistence with optimistic concurrency
- `S3AgentSessionStoreOptions` for bucket, prefix, client, and retention configuration
- `AddS3AgentSessions()` for DI registration
