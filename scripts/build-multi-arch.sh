# Multi-architecture build script for AtlasBank services
#!/bin/bash

# Build script for multi-architecture Docker images
# Usage: ./build-multi-arch.sh [service-name] [tag]

set -e

SERVICE_NAME=${1:-"all"}
TAG=${2:-"latest"}
REGISTRY=${3:-"atlasbank"}

# Supported architectures
ARCHITECTURES="linux/amd64,linux/arm64"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check if buildx is available
check_buildx() {
    if ! docker buildx version > /dev/null 2>&1; then
        log_error "Docker buildx is not available. Please install Docker Desktop or enable buildx."
        exit 1
    fi
}

# Create buildx builder if it doesn't exist
setup_builder() {
    local builder_name="atlasbank-builder"
    
    if ! docker buildx inspect $builder_name > /dev/null 2>&1; then
        log_info "Creating buildx builder: $builder_name"
        docker buildx create --name $builder_name --use
    else
        log_info "Using existing buildx builder: $builder_name"
        docker buildx use $builder_name
    fi
}

# Build single service
build_service() {
    local service=$1
    local dockerfile_path=""
    local context_path=""
    
    case $service in
        "gateway")
            dockerfile_path="gateways/Atlas.ApiGateway/Dockerfile"
            context_path="."
            ;;
        "ledger")
            dockerfile_path="src/Services/Atlas.Ledger/Dockerfile"
            context_path="."
            ;;
        "payments")
            dockerfile_path="src/Services/Atlas.Payments/Dockerfile"
            context_path="."
            ;;
        "nipgw")
            dockerfile_path="src/Payments/Atlas.NipGateway/Dockerfile"
            context_path="src/Payments/Atlas.NipGateway"
            ;;
        "cardsvault")
            dockerfile_path="src/Cards/Atlas.Cards.Vault/Dockerfile"
            context_path="src/Cards/Atlas.Cards.Vault"
            ;;
        "riskfeatures")
            dockerfile_path="src/Risk/Atlas.Risk.Features/Dockerfile"
            context_path="src/Risk/Atlas.Risk.Features"
            ;;
        *)
            log_error "Unknown service: $service"
            return 1
            ;;
    esac
    
    log_info "Building $service for architectures: $ARCHITECTURES"
    
    docker buildx build \
        --platform $ARCHITECTURES \
        --file $dockerfile_path \
        --tag $REGISTRY/$service:$TAG \
        --tag $REGISTRY/$service:latest \
        --push \
        $context_path
    
    log_info "Successfully built and pushed $service"
}

# Build all services
build_all() {
    local services=("gateway" "ledger" "payments" "nipgw" "cardsvault" "riskfeatures")
    
    for service in "${services[@]}"; do
        build_service $service
    done
}

# Main execution
main() {
    log_info "Starting multi-architecture build for AtlasBank"
    log_info "Service: $SERVICE_NAME"
    log_info "Tag: $TAG"
    log_info "Registry: $REGISTRY"
    
    check_buildx
    setup_builder
    
    if [ "$SERVICE_NAME" = "all" ]; then
        build_all
    else
        build_service $SERVICE_NAME
    fi
    
    log_info "Multi-architecture build completed successfully!"
}

# Run main function
main "$@"

