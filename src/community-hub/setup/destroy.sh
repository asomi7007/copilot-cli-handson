#!/usr/bin/env bash
# Destroy the Azure resources for the Lab 502 Community Hub by deleting the resource group.

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &> /dev/null && pwd)"
# shellcheck disable=SC1091
source "${SCRIPT_DIR}/config.sh"

ASSUME_YES=0
for arg in "$@"; do
  case "${arg}" in
    -y|--yes)
      ASSUME_YES=1
      ;;
    -h|--help)
      cat <<EOF
Usage: $(basename "$0") [--yes|-y]

Deletes the Azure resource group "${RESOURCE_GROUP}" (override via RESOURCE_GROUP env var).

Options:
  -y, --yes    Skip the interactive confirmation prompt.
  -h, --help   Show this help message.

You can also set FORCE=1 in the environment to skip the prompt.
EOF
      exit 0
      ;;
    *)
      echo "Unknown argument: ${arg}" >&2
      echo "Use --help for usage." >&2
      exit 2
      ;;
  esac
done

echo "==> Subscription: $(az account show --query '[name,id]' -o tsv | paste -sd ' / ' -)"
echo "==> About to DELETE resource group: ${RESOURCE_GROUP}"

if [[ "${FORCE:-}" != "1" && "${ASSUME_YES}" != "1" ]]; then
  read -r -p "Type the resource group name to confirm deletion: " confirm
  if [[ "${confirm}" != "${RESOURCE_GROUP}" ]]; then
    echo "Confirmation failed. Aborting."
    exit 1
  fi
fi

echo "==> Deleting resource group ${RESOURCE_GROUP}..."
az group delete \
  --name "${RESOURCE_GROUP}" \
  --yes \
