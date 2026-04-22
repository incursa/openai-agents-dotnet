# MCP Specification

## Scope
This specification covers MCP-related APIs in `Incursa.OpenAI.Agents.Mcp`:

- auth context and auth resolver contracts
- tool filtering and metadata resolution
- hosted MCP tool definitions
- streamable HTTP MCP client, factory, caching, retry, and error classification
- MCP observation hooks

## Requirements
- `LIB-MCP-API-001`: Public MCP types are tracked by the public API baselines for `Incursa.OpenAI.Agents`.
- `LIB-MCP-AUTH-001`: Streamable MCP requests apply resolver-provided bearer tokens and dynamic headers on each request.
- `LIB-MCP-META-001`: Streamable MCP tool calls inject resolver-provided metadata into request payloads.
- `LIB-MCP-FILTER-001`: Dynamic tool filters can hide tools and filter failures do not leak blocked tools into the final tool list.
- `LIB-MCP-CACHE-001`: Tool discovery is cached when `CacheToolsList` is enabled.
- `LIB-MCP-RESOURCES-001`: Streamable MCP clients round-trip resource cursors and URIs for resource listing, template listing, and resource reads while returning typed protocol payloads.
- `LIB-MCP-ERROR-001`: JSON-RPC server failures surface as `McpServerException` with useful error details.
- `LIB-MCP-RETRY-001`: Transient MCP failures honor retry settings and emit retry/success observations.
- `LIB-MCP-AUTHFAIL-001`: Authentication failures do not retry and surface as `McpAuthenticationException`.
