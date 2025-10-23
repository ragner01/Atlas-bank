graph TB
    subgraph "Internet"
        INTERNET[Internet Traffic]
    end

    subgraph "Azure Front Door / CDN"
        AFD[Azure Front Door]
        CDN[Azure CDN]
    end

    subgraph "Public Subnet (10.0.2.0/24)"
        APP_GW[Application Gateway WAF v2]
        NAT_GW[NAT Gateway]
    end

    subgraph "Private Subnet (10.0.1.0/24)"
        subgraph "AKS Cluster"
            subgraph "CDE Zone - Cardholder Data Environment"
                CARDS_POD[Cards Service Pods]
                PAYMENTS_POD[Payments Service Pods]
                RISK_POD[Risk Service Pods]
                TOKEN_VAULT[Token Vault Integration]
            end
            
            subgraph "Connected-to-CDE Zone"
                LEDGER_POD[Ledger Service Pods]
                KYC_POD[KYC/AML Service Pods]
                FX_POD[FX Service Pods]
            end
            
            subgraph "Out-of-Scope Zone"
                BACKOFFICE_POD[Backoffice Pods]
                REPORTING_POD[Reporting Pods]
                IDENTITY_POD[Identity Service Pods]
            end
        end
        
        subgraph "Database Subnet (10.0.3.0/24)"
            POSTGRES_PRIMARY[PostgreSQL Primary]
            POSTGRES_REPLICA[PostgreSQL Replica]
            REDIS_CLUSTER[Redis Cluster]
        end
    end

    subgraph "Azure Services"
        EVENT_HUBS[Event Hubs Namespace]
        KEY_VAULT[Key Vault]
        MANAGED_HSM[Managed HSM]
        STORAGE[Storage Account]
        BACKUP_VAULT[Backup Vault]
        LOG_ANALYTICS[Log Analytics]
    end

    subgraph "Network Security Groups"
        NSG_PUBLIC[Public NSG<br/>- Allow HTTPS:443<br/>- Allow HTTP:80<br/>- Deny All Others]
        NSG_PRIVATE[Private NSG<br/>- Allow HTTPS:443<br/>- Allow HTTP:80<br/>- Allow PostgreSQL:5432<br/>- Deny All Others]
        NSG_DATABASE[Database NSG<br/>- Allow PostgreSQL:5432<br/>- Source: Private Subnet Only<br/>- Deny All Others]
    end

    subgraph "PCI DSS Controls"
        FIREWALL[Azure Firewall<br/>- Egress Control<br/>- Threat Intelligence]
        PRIVATE_ENDPOINTS[Private Endpoints<br/>- Key Vault<br/>- Event Hubs<br/>- Storage]
        NETWORK_POLICIES[Kubernetes Network Policies<br/>- Pod-to-Pod Communication<br/>- Egress Restrictions]
    end

    %% Traffic Flow
    INTERNET --> AFD
    AFD --> CDN
    CDN --> APP_GW
    APP_GW --> NAT_GW

    %% Service Communication
    NAT_GW --> LEDGER_POD
    NAT_GW --> PAYMENTS_POD
    NAT_GW --> CARDS_POD
    NAT_GW --> KYC_POD
    NAT_GW --> RISK_POD
    NAT_GW --> FX_POD
    NAT_GW --> BACKOFFICE_POD
    NAT_GW --> REPORTING_POD
    NAT_GW --> IDENTITY_POD

    %% CDE Zone Communication
    CARDS_POD --> TOKEN_VAULT
    PAYMENTS_POD --> TOKEN_VAULT
    RISK_POD --> TOKEN_VAULT
    
    CARDS_POD --> LEDGER_POD
    PAYMENTS_POD --> LEDGER_POD
    RISK_POD --> LEDGER_POD

    %% Connected-to-CDE Communication
    LEDGER_POD --> POSTGRES_PRIMARY
    KYC_POD --> POSTGRES_PRIMARY
    FX_POD --> POSTGRES_PRIMARY

    %% Database Replication
    POSTGRES_PRIMARY --> POSTGRES_REPLICA

    %% Cache Access
    LEDGER_POD --> REDIS_CLUSTER
    PAYMENTS_POD --> REDIS_CLUSTER
    CARDS_POD --> REDIS_CLUSTER

    %% Event Streaming
    LEDGER_POD --> EVENT_HUBS
    PAYMENTS_POD --> EVENT_HUBS
    CARDS_POD --> EVENT_HUBS
    KYC_POD --> EVENT_HUBS
    RISK_POD --> EVENT_HUBS

    %% Security Services
    CARDS_POD --> KEY_VAULT
    PAYMENTS_POD --> KEY_VAULT
    RISK_POD --> KEY_VAULT
    LEDGER_POD --> KEY_VAULT

    CARDS_POD --> MANAGED_HSM
    PAYMENTS_POD --> MANAGED_HSM

    %% Backup and Monitoring
    POSTGRES_PRIMARY --> BACKUP_VAULT
    KEY_VAULT --> BACKUP_VAULT
    MANAGED_HSM --> BACKUP_VAULT

    LEDGER_POD --> LOG_ANALYTICS
    PAYMENTS_POD --> LOG_ANALYTICS
    CARDS_POD --> LOG_ANALYTICS
    KYC_POD --> LOG_ANALYTICS
    RISK_POD --> LOG_ANALYTICS

    %% Network Security
    APP_GW -.-> NSG_PUBLIC
    LEDGER_POD -.-> NSG_PRIVATE
    PAYMENTS_POD -.-> NSG_PRIVATE
    CARDS_POD -.-> NSG_PRIVATE
    KYC_POD -.-> NSG_PRIVATE
    RISK_POD -.-> NSG_PRIVATE
    POSTGRES_PRIMARY -.-> NSG_DATABASE

    %% PCI Controls
    NAT_GW --> FIREWALL
    CARDS_POD --> PRIVATE_ENDPOINTS
    PAYMENTS_POD --> PRIVATE_ENDPOINTS
    RISK_POD --> PRIVATE_ENDPOINTS

    %% Network Policies
    CARDS_POD -.-> NETWORK_POLICIES
    PAYMENTS_POD -.-> NETWORK_POLICIES
    RISK_POD -.-> NETWORK_POLICIES

    classDef internet fill:#e3f2fd
    classDef public fill:#fff3e0
    classDef private fill:#e8f5e8
    classDef cde fill:#ffebee
    classDef connected fill:#fff8e1
    classDef outofscope fill:#f3e5f5
    classDef database fill:#e0f2f1
    classDef azure fill:#e1f5fe
    classDef security fill:#fce4ec
    classDef pci fill:#ffcdd2

    class INTERNET internet
    class AFD,CDN,APP_GW,NAT_GW public
    class LEDGER_POD,KYC_POD,FX_POD connected
    class CARDS_POD,PAYMENTS_POD,RISK_POD,TOKEN_VAULT cde
    class BACKOFFICE_POD,REPORTING_POD,IDENTITY_POD outofscope
    class POSTGRES_PRIMARY,POSTGRES_REPLICA,REDIS_CLUSTER database
    class EVENT_HUBS,KEY_VAULT,MANAGED_HSM,STORAGE,BACKUP_VAULT,LOG_ANALYTICS azure
    class NSG_PUBLIC,NSG_PRIVATE,NSG_DATABASE security
    class FIREWALL,PRIVATE_ENDPOINTS,NETWORK_POLICIES pci
