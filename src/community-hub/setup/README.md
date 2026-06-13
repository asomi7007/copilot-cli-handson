# Setup

Provisioning and deploying the Lab 502 Community Hub in Azure.

## Prerequisites

- .NET 10 SDK
- Azure CLI (`az`) logged in
- `jq` and `zip` installed

## Local mode (default)

```bash
# 1. Provision Azure resources (App Service Plan + Web App)
./provision.sh

# 2. Build and deploy
./deploy.sh
```

## Cloud mode (Azure SQL + Blob + App Insights)

```bash
# 1. Set cloud mode and tenant
export DEPLOY_MODE=cloud
export APP_TENANT=my-tenant

# 2. Provision Azure resources
./provision.sh

# 3. Build and deploy
./deploy.sh
```

## Local development (no Azure)

```bash
# Run directly
dotnet run --project ../CommunityHub/CommunityHub.csproj
```

## Preseed game samples

Upload the HTML source and matching screenshots from `src/game-samples` into the Community Hub:

```bash
./preseed.sh
./preseed.sh http://localhost:1345 Lab502
```

The first argument is the optional Community Hub URL and defaults to `http://localhost:1345`. The second argument is the optional tenant; when omitted, uploads use the hub's current tenant.

## Environment variables

### Provisioning and deployment

These variables are read by `config.sh` and can be overridden before running `./provision.sh`, `./deploy.sh`, or `./destroy.sh`.

| Variable | Default | Description |
|---|---|---|
| `DEPLOY_MODE` | `cloud` | Deployment mode. Use `local` for App Service only, or `cloud` for Web App, Azure SQL, Blob Storage, and App Insights. |
| `RESOURCE_GROUP` | `community-hub-rg` | Azure resource group to create, deploy into, deploy from, or delete. |
| `LOCATION` | `centralus` | Azure region used when creating the resource group and resources. |
| `BASE_NAME` | `bld26` | Base name used to derive Azure resource names. |
| `DEPLOYMENT_NAME` | `community-hub-deploy` | Azure deployment name used by `az deployment group create/show`. |
| `APP_SERVICE_PLAN_SKU` | `B1` | App Service Plan SKU. |
| `WEB_APP_NAME` | empty | Optional explicit Web App name. When empty, Bicep auto-generates a globally unique name. |
| `APP_TENANT` | `Lab502` | Active tenant for cloud mode. Must match `^[a-zA-Z0-9][a-zA-Z0-9-]{0,30}$`. |
| `SQL_DATABASE` | `dashboard` | Azure SQL database name for cloud mode. |
| `SQL_SERVICE_OBJECTIVE` | `S1` | Azure SQL database service objective for cloud mode. |

### Application runtime

| Variable | Default | Description |
|---|---|---|
| `APP_MODE` | `local` | `local` or `cloud` |
| `PORT` | `1345` | HTTP listen port |
| `LOCAL_DATA_DIR` | `.` | Data directory (local mode) |
| `APP_TENANT` | — | Tenant ID (cloud mode, required) |
| `SQL_SERVER` | — | Azure SQL FQDN (cloud mode) |
| `SQL_DATABASE` | `dashboard` | Database name |
| `AZURE_STORAGE_ACCOUNT` | — | Storage account name (cloud mode) |
| `AZURE_BLOB_PUBLIC_BASE` | — | Public blob URL base (cloud mode) |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | — | App Insights connection string |
| `LAB502_DASHBOARD_URL` | `http://localhost:{PORT}` | Community Hub URL for templates/OpenAPI |

## Destroy

```bash
./destroy.sh        # interactive confirmation
./destroy.sh --yes  # skip confirmation
```
