#!/bin/bash
# deploy.sh - Main deployment script for Expense Management System
# Deploys Azure infrastructure and application code WITHOUT GenAI services

set -e

echo "=========================================="
echo "Expense Management System Deployment"
echo "=========================================="

# Configuration - Update these values
RESOURCE_GROUP="rg-expensemgmt-demo"
LOCATION="uksouth"
BASE_NAME="expensemgmt"

# Get current user's Object ID and UPN for SQL Admin
echo "Getting current user information..."
ADMIN_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv)
ADMIN_LOGIN=$(az ad signed-in-user show --query userPrincipalName -o tsv)

echo "Admin Object ID: $ADMIN_OBJECT_ID"
echo "Admin Login: $ADMIN_LOGIN"

# Create resource group if it doesn't exist
echo ""
echo "Step 1: Creating resource group..."
az group create --name $RESOURCE_GROUP --location $LOCATION --output none
echo "✓ Resource group created: $RESOURCE_GROUP"

# Deploy infrastructure
echo ""
echo "Step 2: Deploying Azure infrastructure..."
DEPLOYMENT_OUTPUT=$(az deployment group create \
    --resource-group $RESOURCE_GROUP \
    --template-file infra/main.bicep \
    --parameters baseName=$BASE_NAME \
                 adminObjectId=$ADMIN_OBJECT_ID \
                 adminLogin=$ADMIN_LOGIN \
                 deployGenAI=false \
    --query "properties.outputs" \
    --output json)

echo "✓ Infrastructure deployed"

# Extract deployment outputs
WEB_APP_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.webAppName.value')
WEB_APP_HOSTNAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.webAppHostName.value')
SQL_SERVER_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.sqlServerName.value')
SQL_SERVER_FQDN=$(echo $DEPLOYMENT_OUTPUT | jq -r '.sqlServerFqdn.value')
SQL_DATABASE_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.sqlDatabaseName.value')
MANAGED_IDENTITY_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.managedIdentityName.value')
MANAGED_IDENTITY_CLIENT_ID=$(echo $DEPLOYMENT_OUTPUT | jq -r '.managedIdentityClientId.value')

echo ""
echo "Deployment outputs:"
echo "  Web App: $WEB_APP_NAME"
echo "  Web App URL: https://$WEB_APP_HOSTNAME"
echo "  SQL Server: $SQL_SERVER_FQDN"
echo "  Database: $SQL_DATABASE_NAME"
echo "  Managed Identity: $MANAGED_IDENTITY_NAME"

# Configure App Service settings
echo ""
echo "Step 3: Configuring App Service settings..."
CONNECTION_STRING="Server=tcp:${SQL_SERVER_FQDN},1433;Database=${SQL_DATABASE_NAME};Authentication=Active Directory Managed Identity;User Id=${MANAGED_IDENTITY_CLIENT_ID};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

az webapp config appsettings set \
    --name $WEB_APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --settings "ConnectionStrings__DefaultConnection=$CONNECTION_STRING" \
               "AZURE_CLIENT_ID=$MANAGED_IDENTITY_CLIENT_ID" \
               "ManagedIdentityClientId=$MANAGED_IDENTITY_CLIENT_ID" \
    --output none

echo "✓ App Service settings configured"

# Wait for SQL Server to be ready
echo ""
echo "Step 4: Waiting for SQL Server to be ready..."
sleep 30
echo "✓ Wait complete"

# Add current IP to SQL firewall
echo ""
echo "Step 5: Adding current IP to SQL Server firewall..."
MY_IP=$(curl -s https://api.ipify.org)
az sql server firewall-rule create \
    --resource-group $RESOURCE_GROUP \
    --server $SQL_SERVER_NAME \
    --name "DeploymentMachine" \
    --start-ip-address $MY_IP \
    --end-ip-address $MY_IP \
    --output none 2>/dev/null || echo "Firewall rule may already exist"
echo "✓ Firewall rule added for IP: $MY_IP"

# Update Python scripts with actual server names
echo ""
echo "Step 6: Updating Python script configurations..."
sed -i.bak "s/sql-expensemgmt-REPLACE.database.windows.net/$SQL_SERVER_FQDN/g" run-sql.py && rm -f run-sql.py.bak
sed -i.bak "s/sql-expensemgmt-REPLACE.database.windows.net/$SQL_SERVER_FQDN/g" run-sql-dbrole.py && rm -f run-sql-dbrole.py.bak
sed -i.bak "s/sql-expensemgmt-REPLACE.database.windows.net/$SQL_SERVER_FQDN/g" run-sql-stored-procs.py && rm -f run-sql-stored-procs.py.bak
sed -i.bak "s/MANAGED-IDENTITY-NAME/$MANAGED_IDENTITY_NAME/g" script.sql && rm -f script.sql.bak
echo "✓ Python scripts configured"

# Install Python dependencies
echo ""
echo "Step 7: Installing Python dependencies..."
pip3 install --quiet pyodbc azure-identity
echo "✓ Python dependencies installed"

# Import database schema
echo ""
echo "Step 8: Importing database schema..."
python3 run-sql.py
echo "✓ Database schema imported"

# Configure managed identity database access
echo ""
echo "Step 9: Configuring managed identity database access..."
python3 run-sql-dbrole.py
echo "✓ Managed identity database access configured"

# Create stored procedures
echo ""
echo "Step 10: Creating stored procedures..."
python3 run-sql-stored-procs.py
echo "✓ Stored procedures created"

# Deploy application code
echo ""
echo "Step 11: Deploying application code..."
if [ -f "app.zip" ]; then
    az webapp deploy \
        --resource-group $RESOURCE_GROUP \
        --name $WEB_APP_NAME \
        --src-path ./app.zip \
        --type zip \
        --output none
    echo "✓ Application deployed"
else
    echo "⚠ app.zip not found - Please build the application first"
    echo "  Run 'dotnet publish -c Release -o ./publish' in the src folder"
    echo "  Then create app.zip from the publish folder contents"
fi

echo ""
echo "=========================================="
echo "Deployment Complete!"
echo "=========================================="
echo ""
echo "Application URL: https://$WEB_APP_HOSTNAME/Index"
echo ""
echo "Note: Navigate to /Index to view the application"
echo "      The Chat UI is available but GenAI features require"
echo "      running deploy-with-chat.sh instead"
echo ""
