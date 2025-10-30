# WAiSA PoC Infrastructure Deployment

This directory contains Infrastructure as Code (IaC) using Azure Bicep to deploy all resources needed for the **WAiSA (Workplace AI System Assistant)** proof of concept.

## 📋 Resources Deployed

The deployment creates the following Azure resources:

### Core Services
- **Azure OpenAI** (S0 tier)
  - GPT-4o deployment (capacity: 10)
  - text-embedding-ada-002 deployment (capacity: 10)
- **Azure Cosmos DB** (Serverless)
  - GlobalDocumentDB with Session consistency
- **Azure SQL Database** (Basic tier)
  - SQL Server with administrator login
  - WAiSADB database

### Messaging & Real-time
- **Azure Service Bus** (Basic tier with zone redundancy)
- **Azure SignalR Service** (Free tier)

### Search & AI
- **Azure AI Search** (Basic tier)

### Storage
- **Storage Account** (Standard_LRS) - Application storage
- **Storage Account** (Standard_LRS) - Frontend static website

### Monitoring & Logging
- **Application Insights**
- **Log Analytics Workspace**

### Compute
- **App Service Plan** (Basic B1, Linux)
- **App Service** (Linux, .NET Core 8.0)

### Security & Networking
- **Azure Key Vault** (with RBAC, soft delete, and purge protection)
- **Virtual Network** (172.16.0.0/16)
  - Subnet: snet-eastus2-1 (172.16.0.0/24)

## 🔧 Prerequisites

1. **Azure CLI** - Install from [here](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
2. **Azure Subscription** - Active Azure subscription with Contributor or Owner role
3. **Bicep** - Automatically included with Azure CLI 2.20.0+
4. **Bash Shell** - For running the deployment script (Git Bash on Windows, or native on Linux/macOS)

## 🚀 Quick Start

### Option 1: Using the Deployment Script (Recommended)

1. **Navigate to the infra directory:**
   ```bash
   cd infra
   ```

2. **Login to Azure:**
   ```bash
   az login
   ```

3. **Set your subscription (if you have multiple):**
   ```bash
   az account set --subscription "YOUR_SUBSCRIPTION_ID"
   ```

4. **Run the deployment script:**
   ```bash
   ./deploy.sh \
     --resource-group waisa-poc-rg \
     --location eastus2 \
     --sql-password 'YourSecurePassword123!'
   ```

   The script will:
   - Validate prerequisites
   - Get your Azure AD Object ID automatically
   - Run a what-if analysis for validation
   - Prompt for confirmation
   - Deploy all resources
   - Save outputs to `deployment-outputs.txt`

### Option 2: Manual Deployment

1. **Get your Azure AD Object ID:**
   ```bash
   az ad signed-in-user show --query id -o tsv
   ```

2. **Update the parameters file (`main.bicepparam`):**
   - Set `sqlAdminPassword`
   - Set `keyVaultAdminObjectId` to your Object ID from step 1

3. **Validate the deployment:**
   ```bash
   az deployment group what-if \
     --resource-group waisa-poc-rg \
     --template-file main.bicep \
     --parameters main.bicepparam
   ```

4. **Create the resource group:**
   ```bash
   az group create \
     --name waisa-poc-rg \
     --location eastus2 \
     --tags environment=poc project=WAiSA
   ```

5. **Deploy the infrastructure:**
   ```bash
   az deployment group create \
     --name waisa-deployment-$(date +%Y%m%d-%H%M%S) \
     --resource-group waisa-poc-rg \
     --template-file main.bicep \
     --parameters main.bicepparam
   ```

## 📝 Configuration

### Parameters

Edit `main.bicepparam` to customize the deployment:

| Parameter | Description | Default | Required |
|-----------|-------------|---------|----------|
| `environmentName` | Environment name (poc, dev, prod) | `poc` | No |
| `projectName` | Project name for resource naming | `WAiSA` | No |
| `location` | Azure region | Resource group location | No |
| `uniqueSuffix` | Unique suffix for resource names | Auto-generated | No |
| `sqlAdminUsername` | SQL Server admin username | `waisaadmin` | No |
| `sqlAdminPassword` | SQL Server admin password | - | **Yes** |
| `keyVaultAdminObjectId` | Azure AD Object ID for Key Vault access | - | **Yes** |

### Naming Convention

Resources follow this naming pattern:
- **Pattern:** `waisa-{environment}-{resourceType}-{uniqueSuffix}`
- **Example:** `waisa-poc-openai-hv2lph4y32udy`

The `uniqueSuffix` is automatically generated from the resource group ID to ensure global uniqueness.

## 🔒 Security Best Practices

The deployment follows Azure security best practices:

1. **Key Vault:**
   - RBAC authorization enabled
   - Soft delete enabled (90 days retention)
   - Purge protection enabled
   - Admin granted Key Vault Secrets Officer role

2. **Storage Accounts:**
   - TLS 1.2 minimum
   - HTTPS-only traffic
   - Shared key access disabled (RBAC preferred)
   - Public blob access disabled (except frontend storage)

3. **SQL Server:**
   - TLS 1.2 minimum
   - Firewall rule for Azure services
   - Consider adding specific IP rules for your environment

4. **Cosmos DB:**
   - TLS 1.2 minimum
   - Geo-redundant backup
   - Consider enabling RBAC authentication

5. **App Services:**
   - HTTPS only
   - TLS 1.2 minimum
   - FTPS disabled

## 📊 Post-Deployment Tasks

After deployment completes:

### 1. Store Secrets in Key Vault

```bash
# Get Key Vault name from outputs
KV_NAME=$(jq -r '.properties.outputs.keyVaultUri.value' deployment-output.json | sed 's|https://||' | sed 's|.vault.azure.net/||')

# Store SQL password
az keyvault secret set \
  --vault-name $KV_NAME \
  --name "sql-admin-password" \
  --value "YourSecurePassword123!"

# Store OpenAI key
OPENAI_KEY=$(az cognitiveservices account keys list \
  --resource-group waisa-poc-rg \
  --name $(jq -r '.properties.outputs.openAIName.value' deployment-output.json) \
  --query key1 -o tsv)

az keyvault secret set \
  --vault-name $KV_NAME \
  --name "openai-api-key" \
  --value "$OPENAI_KEY"
```

### 2. Configure App Service Settings

```bash
# Add connection strings and app settings
az webapp config appsettings set \
  --resource-group waisa-poc-rg \
  --name $(jq -r '.properties.outputs.appServiceHostname.value' deployment-output.json | cut -d. -f1) \
  --settings \
    "CosmosDb__Endpoint=$(jq -r '.properties.outputs.cosmosDbEndpoint.value' deployment-output.json)" \
    "OpenAI__Endpoint=$(jq -r '.properties.outputs.openAIEndpoint.value' deployment-output.json)"
```

### 3. Configure SQL Server Firewall

Add your IP address for local development:

```bash
MY_IP=$(curl -s https://api.ipify.org)

az sql server firewall-rule create \
  --resource-group waisa-poc-rg \
  --server $(jq -r '.properties.outputs.sqlServerFqdn.value' deployment-output.json | cut -d. -f1) \
  --name "AllowMyIP" \
  --start-ip-address $MY_IP \
  --end-ip-address $MY_IP
```

### 4. Configure Frontend Static Website

Enable static website hosting on the frontend storage account:

```bash
az storage blob service-properties update \
  --account-name $(jq -r '.properties.outputs.frontendStorageAccountName.value' deployment-output.json) \
  --static-website \
  --404-document 404.html \
  --index-document index.html
```

### 5. Test OpenAI Deployments

```bash
# Test GPT-4o endpoint
OPENAI_ENDPOINT=$(jq -r '.properties.outputs.openAIEndpoint.value' deployment-output.json)
echo "OpenAI endpoint: $OPENAI_ENDPOINT"

# List deployments
az cognitiveservices account deployment list \
  --resource-group waisa-poc-rg \
  --name $(jq -r '.properties.outputs.openAIName.value' deployment-output.json) \
  --output table
```

## 🧹 Cleanup

To delete all deployed resources:

```bash
az group delete \
  --name waisa-poc-rg \
  --yes \
  --no-wait
```

⚠️ **Warning:** This will delete all resources in the resource group. Key Vault will be soft-deleted and can be recovered within 90 days.

## 🔄 Redeployment

To redeploy with the same resource names:

1. Set a fixed `uniqueSuffix` in `main.bicepparam`:
   ```bicep
   param uniqueSuffix = 'hv2lph4y32udy'
   ```

2. Run the deployment script or Azure CLI command again.

## 📁 Files

- `main.bicep` - Main Bicep template with all resource definitions
- `main.bicepparam` - Parameters file for customization
- `deploy.sh` - Automated deployment script with validation
- `README.md` - This documentation file
- `deployment-output.json` - Generated after deployment (contains outputs)
- `deployment-outputs.txt` - Generated after deployment (key-value pairs)

## 🐛 Troubleshooting

### Deployment Fails with "Name Already Exists"

Some Azure resources require globally unique names. If you see this error:
1. Delete the conflicting resource or choose a different name
2. Set a custom `uniqueSuffix` parameter
3. Redeploy

### Permission Denied Errors

Ensure you have:
- **Contributor** or **Owner** role on the subscription or resource group
- **User Access Administrator** role to assign RBAC roles (for Key Vault)

### OpenAI Capacity Issues

If model deployments fail:
1. Check regional capacity availability
2. Try a different region (e.g., `westus`, `westeurope`)
3. Request a quota increase via Azure Portal

### Key Vault Access Issues

If you can't access Key Vault:
1. Verify your Object ID was correctly set
2. Check role assignments:
   ```bash
   az role assignment list \
     --scope "/subscriptions/YOUR_SUB/resourceGroups/waisa-poc-rg/providers/Microsoft.KeyVault/vaults/kvXXXXXX" \
     --assignee YOUR_OBJECT_ID
   ```

## 📚 Additional Resources

- [Azure Bicep Documentation](https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/)
- [Azure OpenAI Service](https://learn.microsoft.com/en-us/azure/ai-services/openai/)
- [Azure Cosmos DB](https://docs.microsoft.com/en-us/azure/cosmos-db/)
- [Azure Key Vault](https://docs.microsoft.com/en-us/azure/key-vault/)
- [Azure App Service](https://docs.microsoft.com/en-us/azure/app-service/)

## 📞 Support

For issues or questions:
1. Check the [Troubleshooting](#troubleshooting) section
2. Review Azure deployment logs in the Azure Portal
3. Check `deployment-output.json` for detailed error messages

---

**Version:** 1.0
**Last Updated:** 2025-10-30
**Project:** WAiSA (Workplace AI System Assistant) PoC
