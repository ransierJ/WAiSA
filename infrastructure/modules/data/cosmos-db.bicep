// ============================================================================
// Cosmos DB Module - NoSQL Database for Device Memory & Context
// ============================================================================

@description('Azure region')
param location string

@description('Cosmos DB account name')
param accountName string

@description('Database name')
param databaseName string

@description('Resource tags')
param tags object

// ============================================================================
// Cosmos DB Account (Serverless for cost optimization)
// ============================================================================

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2023-11-15' = {
  name: accountName
  location: location
  tags: tags
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
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
    enableAutomaticFailover: false
    enableMultipleWriteLocations: false
    publicNetworkAccess: 'Enabled'
  }
}

// ============================================================================
// Database
// ============================================================================

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2023-11-15' = {
  parent: cosmosAccount
  name: databaseName
  properties: {
    resource: {
      id: databaseName
    }
  }
}

// ============================================================================
// Container: DeviceMemory (per-device context summaries)
// ============================================================================

resource deviceMemoryContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-11-15' = {
  parent: database
  name: 'DeviceMemory'
  properties: {
    resource: {
      id: 'DeviceMemory'
      partitionKey: {
        paths: ['/deviceId']
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
      }
    }
  }
}

// ============================================================================
// Container: InteractionHistory (conversation logs)
// ============================================================================

resource interactionHistoryContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-11-15' = {
  parent: database
  name: 'InteractionHistory'
  properties: {
    resource: {
      id: 'InteractionHistory'
      partitionKey: {
        paths: ['/deviceId']
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
      }
      defaultTtl: 2592000 // 30 days TTL for old interactions
    }
  }
}

// ============================================================================
// Container: ContextSnapshots (versioned summaries)
// ============================================================================

resource contextSnapshotsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-11-15' = {
  parent: database
  name: 'ContextSnapshots'
  properties: {
    resource: {
      id: 'ContextSnapshots'
      partitionKey: {
        paths: ['/deviceId']
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
      }
    }
  }
}

// ============================================================================
// Container: AgentChatHistory (agent-specific chat audit - permanent)
// ============================================================================

resource agentChatHistoryContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-11-15' = {
  parent: database
  name: 'AgentChatHistory'
  properties: {
    resource: {
      id: 'AgentChatHistory'
      partitionKey: {
        paths: ['/agentId']
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
      }
      // No TTL - permanent audit trail
    }
  }
}

// ============================================================================
// Outputs
// ============================================================================

output accountId string = cosmosAccount.id
output accountName string = cosmosAccount.name
output endpoint string = cosmosAccount.properties.documentEndpoint
output primaryKey string = cosmosAccount.listKeys().primaryMasterKey
output primaryConnectionString string = 'AccountEndpoint=${cosmosAccount.properties.documentEndpoint};AccountKey=${cosmosAccount.listKeys().primaryMasterKey};'
output databaseName string = database.name
