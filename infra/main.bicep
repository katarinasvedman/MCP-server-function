@description('The location where all resources will be deployed.')
param location string = 'swedencentral'

@description('Environment name used for resource naming')
param environmentName string = 'mcpserver'

@description('The name of the function app to create.')
param functionAppName string = 'func-${environmentName}-${uniqueString(resourceGroup().id)}'

@description('The name of the storage account to create.')
param storageAccountName string = 'st${replace(environmentName, '-', '')}${uniqueString(resourceGroup().id)}'

@description('The name of the app service plan to create.')
param appServicePlanName string = 'asp-${environmentName}-${uniqueString(resourceGroup().id)}'

@description('The name of the app insights resource to create.')
param appInsightsName string = 'ai-${environmentName}-${uniqueString(resourceGroup().id)}'

@description('The SKU of the App Service Plan.')
@allowed([
  'Y1' // Consumption plan
  'B1' // Basic B1
  'S1' // Standard S1
])
param appServicePlanSku string = 'Y1'

@description('Whether to enable MCP authentication for the function')
param mcpAuthEnabled bool = false

@description('Comma-separated list of allowed MCP operations')
param mcpAllowedOperations string = 'getBusinessData,triggerLogicApp,callInternalApi,list_operations,get_openapi_spec'

@description('The creation date for resource tagging')
param createdOn string = utcNow('yyyy-MM-dd')

// Create a unique suffix for resource names
var uniqueSuffix = uniqueString(resourceGroup().id)

// Define tags for all resources
var commonTags = {
  environment: environmentName
  application: 'MCP-Server-Function'
  createdOn: createdOn
}

// Storage Account resource
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  tags: commonTags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    encryption: {
      services: {
        file: {
          keyType: 'Account'
          enabled: true
        }
        blob: {
          keyType: 'Account'
          enabled: true
        }
      }
      keySource: 'Microsoft.Storage'
    }
    accessTier: 'Hot'
  }
}

// Log Analytics Workspace for App Insights
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: 'log-${environmentName}-${uniqueSuffix}'
  location: location
  tags: commonTags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

// Application Insights resource
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  tags: commonTags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Request_Source: 'rest'
    WorkspaceResourceId: logAnalyticsWorkspace.id
  }
}

// App Service Plan resource
resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: appServicePlanName
  location: location
  tags: commonTags
  sku: {
    name: appServicePlanSku
  }
  properties: {
    reserved: true // Required for Linux
  }
}

// User Assigned Managed Identity
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'mi-${environmentName}-${uniqueSuffix}'
  location: location
  tags: commonTags
}

// Function App resource
resource functionApp 'Microsoft.Web/sites@2022-09-01' = {
  name: functionAppName
  location: location
  tags: commonTags
  kind: 'functionapp'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      http20Enabled: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      netFrameworkVersion: 'v8.0'
      use32BitWorkerProcess: false
      cors: {
        allowedOrigins: [
          'https://portal.azure.com'
        ]
      }
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(functionAppName)
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'DOTNET_ISOLATE_TIMEOUT_SECONDS'
          value: '60'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsights.properties.InstrumentationKey
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'MCP_AUTH_ENABLED'
          value: string(mcpAuthEnabled)
        }
        {
          name: 'MCP_ALLOWED_OPERATIONS'
          value: mcpAllowedOperations
        }
        {
          name: 'LOGIC_APP_ENDPOINT'
          value: 'https://prod-12.eastus.logic.azure.com:443/workflows/placeholder' // Replace with actual Logic App endpoint in production
        }
        {
          name: 'INTERNAL_API_BASE_URL'
          value: 'https://api.internal.company.com' // Replace with actual API URL in production
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
      ]
    }
  }
}

// Outputs
output functionAppName string = functionApp.name
output functionAppHostName string = '${functionApp.name}.azurewebsites.net'
output storageAccountName string = storageAccount.name
output applicationInsightsName string = appInsights.name
output managedIdentityId string = managedIdentity.id
output managedIdentityPrincipalId string = managedIdentity.properties.principalId
