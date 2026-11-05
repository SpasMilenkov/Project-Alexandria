#!/bin/bash
set -e

echo "=== Alexandria RustFS Initialization Script ==="
echo ""

# Configuration
CONTAINER_NAME="alexandria-rustfs-dev"
RUSTFS_ENDPOINT="http://localhost:9000"
RUSTFS_CONSOLE="http://localhost:9001"

# Load credentials from environment or use defaults
if [ -z "$RUSTFS_ACCESS_KEY" ] || [ -z "$RUSTFS_SECRET_KEY" ]; then
    echo -e "${YELLOW}Warning: RUSTFS_ACCESS_KEY and/or RUSTFS_SECRET_KEY not set in environment${NC}"
    echo "Attempting to load from .env file..."
    
    if [ -f .env ]; then
        export $(grep -v '^#' .env | grep -E 'RUSTFS_ACCESS_KEY|RUSTFS_SECRET_KEY' | xargs)
    fi
    
    if [ -z "$RUSTFS_ACCESS_KEY" ] || [ -z "$RUSTFS_SECRET_KEY" ]; then
        echo -e "${RED}Error: Credentials not found!${NC}"
        echo "Please set RUSTFS_ACCESS_KEY and RUSTFS_SECRET_KEY in your environment or .env file"
        echo ""
        echo "Example:"
        echo "  export RUSTFS_ACCESS_KEY=dev_admin"
        echo "  export RUSTFS_SECRET_KEY=dev_password_123"
        exit 1
    fi
fi

RUSTFS_ACCESS_KEY="${RUSTFS_ACCESS_KEY}"
RUSTFS_SECRET_KEY="${RUSTFS_SECRET_KEY}"

BUCKETS=("alexandria-files" "alexandria-previews" "alexandria-temp" "alexandria-images")
PUBLIC_BUCKETS=("alexandria-previews")  # Only previews are public

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Check if container is running
if ! docker ps --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
    echo -e "${RED}Error: RustFS container '${CONTAINER_NAME}' is not running${NC}"
    echo "Start it with: docker-compose --profile rustfs up -d"
    exit 1
fi

echo "Waiting for RustFS to be ready..."
sleep 3

echo -e "${BLUE}Using credentials:${NC}"
echo "  Access Key: ${RUSTFS_ACCESS_KEY}"
echo "  Secret Key: ${RUSTFS_SECRET_KEY:0:3}***${RUSTFS_SECRET_KEY: -3}"
echo ""

if ! command -v aws &> /dev/null; then
    echo -e "${RED}AWS CLI (aws) not found.${NC}"
    echo "Please install AWS CLI v2 from: https://docs.aws.amazon.com/cli/latest/userguide/getting-started-install.html"
    exit 1
fi

export AWS_ACCESS_KEY_ID="${RUSTFS_ACCESS_KEY}"
export AWS_SECRET_ACCESS_KEY="${RUSTFS_SECRET_KEY}"
export AWS_DEFAULT_REGION="us-east-1"

s3api() { aws --endpoint-url "${RUSTFS_ENDPOINT}" s3api "$@"; }

echo ""
echo "Step 1: Testing RustFS health endpoint..."
if curl -s -f "${RUSTFS_ENDPOINT}/health" > /dev/null 2>&1; then
    echo -e "${GREEN}✓ RustFS health check passed${NC}"
else
    echo -e "${YELLOW}Warning: Health endpoint not responding (this may be normal)${NC}"
fi

echo ""
echo "Step 2: Testing connection with simple list operation..."
if s3api list-buckets 2>&1 | grep -q "AccessDenied\|InvalidAccessKeyId\|SignatureDoesNotMatch"; then
    echo -e "${RED}Authentication failed!${NC}"
    echo ""
    echo "Debug information:"
    echo "  Endpoint: ${RUSTFS_ENDPOINT}"
    echo "  Access Key: ${RUSTFS_ACCESS_KEY}"
    echo "  Secret Key: ${RUSTFS_SECRET_KEY:0:3}***${RUSTFS_SECRET_KEY: -3}"
    echo ""
    echo "Please verify:"
    echo "  1. RustFS container is running: docker ps | grep rustfs"
    echo "  2. Check container logs: docker logs ${CONTAINER_NAME}"
    echo "  3. Verify credentials in docker-compose match your .env"
    echo "  4. Try accessing web console: ${RUSTFS_CONSOLE}"
    exit 1
fi

echo -e "${GREEN}✓ Connected to RustFS successfully${NC}"

echo ""
echo "Step 3: Creating buckets..."
for bucket in "${BUCKETS[@]}"; do
    if s3api head-bucket --bucket "${bucket}" > /dev/null 2>&1; then
        echo -e "${YELLOW}Bucket '${bucket}' already exists, skipping${NC}"
    else
        s3api create-bucket --bucket "${bucket}" > /dev/null
        echo -e "${GREEN}✓ Created bucket: ${bucket}${NC}"
    fi
done

echo ""
echo "Step 4: Setting bucket policies..."

# Set public read policy for preview bucket
for bucket in "${PUBLIC_BUCKETS[@]}"; do
    echo "Setting public read policy for: ${bucket}"
    s3api put-bucket-policy --bucket "${bucket}" --policy "{
        \"Version\": \"2012-10-17\",
        \"Statement\": [{
            \"Effect\": \"Allow\",
            \"Principal\": {\"AWS\": [\"*\"]},
            \"Action\": [\"s3:GetObject\"],
            \"Resource\": [\"arn:aws:s3:::${bucket}/*\"]
        }]
    }"
    echo -e "${GREEN}✓ Public read access enabled for: ${bucket}${NC}"
done

# Ensure other buckets are private
for bucket in "${BUCKETS[@]}"; do
    # Skip public buckets
    if [[ " ${PUBLIC_BUCKETS[@]} " =~ " ${bucket} " ]]; then
        continue
    fi
    echo "Ensuring private policy for: ${bucket}"
    s3api delete-bucket-policy --bucket "${bucket}" 2>/dev/null || true
    echo -e "${GREEN}✓ Private access for: ${bucket}${NC}"
done

echo ""
echo "Step 5: Verifying bucket configuration..."
aws --endpoint-url "${RUSTFS_ENDPOINT}" s3 ls

echo ""
echo "=== RustFS Initialization Complete! ==="
echo ""
echo "Connection details:"
echo "  S3 Endpoint:    ${RUSTFS_ENDPOINT}"
echo "  S3 Region:      us-east-1 (or your configured region)"
echo "  Web Console:    ${RUSTFS_CONSOLE}"
echo "  Access Key:     ${RUSTFS_ACCESS_KEY}"
echo "  Secret Key:     ${RUSTFS_SECRET_KEY}"
echo ""
echo "Buckets created:"
echo "  • alexandria-files     (private)"
echo "  • alexandria-previews  (public read)"
echo "  • alexandria-temp      (private)"
echo ""
echo "Access credentials:"
echo "  All buckets use the same RustFS root credentials:"
echo "  - Access Key: ${RUSTFS_ACCESS_KEY}"
echo "  - Secret Key: ${RUSTFS_SECRET_KEY}"
echo ""
echo -e "${BLUE}Note: RustFS uses a single-user model by default.${NC}"
echo -e "${BLUE}For production, create additional users via the web console at ${RUSTFS_CONSOLE}${NC}"
echo ""
echo "Next steps:"
echo "  1. Access the web console: ${RUSTFS_CONSOLE}"
echo "  2. Configure your application with the credentials above"
echo "  3. Test S3 API: aws s3 ls --endpoint-url ${RUSTFS_ENDPOINT}"
echo ""
echo "To set custom policies:"
echo "  aws --endpoint-url ${RUSTFS_ENDPOINT} s3api put-bucket-policy --bucket bucket-name --policy file://policy.json"
echo ""