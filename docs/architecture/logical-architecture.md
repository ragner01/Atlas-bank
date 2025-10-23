graph TB
    subgraph "External Systems"
        CUSTOMER[Customer Mobile/Web]
        PARTNER[Partner APIs]
        REGULATOR[Regulatory Bodies]
    end

    subgraph "API Gateway Layer"
        GATEWAY[YARP API Gateway]
        WAF[Azure WAF v2]
        LB[Application Load Balancer]
    end

    subgraph "Authentication & Authorization"
        AUTH[Azure AD B2C]
        JWT[JWT Token Validation]
        RBAC[Role-Based Access Control]
    end

    subgraph "Core Services"
        LEDGER[Ledger Service]
        PAYMENTS[Payments Service]
        CARDS[Cards Service]
        KYC[KYC/AML Service]
        RISK[Risk Service]
        LOANS[Loans Service]
        FX[FX & Treasury Service]
        REPORTING[Reporting Service]
        IDENTITY[Identity Service]
    end

    subgraph "Backoffice"
        BACKOFFICE[Blazor Server Backoffice]
        ADMIN[Admin Portal]
    end

    subgraph "Data Layer"
        POSTGRES[(PostgreSQL<br/>Ledger & Core Data)]
        REDIS[(Redis<br/>Cache & Sessions)]
        CLICKHOUSE[(ClickHouse<br/>Analytics & Features)]
        OPENSEARCH[(OpenSearch<br/>Logs & Search)]
    end

    subgraph "Message Broker"
        KAFKA[Azure Event Hubs<br/>Kafka-Compatible]
    end

    subgraph "Observability"
        JAEGER[Jaeger Tracing]
        GRAFANA[Grafana Dashboards]
        PROMETHEUS[Prometheus Metrics]
        LOGS[Centralized Logging]
    end

    subgraph "Security & Compliance"
        KEYVAULT[Azure Key Vault]
        HSM[Managed HSM]
        BACKUP[Azure Backup]
        AUDIT[Audit Trail]
    end

    %% External connections
    CUSTOMER --> WAF
    PARTNER --> WAF
    REGULATOR --> BACKOFFICE

    %% Gateway connections
    WAF --> LB
    LB --> GATEWAY
    GATEWAY --> AUTH
    AUTH --> JWT
    JWT --> RBAC

    %% Service connections
    GATEWAY --> LEDGER
    GATEWAY --> PAYMENTS
    GATEWAY --> CARDS
    GATEWAY --> KYC
    GATEWAY --> RISK
    GATEWAY --> LOANS
    GATEWAY --> FX
    GATEWAY --> REPORTING
    GATEWAY --> IDENTITY

    %% Backoffice connections
    BACKOFFICE --> LEDGER
    BACKOFFICE --> PAYMENTS
    BACKOFFICE --> CARDS
    BACKOFFICE --> KYC
    BACKOFFICE --> RISK
    ADMIN --> BACKOFFICE

    %% Data connections
    LEDGER --> POSTGRES
    PAYMENTS --> POSTGRES
    CARDS --> POSTGRES
    KYC --> POSTGRES
    RISK --> POSTGRES
    LOANS --> POSTGRES
    FX --> POSTGRES
    REPORTING --> POSTGRES

    LEDGER --> REDIS
    PAYMENTS --> REDIS
    CARDS --> REDIS
    KYC --> REDIS
    RISK --> REDIS

    RISK --> CLICKHOUSE
    REPORTING --> CLICKHOUSE
    FX --> CLICKHOUSE

    %% Message broker connections
    LEDGER --> KAFKA
    PAYMENTS --> KAFKA
    CARDS --> KAFKA
    KYC --> KAFKA
    RISK --> KAFKA

    %% Observability connections
    LEDGER --> JAEGER
    PAYMENTS --> JAEGER
    CARDS --> JAEGER
    KYC --> JAEGER
    RISK --> JAEGER

    LEDGER --> PROMETHEUS
    PAYMENTS --> PROMETHEUS
    CARDS --> PROMETHEUS
    KYC --> PROMETHEUS
    RISK --> PROMETHEUS

    LEDGER --> LOGS
    PAYMENTS --> LOGS
    CARDS --> LOGS
    KYC --> LOGS
    RISK --> LOGS

    %% Security connections
    LEDGER --> KEYVAULT
    PAYMENTS --> KEYVAULT
    CARDS --> KEYVAULT
    KYC --> KEYVAULT
    RISK --> KEYVAULT

    CARDS --> HSM
    PAYMENTS --> HSM

    POSTGRES --> BACKUP
    KEYVAULT --> BACKUP

    %% Audit connections
    LEDGER --> AUDIT
    PAYMENTS --> AUDIT
    CARDS --> AUDIT
    KYC --> AUDIT
    RISK --> AUDIT

    classDef external fill:#e1f5fe
    classDef gateway fill:#f3e5f5
    classDef service fill:#e8f5e8
    classDef data fill:#fff3e0
    classDef security fill:#ffebee
    classDef observability fill:#f1f8e9

    class CUSTOMER,PARTNER,REGULATOR external
    class GATEWAY,WAF,LB,AUTH,JWT,RBAC gateway
    class LEDGER,PAYMENTS,CARDS,KYC,RISK,LOANS,FX,REPORTING,IDENTITY,BACKOFFICE,ADMIN service
    class POSTGRES,REDIS,CLICKHOUSE,OPENSEARCH,KAFKA data
    class KEYVAULT,HSM,BACKUP,AUDIT security
    class JAEGER,GRAFANA,PROMETHEUS,LOGS observability
