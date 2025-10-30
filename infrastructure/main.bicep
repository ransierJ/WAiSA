// ============================================================================
// AI Windows System Administrator - Main Infrastructure
// POC Deployment with cost-optimized resources
// ============================================================================

targetScope = 'resourceGroup'

// ============================================================================
// Parameters
// ============================================================================

@description('Environment name (poc, dev, prod)')
param environmentName string = 'poc'

@description('Project name prefix')
param projectName string = 'waisa'

@description('Azure region for resources')
param location string = 'eastus2'

@description('SQL Server administrator login')
@secure()
param sqlAdminLogin string

@description('SQL Server administrator password')
@secure()
param sqlAdminPassword string

@description('Tags to apply to all resources')
param tags object = {
  project: 'WAiSA'
  environment: environmentName
  managedBy: 'bicep'
  createdDate: utcNow('yyyy-MM-dd')
}

// ============================================================================
// Variables
// ============================================================================

var resourcePrefix = '${projectName}-${environmentName}'
var uniqueSuffix = uniqueString(resourceGroup().id)
// Key Vault name max 24 chars: 'kv' (2) + uniqueSuffix (22) = 24 chars
var keyVaultName = 'kv${take(uniqueSuffix, 22)}'

// ============================================================================
// Module 1: Monitoring (first, so other resources can use it)
// ============================================================================

module monitoring './modules/monitoring/monitoring.bicep' = {
  name: 'monitoring-deployment'
  params: {
    location: location
    workspaceName: '${resourcePrefix}-logs'
    appInsightsName: '${resourcePrefix}-insights'
    tags: tags
  }
}

// ============================================================================
// Module 2: Security (Key Vault)
// ============================================================================

module keyVault './modules/security/key-vault.bicep' = {
  name: 'keyvault-deployment'
  params: {
    location: location
    keyVaultName: keyVaultName
    tags: tags
  }
}

// ============================================================================
// Module 3: Data Layer
// ============================================================================

// Cosmos DB for device memory and context
module cosmosDb './modules/data/cosmos-db.bicep' = {
  name: 'cosmosdb-deployment'
  params: {
    location: location
    accountName: '${resourcePrefix}-cosmos-${uniqueSuffix}'
    databaseName: 'WAiSA'
    tags: tags
  }
}

// SQL Database for structured data
module sqlDatabase './modules/data/sql-database.bicep' = {
  name: 'sql-deployment'
  params: {
    location: location
    serverName: '${resourcePrefix}-sql-${uniqueSuffix}'
    databaseName: 'WAiSADB'
    administratorLogin: sqlAdminLogin
    administratorPassword: sqlAdminPassword
    tags: tags
  }
}

// Storage Account for blobs and tables
module storage './modules/data/storage.bicep' = {
  name: 'storage-deployment'
  params: {
    location: location
    storageAccountName: 'aisyspoc${take(uniqueSuffix, 16)}'
    tags: tags
  }
}

// ============================================================================
// Module 4: AI Services
// ============================================================================

// Azure OpenAI Service
module openai './modules/ai-services/openai.bicep' = {
  name: 'openai-deployment'
  params: {
    location: location
    accountName: '${resourcePrefix}-openai-${uniqueSuffix}'
    tags: tags
  }
}

// Azure AI Search
module aiSearch './modules/ai-services/ai-search.bicep' = {
  name: 'search-deployment'
  params: {
    location: location
    searchServiceName: '${resourcePrefix}-search-${uniqueSuffix}'
    tags: tags
  }
}

// ============================================================================
// Module 5: Messaging & Real-time
// ============================================================================

// Service Bus
module serviceBus './modules/messaging/service-bus.bicep' = {
  name: 'servicebus-deployment'
  params: {
    location: location
    namespaceName: '${resourcePrefix}-sb-${uniqueSuffix}'
    tags: tags
  }
}

// SignalR Service
module signalr './modules/messaging/signalr.bicep' = {
  name: 'signalr-deployment'
  params: {
    location: location
    signalRName: '${resourcePrefix}-signalr-${uniqueSuffix}'
    tags: tags
  }
}

// ============================================================================
// Module 6: Compute (App Service)
// ============================================================================

module appService './modules/compute/app-service.bicep' = {
  name: 'appservice-deployment'
  params: {
    location: location
    appServicePlanName: '${resourcePrefix}-plan'
    webAppName: '${resourcePrefix}-api-${uniqueSuffix}'
    appInsightsInstrumentationKey: monitoring.outputs.appInsightsInstrumentationKey
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    tags: tags
  }
}

// ============================================================================
// Store Secrets in Key Vault
// ============================================================================

resource sqlConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: '${keyVaultName}/sql-connection-string'
  properties: {
    value: sqlDatabase.outputs.connectionString
  }
  dependsOn: [
    keyVault
  ]
}

resource cosmosConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: '${keyVaultName}/cosmos-connection-string'
  properties: {
    value: cosmosDb.outputs.primaryConnectionString
  }
  dependsOn: [
    keyVault
  ]
}

resource storageConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: '${keyVaultName}/storage-connection-string'
  properties: {
    value: storage.outputs.connectionString
  }
  dependsOn: [
    keyVault
  ]
}

resource serviceBusConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: '${keyVaultName}/servicebus-connection-string'
  properties: {
    value: serviceBus.outputs.primaryConnectionString
  }
  dependsOn: [
    keyVault
  ]
}

resource signalrConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: '${keyVaultName}/signalr-connection-string'
  properties: {
    value: signalr.outputs.primaryConnectionString
  }
  dependsOn: [
    keyVault
  ]
}

resource openaiKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: '${keyVaultName}/openai-api-key'
  properties: {
    value: openai.outputs.apiKey
  }
  dependsOn: [
    keyVault
  ]
}

resource searchAdminKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: '${keyVaultName}/search-admin-key'
  properties: {
    value: aiSearch.outputs.adminKey
  }
  dependsOn: [
    keyVault
  ]
}

// ============================================================================
// Grant App Service Managed Identity access to Key Vault
// ============================================================================

resource keyVaultAccessPolicy 'Microsoft.KeyVault/vaults/accessPolicies@2023-07-01' = {
  name: '${keyVaultName}/add'
  properties: {
    accessPolicies: [
      {
        tenantId: subscription().tenantId
        objectId: appService.outputs.webAppPrincipalId
        permissions: {
          secrets: [
            'get'
            'list'
          ]
        }
      }
    ]
  }
  dependsOn: [
    keyVault
  ]
}

// ============================================================================
// Outputs
// ============================================================================

output resourceGroupName string = resourceGroup().name
output location string = location

// Web App
output webAppName string = appService.outputs.webAppName
output webAppUrl string = appService.outputs.webAppUrl
output webAppPrincipalId string = appService.outputs.webAppPrincipalId

// Key Vault
output keyVaultName string = keyVault.outputs.keyVaultName
output keyVaultUri string = keyVault.outputs.keyVaultUri

// Cosmos DB
output cosmosDbAccountName string = cosmosDb.outputs.accountName
output cosmosDbEndpoint string = cosmosDb.outputs.endpoint

// SQL Database
output sqlServerName string = sqlDatabase.outputs.serverName
output sqlDatabaseName string = sqlDatabase.outputs.databaseName

// Storage
output storageAccountName string = storage.outputs.storageAccountName

// AI Services
output openAiEndpoint string = openai.outputs.endpoint
output openAiAccountName string = openai.outputs.accountName
output searchServiceName string = aiSearch.outputs.searchServiceName
output searchEndpoint string = aiSearch.outputs.endpoint

// Messaging
output serviceBusNamespace string = serviceBus.outputs.namespaceName
output signalRName string = signalr.outputs.signalRName

// Monitoring
output appInsightsName string = monitoring.outputs.appInsightsName
output appInsightsInstrumentationKey string = monitoring.outputs.appInsightsInstrumentationKey
output logAnalyticsWorkspaceId string = monitoring.outputs.workspaceId

@description('Next steps for deployment')
output nextSteps string = '''
Deployment Complete! Next steps:

1. Configure Azure OpenAI model deployments:
   az cognitiveservices account deployment create --name ${openai.outputs.accountName} --resource-group ${resourceGroup().name} --deployment-name gpt-4 --model-name gpt-4 --model-version "0613" --model-format OpenAI --sku-capacity 10 --sku-name "Standard"

2. Configure AI Search indexes (will be done via application code)

3. Initialize SQL Database schema (run migrations)

4. Deploy application code to: ${appService.outputs.webAppUrl}

5. Access Key Vault for secrets: ${keyVault.outputs.keyVaultName}
'''
