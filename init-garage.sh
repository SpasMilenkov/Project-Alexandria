#!/bin/bash
set -e

echo "=== Alexandria Garage Initialization Script ==="
echo ""

# Configuration
CONTAINER_NAME="alexandria-garage-dev"
MASTER_KEY_NAME="alexandria-master-key"
PREVIEW_KEY_NAME="alexandria-preview-key"
BUCKETS=("alexandria-files" "alexandria-previews" "alexandria-temp" "alexandria-images")
PUBLIC_BUCKETS=("alexandria-previews")  # Only previews are public
CORS_BUCKETS=("alexandria-images")      # Buckets that need browser CORS access
ZONE="dc1"
CAPACITY="10G"

# CORS allowed origins — add your production domain here when deploying
CORS_ORIGINS=("http://localhost:5173")

# mc alias — must match what you configured with: mc alias set <name> http://localhost:9000 ...
MC_ALIAS="garage"

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Check if container is running
if ! docker ps --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
    echo "Error: Garage container '${CONTAINER_NAME}' is not running"
    echo "Start it with: docker-compose --profile garage up -d"
    exit 1
fi

# Detect available CORS tools — mc preferred, AWS CLI as fallback
HAS_MC=false
HAS_AWS=false
command -v mc  &> /dev/null && HAS_MC=true
command -v aws &> /dev/null && HAS_AWS=true

if [ "${HAS_MC}" = true ]; then
    echo -e "${GREEN}CORS tool: mc (MinIO client)${NC}"
    SKIP_CORS=false
elif [ "${HAS_AWS}" = true ]; then
    echo -e "${YELLOW}CORS tool: AWS CLI (mc not found, falling back)${NC}"
    SKIP_CORS=false
else
    echo -e "${RED}Warning: neither mc nor AWS CLI found — CORS configuration will be skipped.${NC}"
    echo "Install mc:      https://min.io/docs/minio/linux/reference/minio-mc.html"
    echo "Install AWS CLI: pip install awscli"
    SKIP_CORS=true
fi

echo "Waiting for Garage to be ready..."
sleep 5

# Function to execute garage commands
garage_exec() {
    docker exec "${CONTAINER_NAME}" /garage "$@"
}

# Apply CORS via mc — preferred, uses XML format matching the S3 spec exactly
apply_cors_mc() {
    local bucket=$1

    # Build one <AllowedOrigin> element per entry in CORS_ORIGINS
    local origin_elements=""
    for origin in "${CORS_ORIGINS[@]}"; do
        origin_elements+="  <AllowedOrigin>${origin}</AllowedOrigin>"$'\n'
    done

    mc cors set "${MC_ALIAS}/${bucket}" - <<EOF
<CORSConfiguration>
  <CORSRule>
${origin_elements}    <AllowedMethod>GET</AllowedMethod>
    <AllowedMethod>PUT</AllowedMethod>
    <AllowedMethod>POST</AllowedMethod>
    <AllowedMethod>HEAD</AllowedMethod>
    <AllowedHeader>*</AllowedHeader>
    <ExposeHeader>ETag</ExposeHeader>
    <MaxAgeSeconds>3600</MaxAgeSeconds>
  </CORSRule>
</CORSConfiguration>
EOF
}

# Apply CORS via AWS CLI — fallback, uses JSON format
apply_cors_aws() {
    local bucket=$1

    local origins_json
    origins_json=$(printf '"%s",' "${CORS_ORIGINS[@]}")
    origins_json="[${origins_json%,}]"

    local cors_config
    cors_config=$(cat <<EOF
{
  "CORSRules": [{
    "AllowedOrigins": ${origins_json},
    "AllowedMethods": ["GET", "PUT", "POST", "HEAD"],
    "AllowedHeaders": ["*"],
    "ExposeHeaders": ["ETag"],
    "MaxAgeSeconds": 3600
  }]
}
EOF
)

    aws s3api put-bucket-cors \
        --endpoint-url http://localhost:9000 \
        --region garage \
        --bucket "${bucket}" \
        --cors-configuration "${cors_config}"
}

# Dispatcher — tries mc first, falls back to AWS CLI
apply_cors() {
    local bucket=$1
    echo "Applying CORS policy to '${bucket}'..."

    if [ "${HAS_MC}" = true ]; then
        apply_cors_mc "${bucket}"
    else
        apply_cors_aws "${bucket}"
    fi

    echo -e "${GREEN}✓ CORS configured for: ${bucket}${NC}"
    echo "  Allowed origins: ${CORS_ORIGINS[*]}"
    echo "  Allowed methods: GET, PUT, POST, HEAD"
}

echo ""
echo "Step 1: Getting node ID..."
NODE_ID=$(garage_exec node id | grep "^Node ID:" | cut -d' ' -f3 | cut -d'@' -f1 | tr -d '\r\n' | xargs)
echo -e "${GREEN}Node ID: ${NODE_ID}${NC}"

echo ""
echo "Step 2: Checking cluster layout..."
LAYOUT_OUTPUT=$(garage_exec layout show 2>&1)
LAYOUT_VERSION=$(echo "$LAYOUT_OUTPUT" | grep -oP "Current cluster layout version:\s+\K\d+" || echo "0")

echo "Current layout version: ${LAYOUT_VERSION}"

if echo "$LAYOUT_OUTPUT" | grep -q "No nodes currently have a role"; then
    echo "No nodes have roles assigned, configuring layout..."
    garage_exec layout assign -z "${ZONE}" -c "${CAPACITY}" "${NODE_ID}"

    NEXT_VERSION=$((LAYOUT_VERSION + 1))
    echo "Applying layout with version ${NEXT_VERSION}..."
    garage_exec layout apply --version ${NEXT_VERSION}
    echo -e "${GREEN}Layout created and applied${NC}"

    echo "Waiting for layout to be ready..."
    sleep 3
else
    echo -e "${GREEN}Layout already configured and active${NC}"
    garage_exec layout show
fi

echo ""
echo "Step 3: Creating buckets..."
for bucket in "${BUCKETS[@]}"; do
    if garage_exec bucket list 2>/dev/null | grep -q "${bucket}"; then
        echo -e "${YELLOW}Bucket '${bucket}' already exists, skipping${NC}"
    else
        garage_exec bucket create "${bucket}"
        echo -e "${GREEN}Created bucket: ${bucket}${NC}"
    fi
done

echo ""
echo "Step 4: Creating MASTER access key..."
if garage_exec key list | grep -q "${MASTER_KEY_NAME}"; then
    echo -e "${YELLOW}Master key '${MASTER_KEY_NAME}' already exists${NC}"
    echo "Retrieving existing key information..."
    garage_exec key info "${MASTER_KEY_NAME}"
else
    echo -e "${GREEN}Creating new master key: ${MASTER_KEY_NAME}${NC}"
    MASTER_KEY_OUTPUT=$(garage_exec key create "${MASTER_KEY_NAME}")
    echo "${MASTER_KEY_OUTPUT}"

    echo ""
    echo "================================================================"
    echo -e "${GREEN}MASTER KEY CREDENTIALS (Full access to all buckets)${NC}"
    echo "================================================================"
    echo "${MASTER_KEY_OUTPUT}" | grep -E "Key ID:|Secret key:"
    echo "================================================================"
    echo ""
fi

echo ""
echo "Step 5: Creating PREVIEW access key (restricted)..."
if garage_exec key list | grep -q "${PREVIEW_KEY_NAME}"; then
    echo -e "${YELLOW}Preview key '${PREVIEW_KEY_NAME}' already exists${NC}"
    echo "Retrieving existing key information..."
    garage_exec key info "${PREVIEW_KEY_NAME}"
else
    echo -e "${GREEN}Creating new preview key: ${PREVIEW_KEY_NAME}${NC}"
    PREVIEW_KEY_OUTPUT=$(garage_exec key create "${PREVIEW_KEY_NAME}")
    echo "${PREVIEW_KEY_OUTPUT}"

    echo ""
    echo "================================================================"
    echo -e "${BLUE}PREVIEW KEY CREDENTIALS (alexandria-previews only)${NC}"
    echo "================================================================"
    echo "${PREVIEW_KEY_OUTPUT}" | grep -E "Key ID:|Secret key:"
    echo "================================================================"
    echo ""
fi

echo ""
echo "Step 6: Granting MASTER key permissions (full access to all buckets)..."
for bucket in "${BUCKETS[@]}"; do
    garage_exec bucket allow --read --write --owner "${bucket}" --key "${MASTER_KEY_NAME}"
    echo -e "${GREEN}✓ Master key: full access to ${bucket}${NC}"
done

echo ""
echo "Step 7: Granting PREVIEW key permissions..."
garage_exec bucket allow --read --write "alexandria-previews" --key "${PREVIEW_KEY_NAME}"
garage_exec bucket allow --read "alexandria-files" --key "${PREVIEW_KEY_NAME}"
echo -e "${BLUE}✓ Preview key: read/write access to alexandria-previews${NC}"

echo ""
echo "Step 8: Setting public read access for preview bucket..."
for bucket in "${PUBLIC_BUCKETS[@]}"; do
    garage_exec bucket website --allow "${bucket}"
    echo -e "${GREEN}✓ Public access enabled for: ${bucket}${NC}"
done

echo ""
echo "Step 9: Applying CORS policies..."
if [ "${SKIP_CORS}" = true ]; then
    echo -e "${YELLOW}Skipping CORS (neither mc nor AWS CLI found)${NC}"
    echo ""
    echo "Run manually using mc (preferred):"
    for bucket in "${CORS_BUCKETS[@]}"; do
        echo ""
        echo "  mc cors set ${MC_ALIAS}/${bucket} <<'EOF'"
        echo "  <CORSConfiguration>"
        echo "    <CORSRule>"
        echo "      <AllowedOrigin>http://localhost:5173</AllowedOrigin>"
        echo "      <AllowedMethod>GET</AllowedMethod>"
        echo "      <AllowedMethod>PUT</AllowedMethod>"
        echo "      <AllowedMethod>POST</AllowedMethod>"
        echo "      <AllowedMethod>HEAD</AllowedMethod>"
        echo "      <AllowedHeader>*</AllowedHeader>"
        echo "      <ExposeHeader>ETag</ExposeHeader>"
        echo "      <MaxAgeSeconds>3600</MaxAgeSeconds>"
        echo "    </CORSRule>"
        echo "  </CORSConfiguration>"
        echo "  EOF"
    done
    echo ""
    echo "Or using AWS CLI (fallback):"
    for bucket in "${CORS_BUCKETS[@]}"; do
        echo ""
        echo "  aws s3api put-bucket-cors \\"
        echo "    --endpoint-url http://localhost:9000 \\"
        echo "    --region garage \\"
        echo "    --bucket ${bucket} \\"
        echo "    --cors-configuration '{"
        echo "      \"CORSRules\": [{"
        echo "        \"AllowedOrigins\": [\"http://localhost:5173\"],"
        echo "        \"AllowedMethods\": [\"GET\", \"PUT\", \"POST\", \"HEAD\"],"
        echo "        \"AllowedHeaders\": [\"*\"],"
        echo "        \"ExposeHeaders\": [\"ETag\"],"
        echo "        \"MaxAgeSeconds\": 3600"
        echo "      }]"
        echo "    }'"
    done
else
    for bucket in "${CORS_BUCKETS[@]}"; do
        apply_cors "${bucket}"
    done
fi

echo ""
echo "=== Garage Initialization Complete! ==="
echo ""
echo "Connection details:"
echo "  S3 Endpoint: http://localhost:9000"
echo "  S3 Region:   garage"
echo "  Admin API:   http://localhost:9001"
echo ""
echo "Buckets:"
echo "  • alexandria-files     (private — server only, no CORS)"
echo "  • alexandria-previews  (public read — master + preview keys)"
echo "  • alexandria-temp      (private — server only, no CORS)"
echo "  • alexandria-images    (private — CORS enabled for browser GET/PUT/POST/HEAD)"
echo ""
echo "Keys:"
echo "  • ${MASTER_KEY_NAME}  → Full access to ALL buckets"
echo "  • ${PREVIEW_KEY_NAME} → Read/Write access to alexandria-previews ONLY"
echo ""
echo "CORS:"
if [ "${SKIP_CORS}" = true ]; then
    echo -e "  ${RED}• CORS was NOT applied — see manual commands above${NC}"
elif [ "${HAS_MC}" = true ]; then
    echo "  • Applied via mc (XML format)"
else
    echo "  • Applied via AWS CLI (JSON format)"
fi
echo "  • alexandria-images allows GET, PUT, POST, HEAD from: ${CORS_ORIGINS[*]}"
echo "  • All other buckets have no CORS policy (browser cannot access directly)"
echo ""
echo "Next steps:"
echo "  1. Save the credentials shown above to your .env file"
echo "  2. Add 'ImagesBucket=alexandria-images' to your storage config"
echo "  3. For production: add your domain to CORS_ORIGINS at the top of this script"
echo ""
echo "To view credentials later:"
echo "  docker exec ${CONTAINER_NAME} /garage key info ${MASTER_KEY_NAME}"
echo "  docker exec ${CONTAINER_NAME} /garage key info ${PREVIEW_KEY_NAME}"
echo ""
