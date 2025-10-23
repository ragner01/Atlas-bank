# 🚀 AtlasBank Quick Deployment Guide

## **One-Command Deployment**

I've created a comprehensive deployment script that will handle everything automatically. Here's how to deploy AtlasBank to Azure:

### **Step 1: Prerequisites Check**
```bash
# Install Azure CLI (if not installed)
# macOS:
brew install azure-cli

# Ubuntu/Debian:
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash

# Windows:
# Download from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli-windows
```

### **Step 2: Install Additional Tools**
```bash
# Install Terraform
# macOS:
brew install terraform

# Ubuntu/Debian:
wget https://releases.hashicorp.com/terraform/1.6.0/terraform_1.6.0_linux_amd64.zip
unzip terraform_1.6.0_linux_amd64.zip
sudo mv terraform /usr/local/bin/

# Install kubectl
# macOS:
brew install kubectl

# Ubuntu/Debian:
curl -LO "https://dl.k8s.io/release/$(curl -L -s https://dl.k8s.io/release/stable.txt)/bin/linux/amd64/kubectl"
sudo install -o root -g root -m 0755 kubectl /usr/local/bin/kubectl
```

### **Step 3: Run Deployment Script**
```bash
# Make script executable (already done)
chmod +x deploy-atlasbank.sh

# Run the deployment
./deploy-atlasbank.sh
```

## **What the Script Does**

The deployment script will automatically:

1. **✅ Check Prerequisites**: Verify all required tools are installed
2. **🔐 Azure Login**: Authenticate with Azure CLI
3. **🏗️ Create Resources**: Set up resource group and service principal
4. **☁️ Deploy Infrastructure**: Use Terraform to create all Azure resources
5. **🐳 Build Images**: Build and push Docker images to Azure Container Registry
6. **🚀 Deploy to Kubernetes**: Deploy all services to AKS
7. **🧪 Test Deployment**: Verify everything is working
8. **📊 Show Information**: Display deployment details and URLs

## **Expected Resources Created**

- **Resource Group**: `atlasbank-rg`
- **AKS Cluster**: `atlasbank-aks`
- **Container Registry**: `atlasbankacr`
- **PostgreSQL**: `atlasbank-postgres`
- **Event Hubs**: `atlasbank-ehns`
- **Key Vault**: `atlasbank-kv`
- **Load Balancer**: External IP for API Gateway

## **Deployment Time**

- **Infrastructure**: ~10-15 minutes
- **Docker Build**: ~5-10 minutes
- **Kubernetes Deploy**: ~5 minutes
- **Total**: ~20-30 minutes

## **Cost Estimation**

**Monthly Azure Costs (approximate):**
- AKS Cluster (2 nodes): ~$150-200
- PostgreSQL Flexible Server: ~$50-100
- Event Hubs: ~$20-50
- Container Registry: ~$10-20
- Load Balancer: ~$20-30
- **Total**: ~$250-400/month

## **After Deployment**

### **Get Service URLs**
```bash
# Get external IP
kubectl get service gateway-service -n atlasbank

# Test the API
curl http://<EXTERNAL_IP>/health
```

### **Monitor Services**
```bash
# Check pod status
kubectl get pods -n atlasbank

# View logs
kubectl logs -f deployment/ledger-service -n atlasbank
kubectl logs -f deployment/payments-service -n atlasbank
kubectl logs -f deployment/aml-worker -n atlasbank
```

### **Scale Services**
```bash
# Scale ledger service
kubectl scale deployment ledger-service --replicas=3 -n atlasbank

# Scale payments service
kubectl scale deployment payments-service --replicas=3 -n atlasbank
```

## **GitHub Actions Setup**

After deployment, configure GitHub Actions:

1. **Go to Repository Settings**: `https://github.com/ragner01/Atlas-bank/settings/secrets/actions`

2. **Add these secrets**:
   ```
   AZURE_TENANT_ID=<from service principal>
   AZURE_SUBSCRIPTION_ID=<your subscription ID>
   AZURE_CLIENT_ID=<from service principal>
   AZURE_CLIENT_SECRET=<from service principal>
   AZURE_RG=atlasbank-rg
   AZURE_AKS=atlasbank-aks
   ACR_LOGIN_SERVER=atlasbankacr.azurecr.io
   ```

3. **Enable GitHub Actions**: Go to Actions tab and enable workflows

## **Production Readiness Checklist**

- [x] **Infrastructure**: Terraform-managed Azure resources
- [x] **Security**: Service principals and RBAC
- [x] **Scalability**: Kubernetes with auto-scaling
- [x] **Monitoring**: Health checks and logging
- [x] **CI/CD**: GitHub Actions pipeline
- [x] **Documentation**: Complete deployment guides

## **Troubleshooting**

### **Common Issues**

**1. Azure CLI not logged in**
```bash
az login
```

**2. Terraform state locked**
```bash
cd infrastructure/iac/terraform/azure
terraform force-unlock <lock-id>
```

**3. Kubernetes pods not starting**
```bash
kubectl describe pod <pod-name> -n atlasbank
kubectl logs <pod-name> -n atlasbank
```

**4. External IP not assigned**
```bash
kubectl get service gateway-service -n atlasbank
# Wait 5-10 minutes for Azure to assign IP
```

### **Cleanup Commands**
```bash
# Delete Kubernetes resources
kubectl delete namespace atlasbank

# Delete Azure resources
az group delete --name atlasbank-rg --yes --no-wait
```

## **Next Steps After Deployment**

1. **Configure Monitoring**: Set up Azure Monitor and Application Insights
2. **Set Up Alerts**: Configure alerts for service health and performance
3. **Backup Strategy**: Implement database backup and disaster recovery
4. **Security Hardening**: Run security scans and penetration tests
5. **Performance Testing**: Load test the production environment
6. **Documentation**: Update operational runbooks

---

## **Ready to Deploy?**

Run this command to start the deployment:

```bash
./deploy-atlasbank.sh
```

The script will guide you through each step and provide status updates. Your AtlasBank platform will be live on Azure in about 20-30 minutes! 🚀
