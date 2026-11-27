#!/usr/bin/env bash
# Build win-x64 self-contained payload + optional MSI (wixl) for Group Policy Director.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="${ROOT}/artifacts"
PAYLOAD="${OUT}/payload"
STAGE_API="${PAYLOAD}/Api"
STAGE_WORKER="${PAYLOAD}/Worker"
UI_DIST="${ROOT}/ui/dist"
VERSION="${GPD_VERSION:-0.1.0}"
FIX_HEAT="${ROOT}/packaging/fix-heat-wxs.py"

echo "==> Clean artifacts"
rm -rf "${OUT}"
mkdir -p "${STAGE_API}" "${STAGE_WORKER}" "${PAYLOAD}/scripts" "${PAYLOAD}/database"

echo "==> Build UI (Vite)"
(
  cd "${ROOT}/ui"
  if [[ ! -d node_modules ]]; then
    npm ci
  fi
  npm run build
)

echo "==> Publish API win-x64 self-contained"
dotnet publish "${ROOT}/src/GroupPolicyDirector.Api/GroupPolicyDirector.Api.csproj" \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=false \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "${STAGE_API}"

echo "==> Copy UI into API wwwroot"
mkdir -p "${STAGE_API}/wwwroot"
cp -a "${UI_DIST}/." "${STAGE_API}/wwwroot/"

echo "==> Overlay packaging API appsettings"
cp "${ROOT}/packaging/config/api.appsettings.json" "${STAGE_API}/appsettings.json"
rm -f "${STAGE_API}/appsettings.Development.json"
find "${STAGE_API}" -name '*.pdb' -delete

echo "==> Publish Worker win-x64 self-contained"
dotnet publish "${ROOT}/src/GroupPolicyDirector.Worker/GroupPolicyDirector.Worker.csproj" \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=false \
  -o "${STAGE_WORKER}"

cp "${ROOT}/packaging/config/worker.appsettings.json" "${STAGE_WORKER}/appsettings.json"
rm -f "${STAGE_WORKER}/appsettings.Development.json"
find "${STAGE_WORKER}" -name '*.pdb' -delete

echo "==> Copy scripts + SQL schema stubs"
cp -a "${ROOT}/packaging/scripts/." "${PAYLOAD}/scripts/"
cp -a "${ROOT}/database/." "${PAYLOAD}/database/"

echo "==> ZIP payload"
(
  cd "${OUT}"
  zip -qr "GroupPolicyDirector-${VERSION}-win-x64.zip" payload
)

build_msi() {
  if ! command -v wixl >/dev/null 2>&1 || ! command -v wixl-heat >/dev/null 2>&1; then
    echo "!! wixl/wixl-heat not found — skipping MSI (ZIP + Setup.ps1 remain)."
    return 0
  fi

  echo "==> Harvest payload for MSI (single tree avoids File Id collisions)"
  local wix_out="${OUT}/wix"
  mkdir -p "${wix_out}"
  local product_files="${wix_out}/ProductFiles.wxs"

  # Absolute paths required for wixl-heat prefix matching.
  find "${PAYLOAD}" -type f \
    | wixl-heat -p "${PAYLOAD}/" \
        --component-group ProductFiles \
        --directory-ref INSTALLDIR \
        --var var.PayloadSource \
        --win64 \
    > "${product_files}"
  python3 "${FIX_HEAT}" "${product_files}"

  local product="${wix_out}/Product.wxs"
  cp "${ROOT}/packaging/wix/Product.wxs" "${product}"

  echo "==> Link MSI with wixl"
  local msi="${OUT}/GroupPolicyDirector-${VERSION}-win-x64.msi"
  if ! wixl -a x64 -o "${msi}" \
      -D PayloadSource="${PAYLOAD}" \
      "${product}" \
      "${product_files}"; then
    echo "error: wixl failed" >&2
    return 1
  fi
  if [[ ! -f "${msi}" ]]; then
    echo "error: MSI was not produced at ${msi}" >&2
    return 1
  fi
  echo "MSI: ${msi} ($(du -h "${msi}" | cut -f1))"
}

build_msi

echo "==> Done"
ls -lh "${OUT}/GroupPolicyDirector-${VERSION}-win-x64.zip" "${OUT}/GroupPolicyDirector-${VERSION}-win-x64.msi"
echo "Payload: ${PAYLOAD}"
