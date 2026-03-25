#!/bin/bash

# Microhire Agent Chat - Azure Deployment Script
# Uses OneDeploy method for optimal Azure App Service deployment
# Uses Jenny Junkeer account

set -e  # Exit on error

echo "🚀 Starting OneDeploy to Azure App Service..."

# Set variables
RESOURCE_GROUP="rg-JennyJunkeer-9509"
APP_NAME="microhire"
PROJECT_DIR="MicrohireAgentChat"

# Check Azure CLI authentication
echo "🔐 Checking Azure CLI authentication..."
if ! az account show &> /dev/null; then
    echo "⚠️  Not authenticated with Azure. Please log in with Jenny Junkeer account..."
    az login
fi

# Find and switch to Jenny Junkeer subscription immediately
echo "🔍 Looking for Jenny Junkeer subscription..."
JENNY_SUB_ID=$(az account list --query "[?contains(user.name || '', 'JennyJunkeer') || contains(name || '', 'Jenny')].id" -o tsv | head -n 1)
CURRENT_SUB_ID=$(az account show --query id -o tsv)

if [ ! -z "$JENNY_SUB_ID" ] && [ "$CURRENT_SUB_ID" != "$JENNY_SUB_ID" ]; then
    echo "🔄 Switching to Jenny Junkeer subscription..."
    az account set --subscription "$JENNY_SUB_ID"
    echo "✅ Switched to Jenny Junkeer account"
    echo ""
fi

# Display current account and subscription
CURRENT_ACCOUNT=$(az account show --query user.name -o tsv)
CURRENT_SUB=$(az account show --query name -o tsv)
echo "📋 Current Azure Account: $CURRENT_ACCOUNT"
echo "📋 Current Subscription: $CURRENT_SUB"
echo ""

# Verify we're using Jenny Junkeer account
if [ -z "$JENNY_SUB_ID" ]; then
    echo "❌ Error: Jenny Junkeer subscription not found"
    echo "   Please ensure you're logged in with Jenny Junkeer account"
    echo "   Run: az login"
    exit 1
fi

# Verify we can access the resource group
echo "🔍 Verifying resource group access..."
if ! az group show --name $RESOURCE_GROUP &> /dev/null; then
    echo "❌ Error: Cannot access resource group '$RESOURCE_GROUP'"
    echo "   Current account: $CURRENT_ACCOUNT"
    echo "   Please ensure you have access to this resource group"
    exit 1
fi
echo "✅ Resource group verified"
echo ""

# Verify app service exists
echo "🔍 Verifying App Service exists..."
if ! az webapp show --resource-group $RESOURCE_GROUP --name $APP_NAME &> /dev/null; then
    echo "❌ Error: App Service '$APP_NAME' not found in resource group '$RESOURCE_GROUP'"
    exit 1
fi
echo "✅ App Service verified"
echo ""

# Build the application with optimized settings for deployment
echo "📦 Building application..."
cd $PROJECT_DIR
echo "   Publishing to Release configuration..."
dotnet publish -c Release -o ./out --self-contained false --runtime win-x64

# Playwright PDF: the Node driver ships under .playwright/ next to Microsoft.Playwright.dll.
# Do NOT omit it from the zip — that caused Azure logs: .playwrightExists=False / Couldn't find driver.
if [ ! -d "./out/.playwright" ] || [ ! -d "./out/.playwright/node" ]; then
    echo "❌ Error: publish output is missing .playwright/node (quote PDFs will fail on Azure)."
    echo "   Ensure you publish with -r win-x64; on Linux/macOS, playwright.ps1 install may be skipped — the bundled folder should still exist."
    exit 1
fi
echo "   Playwright driver bundle present (.playwright/node)."

# Windows Chromium cannot be bundled when publishing from macOS/Linux (Playwright installs host-OS browsers).
# Production path: run .github/workflows/publish-windows-azure.yml on GitHub Actions (windows-latest), download
# artifact site-win64-playwright (site.zip), deploy with: az webapp deploy ... --src-path site.zip
if [ -d "./out/pw-browsers" ] && [ "$(find ./out/pw-browsers -type f 2>/dev/null | head -1)" ]; then
    echo "   Windows Chromium bundle present (out/pw-browsers) — runtime Playwright install should be skipped."
elif [ "${ALLOW_DEPLOY_WITHOUT_PW_BROWSERS:-}" = "1" ]; then
    echo "⚠️  ALLOW_DEPLOY_WITHOUT_PW_BROWSERS=1 — deploying without bundled Windows Chromium (PDFs may download/install Chromium on first use)."
else
    echo "❌ Error: out/pw-browsers is missing or empty."
    echo "   macOS/Linux publish cannot include Windows Chromium. Use GitHub Actions: workflow \"Publish Windows (Playwright Chromium)\","
    echo "   download artifact site-win64-playwright, deploy site.zip. Or set ALLOW_DEPLOY_WITHOUT_PW_BROWSERS=1 to override (not recommended)."
    exit 1
fi

# Create deployment package
echo "📦 Creating deployment package..."
cd out

# Clean up any existing zip
if [ -f ../site.zip ]; then
    rm ../site.zip
fi

# Create optimized zip package (exclude unnecessary files but keep web.config and .playwright)
echo "   Creating optimized zip package..."
zip -r ../site.zip . -x "*.pdb" "*.log" "*.tmp" "*/.git/*" "*.DS_Store"

# Get package size
PACKAGE_SIZE=$(du -h ../site.zip | cut -f1)
echo "   Package size: $PACKAGE_SIZE"

# Return to project directory
cd ..

# Deploy using OneDeploy (modern Azure deployment method)
echo "☁️  Deploying to Azure App Service using OneDeploy..."

# Use the new deployment method with async deployment
DEPLOYMENT_RESULT=$(az webapp deploy --resource-group $RESOURCE_GROUP --name $APP_NAME --src-path site.zip --type zip --async true)

# Extract deployment ID if available
DEPLOYMENT_ID=$(echo "$DEPLOYMENT_RESULT" | grep -o '"id":"[^"]*"' | cut -d'"' -f4 || echo "")

if [ ! -z "$DEPLOYMENT_ID" ]; then
    echo "📋 Deployment ID: $DEPLOYMENT_ID"
fi

# Wait for deployment to complete and check status
echo "⏳ Waiting for deployment to complete..."
sleep 5

# Check deployment status
echo "🔍 Checking deployment status..."
DEPLOYMENT_STATUS=$(az webapp deployment list-publishing-profiles --resource-group $RESOURCE_GROUP --name $APP_NAME --query "[0].state" -o tsv 2>/dev/null || echo "unknown")

if [ "$DEPLOYMENT_STATUS" = "success" ] || [ "$DEPLOYMENT_STATUS" = "unknown" ]; then
    echo "✅ Deployment completed successfully!"
else
    echo "⚠️  Deployment status: $DEPLOYMENT_STATUS"
    echo "   Checking app health..."
fi

# Get the app URL from Azure (actual default domain, e.g. microhire-geg6hggrhdcqbme9.australiasoutheast-01.azurewebsites.net)
DEFAULT_HOST=$(az webapp show --resource-group $RESOURCE_GROUP --name $APP_NAME --query defaultHostName -o tsv 2>/dev/null || echo "$APP_NAME.azurewebsites.net")
APP_URL="https://$DEFAULT_HOST"
echo "🌐 App URL: $APP_URL"

# Test the deployment by checking if the app responds
echo "🔍 Testing deployment..."
if curl -s --head --request GET "$APP_URL" --max-time 30 | grep "200\|301\|302" > /dev/null; then
    echo "✅ App is responding successfully!"
else
    echo "⚠️  App may still be starting up or there could be an issue"
    echo "   Please check the Azure portal deployment logs for details"
fi

echo ""
echo "🎉 OneDeploy completed!"
echo "📊 Resource Group: $RESOURCE_GROUP"
echo "🏗️  App Service: $APP_NAME"
echo "🌐 URL: $APP_URL"

# Clean up local files
echo "🧹 Cleaning up deployment files..."
rm -rf out site.zip

echo "✨ Deployment process finished!"