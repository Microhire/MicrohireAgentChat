#!/bin/bash

# Microhire Agent Chat - Azure Deployment Script
# Recommended: Zip Deploy method

echo "🚀 Starting deployment to Azure App Service..."

# Set variables
RESOURCE_GROUP="rg-JennyJunkeer-9509"
APP_NAME="microhire"
PROJECT_DIR="MicrohireAgentChat"

# Build the application
echo "📦 Building application..."
cd $PROJECT_DIR
dotnet publish -c Release -o ./publish

# Create deployment package
echo "📦 Creating deployment package..."
cd publish
zip -r ../site.zip .

# Deploy to Azure
echo "☁️  Deploying to Azure App Service..."
az webapp deploy --resource-group $RESOURCE_GROUP --name $APP_NAME --src-path ../site.zip --type zip

echo "✅ Deployment completed!"
echo "🌐 Your app should be available at: https://$APP_NAME.azurewebsites.net"