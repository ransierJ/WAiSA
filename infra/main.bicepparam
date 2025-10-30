// Parameters file for WAiSA PoC Infrastructure Deployment
using './main.bicep'

// Environment and project configuration
param environmentName = 'poc'
param projectName = 'WAiSA'

// Location will default to resource group location
// param location = 'eastus2'

// Unique suffix will be auto-generated based on resource group ID
// If you want consistent naming across deployments, uncomment and set a fixed value
// param uniqueSuffix = 'hv2lph4y32udy'

// SQL Server credentials
// IMPORTANT: Replace these with secure values or use Azure Key Vault references
// For production, use Key Vault references:
// param sqlAdminUsername = getSecret('subscriptionId', 'resourceGroup', 'keyVaultName', 'secretName')
param sqlAdminUsername = 'waisaadmin'
param sqlAdminPassword = '' // SET THIS VALUE or use --parameters flag during deployment

// Azure AD Object ID for Key Vault access
// Get your Object ID by running: az ad signed-in-user show --query id -o tsv
param keyVaultAdminObjectId = '' // SET THIS VALUE

// Tags
param tags = {
  environment: 'poc'
  project: 'WAiSA'
  managedBy: 'bicep'
  createdDate: '2025-10-30'
}
