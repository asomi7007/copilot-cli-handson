#!/usr/bin/env bash
# Upload the game sample screenshots and HTML source into the Community Hub.

set -euo pipefail

HUB_URL="${1:-http://localhost:1345}"
TENANT="${2:-}"

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &> /dev/null && pwd)"
SAMPLES_DIR="$(cd -- "${SCRIPT_DIR}/../../game-samples" &> /dev/null && pwd)"

usage() {
  cat <<'EOF'
Usage: ./preseed.sh [hub-url] [tenant]

Arguments:
  hub-url  Community Hub base URL. Defaults to http://localhost:1345.
  tenant   Optional tenant to seed. When omitted, uploads use the hub's current tenant.
EOF
}

require_tools() {
  local missing=()
  for tool in "$@"; do
    if ! command -v "${tool}" >/dev/null 2>&1; then
      missing+=("${tool}")
    fi
  done

  if [[ ${#missing[@]} -gt 0 ]]; then
    echo "ERROR: Missing required tool(s): ${missing[*]}" >&2
    exit 1
  fi
}

url_encode() {
  local value="$1"
  local encoded=""
  local char
  local hex
  local i

  for ((i = 0; i < ${#value}; i++)); do
    char="${value:i:1}"
    case "${char}" in
      [a-zA-Z0-9.~_-]) encoded+="${char}" ;;
      *) printf -v hex '%%%02X' "'${char}"; encoded+="${hex}" ;;
    esac
  done

  printf '%s' "${encoded}"
}

tenant_query() {
  if [[ -z "${TENANT}" ]]; then
    return
  fi

  printf 'tenant=%s' "$(url_encode "${TENANT}")"
}

append_query() {
  local url="$1"
  local query="$2"

  if [[ -z "${query}" ]]; then
    printf '%s' "${url}"
  elif [[ "${url}" == *\?* ]]; then
    printf '%s&%s' "${url}" "${query}"
  else
    printf '%s?%s' "${url}" "${query}"
  fi
}

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
  usage
  exit 0
fi

if [[ -n "${TENANT}" && ! "${TENANT}" =~ ^[a-zA-Z0-9][a-zA-Z0-9-]{0,30}$ ]]; then
  echo "ERROR: tenant must match ^[a-zA-Z0-9][a-zA-Z0-9-]{0,30}$" >&2
  exit 1
fi

require_tools curl find sort

if [[ ! -d "${SAMPLES_DIR}" ]]; then
  echo "ERROR: game samples folder not found: ${SAMPLES_DIR}" >&2
  exit 1
fi

HUB_URL="${HUB_URL%/}"
TENANT_QUERY="$(tenant_query)"
IMAGE_URL="$(append_query "${HUB_URL}/api/image" "${TENANT_QUERY}")"

echo "==> Community Hub: ${HUB_URL}"
if [[ -n "${TENANT}" ]]; then
  echo "==> Tenant:        ${TENANT}"
else
  echo "==> Tenant:        current"
fi
echo "==> Samples:       ${SAMPLES_DIR}"
echo

game_count=0
image_count=0

while IFS= read -r -d '' html_file; do
  game_name="$(basename "${html_file}" .html)"
  gallery_query="name=$(url_encode "${game_name}")"
  if [[ -n "${TENANT_QUERY}" ]]; then
    gallery_query="${gallery_query}&${TENANT_QUERY}"
  fi
  gallery_url="$(append_query "${HUB_URL}/api/invaders-gallery" "${gallery_query}")"

  echo "Uploading game source: ${game_name}"
  curl -f -sS -X POST \
    -H "Content-Type: text/html; charset=utf-8" \
    --data-binary @"${html_file}" \
    "${gallery_url}" >/dev/null
  ((game_count += 1))

  image_file="${SAMPLES_DIR}/images/${game_name}.png"
  if [[ -f "${image_file}" ]]; then
    echo "Uploading image:       ${game_name}.png"
    curl -f -sS -X POST \
      -F "image=@${image_file}" \
      "${IMAGE_URL}" >/dev/null
    ((image_count += 1))
  else
    echo "WARNING: Missing image for ${game_name}: ${image_file}" >&2
  fi
done < <(find "${SAMPLES_DIR}" -maxdepth 1 -type f -name '*.html' -print0 | sort -z)

echo
echo "Preseed complete: uploaded ${game_count} game source file(s) and ${image_count} image(s)."
