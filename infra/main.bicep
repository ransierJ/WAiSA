// WAiSA PoC Infrastructure Deployment
// This template deploys all resources needed for the WAiSA (Workplace AI System Assistant) proof of concept
// Resources include: Azure OpenAI, Cosmos DB, SQL Database, Service Bus, SignalR, AI Search, Storage, and monitoring

targetScope = 'resourceGroup'

// ============================================================================
// Parameters
// ============================================================================

@description('Environment name (e.g., poc, dev, prod)')
param environmentName string = 'poc'

@description('Project name - used in resource naming')
param projectName string = 'WAiSA'

@description('Location for all resources')
param location string = resourceGroup().location

@description('Unique suffix for resource names (leave empty for auto-generation)')
param uniqueSuffix string = uniqueString(resourceGroup().id)

@description('SQL Server administrator username')
@secure()
param sqlAdminUsername string

@description('SQL Server administrator password')
@secure()
param sqlAdminPassword string

@description('Your Azure AD Object ID for Key Vault access')
param keyVaultAdminObjectId string

@description('Tags to apply to all resources')
param tags object = {
  environment: environmentName
  project: projectName
  managedBy: 'bicep'
  createdDate: utcNow('yyyy-MM-dd')
}

// ============================================================================
// Variables
// ============================================================================

var namingPrefix = 'waisa-${environmentName}'
var storageAccountName = 'aisys${environmentName}${uniqueSuffix}'
var frontendStorageAccountName = 'waisafrontend${uniqueSuffix}'

// ============================================================================
// Log Analytics Workspace
// ============================================================================

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${namingPrefix}-logs'
  location: location
  tags: tags
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

// ============================================================================
// Application Insights
// ============================================================================

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${namingPrefix}-insights'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    RetentionInDays: 30
  }
}

// ============================================================================
// Storage Accounts
// ============================================================================

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
    accessTier: 'Hot'
    allowSharedKeyAccess: false // Disable key-based access, use RBAC instead
  }
}

resource frontendStorageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: frontendStorageAccountName
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: true // Frontend storage needs public access for static website
    supportsHttpsTrafficOnly: true
    accessTier: 'Hot'
  }
}

// Enable static website on frontend storage
resource frontendBlobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: frontendStorageAccount
  name: 'default'
  properties: {}
}

// ============================================================================
// Key Vault
// ============================================================================

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: 'kv${uniqueSuffix}'
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true // Use RBAC instead of access policies
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true // Required by best practices - cannot be disabled
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
    }
  }
}

// Grant Key Vault Secrets Officer role to the admin
resource keyVaultRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, keyVaultAdminObjectId, 'Key Vault Secrets Officer')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7') // Key Vault Secrets Officer
    principalId: keyVaultAdminObjectId
    principalType: 'User'
  }
}

// ============================================================================
// Cosmos DB
// ============================================================================

resource cosmosDb 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' = {
  name: '${namingPrefix}-cosmos-${uniqueSuffix}'
  location: location
  tags: tags
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
      maxIntervalInSeconds: 5
      maxStalenessPrefix: 100
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    capabilities: [
      {
        name: 'EnableServerless'
      }
    ]
    enableAnalyticalStorage: false
    enableAutomaticFailover: false
    enableMultipleWriteLocations: false
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: false // Set to true and use RBAC for better security
    minimalTlsVersion: 'Tls12'
    backupPolicy: {
      type: 'Periodic'
      periodicModeProperties: {
        backupIntervalInMinutes: 240
        backupRetentionIntervalInHours: 8
        backupStorageRedundancy: 'Geo'
      }
    }
  }
}

// ============================================================================
// SQL Server and Database
// ============================================================================

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: '${namingPrefix}-sql-${uniqueSuffix}'
  location: location
  tags: tags
  properties: {
    administratorLogin: sqlAdminUsername
    administratorLoginPassword: sqlAdminPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// Allow Azure services to access SQL Server
resource sqlFirewallRule 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: 'WAiSADB'
  location: location
  tags: tags
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648 // 2 GB
    catalogCollation: 'SQL_Latin1_General_CP1_CI_AS'
    zoneRedundant: false
    readScale: 'Disabled'
    requestedBackupStorageRedundancy: 'Geo'
  }
}

// ============================================================================
// Azure OpenAI
// ============================================================================

resource openAI 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: '${namingPrefix}-openai-${uniqueSuffix}'
  location: location
  tags: tags
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: '${namingPrefix}-openai-${uniqueSuffix}'
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: false
  }
}

// Deploy GPT-4o model
resource gpt4oDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: openAI
  name: 'gpt-4o'
  sku: {
    name: 'Standard'
    capacity: 10
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o'
      version: '2024-11-20'
    }
    versionUpgradeOption: 'OnceNewDefaultVersionAvailable'
  }
}

// Deploy text-embedding-ada-002 model
resource embeddingDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: openAI
  name: 'text-embedding-ada-002'
  sku: {
    name: 'Standard'
    capacity: 10
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'text-embedding-ada-002'
      version: '2'
    }
    versionUpgradeOption: 'OnceNewDefaultVersionAvailable'
  }
  dependsOn: [
    gpt4oDeployment // Deploy sequentially to avoid conflicts
  ]
}

// ============================================================================
// Azure AI Search
// ============================================================================

resource searchService 'Microsoft.Search/searchServices@2024-06-01-preview' = {
  name: '${namingPrefix}-search-${uniqueSuffix}'
  location: location
  tags: tags
  sku: {
    name: 'basic'
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    hostingMode: 'default'
    publicNetworkAccess: 'enabled'
    disableLocalAuth: false
  }
}

// ============================================================================
// Service Bus
// ============================================================================

resource serviceBus 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: '${namingPrefix}-sb-${uniqueSuffix}'
  location: location
  tags: tags
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: false
    zoneRedundant: true
  }
}

// ============================================================================
// SignalR Service
// ============================================================================

resource signalR 'Microsoft.SignalRService/signalR@2024-08-01-preview' = {
  name: '${namingPrefix}-signalr-${uniqueSuffix}'
  location: location
  tags: tags
  kind: 'SignalR'
  sku: {
    name: 'Free_F1'
    tier: 'Free'
    capacity: 1
  }
  properties: {
    tls: {
      clientCertEnabled: false
    }
    features: [
      {
        flag: 'ServiceMode'
        value: 'Default'
        properties: {}
      }
    ]
    cors: {
      allowedOrigins: [
        '*'
      ]
    }
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: false
  }
}

// ============================================================================
// App Service Plan
// ============================================================================

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${namingPrefix}-api-plan'
  location: location
  tags: tags
  kind: 'linux'
  sku: {
    name: 'B1'
    tier: 'Basic'
    capacity: 1
  }
  properties: {
    reserved: true // Required for Linux plans
  }
}

// ============================================================================
// App Service (API)
// ============================================================================

resource appService 'Microsoft.Web/sites@2023-12-01' = {
  name: '${namingPrefix}-api-${uniqueSuffix}'
  location: location
  tags: tags
  kind: 'app,linux'
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0' // Adjust based on your application
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      alwaysOn: true
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: '~3'
        }
      ]
    }
  }
}

// ============================================================================
// Virtual Network
// ============================================================================

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2024-05-01' = {
  name: 'vnet-${location}'
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        '172.16.0.0/16'
      ]
    }
    subnets: [
      {
        name: 'snet-${location}-1'
        properties: {
          addressPrefixes: [
            '172.16.0.0/24'
          ]
          privateEndpointNetworkPolicies: 'Disabled'
          privateLinkServiceNetworkPolicies: 'Enabled'
        }
      }
    ]
  }
}

// ============================================================================
// Outputs
// ============================================================================

@description('Resource Group Name')
output resourceGroupName string = resourceGroup().name

@description('Azure OpenAI endpoint')
output openAIEndpoint string = openAI.properties.endpoint

@description('Azure OpenAI resource name')
output openAIName string = openAI.name

@description('Cosmos DB endpoint')
output cosmosDbEndpoint string = cosmosDb.properties.documentEndpoint

@description('SQL Server FQDN')
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName

@description('SQL Database name')
output sqlDatabaseName string = sqlDatabase.name

@description('Service Bus endpoint')
output serviceBusEndpoint string = serviceBus.properties.serviceBusEndpoint

@description('SignalR endpoint')
output signalREndpoint string = signalR.properties.hostName

@description('AI Search endpoint')
output searchEndpoint string = 'https://${searchService.name}.search.windows.net'

@description('Application Insights connection string')
output appInsightsConnectionString string = appInsights.properties.ConnectionString

@description('App Service default hostname')
output appServiceHostname string = appService.properties.defaultHostName

@description('Key Vault URI')
output keyVaultUri string = keyVault.properties.vaultUri

@description('Storage Account name')
output storageAccountName string = storageAccount.name

@description('Frontend Storage Account name')
output frontendStorageAccountName string = frontendStorageAccount.name

@description('Log Analytics Workspace ID')
output logAnalyticsWorkspaceId string = logAnalytics.id
