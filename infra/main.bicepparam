using 'main.bicep'

// Personal values come from the environment so they stay out of the repository (README "Deploy").
param adminObjectId = readEnvironmentVariable('ISK_ADMIN_OBJECT_ID')
param adminLogin = readEnvironmentVariable('ISK_ADMIN_LOGIN')
param githubRepository = readEnvironmentVariable('ISK_GITHUB_REPOSITORY')
param budgetContactEmail = readEnvironmentVariable('ISK_BUDGET_EMAIL')
param budgetStartDate = '2026-10-01'

param deployApps = bool(readEnvironmentVariable('ISK_DEPLOY_APPS', 'false'))
param image = readEnvironmentVariable('ISK_IMAGE', 'mcr.microsoft.com/dotnet/samples:aspnetapp')

// Entra External ID (not secret). Filled in once the tenant and app registrations exist.
param authAuthority = readEnvironmentVariable('ISK_AUTH_AUTHORITY', '')
param authAudience = readEnvironmentVariable('ISK_AUTH_AUDIENCE', '')
