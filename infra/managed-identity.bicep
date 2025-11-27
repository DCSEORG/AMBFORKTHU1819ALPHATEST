// User Assigned Managed Identity Bicep Template

@description('Location for all resources')
param location string = 'uksouth'

@description('Base name for the managed identity')
param baseName string

// Generate unique suffix and timestamp-like naming
var uniqueSuffix = uniqueString(resourceGroup().id)
var managedIdentityName = toLower('mid-${baseName}-${uniqueSuffix}')

// User Assigned Managed Identity
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: managedIdentityName
  location: location
}

// Outputs
output managedIdentityId string = managedIdentity.id
output managedIdentityName string = managedIdentity.name
output managedIdentityClientId string = managedIdentity.properties.clientId
output managedIdentityPrincipalId string = managedIdentity.properties.principalId
