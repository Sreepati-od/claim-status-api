// main.bicep - Deploys ACR, ACA, APIM, Log Analytics, Managed Identities
param location string = resourceGroup().location
param acrName string
param acaEnvName string
param acaAppName string
param apimName string
param logAnalyticsName string
param openAiKeyVaultName string

resource acr 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' = {
  name: acrName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {}
}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2021-06-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource acaEnv 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: acaEnvName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

resource acaApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: acaAppName
  location: location
  properties: {
    managedEnvironmentId: acaEnv.id
    configuration: {
      registries: [
        {
          server: '${acr.name}.azurecr.io'
        }
      ]
      secrets: [
        {
          name: 'AZURE_OPENAI_KEY'
          value: listSecrets(resourceId('Microsoft.KeyVault/vaults', openAiKeyVaultName), '2021-06-01').value[0].secret
        }
      ]
      activeRevisionsMode: 'Single'
    }
    template: {
      containers: [
        {
          image: '${acr.name}.azurecr.io/claimstatus:latest'
          name: 'claimstatusapi'
          env: [
            {
              name: 'AZURE_OPENAI_ENDPOINT'
              value: 'https://<your-openai-endpoint>'
            }
            {
              name: 'AZURE_OPENAI_KEY'
              secretRef: 'AZURE_OPENAI_KEY'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 2
      }
    }
  }
}

resource apim 'Microsoft.ApiManagement/service@2022-08-01' = {
  name: apimName
  location: location
  sku: {
    name: 'Consumption'
    capacity: 0
  }
  properties: {
    publisherEmail: 'admin@example.com'
    publisherName: 'Admin'
  }
}

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${acaAppName}-identity'
  location: location
}

resource keyVault 'Microsoft.KeyVault/vaults@2021-06-01-preview' = {
  name: openAiKeyVaultName
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    accessPolicies: []
  }
}


output acrName string = acr.name
output acaEnvName string = acaEnv.name
output acaAppName string = acaApp.name
output apimName string = apim.name
output logAnalyticsName string = logAnalytics.name
output keyVaultName string = keyVault.name
