// The API Container App and the Container Apps Jobs, all from one image (root Dockerfile). Secrets are Key Vault
// references read with the runtime identity, so the vault's secrets must exist before this is deployed.
param location string
param image string
param environmentId string
param environmentDomain string
param identityId string
param identityClientId string
param keyVaultUri string
param sqlServerFqdn string
param sqlDatabaseName string
param appInsightsConnectionString string
param acsEndpoint string
param emailSenderAddress string
param appOrigin string
param extraAppOrigins array
param authAuthority string
param authAudience string

var apiName = 'ca-iskbattle-api'
var apiUrl = 'https://${apiName}.${environmentDomain}'

// Connect Timeout covers the free-offer database resuming from auto-pause (up to about a minute).
var sqlConnectionString = 'Server=tcp:${sqlServerFqdn},1433;Database=${sqlDatabaseName};Authentication=Active Directory Managed Identity;User Id=${identityClientId};Encrypt=True;Connect Timeout=90'

var identity = {
  type: 'UserAssigned'
  userAssignedIdentities: { '${identityId}': {} }
}

var unsubscribeKeySecret = {
  name: 'email-unsubscribe-key'
  keyVaultUrl: '${keyVaultUri}secrets/Email--UnsubscribeKey'
  identity: identityId
}

// Shared by the API and the jobs.
var commonEnv = [
  { name: 'AZURE_CLIENT_ID', value: identityClientId } // DefaultAzureCredential (ACS) picks the user-assigned identity
  { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
  { name: 'ConnectionStrings__Sql', value: sqlConnectionString }
  { name: 'Email__Provider', value: 'Acs' }
  { name: 'Email__AcsEndpoint', value: acsEndpoint }
  { name: 'Email__SenderAddress', value: emailSenderAddress }
  { name: 'Email__UnsubscribeKey', secretRef: 'email-unsubscribe-key' }
  { name: 'Links__AppBaseUrl', value: appOrigin }
  { name: 'Links__ApiBaseUrl', value: apiUrl }
]

var corsEnv = [for (origin, i) in concat([appOrigin], extraAppOrigins): { name: 'Cors__AllowedOrigins__${i}', value: origin }]

resource api 'Microsoft.App/containerApps@2024-03-01' = {
  name: apiName
  location: location
  identity: identity
  properties: {
    environmentId: environmentId
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      secrets: [
        unsubscribeKeySecret
        { name: 'stripe-secret-key', keyVaultUrl: '${keyVaultUri}secrets/Stripe--SecretKey', identity: identityId }
        { name: 'stripe-webhook-secret', keyVaultUrl: '${keyVaultUri}secrets/Stripe--WebhookSecret', identity: identityId }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: image
          resources: { cpu: json('0.25'), memory: '0.5Gi' }
          env: concat(commonEnv, corsEnv, [
            { name: 'Auth__Authority', value: authAuthority }
            { name: 'Auth__Audience', value: authAudience }
            { name: 'Stripe__SecretKey', secretRef: 'stripe-secret-key' }
            { name: 'Stripe__WebhookSecret', secretRef: 'stripe-webhook-secret' }
          ])
        }
      ]
      // Scale to zero when idle: the first request after a quiet period pays a cold start.
      scale: { minReplicas: 0, maxReplicas: 1 }
    }
  }
}

// Reminders (ARCHITECTURE.md §8). Cron is UTC; ReminderPolicy.IsRunTime exits early on the trigger that misses
// 08:00 / 20:00 Stockholm. One cron expression per job, hence two jobs.
var reminderJobs = [
  { name: 'caj-reminders-morning', cron: '0 6,7 * * *' }
  { name: 'caj-reminders-seasonend', cron: '0 18,19 28-30 12 *' }
]

resource reminders 'Microsoft.App/jobs@2024-03-01' = [for job in reminderJobs: {
  name: job.name
  location: location
  identity: identity
  properties: {
    environmentId: environmentId
    configuration: {
      triggerType: 'Schedule'
      scheduleTriggerConfig: { cronExpression: job.cron, parallelism: 1, replicaCompletionCount: 1 }
      replicaTimeout: 600
      replicaRetryLimit: 2
      secrets: [unsubscribeKeySecret]
    }
    template: {
      containers: [
        {
          name: 'reminders'
          image: image
          command: ['dotnet', '/app/jobs/IskBattle.Jobs.dll', 'reminders']
          resources: { cpu: json('0.25'), memory: '0.5Gi' }
          env: commonEnv
        }
      ]
    }
  }
}]

// Applies EF Core migrations; started by the deploy workflow before the API gets the new image.
resource migrate 'Microsoft.App/jobs@2024-03-01' = {
  name: 'caj-migrate'
  location: location
  identity: identity
  properties: {
    environmentId: environmentId
    configuration: {
      triggerType: 'Manual'
      manualTriggerConfig: { parallelism: 1, replicaCompletionCount: 1 }
      replicaTimeout: 600
      replicaRetryLimit: 1
    }
    template: {
      containers: [
        {
          name: 'migrate'
          image: image
          command: ['dotnet', '/app/jobs/IskBattle.Jobs.dll', 'migrate']
          resources: { cpu: json('0.25'), memory: '0.5Gi' }
          env: [
            { name: 'AZURE_CLIENT_ID', value: identityClientId }
            { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
            { name: 'ConnectionStrings__Sql', value: sqlConnectionString }
          ]
        }
      ]
    }
  }
}

output apiUrl string = apiUrl
