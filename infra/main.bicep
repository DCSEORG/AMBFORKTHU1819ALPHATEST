// Main Bicep Template
// Orchestrates deployment of all Azure resources

@description('Location for resources (except GenAI which uses swedencentral)')
param location string = 'uksouth'

@description('Base name for resources')
param baseName string = 'expensemgmt'

@description('Azure AD Admin Object ID for SQL Server')
param adminObjectId string

@description('Azure AD Admin Login (UPN) for SQL Server')
param adminLogin string

@description('Deploy GenAI resources (Azure OpenAI and AI Search)')
param deployGenAI bool = false

// Resource Group is implicit - deployment target

// Deploy Managed Identity first
module managedIdentity 'managed-identity.bicep' = {
  name: 'managedIdentityDeployment'
  params: {
    location: location
    baseName: baseName
  }
}

// Deploy App Service with Managed Identity
module appService 'app-service.bicep' = {
  name: 'appServiceDeployment'
  params: {
    location: location
    baseName: baseName
    managedIdentityId: managedIdentity.outputs.managedIdentityId
    managedIdentityClientId: managedIdentity.outputs.managedIdentityClientId
  }
  dependsOn: [
    managedIdentity
  ]
}

// Deploy Azure SQL
module azureSql 'azure-sql.bicep' = {
  name: 'azureSqlDeployment'
  params: {
    location: location
    baseName: baseName
    adminObjectId: adminObjectId
    adminLogin: adminLogin
    managedIdentityPrincipalId: managedIdentity.outputs.managedIdentityPrincipalId
  }
  dependsOn: [
    managedIdentity
  ]
}

// Deploy GenAI resources conditionally
module genai 'genai.bicep' = if (deployGenAI) {
  name: 'genaiDeployment'
  params: {
    location: 'swedencentral' // Always use swedencentral for GPT-4o
    baseName: baseName
    managedIdentityPrincipalId: managedIdentity.outputs.managedIdentityPrincipalId
  }
  dependsOn: [
    managedIdentity
  ]
}

// Outputs
output resourceGroupName string = resourceGroup().name
output managedIdentityName string = managedIdentity.outputs.managedIdentityName
output managedIdentityClientId string = managedIdentity.outputs.managedIdentityClientId
output managedIdentityPrincipalId string = managedIdentity.outputs.managedIdentityPrincipalId
output webAppName string = appService.outputs.webAppName
output webAppHostName string = appService.outputs.webAppHostName
output sqlServerName string = azureSql.outputs.sqlServerName
output sqlServerFqdn string = azureSql.outputs.sqlServerFqdn
output sqlDatabaseName string = azureSql.outputs.sqlDatabaseName
output connectionString string = azureSql.outputs.connectionString

// GenAI outputs (null-safe for when not deployed)
output openAIEndpoint string = deployGenAI ? genai.outputs.openAIEndpoint : ''
output openAIModelName string = deployGenAI ? genai.outputs.openAIModelName : ''
output searchEndpoint string = deployGenAI ? genai.outputs.searchEndpoint : ''
