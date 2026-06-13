#!/usr/bin/env bash
# Build and deploy the .NET app to Azure App Service via zip deploy.
# Assumes provision.sh has already been executed successfully and the user is
# logged in (`az login`) to the right subscription.

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &> /dev/null && pwd)"
APP_SRC_DIR="$(cd -- "${SCRIPT_DIR}/.." &> /dev/null && pwd)"
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

require_tools az jq dotnet zip

AZURE_ACCOUNT_NAME="$(az account show --query user.name -o tsv)"
AZURE_SUBSCRIPTION_NAME="$(az account show --query name -o tsv)"
AZURE_SUBSCRIPTION_ID="$(az account show --query id -o tsv)"
AZURE_TENANT_ID="$(az account show --query tenantId -o tsv)"

echo "==> Azure user:         ${AZURE_ACCOUNT_NAME}"
echo "==> Azure subscription: ${AZURE_SUBSCRIPTION_NAME} (${AZURE_SUBSCRIPTION_ID})"
echo "==> Azure tenant:       ${AZURE_TENANT_ID}"
echo "==> Deploy mode:        ${DEPLOY_MODE}"
echo "==> Resource group:     ${RESOURCE_GROUP}"

echo "==> Reading deployment outputs from resource group ${RESOURCE_GROUP}..."
OUTPUTS_JSON="$(az deployment group show \
  --name "${DEPLOYMENT_NAME}" \
  --resource-group "${RESOURCE_GROUP}" \
  --query properties.outputs \
  --output json)"

if [[ -z "${WEB_APP_NAME:-}" ]]; then
  WEB_APP_NAME="$(echo "${OUTPUTS_JSON}" | jq -r '.webAppName.value')"
fi
APP_URL="$(echo "${OUTPUTS_JSON}" | jq -r '.webAppUrl.value')"

if [[ -z "${WEB_APP_NAME}" || "${WEB_APP_NAME}" == "null" ]]; then
  echo "ERROR: Could not read Web App name from deployment outputs. Did you run provision.sh?" >&2
  exit 1
fi

GIT_COMMIT="$(git -C "${APP_SRC_DIR}" rev-parse --short HEAD 2>/dev/null || echo 'unknown')"

echo "==> Publishing .NET app (commit: ${GIT_COMMIT})..."
PUBLISH_DIR="$(mktemp -d)"
dotnet publish "${APP_SRC_DIR}/CommunityHub/CommunityHub.csproj" \
  -c Release \
  -o "${PUBLISH_DIR}" \
  -p:InformationalVersion="${GIT_COMMIT}" \
  --nologo

echo "==> Creating deployment zip..."
ZIP_FILE="$(mktemp).zip"
(cd "${PUBLISH_DIR}" && zip -r "${ZIP_FILE}" .)

echo "==> Deploying to Web App ${WEB_APP_NAME}..."
az webapp deploy \
  --name "${WEB_APP_NAME}" \
  --resource-group "${RESOURCE_GROUP}" \
  --src-path "${ZIP_FILE}" \
  --type zip \
  --output none

# Clean up
rm -rf "${PUBLISH_DIR}" "${ZIP_FILE}"

echo
echo "Deployment complete."
echo "Mode:   ${DEPLOY_MODE}"
echo "URL:    ${APP_URL}"
