#!/usr/bin/env bash
##############################################################################
# NuVatis NuGet Pack Script
# 작성자: 최진호
# 작성일: 2026-02-25
#
# 사용법:
#   ./pack.sh                    # 기본 (Directory.Build.props 버전 사용)
#   ./pack.sh 0.2.0-beta.1      # 버전 지정
#   ./pack.sh 1.0.0 --push      # 버전 지정 + NuGet push
##############################################################################

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUTPUT_DIR="${SCRIPT_DIR}/nupkg"
SOLUTION="${SCRIPT_DIR}/NuVatis.sln"

VERSION="${1:-}"
PUSH_FLAG="${2:-}"

EXPECTED_PACKAGES=(
    "NuVatis.Core"
    "NuVatis.Generators"
    "NuVatis.PostgreSql"
    "NuVatis.MySql"
    "NuVatis.SqlServer"
    "NuVatis.Extensions.DependencyInjection"
    "NuVatis.Extensions.OpenTelemetry"
    "NuVatis.Extensions.EntityFrameworkCore"
    "NuVatis.Testing"
)

echo "============================================"
echo " NuVatis NuGet Pack"
echo "============================================"

## 1. Clean output
echo "[1/5] Cleaning output directory..."
rm -f "${OUTPUT_DIR}"/*.nupkg "${OUTPUT_DIR}"/*.snupkg
mkdir -p "${OUTPUT_DIR}"

## 2. Restore & Build
echo "[2/5] Building Release..."
VERSION_ARG=""
if [[ -n "${VERSION}" && "${VERSION}" != "--push" ]]; then
    VERSION_ARG="/p:Version=${VERSION}"
    echo "       Version: ${VERSION}"
fi

dotnet build "${SOLUTION}" \
    --configuration Release \
    --verbosity minimal \
    ${VERSION_ARG}

if [[ $? -ne 0 ]]; then
    echo "BUILD FAILED"
    exit 1
fi

## 3. Test
echo "[3/5] Running tests..."
dotnet test "${SOLUTION}" \
    --configuration Release \
    --verbosity minimal \
    --no-build \
    || true

## 4. Pack
echo "[4/5] Packing NuGet packages..."
dotnet pack "${SOLUTION}" \
    --configuration Release \
    --output "${OUTPUT_DIR}" \
    --no-build \
    ${VERSION_ARG}

if [[ $? -ne 0 ]]; then
    echo "PACK FAILED"
    exit 1
fi

## 5. Verify
echo "[5/5] Verifying packages..."
MISSING=0
for PKG in "${EXPECTED_PACKAGES[@]}"; do
    COUNT=$(ls "${OUTPUT_DIR}"/${PKG}.*.nupkg 2>/dev/null | wc -l)
    if [[ ${COUNT} -eq 0 ]]; then
        echo "  MISSING: ${PKG}"
        MISSING=$((MISSING + 1))
    else
        FILE=$(ls "${OUTPUT_DIR}"/${PKG}.*.nupkg)
        SIZE=$(du -h "${FILE}" | cut -f1)
        echo "  OK: $(basename "${FILE}") (${SIZE})"
    fi
done

if [[ ${MISSING} -gt 0 ]]; then
    echo ""
    echo "WARNING: ${MISSING} package(s) missing!"
    exit 1
fi

echo ""
echo "============================================"
echo " All ${#EXPECTED_PACKAGES[@]} packages created successfully"
echo " Output: ${OUTPUT_DIR}/"
echo "============================================"

## Optional: Push to NuGet
##
## NOTE: 프로덕션 배포는 GitHub Actions Trusted Publishing을 사용한다.
##   - .github/workflows/publish.yml 참조
##   - v* 태그 push 시 OIDC 기반으로 NuGet.org에 자동 배포
##   - API 키 관리 불필요 (단기 토큰 자동 발급)
##
## 이 --push 옵션은 로컬 테스트/긴급 수동 배포용으로만 유지한다.
if [[ "${PUSH_FLAG}" == "--push" || "${VERSION}" == "--push" ]]; then
    if [[ -z "${NUGET_API_KEY:-}" ]]; then
        echo ""
        echo "ERROR: NUGET_API_KEY environment variable is not set."
        echo "  export NUGET_API_KEY=your-api-key"
        echo "  ./pack.sh ${VERSION} --push"
        exit 1
    fi

    NUGET_SOURCE="${NUGET_SOURCE:-https://api.nuget.org/v3/index.json}"
    echo ""
    echo "Pushing to ${NUGET_SOURCE}..."
    for NUPKG in "${OUTPUT_DIR}"/*.nupkg; do
        echo "  Pushing $(basename "${NUPKG}")..."
        dotnet nuget push "${NUPKG}" \
            --api-key "${NUGET_API_KEY}" \
            --source "${NUGET_SOURCE}" \
            --skip-duplicate
    done
    echo "Push complete."
fi
