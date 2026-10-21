// ISK Battle infrastructure (ARCHITECTURE.md §10). Deployed from a developer machine with
//   az deployment sub create --subscription <id> --location swedencentral --template-file infra/main.bicep \
//     --parameters infra/main.bicepparam
// The deploy workflow only rolls out new images; it never runs this template.
targetScope = 'subscription'

@description('Region for everything except the Static Web App and the global email resources.')
param location string = 'swedencentral'

@description('Static Web Apps is offered in a few regions only (West Europe takes no new customers); it holds only the public static files and serves them from a global CDN.')
param staticWebAppLocation string = 'eastus2'

param resourceGroupName string = 'rg-iskbattle'

@description('Object ID and login of the Entra user who administers SQL and may write Key Vault secrets.')
param adminObjectId string
param adminLogin string

@description('GitHub repository allowed to deploy via OIDC, as owner/name.')
param githubRepository string

@description('Second pass: deploy the Container App and Jobs. Needs the Key Vault secrets to exist.')
param deployApps bool = false

@description('Container image for the API and the jobs. Pass the running image when redeploying, or it is reset.')
param image string = 'mcr.microsoft.com/dotnet/samples:aspnetapp'

@description('Entra External ID token issuer authority, e.g. https://<subdomain>.ciamlogin.com/<tenant-id>/v2.0.')
param authAuthority string = ''

@description('The API app registration\'s client ID (the access token audience).')
param authAudience string = ''

@description('Extra browser origins for CORS, e.g. the custom app domain at launch. The Static Web App origin is always allowed.')
param extraAppOrigins array = []

@description('Monthly budget in the subscription\'s billing currency (alerts only, nothing is stopped).')
param budgetAmount int = 100
param budgetContactEmail string

@description('First day of the month the budget starts; must not change after the first deployment.')
param budgetStartDate string

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
}

module resources 'resources.bicep' = {
  scope: rg
  name: 'resources'
  params: {
    location: location
    staticWebAppLocation: staticWebAppLocation
    adminObjectId: adminObjectId
    adminLogin: adminLogin
    githubRepository: githubRepository
  }
}

module apps 'apps.bicep' = if (deployApps) {
  scope: rg
  name: 'apps'
  params: {
    location: location
    image: image
    environmentId: resources.outputs.containerAppsEnvironmentId
    environmentDomain: resources.outputs.containerAppsEnvironmentDomain
    identityId: resources.outputs.identityId
    identityClientId: resources.outputs.identityClientId
    keyVaultUri: resources.outputs.keyVaultUri
    sqlServerFqdn: resources.outputs.sqlServerFqdn
    sqlDatabaseName: resources.outputs.sqlDatabaseName
    appInsightsConnectionString: resources.outputs.appInsightsConnectionString
    acsEndpoint: resources.outputs.acsEndpoint
    emailSenderAddress: resources.outputs.emailSenderAddress
    appOrigin: 'https://${resources.outputs.staticWebAppHostname}'
    extraAppOrigins: extraAppOrigins
    authAuthority: authAuthority
    authAudience: authAudience
  }
}

resource budget 'Microsoft.Consumption/budgets@2023-11-01' = {
  name: 'budget-iskbattle'
  properties: {
    category: 'Cost'
    amount: budgetAmount
    timeGrain: 'Monthly'
    timePeriod: { startDate: budgetStartDate }
    notifications: {
      actual50: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 50
        thresholdType: 'Actual'
        contactEmails: [budgetContactEmail]
      }
      actual100: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 100
        thresholdType: 'Actual'
        contactEmails: [budgetContactEmail]
      }
      forecast100: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 100
        thresholdType: 'Forecasted'
        contactEmails: [budgetContactEmail]
      }
    }
  }
}

output resourceGroup string = rg.name
output staticWebAppName string = resources.outputs.staticWebAppName
output appUrl string = 'https://${resources.outputs.staticWebAppHostname}'
output apiUrl string = deployApps ? apps!.outputs.apiUrl : ''
output keyVaultName string = resources.outputs.keyVaultName
output sqlServerFqdn string = resources.outputs.sqlServerFqdn
output identityName string = resources.outputs.identityName
output githubDeployClientId string = resources.outputs.githubDeployClientId
output emailSenderAddress string = resources.outputs.emailSenderAddress
