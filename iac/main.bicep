
@description('Location for all resources')
param location string = resourceGroup().location
@description('Environment name (injected by azd)')
param azdEnvName string
@description('Container image for the Claim Status API')
param claimStatusApiImage string
@description('Container port for the Claim Status API')
param claimStatusApiPort int = 80
@description('Log Analytics retention in days')
param logAnalyticsRetention int = 30
@description('APIM publisher email')
param apimPublisherEmail string
@description('APIM publisher name')
param apimPublisherName string


var prefix = toLower(replace(azdEnvName,'_','-'))
var envName = '${prefix}-cae'
var logAnalyticsName = '${prefix}-logs'
// Construct a globally unique ACR name (5-50 lowercase alphanumerics)
var acrBase = toLower(replace('${prefix}acr','-',''))
var acrSuffix = toLower(substring(uniqueString(resourceGroup().id, 'acr'),0,6))
var acrRaw = '${acrBase}${acrSuffix}'
var acrName = substring(acrRaw, 0, min(length(acrRaw),50))

// Log Analytics workspace
resource logWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
	name: logAnalyticsName
	location: location
	properties: {
		retentionInDays: logAnalyticsRetention
		features: {
			searchVersion: 2
		}
	}
	tags: {
		'azd-env-name': azdEnvName
	}
}

// Azure Container Registry
resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
	name: acrName
	location: location
	sku: {
		name: 'Basic'
	}
	properties: {
		adminUserEnabled: true
	}
	tags: {
		'azd-env-name': azdEnvName
	}
}

// Container Apps Managed Environment
resource managedEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
	name: envName
	location: location
	properties: {
		appLogsConfiguration: {
			destination: 'log-analytics'
			logAnalyticsConfiguration: {
				customerId: logWorkspace.properties.customerId
				sharedKey: listKeys(logWorkspace.id, '2020-08-01').primarySharedKey
			}
		}
	}
	tags: {
		'azd-env-name': azdEnvName
	}
}

// Claim Status API Container App
resource claimStatusApi 'Microsoft.App/containerApps@2024-03-01' = {
	name: 'claimstatusapi'
	location: location
	tags: {
		'azd-env-name': azdEnvName
		'azd-service-name': 'claimstatusapi'
	}
	properties: {
		environmentId: managedEnv.id
		configuration: {
			ingress: {
				external: true
				targetPort: claimStatusApiPort
			}
			secrets: [
				{
					name: 'acr-pwd'
					value: acr.listCredentials().passwords[0].value
				}
			]
			registries: [
				{
					server: acr.properties.loginServer
					username: acr.listCredentials().username
					passwordSecretRef: 'acr-pwd'
				}
			]
		}
		template: {
			containers: [
				{
					name: 'claimstatusapi'
					image: claimStatusApiImage
				}
			]
		}
	}
}

// API Management
resource apim 'Microsoft.ApiManagement/service@2022-08-01' = {
	name: '${prefix}-apim'
	location: location
	sku: {
		name: 'Consumption'
		capacity: 0
	}
	properties: {
		publisherEmail: apimPublisherEmail
		publisherName: apimPublisherName
	}
	tags: {
		'azd-env-name': azdEnvName
	}
}

// Azure OpenAI
resource openai 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
	name: '${prefix}-openai'
	location: location
	kind: 'OpenAI'
	sku: {
		name: 'S0'
	}
	properties: {
		apiProperties: {
			enableDynamicThrottling: true
		}
		networkAcls: {
			defaultAction: 'Allow'
		}
	}
	tags: {
		'azd-env-name': azdEnvName
	}
}


output containerRegistry string = acr.properties.loginServer
output claimStatusApiUrl string = claimStatusApi.properties.configuration.ingress.fqdn
output logAnalyticsWorkspaceId string = logWorkspace.id
