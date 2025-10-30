// ============================================================================
// SignalR Service Module - Real-time Communication
// ============================================================================

@description('Azure region')
param location string

@description('SignalR service name')
param signalRName string

@description('Resource tags')
param tags object

@description('SignalR SKU')
@allowed(['Free_F1', 'Standard_S1'])
param sku string = 'Free_F1'

@description('Unit capacity')
param capacity int = 1

// ============================================================================
// SignalR Service
// ============================================================================

resource signalR 'Microsoft.SignalRService/signalR@2023-02-01' = {
  name: signalRName
  location: location
  tags: tags
  sku: {
    name: sku
    capacity: capacity
  }
  kind: 'SignalR'
  properties: {
    cors: {
      allowedOrigins: [
        '*' // For POC - restrict in production
      ]
    }
    features: [
      {
        flag: 'ServiceMode'
        value: 'Default'
      }
      {
        flag: 'EnableConnectivityLogs'
        value: 'True'
      }
      {
        flag: 'EnableMessagingLogs'
        value: 'True'
      }
    ]
    publicNetworkAccess: 'Enabled'
    tls: {
      clientCertEnabled: false
    }
  }
}

// ============================================================================
// Outputs
// ============================================================================

output signalRId string = signalR.id
output signalRName string = signalR.name
output hostName string = signalR.properties.hostName
output primaryEndpoint string = 'https://${signalR.properties.hostName}'
output primaryKey string = signalR.listKeys().primaryKey
output primaryConnectionString string = signalR.listKeys().primaryConnectionString
