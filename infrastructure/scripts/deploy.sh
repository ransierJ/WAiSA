#!/bin/bash

# ============================================================================
# AI Windows System Administrator - Azure Infrastructure Deployment Script
# ============================================================================

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
RESOURCE_GROUP_NAME="rg-SAIB"
LOCATION="eastus2"
DEPLOYMENT_NAME="ai-sysadmin-deployment-$(date +%Y%m%d-%H%M%S)"

echo -e "${BLUE}============================================================================${NC}"
echo -e "${BLUE}AI Windows System Administrator - Infrastructure Deployment${NC}"
echo -e "${BLUE}============================================================================${NC}"
echo ""

# Check if logged in to Azure
echo -e "${YELLOW}Checking Azure CLI login...${NC}"
if ! az account show > /dev/null 2>&1; then
    echo -e "${RED}Not logged in to Azure. Please run 'az login' first.${NC}"
    exit 1
fi

SUBSCRIPTION_ID=$(az account show --query id -o tsv)
SUBSCRIPTION_NAME=$(az account show --query name -o tsv)
echo -e "${GREEN}✓ Logged in to Azure${NC}"
echo -e "  Subscription: ${SUBSCRIPTION_NAME}"
echo -e "  Subscription ID: ${SUBSCRIPTION_ID}"
echo ""

# Generate secure password for SQL Server
echo -e "${YELLOW}Generating secure SQL admin password...${NC}"
SQL_PASSWORD=$(openssl rand -base64 32 | tr -d "=+/" | cut -c1-25)
echo -e "${GREEN}✓ Password generated${NC}"
echo ""

# Create resource group
echo -e "${YELLOW}Creating resource group...${NC}"
if az group show --name "${RESOURCE_GROUP_NAME}" > /dev/null 2>&1; then
    echo -e "${YELLOW}Resource group already exists${NC}"
else
    az group create \
        --name "${RESOURCE_GROUP_NAME}" \
        --location "${LOCATION}" \
        --tags project="AI-Windows-SysAdmin" environment="poc" \
        --output none
    echo -e "${GREEN}✓ Resource group created${NC}"
fi
echo ""

# Validate Bicep template
echo -e "${YELLOW}Validating Bicep template...${NC}"
az deployment group validate \
    --resource-group "${RESOURCE_GROUP_NAME}" \
    --template-file ../main.bicep \
    --parameters \
        environmentName="poc" \
        projectName="ai-sysadmin" \
        location="${LOCATION}" \
        sqlAdminLogin="sqladmin" \
        sqlAdminPassword="${SQL_PASSWORD}" \
    --output none

if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓ Template validation successful${NC}"
else
    echo -e "${RED}✗ Template validation failed${NC}"
    exit 1
fi
echo ""

# Deploy infrastructure
echo -e "${YELLOW}Deploying infrastructure...${NC}"
echo -e "${YELLOW}This may take 10-15 minutes...${NC}"
echo ""

az deployment group create \
    --name "${DEPLOYMENT_NAME}" \
    --resource-group "${RESOURCE_GROUP_NAME}" \
    --template-file ../main.bicep \
    --parameters \
        environmentName="poc" \
        projectName="ai-sysadmin" \
        location="${LOCATION}" \
        sqlAdminLogin="sqladmin" \
        sqlAdminPassword="${SQL_PASSWORD}" \
    --output json > deployment-output.json

if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓ Infrastructure deployment successful!${NC}"
else
    echo -e "${RED}✗ Infrastructure deployment failed${NC}"
    exit 1
fi
echo ""

# Extract outputs
echo -e "${YELLOW}Extracting deployment outputs...${NC}"
WEB_APP_URL=$(jq -r '.properties.outputs.webAppUrl.value' deployment-output.json)
KEY_VAULT_NAME=$(jq -r '.properties.outputs.keyVaultName.value' deployment-output.json)
OPENAI_ACCOUNT=$(jq -r '.properties.outputs.openAiAccountName.value' deployment-output.json)
SEARCH_SERVICE=$(jq -r '.properties.outputs.searchServiceName.value' deployment-output.json)
echo -e "${GREEN}✓ Outputs extracted${NC}"
echo ""

# Save credentials
echo -e "${YELLOW}Saving deployment information...${NC}"
cat > deployment-info.txt << EOF
============================================================================
AI Windows System Administrator - Deployment Information
============================================================================
Deployed: $(date)
Resource Group: ${RESOURCE_GROUP_NAME}
Location: ${LOCATION}

IMPORTANT: Save these credentials securely!

SQL Server Admin Login: sqladmin
SQL Server Admin Password: ${SQL_PASSWORD}

Web App URL: ${WEB_APP_URL}
Key Vault Name: ${KEY_VAULT_NAME}
OpenAI Account: ${OPENAI_ACCOUNT}
Search Service: ${SEARCH_SERVICE}

============================================================================
Next Steps:
============================================================================

1. Deploy OpenAI models:

   # Deploy GPT-4
   az cognitiveservices account deployment create \\
     --name ${OPENAI_ACCOUNT} \\
     --resource-group ${RESOURCE_GROUP_NAME} \\
     --deployment-name gpt-4 \\
     --model-name gpt-4 \\
     --model-version "0613" \\
     --model-format OpenAI \\
     --sku-capacity 10 \\
     --sku-name "Standard"

   # Deploy embeddings model
   az cognitiveservices account deployment create \\
     --name ${OPENAI_ACCOUNT} \\
     --resource-group ${RESOURCE_GROUP_NAME} \\
     --deployment-name text-embedding-3-large \\
     --model-name text-embedding-3-large \\
     --model-version "1" \\
     --model-format OpenAI \\
     --sku-capacity 10 \\
     --sku-name "Standard"

2. Access Key Vault:
   az keyvault show --name ${KEY_VAULT_NAME}

3. Deploy your application to:
   ${WEB_APP_URL}

============================================================================
EOF

echo -e "${GREEN}✓ Deployment information saved to: deployment-info.txt${NC}"
echo ""

# Display summary
echo -e "${BLUE}============================================================================${NC}"
echo -e "${GREEN}Deployment Complete!${NC}"
echo -e "${BLUE}============================================================================${NC}"
echo ""
echo -e "${YELLOW}Resource Group:${NC} ${RESOURCE_GROUP_NAME}"
echo -e "${YELLOW}Web App URL:${NC} ${WEB_APP_URL}"
echo -e "${YELLOW}Key Vault:${NC} ${KEY_VAULT_NAME}"
echo ""
echo -e "${RED}IMPORTANT:${NC} Deployment credentials saved to: ${GREEN}deployment-info.txt${NC}"
echo -e "${RED}Keep this file secure and do not commit it to version control!${NC}"
echo ""
echo -e "${YELLOW}Next: Deploy OpenAI models (see deployment-info.txt for commands)${NC}"
echo ""
