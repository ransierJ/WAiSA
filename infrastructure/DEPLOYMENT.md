# WAiSA Production Deployment Guide

**Date**: October 23, 2025
**Version**: 1.0.0
**Target**: Azure Cloud

---

## 📋 Prerequisites

Before deploying WAiSA to Azure, ensure you have:

### Required Tools
- ✅ **Azure CLI** (v2.50.0+) - Installed and authenticated
- ✅ **.NET 8.0 SDK** - For building and migrating the backend
- ✅ **Node.js 20.x** - For building the frontend
- ✅ **Git** - For version control

### Azure Subscription Requirements
- ✅ Active Azure subscription with appropriate permissions
- ✅ Sufficient quota for:
  - Azure OpenAI Service (if not already allocated)
  - App Service (Basic or higher tier)
  - SQL Database
  - Cosmos DB
  - Azure AI Search

### Estimated Monthly Costs (POC Environment)
- **Basic Tier**: ~$150-200/month
  - App Service Basic (B1): ~$13/month
  - SQL Database Basic: ~$5/month
  - Cosmos DB (400 RU/s): ~$24/month
  - Azure OpenAI (pay-per-token): Variable
  - Storage, Service Bus, SignalR: ~$10-20/month
  - AI Search Basic: ~$75/month

---

## 🚀 Quick Start

### Option 1: Automated Deployment (Recommended)

```bash
# Navigate to infrastructure directory
cd infrastructure

# Run the deployment script
./deploy.sh
```

The script will:
1. ✅ Check prerequisites
2. ✅ Prompt for SQL credentials
3. ✅ Create resource group
4. ✅ Deploy all Azure resources
5. ✅ Configure Azure OpenAI models
6. ✅ Run database migrations
7. ✅ Deploy backend API
8. ✅ Provide deployment summary

**Estimated time**: 15-20 minutes

---

### Option 2: Manual Step-by-Step Deployment

#### Step 1: Create Resource Group

```bash
RESOURCE_GROUP="waisa-poc-rg"
LOCATION="eastus2"

az group create \
  --name $RESOURCE_GROUP \
  --location $LOCATION \
  --tags project=WAiSA environment=poc
```

#### Step 2: Deploy Infrastructure

```bash
# Using inline parameters
az deployment group create \
  --resource-group $RESOURCE_GROUP \
  --template-file main.bicep \
  --parameters \
    environmentName=poc \
    projectName=waisa \
    location=eastus2 \
    sqlAdminLogin=waisaadmin \
    sqlAdminPassword='YourSecurePassword123!'
```

Or using a parameters file:

```bash
# Copy and edit parameters file
cp parameters.example.json parameters.poc.json
# Edit parameters.poc.json with your values

# Deploy using parameters file
az deployment group create \
  --resource-group $RESOURCE_GROUP \
  --template-file main.bicep \
  --parameters @parameters.poc.json
```

**Deployment time**: ~10-15 minutes

#### Step 3: Extract Resource Names

```bash
# Get deployment outputs
az deployment group show \
  --resource-group $RESOURCE_GROUP \
  --name main \
  --query properties.outputs \
  --output json > deployment-outputs.json

# Extract specific values
OPENAI_ACCOUNT=$(az deployment group show --resource-group $RESOURCE_GROUP --name main --query 'properties.outputs.openAiAccountName.value' -o tsv)
WEB_APP_NAME=$(az deployment group show --resource-group $RESOURCE_GROUP --name main --query 'properties.outputs.webAppName.value' -o tsv)
SQL_SERVER=$(az deployment group show --resource-group $RESOURCE_GROUP --name main --query 'properties.outputs.sqlServerName.value' -o tsv)

echo "OpenAI Account: $OPENAI_ACCOUNT"
echo "Web App: $WEB_APP_NAME"
echo "SQL Server: $SQL_SERVER"
```

#### Step 4: Deploy Azure OpenAI Models

```bash
# Deploy GPT-4 model
az cognitiveservices account deployment create \
  --name $OPENAI_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --deployment-name gpt-4 \
  --model-name gpt-4 \
  --model-version "0613" \
  --model-format OpenAI \
  --sku-capacity 10 \
  --sku-name "Standard"

# Deploy embedding model
az cognitiveservices account deployment create \
  --name $OPENAI_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --deployment-name text-embedding-ada-002 \
  --model-name text-embedding-ada-002 \
  --model-version "2" \
  --model-format OpenAI \
  --sku-capacity 10 \
  --sku-name "Standard"
```

**Note**: If you encounter quota errors, request quota increase in Azure Portal or use alternative models.

#### Step 5: Run Database Migrations

```bash
cd ../backend

# Build solution
dotnet build WAiSA.sln --configuration Release

# Run Entity Framework migrations
dotnet ef database update \
  --project WAiSA.Infrastructure \
  --startup-project WAiSA.API \
  --connection "Server=tcp:$SQL_SERVER.database.windows.net,1433;Database=WAiSADB;User ID=waisaadmin;Password=YourPassword;Encrypt=true;"
```

#### Step 6: Deploy Backend API

```bash
# Publish backend
dotnet publish WAiSA.API/WAiSA.API.csproj \
  --configuration Release \
  --output ./publish

# Create deployment package
cd publish
zip -r ../deploy.zip .
cd ..

# Deploy to App Service
az webapp deployment source config-zip \
  --resource-group $RESOURCE_GROUP \
  --name $WEB_APP_NAME \
  --src deploy.zip

# Clean up
rm deploy.zip
rm -rf publish
```

#### Step 7: Verify Deployment

```bash
# Check web app status
az webapp show \
  --resource-group $RESOURCE_GROUP \
  --name $WEB_APP_NAME \
  --query state

# Test health endpoint
curl https://$WEB_APP_NAME.azurewebsites.net/health

# View logs
az webapp log tail \
  --resource-group $RESOURCE_GROUP \
  --name $WEB_APP_NAME
```

---

## 🎨 Deploy Frontend

### Option 1: Azure Static Web Apps (Recommended)

```bash
cd ../frontend

# Build frontend
npm install
npm run build

# Deploy using SWA CLI
npx @azure/static-web-apps-cli deploy \
  --app-location ./dist \
  --resource-group $RESOURCE_GROUP \
  --app-name waisa-poc-web
```

### Option 2: Azure Storage Static Website

```bash
# Enable static website on storage account
STORAGE_ACCOUNT=$(az storage account list \
  --resource-group $RESOURCE_GROUP \
  --query "[0].name" -o tsv)

az storage blob service-properties update \
  --account-name $STORAGE_ACCOUNT \
  --static-website \
  --index-document index.html \
  --404-document index.html

# Upload build files
az storage blob upload-batch \
  --account-name $STORAGE_ACCOUNT \
  --source ./dist \
  --destination '$web'

# Get website URL
az storage account show \
  --name $STORAGE_ACCOUNT \
  --query "primaryEndpoints.web" -o tsv
```

---

## 🔧 Configuration

### Environment Variables

After deployment, configure these App Service application settings:

```bash
az webapp config appsettings set \
  --resource-group $RESOURCE_GROUP \
  --name $WEB_APP_NAME \
  --settings \
    "ASPNETCORE_ENVIRONMENT=Production" \
    "AzureOpenAI__Endpoint=https://$OPENAI_ACCOUNT.openai.azure.com/" \
    "AzureOpenAI__DeploymentName=gpt-4" \
    "Logging__LogLevel__Default=Information"
```

### Key Vault Integration

The deployment automatically stores sensitive connection strings in Azure Key Vault and configures App Service managed identity for access.

Verify Key Vault secrets:

```bash
KEY_VAULT=$(az keyvault list --resource-group $RESOURCE_GROUP --query "[0].name" -o tsv)

az keyvault secret list --vault-name $KEY_VAULT --output table
```

---

## 🛡️ Security Hardening

### 1. Enable Azure AD Authentication

```bash
# Configure Azure AD authentication for App Service
az webapp auth update \
  --resource-group $RESOURCE_GROUP \
  --name $WEB_APP_NAME \
  --enabled true \
  --action LoginWithAzureActiveDirectory
```

### 2. Configure Firewall Rules

```bash
# Restrict SQL Server access
az sql server firewall-rule create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0

# Add your IP for management access
MY_IP=$(curl -s ifconfig.me)
az sql server firewall-rule create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER \
  --name AllowMyIP \
  --start-ip-address $MY_IP \
  --end-ip-address $MY_IP
```

### 3. Enable HTTPS Only

```bash
az webapp update \
  --resource-group $RESOURCE_GROUP \
  --name $WEB_APP_NAME \
  --https-only true
```

---

## 📊 Monitoring & Observability

### Application Insights

Application Insights is automatically configured. View metrics:

```bash
# Get Application Insights resource
APP_INSIGHTS=$(az monitor app-insights component list \
  --resource-group $RESOURCE_GROUP \
  --query "[0].name" -o tsv)

# View recent exceptions
az monitor app-insights metrics show \
  --resource-group $RESOURCE_GROUP \
  --app $APP_INSIGHTS \
  --metric exceptions/count
```

### Log Analytics

Query logs using Kusto Query Language (KQL):

```bash
# View recent traces
az monitor log-analytics query \
  --workspace $LOG_WORKSPACE \
  --analytics-query "traces | where timestamp > ago(1h) | order by timestamp desc | take 50"
```

### Azure Monitor Alerts

Set up alerts for critical metrics:

```bash
# Alert on API failures
az monitor metrics alert create \
  --name waisa-api-failures \
  --resource-group $RESOURCE_GROUP \
  --scopes "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Web/sites/$WEB_APP_NAME" \
  --condition "avg Http5xx > 5" \
  --window-size 5m \
  --evaluation-frequency 1m
```

---

## 🔄 Continuous Deployment

### GitHub Actions (Already Configured)

The repository includes CI/CD workflows:
- `.github/workflows/backend-ci.yml` - Backend build and test
- `.github/workflows/frontend-ci.yml` - Frontend build

To enable CD, add these GitHub secrets:

```
AZURE_CREDENTIALS           # Service principal JSON
AZURE_SUBSCRIPTION_ID       # Your subscription ID
AZURE_RESOURCE_GROUP        # waisa-poc-rg
AZURE_WEBAPP_NAME          # Output from deployment
```

### Create Service Principal

```bash
az ad sp create-for-rbac \
  --name "waisa-github-deploy" \
  --role contributor \
  --scopes /subscriptions/$(az account show --query id -o tsv)/resourceGroups/$RESOURCE_GROUP \
  --sdk-auth
```

Copy the JSON output to GitHub Secrets as `AZURE_CREDENTIALS`.

---

## 🧪 Post-Deployment Testing

### Health Check

```bash
curl https://$WEB_APP_NAME.azurewebsites.net/health
```

Expected response:
```json
{
  "status": "Healthy",
  "checks": {
    "database": "Healthy",
    "azureOpenAI": "Healthy",
    "cosmosDb": "Healthy"
  }
}
```

### API Documentation

Visit: `https://$WEB_APP_NAME.azurewebsites.net/swagger`

### Test Chat Endpoint

```bash
curl -X POST https://$WEB_APP_NAME.azurewebsites.net/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "deviceId": "test-device-001",
    "message": "What is your name?",
    "conversationId": "test-conversation"
  }'
```

---

## 🔧 Troubleshooting

### Common Issues

#### 1. Azure OpenAI Quota Error

**Error**: "The subscription does not have QuotaId/Feature required by SKU"

**Solution**:
- Request Azure OpenAI access: https://aka.ms/oai/access
- Use alternative region with availability
- Adjust model capacity in deployment

#### 2. App Service Not Starting

**Check logs**:
```bash
az webapp log tail --resource-group $RESOURCE_GROUP --name $WEB_APP_NAME
```

**Common fixes**:
- Verify connection strings in Key Vault
- Check managed identity has Key Vault access
- Ensure .NET 8.0 runtime is selected

#### 3. Database Connection Failures

**Check firewall**:
```bash
az sql server firewall-rule list \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER
```

**Check connection string**:
```bash
KEY_VAULT=$(az keyvault list --resource-group $RESOURCE_GROUP --query "[0].name" -o tsv)
az keyvault secret show --vault-name $KEY_VAULT --name sql-connection-string
```

#### 4. High Costs

**Review pricing**:
```bash
az consumption usage list \
  --start-date $(date -d "7 days ago" +%Y-%m-%d) \
  --end-date $(date +%Y-%m-%d) \
  --output table
```

**Cost optimization**:
- Scale down App Service tier for non-production
- Reduce Cosmos DB RU/s
- Use reserved capacity for predictable workloads

---

## 📚 Additional Resources

- [Azure App Service Documentation](https://docs.microsoft.com/en-us/azure/app-service/)
- [Azure OpenAI Service Documentation](https://docs.microsoft.com/en-us/azure/cognitive-services/openai/)
- [Azure SQL Database Documentation](https://docs.microsoft.com/en-us/azure/azure-sql/)
- [Azure Cosmos DB Documentation](https://docs.microsoft.com/en-us/azure/cosmos-db/)
- [WAiSA GitHub Repository](https://github.com/yourusername/waisa)

---

## 🆘 Support

For issues or questions:
1. Check Application Insights logs
2. Review Azure Monitor alerts
3. Check GitHub Issues
4. Contact: your-email@example.com

---

**Deployment Script**: `deploy.sh`
**Infrastructure Template**: `main.bicep`
**Parameters Example**: `parameters.example.json`

**Happy deploying! 🚀**
