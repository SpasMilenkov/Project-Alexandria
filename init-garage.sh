#!/bin/bash
set -e

echo "=== Alexandria Garage Initialization Script ==="
echo ""

# Configuration
CONTAINER_NAME="alexandria-garage-dev"
MASTER_KEY_NAME="alexandria-master-key"
PREVIEW_KEY_NAME="alexandria-preview-key"
BUCKETS=("alexandria-files" "alexandria-previews" "alexandria-temp")
PUBLIC_BUCKETS=("alexandria-previews")  # Only previews are public
ZONE="dc1"
CAPACITY="10G"

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Check if container is running
if ! docker ps --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
    echo "Error: Garage container '${CONTAINER_NAME}' is not running"
    echo "Start it with: docker-compose --profile garage up -d"
    exit 1
fi

echo "Waiting for Garage to be ready..."
sleep 5

# Function to execute garage commands
garage_exec() {
    docker exec "${CONTAINER_NAME}" /garage "$@"
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

# Check if node has a role assigned
if echo "$LAYOUT_OUTPUT" | grep -q "No nodes currently have a role"; then
    echo "No nodes have roles assigned, configuring layout..."
    garage_exec layout assign -z "${ZONE}" -c "${CAPACITY}" "${NODE_ID}"
    
    # Apply with the next version
    NEXT_VERSION=$((LAYOUT_VERSION + 1))
    echo "Applying layout with version ${NEXT_VERSION}..."
    garage_exec layout apply --version ${NEXT_VERSION}
    echo -e "${GREEN}Layout created and applied${NC}"
    
    # Wait for layout to be applied
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
echo "Step 7: Granting PREVIEW key permissions (alexandria-previews read and write, alexandria-files read-only)..."
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
echo "=== Garage Initialization Complete! ==="
echo ""
echo "Connection details:"
echo "  S3 Endpoint: http://localhost:9000"
echo "  S3 Region:   garage"
echo "  Admin API:   http://localhost:9001"
echo ""
echo "Buckets created:"
echo "  • alexandria-files     (private, master key only)"
echo "  • alexandria-previews  (public read, master + preview keys)"
echo "  • alexandria-temp      (private, master key only)"
echo ""
echo "Keys created:"
echo "  • ${MASTER_KEY_NAME}  → Full access to ALL buckets"
echo "  • ${PREVIEW_KEY_NAME} → Read/Write access to alexandria-previews ONLY"
echo ""
echo "Next steps:"
echo "  1. Save the credentials shown above to your .env file"
echo "  2. Use MASTER key for your main application"
echo "  3. Use PREVIEW key for your preview generation service"
echo "  4. Test with: aws s3 ls --endpoint-url http://localhost:9000"
echo ""
echo "To view credentials later:"
echo "  docker exec ${CONTAINER_NAME} /garage key info ${MASTER_KEY_NAME}"
echo "  docker exec ${CONTAINER_NAME} /garage key info ${PREVIEW_KEY_NAME}"
echo ""