# 🚀 Quick Start Guide

Deploy the WAiSA PoC infrastructure in under 5 minutes!

## Prerequisites Check

```bash
# 1. Verify Azure CLI is installed
az --version

# 2. Login to Azure
az login

# 3. Verify your subscription
az account show

# 4. Get your Object ID (save this for later)
az ad signed-in-user show --query id -o tsv
```

## Deploy in 3 Steps

### Step 1: Navigate to infra directory
```bash
cd infra
```

### Step 2: Update parameters
Edit `main.bicepparam` and set:
- `sqlAdminPassword` - Your secure password
- `keyVaultAdminObjectId` - Your Object ID from prerequisites

### Step 3: Deploy!
```bash
# Quick deploy (interactive)
./deploy.sh

# Or with parameters
./deploy.sh \
  --sql-password 'YourSecurePassword123!' \
  --admin-object-id 'your-object-id-here'
```

## What Gets Deployed?

✅ Azure OpenAI (GPT-4o + Embeddings)
✅ Cosmos DB (Serverless)
✅ SQL Database (Basic)
✅ Service Bus (Basic)
✅ SignalR (Free)
✅ AI Search (Basic)
✅ Storage Accounts (x2)
✅ App Service + Plan
✅ Key Vault
✅ Application Insights
✅ Virtual Network

## After Deployment

```bash
# View outputs
cat deployment-outputs.txt

# View in Azure Portal
# Link will be shown after deployment completes
```

## Cleanup

```bash
# Delete everything
az group delete --name waisa-poc-rg --yes --no-wait
```

---

📖 For detailed instructions, see [README.md](README.md)
