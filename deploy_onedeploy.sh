#!/bin/bash

# Microhire Agent Chat - Azure OneDeploy Script
# Uses Azure CLI with OneDeploy (recommended for .NET Core apps)
# Includes Dev Mode enablement

set -e  # Exit on error

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Print colored output
print_info() { echo -e "${BLUE}ℹ️  $1${NC}"; }
print_success() { echo -e "${GREEN}✅ $1${NC}"; }
print_warning() { echo -e "${YELLOW}⚠️  $1${NC}"; }
print_error() { echo -e "${RED}❌ $1${NC}"; }

# Set variables
RESOURCE_GROUP="rg-JennyJunkeer-9509"
APP_NAME="microhire"
PROJECT_DIR="MicrohireAgentChat"
DEPLOYMENT_NAME="OneDeploy-$(date +%s)"

print_info "===== Microhire Agent Chat - Azure OneDeploy ====="
print_info "Resource Group: $RESOURCE_GROUP"
print_info "App Service: $APP_NAME"
print_info "Deployment ID: $DEPLOYMENT_NAME"
echo ""

# Check if azure cli is installed
if ! command -v az &> /dev/null; then
    print_error "Azure CLI is not installed. Please install it first."
    exit 1
fi

print_info "Checking Azure CLI authentication..."
if ! az account show &> /dev/null; then
    print_warning "Not authenticated with Azure. Running 'az login'..."
    az login
fi

# Display current subscription
CURRENT_SUB=$(az account show --query name -o tsv)
print_info "Current Azure Subscription: $CURRENT_SUB"
echo ""

# Step 1: Build the application
print_info "Step 1/5: Building application in Release mode..."
cd $PROJECT_DIR

if ! dotnet publish -c Release -o ./publish; then
    print_error "Build failed!"
    exit 1
fi
print_success "Build completed"
echo ""

# Step 2: Create deployment package
print_info "Step 2/5: Creating deployment package..."
cd publish

if [ -f "../site.zip" ]; then
    print_warning "Existing site.zip found, removing..."
    rm ../site.zip
fi

zip -r -q ../site.zip .
print_success "Deployment package created (site.zip)"
echo ""

# Step 3: Verify App Service exists
print_info "Step 3/5: Verifying App Service exists..."
if ! az webapp show --resource-group $RESOURCE_GROUP --name $APP_NAME &> /dev/null; then
    print_error "App Service '$APP_NAME' not found in resource group '$RESOURCE_GROUP'"
    exit 1
fi
print_success "App Service verified"
echo ""

# Step 4: Enable Dev Mode via App Settings
print_info "Step 4/5: Configuring App Settings (Dev Mode enabled)..."
az webapp config appsettings set \
    --resource-group $RESOURCE_GROUP \
    --name $APP_NAME \
    --settings "DevMode__Enabled=true" \
    > /dev/null
print_success "Dev Mode enabled in App Settings"
echo ""

# Step 5: Deploy using OneDeploy (ZIP Deploy)
print_info "Step 5/5: Deploying to Azure App Service using OneDeploy..."
print_info "Uploading and deploying package..."

if az webapp deploy \
    --resource-group $RESOURCE_GROUP \
    --name $APP_NAME \
    --src-path ../site.zip \
    --type zip; then
    print_success "Deployment completed successfully!"
else
    print_error "Deployment failed!"
    exit 1
fi
echo ""

# Step 6: Wait for deployment to stabilize
print_info "Waiting for App Service to restart and stabilize (30 seconds)..."
sleep 30
print_success "App Service should now be online"
echo ""

# Display success information
print_success "===== Deployment Summary ====="
echo "App Service: https://$APP_NAME.azurewebsites.net"
echo "Dev Mode: ENABLED"
echo "Deployment Time: $(date)"
echo ""
print_info "Next steps:"
echo "1. Visit https://$APP_NAME.azurewebsites.net to test the application"
echo "2. The Test button should be visible (Dev Mode enabled)"
echo "3. Use the test scenarios to verify the bug reproduction flows"
echo ""
print_success "Deployment complete! 🎉"
