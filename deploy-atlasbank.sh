#!/bin/bash

# AtlasBank Azure Deployment Script
# This script automates the complete deployment of AtlasBank to Azure

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
RESOURCE_GROUP="atlasbank-rg"
LOCATION="eastus"
AKS_NAME="atlasbank-aks"
ACR_NAME="atlasbankacr"
SERVICE_PRINCIPAL_NAME="atlasbank-github-actions"

echo -e "${BLUE}🏦 AtlasBank Azure Deployment Script${NC}"
echo "=============================================="
echo ""

# Function to print status
print_status() {
    echo -e "${BLUE}[$(date +'%H:%M:%S')]${NC} $1"
}

print_success() {
    echo -e "${GREEN}✅ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

print_error() {
    echo -e "${RED}❌ $1${NC}"
}

# Check prerequisites
check_prerequisites() {
    print_status "Checking prerequisites..."
    
    # Check if Azure CLI is installed
    if ! command -v az &> /dev/null; then
        print_error "Azure CLI is not installed. Please install it first."
        echo "Visit: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
        exit 1
    fi
    
    # Check if Terraform is installed
    if ! command -v terraform &> /dev/null; then
        print_error "Terraform is not installed. Please install it first."
        echo "Visit: https://www.terraform.io/downloads.html"
        exit 1
    fi
    
    # Check if kubectl is installed
    if ! command -v kubectl &> /dev/null; then
        print_error "kubectl is not installed. Please install it first."
        echo "Visit: https://kubernetes.io/docs/tasks/tools/"
        exit 1
    fi
    
    # Check if Docker is installed
    if ! command -v docker &> /dev/null; then
        print_error "Docker is not installed. Please install it first."
        echo "Visit: https://docs.docker.com/get-docker/"
        exit 1
    fi
    
    print_success "All prerequisites are installed"
}

# Login to Azure
login_azure() {
    print_status "Logging into Azure..."
    
    # Check if already logged in
    if az account show &> /dev/null; then
        print_success "Already logged into Azure"
        az account show --query "name" -o tsv
    else
        print_status "Please log in to Azure..."
        az login
        print_success "Successfully logged into Azure"
    fi
}

# Create resource group
create_resource_group() {
    print_status "Creating resource group: $RESOURCE_GROUP"
    
    if az group show --name $RESOURCE_GROUP &> /dev/null; then
        print_warning "Resource group $RESOURCE_GROUP already exists"
    else
        az group create --name $RESOURCE_GROUP --location $LOCATION
        print_success "Resource group $RESOURCE_GROUP created"
    fi
}

# Create service principal
create_service_principal() {
    print_status "Creating service principal: $SERVICE_PRINCIPAL_NAME"
    
    # Check if service principal already exists
    if az ad sp list --display-name $SERVICE_PRINCIPAL_NAME --query "[0].displayName" -o tsv 2>/dev/null | grep -q $SERVICE_PRINCIPAL_NAME; then
        print_warning "Service principal $SERVICE_PRINCIPAL_NAME already exists"
    else
        # Create service principal
        SP_OUTPUT=$(az ad sp create-for-rbac --name $SERVICE_PRINCIPAL_NAME \
            --role contributor \
            --scopes /subscriptions/$(az account show --query id -o tsv)/resourceGroups/$RESOURCE_GROUP \
            --sdk-auth)
        
        print_success "Service principal created"
        echo ""
        print_warning "IMPORTANT: Save these credentials for GitHub Secrets:"
        echo "$SP_OUTPUT" | jq .
        echo ""
        print_warning "Add these to GitHub Secrets in your repository:"
        echo "- AZURE_TENANT_ID"
        echo "- AZURE_SUBSCRIPTION_ID" 
        echo "- AZURE_CLIENT_ID"
        echo "- AZURE_CLIENT_SECRET"
        echo ""
        read -p "Press Enter after adding secrets to GitHub..."
    fi
}

# Deploy infrastructure with Terraform
deploy_infrastructure() {
    print_status "Deploying infrastructure with Terraform..."
    
    cd infrastructure/iac/terraform/azure
    
    # Initialize Terraform
    print_status "Initializing Terraform..."
    terraform init
    
    # Plan deployment
    print_status "Planning Terraform deployment..."
    terraform plan -var="prefix=atlasbank" -var="location=$LOCATION" -var="resource_group_name=$RESOURCE_GROUP"
    
    # Ask for confirmation
    echo ""
    print_warning "This will create Azure resources. Continue? (y/N)"
    read -r response
    if [[ ! "$response" =~ ^[Yy]$ ]]; then
        print_error "Deployment cancelled"
        exit 1
    fi
    
    # Apply deployment
    print_status "Applying Terraform deployment..."
    terraform apply -var="prefix=atlasbank" -var="location=$LOCATION" -var="resource_group_name=$RESOURCE_GROUP" -auto-approve
    
    print_success "Infrastructure deployed successfully"
    
    cd ../../..
}

# Build and push Docker images
build_and_push_images() {
    print_status "Building and pushing Docker images..."
    
    # Get ACR login server
    ACR_LOGIN_SERVER=$(az acr list --resource-group $RESOURCE_GROUP --query "[0].loginServer" -o tsv)
    
    # Login to ACR
    print_status "Logging into Azure Container Registry..."
    az acr login --name $(echo $ACR_LOGIN_SERVER | cut -d'.' -f1)
    
    # Build and push Ledger service
    print_status "Building Ledger service..."
    docker build -t $ACR_LOGIN_SERVER/ledger:latest src/Services/Atlas.Ledger/
    docker push $ACR_LOGIN_SERVER/ledger:latest
    
    # Build and push Payments service
    print_status "Building Payments service..."
    docker build -t $ACR_LOGIN_SERVER/payments:latest src/Services/Atlas.Payments/
    docker push $ACR_LOGIN_SERVER/payments:latest
    
    # Build and push AML Worker
    print_status "Building AML Worker..."
    docker build -t $ACR_LOGIN_SERVER/aml-worker:latest src/KycAml/Atlas.KycAml.Worker/
    docker push $ACR_LOGIN_SERVER/aml-worker:latest
    
    # Build and push API Gateway
    print_status "Building API Gateway..."
    docker build -t $ACR_LOGIN_SERVER/gateway:latest src/Gateways/Atlas.ApiGateway/
    docker push $ACR_LOGIN_SERVER/gateway:latest
    
    print_success "All Docker images built and pushed"
}

# Deploy to Kubernetes
deploy_to_kubernetes() {
    print_status "Deploying to Azure Kubernetes Service..."
    
    # Get AKS credentials
    print_status "Getting AKS credentials..."
    az aks get-credentials --resource-group $RESOURCE_GROUP --name $AKS_NAME --overwrite-existing
    
    # Create namespace
    print_status "Creating namespace..."
    kubectl create namespace atlasbank --dry-run=client -o yaml | kubectl apply -f -
    
    # Get ACR login server
    ACR_LOGIN_SERVER=$(az acr list --resource-group $RESOURCE_GROUP --query "[0].loginServer" -o tsv)
    
    # Create Kubernetes secrets
    print_status "Creating Kubernetes secrets..."
    
    # Get database connection string
    DB_CONNECTION=$(az postgres flexible-server show --resource-group $RESOURCE_GROUP --name atlasbank-postgres --query "fullyQualifiedDomainName" -o tsv)
    
    # Create secret for database
    kubectl create secret generic atlasbank-secrets \
        --namespace atlasbank \
        --from-literal=database-connection="Host=$DB_CONNECTION;Port=5432;Database=atlas;Username=atlasadmin;Password=AtlasBank2024!" \
        --dry-run=client -o yaml | kubectl apply -f -
    
    # Create secret for Event Hubs
    EVENTHUBS_CONNECTION=$(az eventhubs namespace authorization-rule keys list \
        --resource-group $RESOURCE_GROUP \
        --namespace-name atlasbank-ehns \
        --name producer \
        --query "primaryConnectionString" -o tsv)
    
    kubectl create secret generic eventhubs-secrets \
        --namespace atlasbank \
        --from-literal=connection-string="$EVENTHUBS_CONNECTION" \
        --dry-run=client -o yaml | kubectl apply -f -
    
    # Deploy services
    print_status "Deploying services to Kubernetes..."
    
    # Create deployment manifests
    cat > k8s-deployment.yaml << EOF
apiVersion: apps/v1
kind: Deployment
metadata:
  name: ledger-service
  namespace: atlasbank
spec:
  replicas: 2
  selector:
    matchLabels:
      app: ledger-service
  template:
    metadata:
      labels:
        app: ledger-service
    spec:
      containers:
      - name: ledger
        image: $ACR_LOGIN_SERVER/ledger:latest
        ports:
        - containerPort: 5181
        env:
        - name: ConnectionStrings__Ledger
          valueFrom:
            secretKeyRef:
              name: atlasbank-secrets
              key: database-connection
        - name: KAFKA_BOOTSTRAP
          valueFrom:
            secretKeyRef:
              name: eventhubs-secrets
              key: connection-string
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
---
apiVersion: v1
kind: Service
metadata:
  name: ledger-service
  namespace: atlasbank
spec:
  selector:
    app: ledger-service
  ports:
  - port: 80
    targetPort: 5181
  type: ClusterIP
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: payments-service
  namespace: atlasbank
spec:
  replicas: 2
  selector:
    matchLabels:
      app: payments-service
  template:
    metadata:
      labels:
        app: payments-service
    spec:
      containers:
      - name: payments
        image: $ACR_LOGIN_SERVER/payments:latest
        ports:
        - containerPort: 5191
        env:
        - name: ConnectionStrings__Payments
          valueFrom:
            secretKeyRef:
              name: atlasbank-secrets
              key: database-connection
        - name: Services__LedgerGrpc
          value: "http://ledger-service:80"
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
---
apiVersion: v1
kind: Service
metadata:
  name: payments-service
  namespace: atlasbank
spec:
  selector:
    app: payments-service
  ports:
  - port: 80
    targetPort: 5191
  type: ClusterIP
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: aml-worker
  namespace: atlasbank
spec:
  replicas: 1
  selector:
    matchLabels:
      app: aml-worker
  template:
    metadata:
      labels:
        app: aml-worker
    spec:
      containers:
      - name: aml-worker
        image: $ACR_LOGIN_SERVER/aml-worker:latest
        env:
        - name: KAFKA_BOOTSTRAP
          valueFrom:
            secretKeyRef:
              name: eventhubs-secrets
              key: connection-string
        - name: Rules__Path
          value: "/app/config/rules/aml-rules.yaml"
        - name: Topics__LedgerEvents
          value: "ledger-events"
        resources:
          requests:
            memory: "128Mi"
            cpu: "100m"
          limits:
            memory: "256Mi"
            cpu: "200m"
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: gateway-service
  namespace: atlasbank
spec:
  replicas: 2
  selector:
    matchLabels:
      app: gateway-service
  template:
    metadata:
      labels:
        app: gateway-service
    spec:
      containers:
      - name: gateway
        image: $ACR_LOGIN_SERVER/gateway:latest
        ports:
        - containerPort: 5080
        resources:
          requests:
            memory: "128Mi"
            cpu: "100m"
          limits:
            memory: "256Mi"
            cpu: "200m"
---
apiVersion: v1
kind: Service
metadata:
  name: gateway-service
  namespace: atlasbank
spec:
  selector:
    app: gateway-service
  ports:
  - port: 80
    targetPort: 5080
  type: LoadBalancer
EOF
    
    # Apply deployment
    kubectl apply -f k8s-deployment.yaml
    
    # Wait for deployments
    print_status "Waiting for deployments to be ready..."
    kubectl wait --for=condition=available --timeout=300s deployment/ledger-service -n atlasbank
    kubectl wait --for=condition=available --timeout=300s deployment/payments-service -n atlasbank
    kubectl wait --for=condition=available --timeout=300s deployment/aml-worker -n atlasbank
    kubectl wait --for=condition=available --timeout=300s deployment/gateway-service -n atlasbank
    
    print_success "All services deployed to Kubernetes"
}

# Test deployment
test_deployment() {
    print_status "Testing deployment..."
    
    # Get external IP
    EXTERNAL_IP=$(kubectl get service gateway-service -n atlasbank -o jsonpath='{.status.loadBalancer.ingress[0].ip}')
    
    if [ -z "$EXTERNAL_IP" ]; then
        print_warning "External IP not yet assigned. Waiting..."
        sleep 30
        EXTERNAL_IP=$(kubectl get service gateway-service -n atlasbank -o jsonpath='{.status.loadBalancer.ingress[0].ip}')
    fi
    
    if [ -n "$EXTERNAL_IP" ]; then
        print_success "External IP: $EXTERNAL_IP"
        
        # Test health endpoints
        print_status "Testing health endpoints..."
        
        # Wait a bit for services to be ready
        sleep 30
        
        # Test gateway health
        if curl -f "http://$EXTERNAL_IP/health" &> /dev/null; then
            print_success "Gateway health check passed"
        else
            print_warning "Gateway health check failed"
        fi
        
        # Test payment through gateway
        print_status "Testing payment through gateway..."
        RESPONSE=$(curl -s -X POST "http://$EXTERNAL_IP/payments/transfers" \
            -H "Content-Type: application/json" \
            -H "Idempotency-Key: deployment-test-$(date +%s)" \
            -H "X-Tenant-Id: tnt_demo" \
            -d '{
                "SourceAccountId": "acc_001",
                "DestinationAccountId": "acc_002",
                "Minor": 10000,
                "Currency": "NGN",
                "Narration": "Deployment test"
            }')
        
        if echo "$RESPONSE" | grep -q "status"; then
            print_success "Payment test passed"
        else
            print_warning "Payment test failed"
        fi
        
    else
        print_warning "Could not get external IP. Check service status manually."
    fi
}

# Show deployment information
show_deployment_info() {
    print_status "Deployment Information"
    echo "======================"
    echo ""
    echo "Resource Group: $RESOURCE_GROUP"
    echo "Location: $LOCATION"
    echo "AKS Cluster: $AKS_NAME"
    echo "ACR Registry: $ACR_NAME"
    echo ""
    
    # Get external IP
    EXTERNAL_IP=$(kubectl get service gateway-service -n atlasbank -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>/dev/null || echo "Not available")
    echo "External IP: $EXTERNAL_IP"
    echo ""
    
    echo "Service URLs:"
    echo "- Gateway: http://$EXTERNAL_IP"
    echo "- Health Check: http://$EXTERNAL_IP/health"
    echo "- Payments API: http://$EXTERNAL_IP/payments/transfers"
    echo ""
    
    echo "Useful Commands:"
    echo "- kubectl get pods -n atlasbank"
    echo "- kubectl get services -n atlasbank"
    echo "- kubectl logs -f deployment/ledger-service -n atlasbank"
    echo "- kubectl logs -f deployment/payments-service -n atlasbank"
    echo "- kubectl logs -f deployment/aml-worker -n atlasbank"
    echo ""
    
    print_success "AtlasBank deployed successfully!"
}

# Main deployment function
main() {
    echo "Starting AtlasBank deployment to Azure..."
    echo ""
    
    check_prerequisites
    login_azure
    create_resource_group
    create_service_principal
    deploy_infrastructure
    build_and_push_images
    deploy_to_kubernetes
    test_deployment
    show_deployment_info
    
    echo ""
    print_success "🎉 AtlasBank deployment completed successfully!"
    print_warning "Don't forget to:"
    echo "1. Add GitHub Secrets for CI/CD"
    echo "2. Configure monitoring and alerts"
    echo "3. Set up backup procedures"
    echo "4. Schedule security assessments"
}

# Run main function
main "$@"
