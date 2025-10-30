#!/bin/bash
# WAiSA PoC Infrastructure Deployment Script
# This script deploys all Azure resources needed for the WAiSA proof of concept

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_info() { echo -e "${BLUE}ℹ ${1}${NC}"; }
print_success() { echo -e "${GREEN}✓ ${1}${NC}"; }
print_warning() { echo -e "${YELLOW}⚠ ${1}${NC}"; }
print_error() { echo -e "${RED}✗ ${1}${NC}"; }

# Default values
RESOURCE_GROUP="waisa-poc-rg"
LOCATION="eastus2"
ENVIRONMENT="poc"
SUBSCRIPTION_ID=""
SQL_ADMIN_USERNAME="waisaadmin"
SQL_ADMIN_PASSWORD=""
KEY_VAULT_ADMIN_OBJECT_ID=""

# Parse command line arguments
while [[ $# -gt 0 ]]; do
  case $1 in
    --resource-group|-g)
      RESOURCE_GROUP="$2"
      shift 2
      ;;
    --location|-l)
      LOCATION="$2"
      shift 2
      ;;
    --subscription|-s)
      SUBSCRIPTION_ID="$2"
      shift 2
      ;;
    --sql-username)
      SQL_ADMIN_USERNAME="$2"
      shift 2
      ;;
    --sql-password)
      SQL_ADMIN_PASSWORD="$2"
      shift 2
      ;;
    --admin-object-id)
      KEY_VAULT_ADMIN_OBJECT_ID="$2"
      shift 2
      ;;
    --help|-h)
      echo "Usage: $0 [OPTIONS]"
      echo ""
      echo "Options:"
      echo "  --resource-group, -g      Resource group name (default: waisa-poc-rg)"
      echo "  --location, -l            Azure region (default: eastus2)"
      echo "  --subscription, -s        Azure subscription ID"
      echo "  --sql-username            SQL admin username (default: waisaadmin)"
      echo "  --sql-password            SQL admin password (required)"
      echo "  --admin-object-id         Azure AD Object ID for Key Vault access (required)"
      echo "  --help, -h                Show this help message"
      echo ""
      echo "Example:"
      echo "  $0 --sql-password 'YourSecurePassword123!' --admin-object-id 'your-aad-object-id'"
      exit 0
      ;;
    *)
      print_error "Unknown option: $1"
      echo "Use --help for usage information"
      exit 1
      ;;
  esac
done

# Banner
echo ""
echo "╔═══════════════════════════════════════════════════════════════╗"
echo "║         WAiSA PoC Infrastructure Deployment                   ║"
echo "║         Workplace AI System Assistant - Proof of Concept      ║"
echo "╚═══════════════════════════════════════════════════════════════╝"
echo ""

# Validation
print_info "Validating prerequisites..."

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    print_error "Azure CLI is not installed. Please install it first."
    echo "Visit: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
fi
print_success "Azure CLI is installed"

# Check if logged in to Azure
if ! az account show &> /dev/null; then
    print_error "Not logged in to Azure. Please run 'az login' first."
    exit 1
fi
print_success "Authenticated to Azure"

# Get subscription if not provided
if [ -z "$SUBSCRIPTION_ID" ]; then
    SUBSCRIPTION_ID=$(az account show --query id -o tsv)
fi
print_info "Using subscription: $SUBSCRIPTION_ID"

# Get Object ID if not provided
if [ -z "$KEY_VAULT_ADMIN_OBJECT_ID" ]; then
    print_info "Getting your Azure AD Object ID..."
    KEY_VAULT_ADMIN_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv)
fi
print_success "Key Vault admin Object ID: $KEY_VAULT_ADMIN_OBJECT_ID"

# Check if password was provided
if [ -z "$SQL_ADMIN_PASSWORD" ]; then
    print_error "SQL admin password is required. Use --sql-password flag or set it interactively."
    read -s -p "Enter SQL admin password: " SQL_ADMIN_PASSWORD
    echo ""
    read -s -p "Confirm SQL admin password: " SQL_PASSWORD_CONFIRM
    echo ""

    if [ "$SQL_ADMIN_PASSWORD" != "$SQL_PASSWORD_CONFIRM" ]; then
        print_error "Passwords do not match. Exiting."
        exit 1
    fi
fi

# Validate password complexity
if [[ ! "$SQL_ADMIN_PASSWORD" =~ [A-Z] ]] || \
   [[ ! "$SQL_ADMIN_PASSWORD" =~ [a-z] ]] || \
   [[ ! "$SQL_ADMIN_PASSWORD" =~ [0-9] ]] || \
   [ ${#SQL_ADMIN_PASSWORD} -lt 8 ]; then
    print_error "Password must be at least 8 characters and contain uppercase, lowercase, and numbers."
    exit 1
fi
print_success "Password validation passed"

# Create resource group if it doesn't exist
print_info "Checking resource group..."
if ! az group show --name "$RESOURCE_GROUP" &> /dev/null; then
    print_info "Creating resource group: $RESOURCE_GROUP in $LOCATION"
    az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --tags environment="$ENVIRONMENT" project="WAiSA"
    print_success "Resource group created"
else
    print_success "Resource group already exists"
fi

# Validate deployment with what-if
print_info "Validating deployment (what-if analysis)..."
DEPLOYMENT_NAME="waisa-deployment-$(date +%Y%m%d-%H%M%S)"

az deployment group what-if \
    --name "$DEPLOYMENT_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --template-file "main.bicep" \
    --parameters "main.bicepparam" \
    --parameters sqlAdminUsername="$SQL_ADMIN_USERNAME" \
    --parameters sqlAdminPassword="$SQL_ADMIN_PASSWORD" \
    --parameters keyVaultAdminObjectId="$KEY_VAULT_ADMIN_OBJECT_ID" \
    --parameters location="$LOCATION"

print_warning "Review the what-if results above."
read -p "Do you want to proceed with the deployment? (yes/no): " CONFIRM

if [ "$CONFIRM" != "yes" ] && [ "$CONFIRM" != "y" ]; then
    print_info "Deployment cancelled by user."
    exit 0
fi

# Deploy infrastructure
print_info "Starting deployment... This may take 10-15 minutes."
az deployment group create \
    --name "$DEPLOYMENT_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --template-file "main.bicep" \
    --parameters "main.bicepparam" \
    --parameters sqlAdminUsername="$SQL_ADMIN_USERNAME" \
    --parameters sqlAdminPassword="$SQL_ADMIN_PASSWORD" \
    --parameters keyVaultAdminObjectId="$KEY_VAULT_ADMIN_OBJECT_ID" \
    --parameters location="$LOCATION" \
    --output json > deployment-output.json

if [ $? -eq 0 ]; then
    print_success "Deployment completed successfully!"

    # Extract outputs
    print_info "Deployment outputs:"
    echo ""
    jq -r '.properties.outputs | to_entries[] | "  \(.key): \(.value.value)"' deployment-output.json

    # Save outputs to file
    print_info "Saving outputs to deployment-outputs.txt..."
    jq -r '.properties.outputs | to_entries[] | "\(.key)=\(.value.value)"' deployment-output.json > deployment-outputs.txt

    echo ""
    print_success "All resources deployed successfully!"
    print_info "Azure Portal: https://portal.azure.com/#@/resource/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP"

else
    print_error "Deployment failed. Check deployment-output.json for details."
    exit 1
fi

# Post-deployment configuration suggestions
echo ""
print_warning "Post-deployment tasks:"
echo "  1. Store SQL admin password in Key Vault"
echo "  2. Configure App Service deployment settings"
echo "  3. Set up Azure OpenAI deployments if not automatically created"
echo "  4. Configure Service Bus queues/topics as needed"
echo "  5. Set up Application Insights alerts"
echo "  6. Review and configure firewall rules for SQL Server"
echo ""

print_success "Deployment script completed!"
