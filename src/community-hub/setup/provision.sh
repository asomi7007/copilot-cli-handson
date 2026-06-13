#!/usr/bin/env bash
# Provision the Azure resources for the Lab 502 Community Hub (.NET version).
# Supports two modes via DEPLOY_MODE (config.sh):
#   local — App Service Plan + Web App (default)
#   cloud — Web App + Azure SQL + Blob Storage + App Insights

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &> /dev/null && pwd)"
# shellcheck disable=SC1091
source "${SCRIPT_DIR}/config.sh"

require_tools() {
  local missing=()
  for tool in "$@"; do
    if ! command -v "${tool}" >/dev/null 2>&1; then
      missing+=("${tool}")
    fi
  done

  if [[ ${#missing[@]} -gt 0 ]]; then
    echo "ERROR: Missing required tool(s): ${missing[*]}" >&2
    echo "       Install the missing tool(s), then run this script again." >&2
    exit 1
  fi
}

require_tools az

AZURE_ACCOUNT_NAME="$(az account show --query user.name -o tsv)"
AZURE_SUBSCRIPTION_NAME="$(az account show --query name -o tsv)"
AZURE_SUBSCRIPTION_ID="$(az account show --query id -o tsv)"
AZURE_TENANT_ID="$(az account show --query tenantId -o tsv)"

echo "==> Azure user:         ${AZURE_ACCOUNT_NAME}"
echo "==> Azure subscription: ${AZURE_SUBSCRIPTION_NAME} (${AZURE_SUBSCRIPTION_ID})"
echo "==> Azure tenant:       ${AZURE_TENANT_ID}"
echo "==> Deploy mode:        ${DEPLOY_MODE}"
echo "==> Resource group:     ${RESOURCE_GROUP} (${LOCATION})"
echo "==> Base name:          ${BASE_NAME}"
echo "==> Web App name:       ${WEB_APP_NAME:-(will be auto-generated)}"
echo "==> Deployment mode:    ${DEPLOY_MODE}"

if [[ "${DEPLOY_MODE}" == "cloud" ]]; then
  if [[ -z "${APP_TENANT}" ]]; then
    echo "ERROR: APP_TENANT is required when DEPLOY_MODE=cloud" >&2
    exit 1
  fi
  if ! [[ "${APP_TENANT}" =~ ^[a-zA-Z0-9][a-zA-Z0-9-]{0,30}$ ]]; then
    echo "ERROR: APP_TENANT must match ^[a-zA-Z0-9][a-zA-Z0-9-]{0,30}$" >&2
    exit 1
  fi
  echo "==> Tenant:             ${APP_TENANT}"
fi

echo "==> Creating resource group ${RESOURCE_GROUP}..."
az group create \
  --name "${RESOURCE_GROUP}" \
  --location "${LOCATION}" \
  --output none

if [[ "${DEPLOY_MODE}" == "cloud" ]]; then
  echo "==> Deploying cloud Bicep template (Web App + Azure SQL + Blob)..."
  az deployment group create \
    --name "${DEPLOYMENT_NAME}" \
    --resource-group "${RESOURCE_GROUP}" \
    --template-file "${SCRIPT_DIR}/main-cloud.bicep" \
    --parameters \
        baseName="${BASE_NAME}" \
        location="${LOCATION}" \
        appServicePlanSku="${APP_SERVICE_PLAN_SKU}" \
        webAppName="${WEB_APP_NAME}" \
        appTenant="${APP_TENANT}" \
        sqlDatabase="${SQL_DATABASE}" \
        sqlServiceObjective="${SQL_SERVICE_OBJECTIVE}" \
    --output none
else
  echo "==> Deploying local Bicep template (App Service)..."
  az deployment group create \
    --name "${DEPLOYMENT_NAME}" \
    --resource-group "${RESOURCE_GROUP}" \
    --template-file "${SCRIPT_DIR}/main.bicep" \
    --parameters \
        baseName="${BASE_NAME}" \
        location="${LOCATION}" \
        appServicePlanSku="${APP_SERVICE_PLAN_SKU}" \
        webAppName="${WEB_APP_NAME}" \
    --output none
fi

echo "==> Deployment outputs:"
az deployment group show \
  --name "${DEPLOYMENT_NAME}" \
  --resource-group "${RESOURCE_GROUP}" \
  --query properties.outputs \
  --output json | sed 's/^/  /'

APP_URL="$(az deployment group show \
  --name "${DEPLOYMENT_NAME}" \
  --resource-group "${RESOURCE_GROUP}" \
  --query properties.outputs.webAppUrl.value \
  --output tsv 2>/dev/null || true)"

echo
echo "Provisioning complete."
if [[ -n "${APP_URL}" ]]; then
  echo "App URL: ${APP_URL}"
fi
