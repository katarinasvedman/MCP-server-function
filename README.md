# MCP Server Azure Function

A Model Context Protocol (MCP) server implementation using Azure Functions with the .NET 8.0 isolated worker model.

## Overview

This project implements a Model Context Protocol (MCP) server as an Azure Function, enabling interaction with language models that support the MCP standard. The implementation uses the Azure Functions v4 isolated worker model which offers better performance, scalability, and compatibility with .NET 8.

### Features

- Implements MCP protocol operations:
  - `list_operations`: Standard MCP operation to list all available operations
  - `get_openapi_spec`: Standard MCP operation to provide API documentation
  - `getBusinessData`: Custom operation to retrieve business metrics and data
  - `triggerLogicApp`: Custom operation to trigger Azure Logic Apps
  - `callInternalApi`: Custom operation to proxy requests to internal APIs

- Built on Azure Functions v4 with isolated worker process
- Uses .NET 8.0 for improved performance and features
- Structured error handling and logging
- JSON serialization with System.Text.Json
- Infrastructure as Code (IaC) using Bicep templates

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or [Visual Studio Code](https://code.visualstudio.com/)
- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) (for deployment)
- [Azure Bicep CLI](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/install#azure-cli) (for infrastructure deployment)

## Setup and Configuration

### Local Development Setup

1. **Clone the repository**

2. **Configure local.settings.json**

   The local.settings.json file contains the necessary configuration for running the function locally. Make sure it includes:

   ```json
   {
       "IsEncrypted": false,
       "Values": {
           "AzureWebJobsStorage": "UseDevelopmentStorage=true",
           "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
           "DOTNET_ISOLATE_TIMEOUT_SECONDS": "60",
           "LOGIC_APP_ENDPOINT": "https://prod-12.eastus.logic.azure.com:443/workflows/your-logic-app-id",
           "INTERNAL_API_BASE_URL": "https://api.internal.company.com",
           "INTERNAL_API_KEY": "your-api-key-here",
           "MCP_AUTH_ENABLED": "false",
           "MCP_ALLOWED_OPERATIONS": "getBusinessData,triggerLogicApp,callInternalApi,list_operations,get_openapi_spec"
       }
   }
   ```

3. **Install dependencies**

   ```bash
   dotnet restore
   ```

### Running Locally

1. **Start the function**

   ```bash
   func start
   ```

   The function will be available at `http://localhost:7071/api/MCPFunction`

2. **Stop the function**

   Press `Ctrl+C` in the terminal where the function is running, or
   find the PID using `netstat -ano | findstr :7071 | findstr LISTENING` 
   and then kill it with `taskkill /F /PID <PID>`

## Testing

### Local Testing with JSON Files

The project includes test JSON files to help validate the MCP server functionality:

1. **Test list_operations**

   Use the `test-list-operations.json` file to test the standard MCP operation that lists all available operations:

   ```bash
   curl -X POST http://localhost:7071/api/MCPFunction -H "Content-Type: application/json" -d @test-list-operations.json
   ```

2. **Test getBusinessData**

   Use the `test-business-data.json` file to test retrieving business data:

   ```bash
   curl -X POST http://localhost:7071/api/MCPFunction -H "Content-Type: application/json" -d @test-business-data.json
   ```

3. **Test other operations**

   You can create additional test files for other operations following the MCP protocol format:

   ```json
   {
     "requestId": "unique-request-id",
     "messageType": "invoke",
     "invokeRequest": {
       "operation": "operationName",
       "parameters": {
         "param1": "value1",
         "param2": "value2"
       }
     }
   }
   ```

### Testing Deployed Functions with Function Keys

When testing functions deployed to Azure, you need to include the function key in your requests:

1. **Using function key in curl request**

   ```bash
   curl -X POST https://your-function-app.azurewebsites.net/api/MCPFunction \
     -H "Content-Type: application/json" \
     -H "x-functions-key: YOUR_FUNCTION_KEY" \
     -d @test-list-operations.json
   ```

2. **Alternative: Using function key as query parameter**

   ```bash
   curl -X POST "https://your-function-app.azurewebsites.net/api/MCPFunction?code=YOUR_FUNCTION_KEY" \
     -H "Content-Type: application/json" \
     -d @test-list-operations.json
   ```

3. **Testing with Postman**

   - Create a new POST request to your function URL
   - Add an `x-functions-key` header with your function key
   - Set the `Content-Type` header to `application/json`
   - Add the MCP request body in JSON format
   - Send the request and examine the response

## Deployment to Azure

### Deploying with Bicep (Recommended)

This project includes Bicep templates for Infrastructure as Code deployment, following Azure best practices.

1. **Navigate to the infrastructure directory**

   ```bash
   cd infra
   ```

2. **Deploy to a specific Azure region (e.g., Sweden Central)**

   ```bash
   # Create resource group (if it doesn't exist)
   az group create --name rg-mcp-function --location swedencentral

   # Validate the deployment with what-if operation (recommended practice)
   az deployment group what-if --name mcp-server-deployment --resource-group rg-mcp-function --template-file main.bicep --parameters main.parameters.json

   # Deploy the infrastructure
   az deployment group create --name mcp-server-deployment --resource-group rg-mcp-function --template-file main.bicep --parameters main.parameters.json
   ```

3. **Deploy function code to the created Function App**

   ```bash
   # Build the project for release
   dotnet publish -c Release

   # Deploy to Azure Function App (replace FUNCTION_APP_NAME with your deployed function app name)
   func azure functionapp publish FUNCTION_APP_NAME --dotnet-isolated
   ```

4. **Retrieve function key for testing**

   ```bash
   # List function keys
   az functionapp function keys list --name FUNCTION_APP_NAME --resource-group rg-mcp-function --function-name MCPFunction
   ```

### Bicep Template Details

The Bicep template (`main.bicep`) includes:

- Function App with .NET 8.0 isolated worker runtime
- App Service Plan (Consumption or dedicated)
- Storage Account for function execution
- Application Insights for monitoring
- Log Analytics Workspace for logs
- User-Assigned Managed Identity for secure authentication

Key parameters that can be customized:
- `location`: Azure region for deployment (default: swedencentral)
- `environmentName`: Name prefix for resources
- `appServicePlanSku`: SKU for the App Service Plan (Y1, B1, S1)
- `mcpAuthEnabled`: Whether to enable MCP authentication
- `mcpAllowedOperations`: Comma-separated list of allowed operations

### Deploying with Azure CLI (Alternative)

1. **Create a Function App in Azure**

   ```bash
   az login
   az group create --name mcp-server-rg --location eastus
   az storage account create --name mcpserverfunc --location eastus --resource-group mcp-server-rg --sku Standard_LRS
   az functionapp create --resource-group mcp-server-rg --consumption-plan-location eastus --runtime dotnet-isolated --functions-version 4 --name mcp-server-function --storage-account mcpserverfunc --os-type Windows
   ```

2. **Set Function App settings**

   ```bash
   az functionapp config appsettings set --name mcp-server-function --resource-group mcp-server-rg --settings "LOGIC_APP_ENDPOINT=https://prod-12.eastus.logic.azure.com:443/workflows/your-logic-app-id" "INTERNAL_API_BASE_URL=https://api.internal.company.com" "MCP_AUTH_ENABLED=true" "MCP_ALLOWED_OPERATIONS=getBusinessData,triggerLogicApp,callInternalApi,list_operations,get_openapi_spec"
   ```

   For sensitive settings like API keys, use Azure Key Vault:

   ```bash
   az keyvault create --name mcpserver-kv --resource-group mcp-server-rg --location eastus
   az keyvault secret set --vault-name mcpserver-kv --name "InternalApiKey" --value "your-api-key-here"
   az functionapp identity assign --name mcp-server-function --resource-group mcp-server-rg
   # Grant the function access to Key Vault
   $principalId=$(az functionapp identity show --name mcp-server-function --resource-group mcp-server-rg --query principalId --output tsv)
   az keyvault set-policy --name mcpserver-kv --object-id $principalId --secret-permissions get list
   # Add Key Vault reference to Function App settings
   az functionapp config appsettings set --name mcp-server-function --resource-group mcp-server-rg --settings "INTERNAL_API_KEY=@Microsoft.KeyVault(SecretUri=https://mcpserver-kv.vault.azure.net/secrets/InternalApiKey/)"
   ```

3. **Publish the Function**

   ```bash
   func azure functionapp publish mcp-server-function
   ```

### Deploying with Azure DevOps or GitHub Actions

Create a CI/CD pipeline using:
- Azure DevOps Azure Functions pipeline template
- GitHub Actions Azure Functions workflow

## Production Deployment Considerations

### Regional Selection

- **Sweden Central Region Benefits**:
  - Strong compliance offerings for European data residency requirements
  - Sustainability focus with high renewable energy usage
  - Low-latency access for Nordic and Northern European users
  - Modern Azure datacenter with latest infrastructure

### Security Best Practices

- Enable MCP authentication by setting `MCP_AUTH_ENABLED=true` in production
- Store sensitive information like API keys in Azure Key Vault
- Use managed identities for authenticating with other Azure services 
- Monitor function execution with Application Insights
- Regularly update dependencies to address security vulnerabilities
- Enable HTTPS-only access to your function app
- Use the minimum TLS version 1.2
- Disable FTP access to your function app

### Monitoring and Operational Excellence

- Enable Application Insights for comprehensive monitoring
- Set up alerts for abnormal function behavior
- Configure diagnostic settings for log retention
- Use resource tags for better resource management
- Implement proper RBAC for administrative access
- Regular backup of function app configurations

## Troubleshooting

### Common Issues

1. **System.Net.Primitives assembly not found**

   This can occur when running .NET 8 functions with the in-process model. The solution is to use the isolated worker process model, which is configured in this project.

2. **Function doesn't start locally**

   Ensure you have the right .NET SDK version specified in global.json:
   ```json
   {
     "sdk": {
       "version": "8.0.115"
     }
   }
   ```

3. **Authentication or authorization failures**

   Check your `MCP_AUTH_ENABLED` setting and ensure proper authentication is configured in Azure.

4. **Bicep deployment errors**

   - Check for linting errors with `az bicep build --file main.bicep`
   - Verify resource name conventions and uniqueness
   - Ensure proper parameter types are used
   - Use what-if operation to validate changes before deployment
   - Common Bicep errors include:
     - Using functions in incorrect locations (e.g., utcNow() can only be used in parameter defaults)
     - Invalid variable references
     - Missing parameters or expressions

5. **Function Key Issues**

   If you receive 401 Unauthorized errors when testing your deployed function, ensure you're including the function key with your requests, either as an `x-functions-key` header or as a `?code=` query parameter.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Run tests
5. Submit a pull request

## License

[Specify the license under which this project is released]