# 🚀 AtlasBank Deployment Guide

## **Overview**

This guide covers deploying AtlasBank to production environments with enterprise-grade security, scalability, and compliance features.

## **🏗️ Architecture Components**

### **Core Services**
- **Ledger Service**: Double-entry bookkeeping with PostgreSQL
- **Payments Service**: Transfer processing with gRPC
- **AML Worker**: Real-time risk monitoring
- **API Gateway**: YARP reverse proxy with routing

### **Infrastructure**
- **Azure Kubernetes Service (AKS)**: Container orchestration
- **Azure PostgreSQL Flexible Server**: Managed database
- **Azure Event Hubs**: Event streaming platform
- **Azure Key Vault**: Secrets management
- **Azure Managed HSM**: Hardware security modules

## **🔧 Prerequisites**

### **Development Environment**
```bash
# Required tools
dotnet --version          # .NET 8 SDK
docker --version          # Docker Desktop
kubectl version          # Kubernetes CLI
az --version             # Azure CLI
terraform --version      # Terraform
make --version           # Make utility
```

### **Azure Resources**
- Azure Subscription with appropriate permissions
- Resource Group for AtlasBank resources
- Service Principal for CI/CD automation
- Azure Container Registry (ACR)

## **🚀 Deployment Options**

### **Option 1: Azure Kubernetes Service (Recommended)**

#### **Step 1: Infrastructure Setup**
```bash
# Navigate to Terraform directory
cd infrastructure/iac/terraform/azure

# Initialize Terraform
terraform init

# Review the plan
terraform plan -var-file="environments/production.tfvars"

# Deploy infrastructure
terraform apply -var-file="environments/production.tfvars"
```

#### **Step 2: Build and Push Images**
```bash
# Login to Azure Container Registry
az acr login --name your-atlasbank-acr

# Build and push all images
make build-images
make push-images

# Verify images
az acr repository list --name your-atlasbank-acr
```

#### **Step 3: Deploy to AKS**
```bash
# Get AKS credentials
az aks get-credentials --resource-group atlasbank-rg --name atlasbank-aks

# Deploy applications
kubectl apply -f infrastructure/k8s/namespace.yaml
kubectl apply -f infrastructure/k8s/configmaps/
kubectl apply -f infrastructure/k8s/secrets/
kubectl apply -f infrastructure/k8s/deployments/
kubectl apply -f infrastructure/k8s/services/
kubectl apply -f infrastructure/k8s/ingress/

# Verify deployment
kubectl get pods -n atlasbank
kubectl get services -n atlasbank
```

### **Option 2: Docker Compose (Development/Testing)**

#### **Production Docker Compose**
```bash
# Start all services
docker-compose -f infrastructure/docker/docker-compose.prod.yml up -d

# Scale services for high availability
docker-compose up -d --scale ledgerapi=3 --scale paymentsapi=2 --scale amlworker=2

# Monitor services
docker-compose logs -f
```

### **Option 3: Manual Deployment**

#### **Step 1: Database Setup**
```bash
# Connect to PostgreSQL
psql -h your-postgres-server.postgres.database.azure.com -U atlasbank_admin -d atlasbank

# Run migrations
dotnet ef database update --project src/Services/Atlas.Ledger
dotnet ef database update --project src/Services/Atlas.Payments
```

#### **Step 2: Service Deployment**
```bash
# Deploy Ledger Service
cd src/Services/Atlas.Ledger
dotnet publish -c Release -o ./publish
docker build -t atlasbank/ledger:latest .
docker run -d -p 5181:5181 -p 7001:7001 --name ledger-service atlasbank/ledger:latest

# Deploy Payments Service
cd src/Services/Atlas.Payments
dotnet publish -c Release -o ./publish
docker build -t atlasbank/payments:latest .
docker run -d -p 5191:5191 --name payments-service atlasbank/payments:latest

# Deploy AML Worker
cd src/KycAml/Atlas.KycAml.Worker
dotnet publish -c Release -o ./publish
docker build -t atlasbank/aml-worker:latest .
docker run -d --name aml-worker atlasbank/aml-worker:latest
```

## **🔐 Security Configuration**

### **Azure Key Vault Setup**
```bash
# Create Key Vault
az keyvault create --name atlasbank-kv --resource-group atlasbank-rg --location eastus

# Add secrets
az keyvault secret set --vault-name atlasbank-kv --name "postgres-connection-string" --value "your-connection-string"
az keyvault secret set --vault-name atlasbank-kv --name "kafka-connection-string" --value "your-kafka-connection"
az keyvault secret set --vault-name atlasbank-kv --name "jwt-signing-key" --value "your-jwt-key"
```

### **Kubernetes Secrets**
```bash
# Create secrets from Key Vault
kubectl create secret generic atlasbank-secrets \
  --from-literal=postgres-connection="$(az keyvault secret show --vault-name atlasbank-kv --name postgres-connection-string --query value -o tsv)" \
  --from-literal=kafka-connection="$(az keyvault secret show --vault-name atlasbank-kv --name kafka-connection-string --query value -o tsv)" \
  --namespace atlasbank
```

### **Network Policies**
```bash
# Apply network policies for PCI compliance
kubectl apply -f infrastructure/policies/network/
kubectl apply -f infrastructure/policies/gatekeeper/
```

## **📊 Monitoring Setup**

### **Prometheus & Grafana**
```bash
# Deploy monitoring stack
kubectl apply -f infrastructure/monitoring/prometheus/
kubectl apply -f infrastructure/monitoring/grafana/

# Access Grafana
kubectl port-forward svc/grafana 3000:80 -n monitoring
# Open http://localhost:3000 (admin/admin)
```

### **Jaeger Tracing**
```bash
# Deploy Jaeger
kubectl apply -f infrastructure/monitoring/jaeger/

# Access Jaeger UI
kubectl port-forward svc/jaeger-query 16686:80 -n monitoring
# Open http://localhost:16686
```

### **ELK Stack**
```bash
# Deploy Elasticsearch, Logstash, Kibana
kubectl apply -f infrastructure/monitoring/elk/

# Access Kibana
kubectl port-forward svc/kibana 5601:80 -n monitoring
# Open http://localhost:5601
```

## **🔄 CI/CD Pipeline**

### **GitHub Actions Workflow**
```yaml
# .github/workflows/ci-cd.yml
name: CI/CD Pipeline

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      
      - name: Restore dependencies
        run: dotnet restore
      
      - name: Build
        run: dotnet build --no-restore
      
      - name: Test
        run: dotnet test --no-build --verbosity normal
      
      - name: Security scan
        run: dotnet list package --vulnerable

  deploy:
    needs: build-and-test
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    steps:
      - name: Deploy to AKS
        run: |
          az aks get-credentials --resource-group atlasbank-rg --name atlasbank-aks
          kubectl set image deployment/ledger-service ledger-service=your-acr.azurecr.io/ledger:${{ github.sha }}
          kubectl set image deployment/payments-service payments-service=your-acr.azurecr.io/payments:${{ github.sha }}
          kubectl rollout status deployment/ledger-service
          kubectl rollout status deployment/payments-service
```

### **Azure DevOps Pipeline**
```yaml
# azure-pipelines.yml
trigger:
- main

pool:
  vmImage: 'ubuntu-latest'

variables:
  buildConfiguration: 'Release'
  azureSubscription: 'your-service-connection'
  containerRegistry: 'your-acr.azurecr.io'
  imageRepository: 'atlasbank'

stages:
- stage: Build
  displayName: Build and test
  jobs:
  - job: Build
    displayName: Build
    steps:
    - task: DotNetCoreCLI@2
      displayName: Restore
      inputs:
        command: 'restore'
        projects: '**/*.csproj'
    
    - task: DotNetCoreCLI@2
      displayName: Build
      inputs:
        command: 'build'
        projects: '**/*.csproj'
        arguments: '--configuration $(buildConfiguration)'
    
    - task: DotNetCoreCLI@2
      displayName: Test
      inputs:
        command: 'test'
        projects: '**/*Tests/*.csproj'
        arguments: '--configuration $(buildConfiguration)'

- stage: Deploy
  displayName: Deploy to AKS
  dependsOn: Build
  condition: succeeded()
  jobs:
  - deployment: Deploy
    displayName: Deploy
    environment: 'production'
    strategy:
      runOnce:
        deploy:
          steps:
          - task: Kubernetes@1
            displayName: Deploy to AKS
            inputs:
              connectionType: 'Azure Resource Manager'
              azureSubscriptionEndpoint: '$(azureSubscription)'
              azureResourceGroup: 'atlasbank-rg'
              kubernetesCluster: 'atlasbank-aks'
              command: 'apply'
              useConfigurationFile: true
              configuration: 'infrastructure/k8s/'
```

## **🔍 Health Checks & Monitoring**

### **Service Health Endpoints**
```bash
# Check all service health
curl http://your-gateway-url/health
curl http://your-ledger-url/health
curl http://your-payments-url/health

# Detailed health checks
curl http://your-ledger-url/health/ready
curl http://your-ledger-url/health/live
```

### **Kubernetes Health Checks**
```yaml
# Example health check configuration
apiVersion: apps/v1
kind: Deployment
metadata:
  name: ledger-service
spec:
  template:
    spec:
      containers:
      - name: ledger-service
        image: atlasbank/ledger:latest
        ports:
        - containerPort: 5181
        livenessProbe:
          httpGet:
            path: /health/live
            port: 5181
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 5181
          initialDelaySeconds: 5
          periodSeconds: 5
```

## **📈 Scaling & Performance**

### **Horizontal Pod Autoscaling**
```bash
# Apply HPA configuration
kubectl apply -f infrastructure/k8s/hpa/

# Check HPA status
kubectl get hpa -n atlasbank
```

### **Database Scaling**
```bash
# Scale PostgreSQL (Azure CLI)
az postgres flexible-server update \
  --resource-group atlasbank-rg \
  --name atlasbank-postgres \
  --sku-name Standard_D4s_v3 \
  --tier GeneralPurpose
```

### **Load Testing**
```bash
# Run load tests
k6 run tests/load/payments-load-test.js
k6 run tests/load/ledger-load-test.js

# Monitor during load test
kubectl top pods -n atlasbank
```

## **🛠️ Troubleshooting**

### **Common Issues**

#### **Service Not Starting**
```bash
# Check pod logs
kubectl logs -f deployment/ledger-service -n atlasbank

# Check pod status
kubectl describe pod <pod-name> -n atlasbank

# Check service endpoints
kubectl get endpoints -n atlasbank
```

#### **Database Connection Issues**
```bash
# Test database connectivity
kubectl run postgres-client --rm -i --tty --image postgres:13 -- psql -h atlasbank-postgres.postgres.database.azure.com -U atlasbank_admin -d atlasbank

# Check connection string
kubectl get secret atlasbank-secrets -n atlasbank -o yaml
```

#### **Kafka/Event Hubs Issues**
```bash
# Check Kafka connectivity
kubectl run kafka-client --rm -i --tty --image confluentinc/cp-kafka:latest -- kafka-topics --bootstrap-server your-eventhubs-namespace.servicebus.windows.net:9093 --list

# Check consumer groups
kubectl logs deployment/aml-worker -n atlasbank
```

### **Performance Issues**

#### **High Memory Usage**
```bash
# Check memory usage
kubectl top pods -n atlasbank

# Adjust resource limits
kubectl edit deployment ledger-service -n atlasbank
```

#### **Slow Database Queries**
```bash
# Check PostgreSQL performance
kubectl exec -it deployment/postgres -n atlasbank -- psql -c "SELECT * FROM pg_stat_activity;"

# Analyze slow queries
kubectl exec -it deployment/postgres -n atlasbank -- psql -c "SELECT * FROM pg_stat_statements ORDER BY mean_time DESC LIMIT 10;"
```

## **🔒 Security Hardening**

### **Network Security**
```bash
# Apply network policies
kubectl apply -f infrastructure/policies/network/

# Check network policies
kubectl get networkpolicies -n atlasbank
```

### **Pod Security**
```bash
# Apply pod security policies
kubectl apply -f infrastructure/policies/pod-security/

# Check security contexts
kubectl get pods -n atlasbank -o jsonpath='{.items[*].spec.securityContext}'
```

### **RBAC Configuration**
```bash
# Apply RBAC
kubectl apply -f infrastructure/policies/rbac/

# Check permissions
kubectl auth can-i create pods --as=system:serviceaccount:atlasbank:ledger-service
```

## **📋 Deployment Checklist**

### **Pre-Deployment**
- [ ] Infrastructure provisioned (AKS, PostgreSQL, Event Hubs)
- [ ] Secrets configured in Key Vault
- [ ] Container images built and pushed to ACR
- [ ] Network policies applied
- [ ] RBAC configured

### **Deployment**
- [ ] Namespace created
- [ ] ConfigMaps applied
- [ ] Secrets created
- [ ] Deployments applied
- [ ] Services exposed
- [ ] Ingress configured

### **Post-Deployment**
- [ ] Health checks passing
- [ ] Monitoring configured
- [ ] Logging working
- [ ] Performance baseline established
- [ ] Security scans completed
- [ ] Documentation updated

## **📞 Support**

- **Documentation**: [docs/](docs/)
- **Issues**: [GitHub Issues](https://github.com/your-org/atlasbank/issues)
- **Emergency**: ops@your-org.com
- **Security**: security@your-org.com

---

**AtlasBank Deployment Guide** - *Production-ready fintech platform* 🚀
