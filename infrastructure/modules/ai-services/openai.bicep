// ============================================================================
// Azure OpenAI Module - GPT-4 and Embeddings
// ============================================================================

@description('Azure region')
param location string

@description('Azure OpenAI account name')
param accountName string

@description('Resource tags')
param tags object

// ============================================================================
// Azure OpenAI Account
// ============================================================================

resource openAIAccount 'Microsoft.CognitiveServices/accounts@2023-10-01-preview' = {
  name: accountName
  location: location
  tags: tags
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: accountName
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
    }
  }
}

// ============================================================================
// Model Deployments - Will be created via Azure CLI after deployment
// Note: Bicep doesn't yet fully support OpenAI deployments in all regions
// Use the Azure CLI commands from the output nextSteps
// ============================================================================

// ============================================================================
// Outputs
// ============================================================================

output accountId string = openAIAccount.id
output accountName string = openAIAccount.name
output endpoint string = openAIAccount.properties.endpoint
output apiKey string = openAIAccount.listKeys().key1

@description('Commands to deploy models')
output deploymentCommands string = '''
# Deploy GPT-4 model:
az cognitiveservices account deployment create \\
  --name ${openAIAccount.name} \\
  --resource-group <resource-group-name> \\
  --deployment-name gpt-4 \\
  --model-name gpt-4 \\
  --model-version "0613" \\
  --model-format OpenAI \\
  --sku-capacity 10 \\
  --sku-name "Standard"

# Deploy text-embedding-3-large model:
az cognitiveservices account deployment create \\
  --name ${openAIAccount.name} \\
  --resource-group <resource-group-name> \\
  --deployment-name text-embedding-3-large \\
  --model-name text-embedding-3-large \\
  --model-version "1" \\
  --model-format OpenAI \\
  --sku-capacity 10 \\
  --sku-name "Standard"
'''
