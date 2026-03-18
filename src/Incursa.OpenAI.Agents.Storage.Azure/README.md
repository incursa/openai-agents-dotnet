# Incursa.OpenAI.Agents.Storage.Azure

Azure Blob-backed session storage for `Incursa.OpenAI.Agents`.

Included in this package:
- `AzureAgentSessionStore` for durable Azure Blob session persistence with optimistic concurrency
- `AzureAgentSessionStoreOptions` for connection, container, prefix, and retention configuration
- `AddAzureAgentSessions()` for DI registration
