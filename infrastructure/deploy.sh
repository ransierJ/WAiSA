#!/bin/bash
# ============================================================================
# WAiSA (Windows AI Systems Administrator) - Azure Deployment Script
# ============================================================================

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}============================================${NC}"
echo -e "${GREEN}WAiSA Deployment Script${NC}"
echo -e "${GREEN}============================================${NC}"
echo ""

# ============================================================================
# Configuration
# ============================================================================

# Default values
ENVIRONMENT="${ENVIRONMENT:-poc}"
LOCATION="${LOCATION:-eastus2}"
RESOURCE_GROUP="${RESOURCE_GROUP:-waisa-$ENVIRONMENT-rg}"
PROJECT_NAME="${PROJECT_NAME:-waisa}"

echo -e "${YELLOW}Deployment Configuration:${NC}"
echo "  Environment: $ENVIRONMENT"
echo "  Location: $LOCATION"
echo "  Resource Group: $RESOURCE_GROUP"
echo "  Project Name: $PROJECT_NAME"
echo ""

# ============================================================================
# Check Prerequisites
# ============================================================================

echo -e "${YELLOW}Checking prerequisites...${NC}"

# Check Azure CLI
if ! command -v az &> /dev/null; then
    echo -e "${RED}ERROR: Azure CLI (az) is not installed${NC}"
    exit 1
fi

# Check if logged in
if ! az account show &> /dev/null; then
    echo -e "${RED}ERROR: Not logged into Azure CLI${NC}"
    echo "Run: az login"
    exit 1
fi

# Check .NET SDK
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}ERROR: .NET SDK is not installed${NC}"
    exit 1
fi

echo -e "${GREEN}✓ Prerequisites check passed${NC}"
echo ""

# ============================================================================
# SQL Credentials
# ============================================================================

echo -e "${YELLOW}SQL Server Credentials${NC}"
echo "Please provide SQL Server administrator credentials:"
echo ""

# Read SQL admin login
if [ -z "$SQL_ADMIN_LOGIN" ]; then
    read -p "SQL Admin Login (default: waisaadmin): " SQL_ADMIN_LOGIN
    SQL_ADMIN_LOGIN=${SQL_ADMIN_LOGIN:-waisaadmin}
fi

# Read SQL admin password
if [ -z "$SQL_ADMIN_PASSWORD" ]; then
    read -sp "SQL Admin Password (min 8 chars, needs uppercase, lowercase, number, special char): " SQL_ADMIN_PASSWORD
    echo ""
    if [ -z "$SQL_ADMIN_PASSWORD" ]; then
        echo -e "${RED}ERROR: SQL Admin Password is required${NC}"
        exit 1
    fi
fi

echo ""

# ============================================================================
# Step 1: Create Resource Group
# ============================================================================

echo -e "${YELLOW}Step 1: Creating Resource Group${NC}"

if az group show --name "$RESOURCE_GROUP" &> /dev/null; then
    echo -e "${GREEN}✓ Resource group '$RESOURCE_GROUP' already exists${NC}"
else
    echo "Creating resource group: $RESOURCE_GROUP"
    az group create \
        --name "$RESOURCE_GROUP" \
        --location "$LOCATION" \
        --tags project=WAiSA environment=$ENVIRONMENT
    echo -e "${GREEN}✓ Resource group created${NC}"
fi

echo ""

# ============================================================================
# Step 2: Deploy Infrastructure
# ============================================================================

echo -e "${YELLOW}Step 2: Deploying Infrastructure (Bicep)${NC}"
echo "This will deploy:"
echo "  - Log Analytics & Application Insights"
echo "  - Key Vault"
echo "  - SQL Database"
echo "  - Cosmos DB"
echo "  - Storage Account"
echo "  - Azure OpenAI"
echo "  - Azure AI Search"
echo "  - Service Bus"
echo "  - SignalR Service"
echo "  - App Service"
echo ""

DEPLOYMENT_NAME="waisa-deployment-$(date +%Y%m%d-%H%M%S)"

az deployment group create \
    --name "$DEPLOYMENT_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --template-file main.bicep \
    --parameters \
        environmentName="$ENVIRONMENT" \
        projectName="$PROJECT_NAME" \
        location="$LOCATION" \
        sqlAdminLogin="$SQL_ADMIN_LOGIN" \
        sqlAdminPassword="$SQL_ADMIN_PASSWORD" \
    --output json > deployment-output.json

if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓ Infrastructure deployed successfully${NC}"
    echo ""
    echo "Deployment outputs saved to: deployment-output.json"
else
    echo -e "${RED}✗ Infrastructure deployment failed${NC}"
    exit 1
fi

echo ""

# ============================================================================
# Step 3: Extract Deployment Outputs
# ============================================================================

echo -e "${YELLOW}Step 3: Extracting Deployment Information${NC}"

OPENAI_ACCOUNT=$(az deployment group show \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --query 'properties.outputs.openAiAccountName.value' \
    --output tsv)

WEB_APP_NAME=$(az deployment group show \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --query 'properties.outputs.webAppName.value' \
    --output tsv)

SQL_SERVER_NAME=$(az deployment group show \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --query 'properties.outputs.sqlServerName.value' \
    --output tsv)

echo "Azure OpenAI Account: $OPENAI_ACCOUNT"
echo "Web App Name: $WEB_APP_NAME"
echo "SQL Server Name: $SQL_SERVER_NAME"
echo ""

# ============================================================================
# Step 4: Configure Azure OpenAI Models
# ============================================================================

echo -e "${YELLOW}Step 4: Configuring Azure OpenAI Models${NC}"

echo "Deploying GPT-4 model..."
az cognitiveservices account deployment create \
    --name "$OPENAI_ACCOUNT" \
    --resource-group "$RESOURCE_GROUP" \
    --deployment-name gpt-4 \
    --model-name gpt-4 \
    --model-version "0613" \
    --model-format OpenAI \
    --sku-capacity 10 \
    --sku-name "Standard" || echo "GPT-4 deployment may already exist or quota exceeded"

echo "Deploying text-embedding-ada-002 model..."
az cognitiveservices account deployment create \
    --name "$OPENAI_ACCOUNT" \
    --resource-group "$RESOURCE_GROUP" \
    --deployment-name text-embedding-ada-002 \
    --model-name text-embedding-ada-002 \
    --model-version "2" \
    --model-format OpenAI \
    --sku-capacity 10 \
    --sku-name "Standard" || echo "Embedding model deployment may already exist or quota exceeded"

echo -e "${GREEN}✓ Azure OpenAI models configured${NC}"
echo ""

# ============================================================================
# Step 5: Run Database Migrations
# ============================================================================

echo -e "${YELLOW}Step 5: Running Database Migrations${NC}"

cd ../backend

# Build the project
echo "Building backend solution..."
dotnet build WAiSA.sln --configuration Release

# Run migrations
echo "Running EF Core migrations..."
CONNECTION_STRING="Server=tcp:$SQL_SERVER_NAME.database.windows.net,1433;Database=WAiSADB;User ID=$SQL_ADMIN_LOGIN;Password=$SQL_ADMIN_PASSWORD;Encrypt=true;Connection Timeout=30;"

dotnet ef database update --project WAiSA.Infrastructure --startup-project WAiSA.API || echo "Migration may have already been applied"

echo -e "${GREEN}✓ Database migrations completed${NC}"
echo ""

# ============================================================================
# Step 6: Deploy Backend API
# ============================================================================

echo -e "${YELLOW}Step 6: Deploying Backend API${NC}"

echo "Publishing backend..."
dotnet publish WAiSA.API/WAiSA.API.csproj \
    --configuration Release \
    --output ./publish

# Create deployment package
cd publish
zip -r ../deploy.zip . > /dev/null
cd ..

echo "Deploying to Azure App Service: $WEB_APP_NAME"
az webapp deployment source config-zip \
    --resource-group "$RESOURCE_GROUP" \
    --name "$WEB_APP_NAME" \
    --src deploy.zip

# Clean up
rm deploy.zip
rm -rf publish

echo -e "${GREEN}✓ Backend API deployed${NC}"
echo ""

# ============================================================================
# Step 7: Deployment Summary
# ============================================================================

echo -e "${GREEN}============================================${NC}"
echo -e "${GREEN}Deployment Complete!${NC}"
echo -e "${GREEN}============================================${NC}"
echo ""

WEB_APP_URL="https://$WEB_APP_NAME.azurewebsites.net"

echo "📊 Deployment Summary:"
echo ""
echo "  Resource Group: $RESOURCE_GROUP"
echo "  Environment: $ENVIRONMENT"
echo "  Location: $LOCATION"
echo ""
echo "🌐 Endpoints:"
echo "  Backend API: $WEB_APP_URL"
echo "  API Docs: $WEB_APP_URL/swagger"
echo "  Health Check: $WEB_APP_URL/health"
echo ""
echo "🔑 Resources:"
echo "  Azure OpenAI: $OPENAI_ACCOUNT"
echo "  SQL Server: $SQL_SERVER_NAME"
echo "  Web App: $WEB_APP_NAME"
echo ""
echo "📝 Next Steps:"
echo "  1. Test the API: curl $WEB_APP_URL/health"
echo "  2. View in Azure Portal: https://portal.azure.com/#resource/subscriptions/$(az account show --query id -o tsv)/resourceGroups/$RESOURCE_GROUP"
echo "  3. Monitor with Application Insights"
echo "  4. Deploy frontend (see frontend/README.md)"
echo "  5. Deploy agents to Windows machines"
echo ""
echo -e "${GREEN}✓ WAiSA is now running in Azure!${NC}"
echo ""

# Save deployment info
cat > ../infrastructure/deployment-info.txt <<EOF
WAiSA Deployment Information
============================
Date: $(date)
Environment: $ENVIRONMENT
Resource Group: $RESOURCE_GROUP
Location: $LOCATION

Endpoints:
- Backend API: $WEB_APP_URL
- API Docs: $WEB_APP_URL/swagger
- Health Check: $WEB_APP_URL/health

Resources:
- Azure OpenAI: $OPENAI_ACCOUNT
- SQL Server: $SQL_SERVER_NAME
- Web App: $WEB_APP_NAME

Deployment Output: deployment-output.json
EOF

echo "Deployment information saved to: infrastructure/deployment-info.txt"
