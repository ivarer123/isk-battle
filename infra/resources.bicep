// Everything except the Container App and Jobs (apps.bicep): monitoring, identities, Key Vault, SQL, email and the
// Static Web App.
param location string
param staticWebAppLocation string
param adminObjectId string
param adminLogin string
param githubRepository string

var suffix = take(uniqueString(resourceGroup().id), 6)

// Built-in role definition IDs.
var keyVaultSecretsUser = '4633458b-17de-408a-b874-0445c86b69e6'
var keyVaultSecretsOfficer = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'
var contributor = 'b24988ac-6180-42a0-ab88-20f7382dd24c'

// --- Monitoring -------------------------------------------------------------------------------------------------

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-iskbattle'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
    // Hard cap below the 5 GB/month free ingestion, so logging can never cost money.
    workspaceCapping: { dailyQuotaGb: json('0.15') }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-iskbattle'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logs.id
  }
}

// --- Identities -------------------------------------------------------------------------------------------------

// Runtime identity of the API and the jobs: SQL user, Key Vault reader, email sender.
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-iskbattle'
  location: location
}

// GitHub Actions deploy identity (OIDC, no stored secret). Can roll out images, nothing outside this group.
resource githubIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-iskbattle-github'
  location: location

  resource mainBranch 'federatedIdentityCredentials' = {
    name: 'github-main'
    properties: {
      issuer: 'https://token.actions.githubusercontent.com'
      subject: 'repo:${githubRepository}:ref:refs/heads/main'
      audiences: ['api://AzureADTokenExchange']
    }
  }
}

resource githubContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, githubIdentity.id, contributor)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', contributor)
    principalId: githubIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// --- Key Vault --------------------------------------------------------------------------------------------------

// Secrets (names map to configuration keys, "--" = ":"): Stripe--SecretKey, Stripe--WebhookSecret,
// Email--UnsubscribeKey. Their values are set outside Bicep, never in source control.
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: 'kv-iskbattle-${suffix}'
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
  }
}

resource identityReadsSecrets 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, identity.id, keyVaultSecretsUser)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUser)
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource adminWritesSecrets 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, adminObjectId, keyVaultSecretsOfficer)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsOfficer)
    principalId: adminObjectId
    principalType: 'User'
  }
}

// --- SQL --------------------------------------------------------------------------------------------------------

// Entra-only authentication. The runtime identity is added as a database user once, by the admin (README).
resource sqlServer 'Microsoft.Sql/servers@2023-08-01' = {
  name: 'sql-iskbattle-${suffix}'
  location: location
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: adminLogin
      sid: adminObjectId
      tenantId: subscription().tenantId
      principalType: 'User'
    }
  }

  // Container Apps on consumption have no fixed outbound IP; access still requires an Entra token.
  resource allowAzure 'firewallRules' = {
    name: 'AllowAllWindowsAzureIps'
    properties: { startIpAddress: '0.0.0.0', endIpAddress: '0.0.0.0' }
  }
}

// Free offer: General Purpose serverless, 100,000 vCore seconds + 32 GB per month, auto-pause when used up.
// Before launch, switch freeLimitExhaustionBehavior to BillOverUsage ("continue using the database").
resource database 'Microsoft.Sql/servers/databases@2023-08-01' = {
  parent: sqlServer
  name: 'iskbattle'
  location: location
  sku: { name: 'GP_S_Gen5_2', tier: 'GeneralPurpose', family: 'Gen5', capacity: 2 }
  properties: {
    useFreeLimit: true
    freeLimitExhaustionBehavior: 'AutoPause'
    autoPauseDelay: 60
    minCapacity: json('0.5')
    maxSizeBytes: 34359738368
    requestedBackupStorageRedundancy: 'Local'
  }
}

// --- Email ------------------------------------------------------------------------------------------------------

// Azure-managed sender domain (DoNotReply@<guid>.azurecomm.net) until the custom domain is added at launch.
resource emailService 'Microsoft.Communication/emailServices@2023-04-01' = {
  name: 'ecs-iskbattle'
  location: 'global'
  properties: { dataLocation: 'Europe' }

  resource managedDomain 'domains' = {
    name: 'AzureManagedDomain'
    location: 'global'
    properties: {
      domainManagement: 'AzureManaged'
      userEngagementTracking: 'Disabled'
    }
  }
}

resource communication 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: 'acs-iskbattle-${suffix}'
  location: 'global'
  properties: {
    dataLocation: 'Europe'
    linkedDomains: [emailService::managedDomain.id]
  }
}

resource identitySendsEmail 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(communication.id, identity.id, contributor)
  scope: communication
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', contributor)
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// --- Hosting ----------------------------------------------------------------------------------------------------

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'cae-iskbattle'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

// Deployed by the workflow with its deployment token (az staticwebapp secrets list).
resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: 'swa-iskbattle'
  location: staticWebAppLocation
  sku: { name: 'Free', tier: 'Free' }
  properties: {}
}

output containerAppsEnvironmentId string = containerAppsEnvironment.id
output containerAppsEnvironmentDomain string = containerAppsEnvironment.properties.defaultDomain
output identityId string = identity.id
output identityName string = identity.name
output identityClientId string = identity.properties.clientId
output githubDeployClientId string = githubIdentity.properties.clientId
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = database.name
output appInsightsConnectionString string = appInsights.properties.ConnectionString
output acsEndpoint string = 'https://${communication.properties.hostName}'
output emailSenderAddress string = 'DoNotReply@${emailService::managedDomain.properties.mailFromSenderDomain}'
output staticWebAppName string = staticWebApp.name
output staticWebAppHostname string = staticWebApp.properties.defaultHostname
