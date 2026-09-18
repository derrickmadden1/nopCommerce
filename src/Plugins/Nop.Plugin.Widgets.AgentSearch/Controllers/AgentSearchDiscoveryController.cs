using Microsoft.AspNetCore.Mvc;

namespace Nop.Plugin.Widgets.AgentSearch.Controllers
{
    [ApiController]
    public class AgentSearchDiscoveryController : ControllerBase
    {
        /// <summary>
        /// Serves standard /.well-known/api-catalog endpoint so AI agents can discover the endpoint.
        /// </summary>
        [HttpGet(".well-known/api-catalog")]
        [Produces("application/json")]
        public IActionResult GetApiCatalog()
        {
            var host = $"{Request.Scheme}://{Request.Host}";
            var catalog = new
            {
                linkset = new[]
                {
                    new
                    {
                        anchor = $"{host}/api/agent/search",
                        rel = "service-desc",
                        href = $"{host}/.well-known/openapi-agent-search.json",
                        type = "application/openapi+json"
                    }
                }
            };
            return Ok(catalog);
        }

        /// <summary>
        /// Serves the OpenAPI 3.0 specification for /api/agent/search.
        /// </summary>
        [HttpGet(".well-known/openapi-agent-search.json")]
        [Produces("application/json")]
        public IActionResult GetOpenApiSpec()
        {
            var host = $"{Request.Scheme}://{Request.Host}";

            // Custom JSON response formatting for OpenAPI property keys containing slashes/dots
            var specJson = $@"{{
  ""openapi"": ""3.0.3"",
  ""info"": {{
    ""title"": ""Rose Cottage Croft Agent Search API"",
    ""version"": ""1.0.0"",
    ""description"": ""Exposes product search capabilities to AI agents.""
  }},
  ""servers"": [
    {{ ""url"": ""{host}"" }}
  ],
  ""paths"": {{
    ""/api/agent/search"": {{
      ""post"": {{
        ""summary"": ""Search products in catalog for AI agents"",
        ""operationId"": ""searchProducts"",
        ""requestBody"": {{
          ""required"": true,
          ""content"": {{
            ""application/json"": {{
              ""schema"": {{
                ""type"": ""object"",
                ""required"": [""query""],
                ""properties"": {{
                  ""query"": {{ ""type"": ""string"", ""description"": ""Natural language or keyword query"" }},
                  ""max_results"": {{ ""type"": ""integer"", ""default"": 10, ""description"": ""Max search results"" }},
                  ""filters"": {{
                    ""type"": ""object"",
                    ""properties"": {{
                      ""min_price"": {{ ""type"": ""number"" }},
                      ""max_price"": {{ ""type"": ""number"" }},
                      ""category"": {{ ""type"": ""string"" }},
                      ""in_stock"": {{ ""type"": ""boolean"" }}
                    }}
                  }}
                }}
              }}
            }}
          }}
        }},
        ""responses"": {{
          ""200"": {{
            ""description"": ""Successful product search response"",
            ""content"": {{
              ""application/json"": {{
                ""schema"": {{
                  ""type"": ""object"",
                  ""properties"": {{
                    ""results"": {{
                      ""type"": ""array"",
                      ""items"": {{
                        ""type"": ""object"",
                        ""properties"": {{
                          ""id"": {{ ""type"": ""string"" }},
                          ""name"": {{ ""type"": ""string"" }},
                          ""description"": {{ ""type"": ""string"" }},
                          ""price"": {{
                            ""type"": ""object"",
                            ""properties"": {{
                              ""amount"": {{ ""type"": ""number"" }},
                              ""currency"": {{ ""type"": ""string"" }}
                            }}
                          }},
                          ""availability"": {{ ""type"": ""string"" }},
                          ""categoryNames"": {{ ""type"": ""array"", ""items"": {{ ""type"": ""string"" }} }}
                        }}
                      }}
                    }},
                    ""total"": {{ ""type"": ""integer"" }},
                    ""queryInterpreted"": {{ ""type"": ""string"" }}
                  }}
                }}
              }}
            }}
          }}
        }}
      }}
    }}
  }}
}}";

            return Content(specJson, "application/json");
        }
    }
}
