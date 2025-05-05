using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace MCP_server_function
{
    /// <summary>
    /// Model Context Protocol (MCP) server implemented as an Azure Function with isolated worker model
    /// </summary>
    public class MCPFunction
    {
        // Define available operations with descriptions following MCP standard
        private static readonly Dictionary<string, OperationDefinition> _availableOperations = new Dictionary<string, OperationDefinition>
        {
            { "getBusinessData", new OperationDefinition 
                { 
                    Name = "getBusinessData",
                    Description = "Retrieves business data including customer counts, sales data, and top products",
                    Parameters = new Dictionary<string, object>
                    {
                        { "period", "Optional. Time period for data retrieval (e.g., 'last30days', 'lastQuarter')" }
                    }
                } 
            },
            { "triggerLogicApp", new OperationDefinition 
                { 
                    Name = "triggerLogicApp",
                    Description = "Triggers a specified Logic App workflow",
                    Parameters = new Dictionary<string, object>
                    {
                        { "workflowName", "Required. The name of the Logic App workflow to trigger" },
                        { "inputs", "Optional. JSON object containing input parameters for the Logic App" }
                    }
                } 
            },
            { "callInternalApi", new OperationDefinition 
                { 
                    Name = "callInternalApi",
                    Description = "Calls an internal API endpoint",
                    Parameters = new Dictionary<string, object>
                    {
                        { "apiPath", "Required. Path to the API endpoint (e.g., '/inventory', '/orders')" },
                        { "method", "Optional. HTTP method (default: GET)" },
                        { "body", "Optional. Request body for POST, PUT, or PATCH requests" }
                    }
                } 
            },
            { "list_operations", new OperationDefinition 
                { 
                    Name = "list_operations",
                    Description = "Returns a list of all available operations and their descriptions (MCP standard operation)",
                    Parameters = new Dictionary<string, object>()
                } 
            },
            { "get_openapi_spec", new OperationDefinition 
                { 
                    Name = "get_openapi_spec",
                    Description = "Returns the OpenAPI specification for this MCP server (MCP standard operation)",
                    Parameters = new Dictionary<string, object>()
                } 
            }
        };
        
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public MCPFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<MCPFunction>();
            _httpClient = new HttpClient();
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        /// <summary>
        /// MCP Server endpoint that processes model context protocol requests
        /// </summary>
        [Function("MCPFunction")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("MCP server function processing a request");

            try
            {
                // Read and deserialize the request
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var mcpRequest = JsonSerializer.Deserialize<MCPRequest>(requestBody, _jsonOptions);
                
                if (mcpRequest == null)
                {
                    _logger.LogError("Failed to deserialize MCP request");
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Invalid request format");
                    return badResponse;
                }
                
                _logger.LogInformation($"Request received - MessageType: {mcpRequest.MessageType}, RequestId: {mcpRequest.RequestId}");

                // Process the request based on the message type
                MCPResponse response = mcpRequest.MessageType switch
                {
                    "invoke" => await HandleInvokeRequest(mcpRequest),
                    "stream" => await HandleStreamRequest(mcpRequest),
                    "heartbeat" => HandleHeartbeatRequest(mcpRequest),
                    _ => new MCPResponse
                    {
                        RequestId = mcpRequest.RequestId,
                        Status = "error",
                        Error = new ErrorDetails { Message = $"Unsupported message type: {mcpRequest.MessageType}" }
                    }
                };

                var httpResponse = req.CreateResponse(HttpStatusCode.OK);
                await httpResponse.WriteAsJsonAsync(response);
                return httpResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing MCP request");
                
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                var errorResult = new MCPResponse
                {
                    Status = "error",
                    Error = new ErrorDetails { Message = $"Internal server error: {ex.Message}" }
                };
                
                await errorResponse.WriteAsJsonAsync(errorResult);
                return errorResponse;
            }
        }

        /// <summary>
        /// Handles 'invoke' message type requests
        /// </summary>
        private async Task<MCPResponse> HandleInvokeRequest(MCPRequest request)
        {
            _logger.LogInformation($"Processing invoke request for {request.InvokeRequest?.Operation}");

            if (request.InvokeRequest == null)
            {
                return new MCPResponse
                {
                    RequestId = request.RequestId,
                    Status = "error",
                    Error = new ErrorDetails { Message = "Invoke request is missing required fields" }
                };
            }

            // Mock different operations based on requested operation name
            return request.InvokeRequest.Operation switch
            {
                "getBusinessData" => await MockBusinessDataOperation(request),
                "triggerLogicApp" => await MockLogicAppTrigger(request),
                "callInternalApi" => await MockInternalApiCall(request),
                "list_operations" => await ListOperationsOperation(request),
                "get_openapi_spec" => await GetOpenApiSpecOperation(request),
                _ => new MCPResponse
                {
                    RequestId = request.RequestId,
                    Status = "error",
                    Error = new ErrorDetails { Message = $"Operation not supported: {request.InvokeRequest.Operation}" }
                }
            };
        }

        /// <summary>
        /// Handles 'stream' message type requests
        /// </summary>
        private async Task<MCPResponse> HandleStreamRequest(MCPRequest request)
        {
            _logger.LogInformation("Processing stream request");
            
            // For simplicity, we'll just echo back the request's content in the streaming response
            return new MCPResponse
            {
                RequestId = request.RequestId,
                Status = "success", 
                StreamResponse = new StreamResponse
                {
                    Data = $"Streaming response for request {request.RequestId}",
                    IsFinal = true
                }
            };
        }

        /// <summary>
        /// Handles 'heartbeat' message type requests
        /// </summary>
        private MCPResponse HandleHeartbeatRequest(MCPRequest request)
        {
            _logger.LogInformation("Processing heartbeat request");
            
            return new MCPResponse
            {
                RequestId = request.RequestId,
                Status = "success"
            };
        }

        #region Mock Internal Business Process Operations
        
        /// <summary>
        /// Mock operation for retrieving business data
        /// </summary>
        private async Task<MCPResponse> MockBusinessDataOperation(MCPRequest request)
        {
            _logger.LogInformation("Mocking business data operation");
            
            // Simulate processing delay
            await Task.Delay(500);
            
            // Mock business data retrieval
            var businessData = new Dictionary<string, object>
            {
                { "customerCount", 1250 },
                { "salesData", new Dictionary<string, double> {
                    { "Q1", 1250000.50 },
                    { "Q2", 1345000.75 },
                    { "Q3", 1100000.25 },
                    { "Q4", 1500000.00 }
                }},
                { "topProducts", new List<Dictionary<string, object>> {
                    new Dictionary<string, object> { { "id", "PRD-001" }, { "name", "Widget A" }, { "sales", 1250 } },
                    new Dictionary<string, object> { { "id", "PRD-002" }, { "name", "Widget B" }, { "sales", 984 } },
                    new Dictionary<string, object> { { "id", "PRD-003" }, { "name", "Widget C" }, { "sales", 879 } }
                }}
            };
            
            return new MCPResponse
            {
                RequestId = request.RequestId,
                Status = "success",
                InvokeResponse = new InvokeResponse
                {
                    Result = businessData
                }
            };
        }
        
        /// <summary>
        /// Mock operation for triggering a Logic App
        /// </summary>
        private async Task<MCPResponse> MockLogicAppTrigger(MCPRequest request)
        {
            _logger.LogInformation("Mocking Logic App trigger");
            
            // Get input parameters
            var parameters = request.InvokeRequest?.Parameters;
            var workflowName = parameters?.ContainsKey("workflowName") == true ? 
                parameters["workflowName"]?.ToString() : 
                "default-workflow";
            
            // Simulate a call to the Logic App trigger URL
            // In a real implementation, you would use HttpClient to call the Logic App's HTTP trigger
            await Task.Delay(700); // Simulate network latency
            
            // Mock Logic App execution details that would be returned
            var logicAppResult = new Dictionary<string, object>
            {
                { "executionId", Guid.NewGuid().ToString() },
                { "workflowName", workflowName ?? "unknown" },
                { "status", "Succeeded" },
                { "startTime", DateTime.UtcNow.ToString("o") },
                { "endTime", DateTime.UtcNow.AddSeconds(2).ToString("o") },
                { "output", new Dictionary<string, object> {
                    { "result", "Process completed successfully" },
                    { "processedItems", 42 }
                }}
            };
            
            return new MCPResponse
            {
                RequestId = request.RequestId,
                Status = "success",
                InvokeResponse = new InvokeResponse
                {
                    Result = logicAppResult
                }
            };
        }
        
        /// <summary>
        /// Mock operation for calling an internal API
        /// </summary>
        private async Task<MCPResponse> MockInternalApiCall(MCPRequest request)
        {
            _logger.LogInformation("Mocking internal API call");
            
            // Get input parameters
            var parameters = request.InvokeRequest?.Parameters;
            var apiPath = parameters?.ContainsKey("apiPath") == true ? 
                parameters["apiPath"]?.ToString() : 
                "/api/default";
            var method = parameters?.ContainsKey("method") == true ? 
                parameters["method"]?.ToString() : 
                "GET";
            
            // Simulate API call processing
            await Task.Delay(300); // Simulate network latency
            
            // Generate different responses based on the requested API path
            Dictionary<string, object> apiResponse;
            
            if (apiPath?.Contains("inventory") == true)
            {
                apiResponse = new Dictionary<string, object>
                {
                    { "inventoryItems", new List<Dictionary<string, object>> {
                        new Dictionary<string, object> { { "sku", "SKU123" }, { "name", "Item 1" }, { "count", 42 }, { "status", "InStock" } },
                        new Dictionary<string, object> { { "sku", "SKU456" }, { "name", "Item 2" }, { "count", 18 }, { "status", "LowStock" } },
                        new Dictionary<string, object> { { "sku", "SKU789" }, { "name", "Item 3" }, { "count", 0 }, { "status", "OutOfStock" } }
                    }},
                    { "lastUpdated", DateTime.UtcNow.ToString("o") }
                };
            }
            else if (apiPath?.Contains("orders") == true)
            {
                apiResponse = new Dictionary<string, object>
                {
                    { "pendingOrders", 24 },
                    { "completedOrders", 156 },
                    { "recentOrders", new List<Dictionary<string, object>> {
                        new Dictionary<string, object> { { "id", "ORD-001" }, { "customer", "CUST-123" }, { "amount", 125.50 }, { "status", "Shipped" } },
                        new Dictionary<string, object> { { "id", "ORD-002" }, { "customer", "CUST-456" }, { "amount", 75.25 }, { "status", "Processing" } }
                    }}
                };
            }
            else
            {
                apiResponse = new Dictionary<string, object>
                {
                    { "status", "ok" },
                    { "message", "Default API response" },
                    { "timestamp", DateTime.UtcNow.ToString("o") }
                };
            }
            
            return new MCPResponse
            {
                RequestId = request.RequestId,
                Status = "success",
                InvokeResponse = new InvokeResponse
                {
                    Result = apiResponse
                }
            };
        }
        
        /// <summary>
        /// Standard MCP operation for retrieving a list of all available operations
        /// </summary>
        private async Task<MCPResponse> ListOperationsOperation(MCPRequest request)
        {
            _logger.LogInformation("Executing list_operations operation (MCP standard)");
            
            // Simulate minimal processing delay for realism
            await Task.Delay(100);
            
            // Read allowed operations from configuration if available
            string allowedOperationsStr = Environment.GetEnvironmentVariable("MCP_ALLOWED_OPERATIONS") ?? string.Empty;
            var allowedOperations = !string.IsNullOrEmpty(allowedOperationsStr) 
                ? allowedOperationsStr.Split(',').ToHashSet()
                : null;
                
            // Filter operations based on allowed operations if configured
            var operations = allowedOperations != null
                ? _availableOperations.Where(t => allowedOperations.Contains(t.Key)).ToDictionary(t => t.Key, t => t.Value)
                : _availableOperations;
                
            return new MCPResponse
            {
                RequestId = request.RequestId,
                Status = "success",
                InvokeResponse = new InvokeResponse
                {
                    Result = new Dictionary<string, object>
                    {
                        { "operations", operations }
                    }
                }
            };
        }

        /// <summary>
        /// Standard MCP operation for retrieving the OpenAPI specification
        /// </summary>
        private async Task<MCPResponse> GetOpenApiSpecOperation(MCPRequest request)
        {
            _logger.LogInformation("Executing get_openapi_spec operation (MCP standard)");
            
            // Simulate processing delay
            await Task.Delay(200);
            
            // Create a simplified OpenAPI specification
            var openApiSpec = new Dictionary<string, object>
            {
                { "openapi", "3.0.0" },
                { "info", new Dictionary<string, object>
                    {
                        { "title", "MCP Server API" },
                        { "version", "1.0.0" },
                        { "description", "Model Context Protocol server for connecting to internal business processes" }
                    }
                },
                { "paths", new Dictionary<string, object>
                    {
                        { "/api/MCPFunction", new Dictionary<string, object>
                            {
                                { "post", new Dictionary<string, object>
                                    {
                                        { "summary", "Process MCP requests" },
                                        { "requestBody", new Dictionary<string, object>
                                            {
                                                { "content", new Dictionary<string, object>
                                                    {
                                                        { "application/json", new Dictionary<string, object>
                                                            {
                                                                { "schema", new Dictionary<string, object>
                                                                    {
                                                                        { "$ref", "#/components/schemas/MCPRequest" }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        },
                                        { "responses", new Dictionary<string, object>
                                            {
                                                { "200", new Dictionary<string, object>
                                                    {
                                                        { "description", "Successful operation" },
                                                        { "content", new Dictionary<string, object>
                                                            {
                                                                { "application/json", new Dictionary<string, object>
                                                                    {
                                                                        { "schema", new Dictionary<string, object>
                                                                            {
                                                                                { "$ref", "#/components/schemas/MCPResponse" }
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                { "components", new Dictionary<string, object>
                    {
                        { "schemas", new Dictionary<string, object>
                            {
                                // Simplified schemas for brevity
                                { "MCPRequest", new Dictionary<string, object>
                                    {
                                        { "type", "object" },
                                        { "properties", new Dictionary<string, object>
                                            {
                                                { "requestId", new Dictionary<string, object> { { "type", "string" } } },
                                                { "messageType", new Dictionary<string, object> { { "type", "string" } } },
                                                { "invokeRequest", new Dictionary<string, object> { { "$ref", "#/components/schemas/InvokeRequest" } } },
                                                { "streamRequest", new Dictionary<string, object> { { "$ref", "#/components/schemas/StreamRequest" } } }
                                            }
                                        }
                                    }
                                },
                                { "MCPResponse", new Dictionary<string, object>
                                    {
                                        { "type", "object" },
                                        { "properties", new Dictionary<string, object>
                                            {
                                                { "requestId", new Dictionary<string, object> { { "type", "string" } } },
                                                { "status", new Dictionary<string, object> { { "type", "string" } } },
                                                { "invokeResponse", new Dictionary<string, object> { { "$ref", "#/components/schemas/InvokeResponse" } } },
                                                { "streamResponse", new Dictionary<string, object> { { "$ref", "#/components/schemas/StreamResponse" } } },
                                                { "error", new Dictionary<string, object> { { "$ref", "#/components/schemas/ErrorDetails" } } }
                                            }
                                        }
                                    }
                                },
                                { "InvokeRequest", new Dictionary<string, object>
                                    {
                                        { "type", "object" },
                                        { "properties", new Dictionary<string, object>
                                            {
                                                { "operation", new Dictionary<string, object> { { "type", "string" } } },
                                                { "parameters", new Dictionary<string, object> { { "type", "object" } } }
                                            }
                                        }
                                    }
                                },
                                { "StreamRequest", new Dictionary<string, object>
                                    {
                                        { "type", "object" },
                                        { "properties", new Dictionary<string, object>
                                            {
                                                { "operation", new Dictionary<string, object> { { "type", "string" } } },
                                                { "parameters", new Dictionary<string, object> { { "type", "object" } } }
                                            }
                                        }
                                    }
                                },
                                { "InvokeResponse", new Dictionary<string, object>
                                    {
                                        { "type", "object" },
                                        { "properties", new Dictionary<string, object>
                                            {
                                                { "result", new Dictionary<string, object> { { "type", "object" } } }
                                            }
                                        }
                                    }
                                },
                                { "StreamResponse", new Dictionary<string, object>
                                    {
                                        { "type", "object" },
                                        { "properties", new Dictionary<string, object>
                                            {
                                                { "data", new Dictionary<string, object> { { "type", "string" } } },
                                                { "isFinal", new Dictionary<string, object> { { "type", "boolean" } } }
                                            }
                                        }
                                    }
                                },
                                { "ErrorDetails", new Dictionary<string, object>
                                    {
                                        { "type", "object" },
                                        { "properties", new Dictionary<string, object>
                                            {
                                                { "message", new Dictionary<string, object> { { "type", "string" } } },
                                                { "code", new Dictionary<string, object> { { "type", "string" } } }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };
            
            return new MCPResponse
            {
                RequestId = request.RequestId,
                Status = "success",
                InvokeResponse = new InvokeResponse
                {
                    Result = openApiSpec
                }
            };
        }
        
        #endregion
    }

    #region MCP Protocol Models

    /// <summary>
    /// The base MCP request structure
    /// </summary>
    public class MCPRequest
    {
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("messageType")]
        public string MessageType { get; set; } = "invoke";

        [JsonPropertyName("invokeRequest")]
        public InvokeRequest? InvokeRequest { get; set; }

        [JsonPropertyName("streamRequest")]
        public StreamRequest? StreamRequest { get; set; }
    }

    /// <summary>
    /// Structure for invoke request operations
    /// </summary>
    public class InvokeRequest
    {
        [JsonPropertyName("operation")]
        public string? Operation { get; set; }

        [JsonPropertyName("parameters")]
        public Dictionary<string, object>? Parameters { get; set; }
    }

    /// <summary>
    /// Structure for stream request operations
    /// </summary>
    public class StreamRequest
    {
        [JsonPropertyName("operation")]
        public string? Operation { get; set; }

        [JsonPropertyName("parameters")]
        public Dictionary<string, object>? Parameters { get; set; }
    }

    /// <summary>
    /// The base MCP response structure
    /// </summary>
    public class MCPResponse
    {
        [JsonPropertyName("requestId")]
        public string? RequestId { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("invokeResponse")]
        public InvokeResponse? InvokeResponse { get; set; }

        [JsonPropertyName("streamResponse")]
        public StreamResponse? StreamResponse { get; set; }

        [JsonPropertyName("error")]
        public ErrorDetails? Error { get; set; }
    }

    /// <summary>
    /// Structure for invoke response data
    /// </summary>
    public class InvokeResponse
    {
        [JsonPropertyName("result")]
        public object? Result { get; set; }
    }

    /// <summary>
    /// Structure for stream response data
    /// </summary>
    public class StreamResponse
    {
        [JsonPropertyName("data")]
        public string? Data { get; set; }

        [JsonPropertyName("isFinal")]
        public bool IsFinal { get; set; }
    }

    /// <summary>
    /// Error details for MCP responses
    /// </summary>
    public class ErrorDetails
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }
    }

    /// <summary>
    /// Describes an operation that can be invoked through the MCP protocol
    /// </summary>
    public class OperationDefinition
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        
        [JsonPropertyName("parameters")]
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }

    #endregion
}
