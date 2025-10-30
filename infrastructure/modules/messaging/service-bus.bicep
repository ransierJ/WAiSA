// ============================================================================
// Service Bus Module - Message Queue
// ============================================================================

@description('Azure region')
param location string

@description('Service Bus namespace name')
param namespaceName string

@description('Resource tags')
param tags object

@description('Service Bus SKU')
@allowed(['Basic', 'Standard', 'Premium'])
param sku string = 'Basic'

// ============================================================================
// Service Bus Namespace
// ============================================================================

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: namespaceName
  location: location
  tags: tags
  sku: {
    name: sku
    tier: sku
  }
  properties: {
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: false
  }
}

// ============================================================================
// Queue: Command Queue
// ============================================================================

resource commandQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'command-queue'
  properties: {
    lockDuration: 'PT5M'
    maxSizeInMegabytes: 1024
    requiresDuplicateDetection: false
    requiresSession: false
    defaultMessageTimeToLive: 'P1D'
    deadLetteringOnMessageExpiration: true
    maxDeliveryCount: 5
    enableBatchedOperations: true
    enablePartitioning: false
    enableExpress: false
  }
}

// ============================================================================
// Queue: Event Queue
// ============================================================================

resource eventQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'event-queue'
  properties: {
    lockDuration: 'PT3M'
    maxSizeInMegabytes: 1024
    requiresDuplicateDetection: false
    requiresSession: false
    defaultMessageTimeToLive: 'P1D'
    deadLetteringOnMessageExpiration: true
    maxDeliveryCount: 10
    enableBatchedOperations: true
    enablePartitioning: false
    enableExpress: false
  }
}

// ============================================================================
// Outputs
// ============================================================================

output namespaceId string = serviceBusNamespace.id
output namespaceName string = serviceBusNamespace.name
output primaryConnectionString string = listKeys('${serviceBusNamespace.id}/AuthorizationRules/RootManageSharedAccessKey', serviceBusNamespace.apiVersion).primaryConnectionString
output commandQueueName string = commandQueue.name
output eventQueueName string = eventQueue.name
