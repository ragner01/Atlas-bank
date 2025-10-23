# GitHub Secrets Configuration for AtlasBank CI/CD

## Required GitHub Secrets

The following secrets must be configured in the GitHub repository settings:

### Azure Authentication
- `AZURE_TENANT_ID`: Azure Active Directory tenant ID
- `AZURE_CLIENT_ID`: Service principal client ID
- `AZURE_CLIENT_SECRET`: Service principal client secret
- `AZURE_SUBSCRIPTION_ID`: Azure subscription ID

### Container Registry
- `ACR_LOGIN_SERVER`: Azure Container Registry login server (e.g., atlasbank.azurecr.io)
- `ACR_USERNAME`: Container registry username
- `ACR_PASSWORD`: Container registry password

### Kubernetes
- `KUBE_CONFIG`: Base64 encoded kubeconfig file
- `KUBE_NAMESPACE`: Target Kubernetes namespace (e.g., atlasbank-prod)

### Database
- `POSTGRES_CONNECTION_STRING`: PostgreSQL connection string for production
- `REDIS_CONNECTION_STRING`: Redis connection string for production

### Event Hubs
- `EVENTHUBS_CONNECTION_STRING`: Event Hubs connection string
- `EVENTHUBS_KAFKA_BOOTSTRAP_SERVERS`: Kafka bootstrap servers for Event Hubs

### Key Vault
- `KEYVAULT_URL`: Azure Key Vault URL
- `KEYVAULT_CLIENT_ID`: Key Vault access client ID
- `KEYVAULT_CLIENT_SECRET`: Key Vault access client secret

### Monitoring
- `JAEGER_ENDPOINT`: Jaeger tracing endpoint
- `PROMETHEUS_ENDPOINT`: Prometheus metrics endpoint
- `LOG_ANALYTICS_WORKSPACE_ID`: Log Analytics workspace ID

### Security
- `JWT_SECRET_KEY`: JWT signing secret key
- `ENCRYPTION_KEY`: Data encryption key
- `HSM_KEY_ID`: Hardware Security Module key ID

### External Services
- `SWIFT_API_KEY`: SWIFT API key for international transfers
- `CARD_NETWORK_API_KEY`: Card network API key
- `KYC_PROVIDER_API_KEY`: KYC provider API key
- `AML_PROVIDER_API_KEY`: AML provider API key

## Setting GitHub Secrets

1. Go to your GitHub repository
2. Navigate to Settings > Secrets and variables > Actions
3. Click "New repository secret"
4. Add each secret with the exact name and value

## Example Values (Development)

```bash
# Azure Authentication
AZURE_TENANT_ID=12345678-1234-1234-1234-123456789012
AZURE_CLIENT_ID=87654321-4321-4321-4321-210987654321
AZURE_CLIENT_SECRET=your-service-principal-secret
AZURE_SUBSCRIPTION_ID=11111111-2222-3333-4444-555555555555

# Container Registry
ACR_LOGIN_SERVER=atlasbank.azurecr.io
ACR_USERNAME=atlasbank
ACR_PASSWORD=your-acr-password

# Database
POSTGRES_CONNECTION_STRING=Host=atlas-postgres.postgres.database.azure.com;Port=5432;Database=atlas_bank;Username=atlas_admin;Password=your-secure-password;SslMode=Require
REDIS_CONNECTION_STRING=atlas-redis.redis.cache.windows.net:6380,password=your-redis-password,ssl=True

# Event Hubs
EVENTHUBS_CONNECTION_STRING=Endpoint=sb://atlas-eventhubs.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=your-key
EVENTHUBS_KAFKA_BOOTSTRAP_SERVERS=atlas-eventhubs.servicebus.windows.net:9093

# Monitoring
JAEGER_ENDPOINT=https://atlas-jaeger.azurewebsites.net/api/traces
PROMETHEUS_ENDPOINT=https://atlas-prometheus.azurewebsites.net
LOG_ANALYTICS_WORKSPACE_ID=12345678-1234-1234-1234-123456789012
```

## Security Notes

- Never commit secrets to the repository
- Use Azure Key Vault for production secrets
- Rotate secrets regularly
- Use least privilege principle for service principals
- Enable audit logging for secret access
