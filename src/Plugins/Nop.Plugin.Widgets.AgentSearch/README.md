# Nop.Plugin.Widgets.AgentSearch

Exposes your existing Azure AI Search index (indexed by `Nop.Plugin.Search.AzureAI`) to AI agents via `POST /api/agent/search`.

## Configuration & Setup

1. **Solution Integration**: The plugin is located at `src/Plugins/Nop.Plugin.Widgets.AgentSearch` and is registered in `src/NopCommerce.sln`.
2. **Framework & Dependencies**: Built for `.NET 10.0` (`net10.0`) and uses `Azure.Search.Documents` version `11.7.0` matching `Nop.Plugin.Search.AzureAI` and `Nop.Plugin.Widgets.AiChatbot`.
3. **Index Schema Alignment**: Pre-configured to query the product index populated by `Nop.Plugin.Search.AzureAI`, mapping fields `id`, `name`, `shortDescription`, `price`, `published`, `categoryNames`, and `manufacturerNames`.
4. **App Settings Configuration**: Add or verify the `AzureSearch` section in `appsettings.json`:
   ```json
   "AzureSearch": {
     "ServiceEndpoint": "https://<your-service>.search.windows.net",
     "IndexName": "products",
     "ApiKey": "<query-key>"
   }
   ```
   *Note: Use a query key rather than an admin key since this endpoint only performs searches.*
5. **Installation**: Build the solution and install the plugin via nopCommerce Admin > Configuration > Local Plugins.
6. **Testing the Endpoint**:
   ```bash
   curl -X POST https://rosecottagecroft.co.uk/api/agent/search \
     -H "Content-Type: application/json" \
     -d '{"query": "wool jumper under 60", "max_results": 5}'
   ```

## Key Configuration Options & Stubs

- **Query Understanding (`PassthroughQueryUnderstandingService`)**: Currently defaults to a pass-through service. Enable `UseQueryUnderstanding` in plugin settings once a LLM constraint-extraction service (e.g. using Azure OpenAI via `SeoEnhancements` / `AiChatbot`) is wired up.
- **API Key Authentication**: Controlled via `RequireApiKey` and `ApiKey` in plugin settings. When enabled, clients must provide the key via the `X-Api-Key` HTTP header.
- **Published Filtering**: Automatically enforces `published eq true` so unlisted products are hidden from search results.

## Agent Discoverability

The plugin automatically serves discovery endpoints for autonomous AI agents:

1. **API Catalog (`GET /.well-known/api-catalog`)**:
   Standard IETF linkset declaring the `service-desc` for the Agent Search API.
2. **OpenAPI Specification (`GET /.well-known/openapi-agent-search.json`)**:
   OpenAPI 3.0 specification defining the `/api/agent/search` endpoint schema, parameters (`query`, `max_results`, `filters`), and response payloads.

