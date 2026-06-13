# Shared configuration for the setup scripts.
# Override any of these via environment variables before invoking the scripts.

# ── Deploy mode: "local" (App Service) or "cloud" (Web App + Azure SQL + Blob) ──
: "${DEPLOY_MODE:=cloud}"

# ── Common settings ──
: "${RESOURCE_GROUP:=community-hub-rg}"
: "${LOCATION:=centralus}"
: "${BASE_NAME:=bld26}"
: "${DEPLOYMENT_NAME:=community-hub-deploy}"

# ── App Service ──
: "${APP_SERVICE_PLAN_SKU:=B1}"
# Optional: explicit Web App name. If empty, the Bicep template auto-generates
# a unique name based on BASE_NAME and the resource group id.
# Must be globally unique across Azure (3-60 chars, lowercase letters, digits, hyphens).
: "${WEB_APP_NAME:=}"

# ── Cloud-mode settings (Web App + Azure SQL + Blob) ──
# Tenant id (required when DEPLOY_MODE=cloud). Lowercase letters, digits, hyphens.
: "${APP_TENANT:=Lab502}"
: "${SQL_DATABASE:=dashboard}"
: "${SQL_SERVICE_OBJECTIVE:=S1}"

export DEPLOY_MODE RESOURCE_GROUP LOCATION BASE_NAME DEPLOYMENT_NAME \
       APP_SERVICE_PLAN_SKU WEB_APP_NAME \
       APP_TENANT SQL_DATABASE SQL_SERVICE_OBJECTIVE
