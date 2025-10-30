// ============================================================================
// Azure AI Search Module - Vector Search for Knowledge Base
// ============================================================================

@description('Azure region')
param location string

@description('Search service name')
param searchServiceName string

@description('Resource tags')
param tags object

@description('Search service SKU')
@allowed(['free', 'basic', 'standard', 'standard2', 'standard3', 'storage_optimized_l1', 'storage_optimized_l2'])
param sku string = 'basic'

@description('Replica count')
param replicaCount int = 1

@description('Partition count')
param partitionCount int = 1

// ============================================================================
// Azure AI Search Service
// ============================================================================

resource searchService 'Microsoft.Search/searchServices@2023-11-01' = {
  name: searchServiceName
  location: location
  tags: tags
  sku: {
    name: sku
  }
  properties: {
    replicaCount: replicaCount
    partitionCount: partitionCount
    hostingMode: 'default'
    publicNetworkAccess: 'enabled'
    encryptionWithCmk: {
      enforcement: 'Unspecified'
    }
    disableLocalAuth: false
    authOptions: {
      apiKeyOnly: {}
    }
    semanticSearch: 'free'
  }
}

// ============================================================================
// Outputs
// ============================================================================

output searchServiceId string = searchService.id
output searchServiceName string = searchService.name
output endpoint string = 'https://${searchService.name}.search.windows.net'
output adminKey string = searchService.listAdminKeys().primaryKey
output queryKey string = searchService.listQueryKeys().value[0].key

@description('Index schema for knowledge base - Create via application code or REST API')
output knowledgeBaseIndexSchema string = '''
{
  "name": "knowledge-base-index",
  "fields": [
    {"name": "id", "type": "Edm.String", "key": true, "searchable": false},
    {"name": "title", "type": "Edm.String", "searchable": true, "filterable": false},
    {"name": "description", "type": "Edm.String", "searchable": true, "filterable": false},
    {"name": "solution", "type": "Edm.String", "searchable": true, "filterable": false},
    {"name": "category", "type": "Edm.String", "searchable": false, "filterable": true, "facetable": true},
    {"name": "application", "type": "Edm.String", "searchable": false, "filterable": true, "facetable": true},
    {"name": "severity", "type": "Edm.String", "searchable": false, "filterable": true, "facetable": true},
    {"name": "tags", "type": "Collection(Edm.String)", "searchable": true, "filterable": true, "facetable": true},
    {"name": "embedding", "type": "Collection(Edm.Single)", "dimensions": 1536, "vectorSearchProfile": "vector-profile"},
    {"name": "successCount", "type": "Edm.Int32", "searchable": false, "filterable": true, "sortable": true},
    {"name": "failureCount", "type": "Edm.Int32", "searchable": false, "filterable": true, "sortable": true},
    {"name": "createdAt", "type": "Edm.DateTimeOffset", "searchable": false, "filterable": true, "sortable": true},
    {"name": "updatedAt", "type": "Edm.DateTimeOffset", "searchable": false, "filterable": true, "sortable": true}
  ],
  "vectorSearch": {
    "algorithms": [
      {
        "name": "vector-config",
        "kind": "hnsw",
        "hnswParameters": {
          "m": 4,
          "efConstruction": 400,
          "efSearch": 500,
          "metric": "cosine"
        }
      }
    ],
    "profiles": [
      {
        "name": "vector-profile",
        "algorithm": "vector-config"
      }
    ]
  },
  "semantic": {
    "configurations": [
      {
        "name": "semantic-config",
        "prioritizedFields": {
          "titleField": {"fieldName": "title"},
          "contentFields": [{"fieldName": "description"}, {"fieldName": "solution"}],
          "keywordsFields": [{"fieldName": "tags"}]
        }
      }
    ]
  }
}
'''
