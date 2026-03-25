# Incursa.OpenAI.Agents.Storage.Azure

Azure Blob-backed session storage for `Incursa.OpenAI.Agents`.

Included in this package:
- [`AzureAgentSessionStore`](https://github.com/incursa/openai-agents-dotnet/blob/main/src/Incursa.OpenAI.Agents.Storage.Azure/AzureAgentSessionStore.cs) for durable Azure Blob session persistence with optimistic concurrency
- [`AzureAgentSessionStoreOptions`](https://github.com/incursa/openai-agents-dotnet/blob/main/src/Incursa.OpenAI.Agents.Storage.Azure/AzureAgentSessionStoreOptions.cs) for connection, container, prefix, and retention configuration
- [`AddAzureAgentSessions()`](https://github.com/incursa/openai-agents-dotnet/blob/main/src/Incursa.OpenAI.Agents.Storage.Azure/AzureAgentSessionServiceCollectionExtensions.cs) for DI registration
