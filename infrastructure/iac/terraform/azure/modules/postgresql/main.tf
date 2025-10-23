# Azure PostgreSQL Flexible Server Terraform Module
# This module creates PostgreSQL Flexible Server with PCI compliance features

terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~>3.0"
    }
  }
}

# PostgreSQL Flexible Server
resource "azurerm_postgresql_flexible_server" "main" {
  name                   = var.server_name
  resource_group_name    = var.resource_group_name
  location               = var.location
  version                = var.postgresql_version
  administrator_login    = var.administrator_login
  administrator_password = var.administrator_password
  
  # Storage configuration
  storage_mb = var.storage_mb
  sku_name   = var.sku_name
  
  # Backup configuration
  backup_retention_days        = var.backup_retention_days
  geo_redundant_backup_enabled = var.geo_redundant_backup_enabled
  
  # High availability
  high_availability {
    mode                      = var.ha_mode
    standby_availability_zone = var.standby_availability_zone
  }
  
  # Maintenance window
  maintenance_window {
    day_of_week  = var.maintenance_day_of_week
    start_hour   = var.maintenance_start_hour
    start_minute = var.maintenance_start_minute
  }
  
  # Network configuration
  delegated_subnet_id = var.subnet_id
  private_dns_zone_id = var.private_dns_zone_id
  
  # Security
  ssl_enforcement_enabled = true
  ssl_minimal_tls_version_enforced = "TLS1_2"
  
  tags = var.tags
}

# Database for each service
resource "azurerm_postgresql_flexible_server_database" "ledger" {
  name      = "atlas_ledger"
  server_id = azurerm_postgresql_flexible_server.main.id
  collation = "en_US.utf8"
  charset   = "utf8"
}

resource "azurerm_postgresql_flexible_server_database" "payments" {
  name      = "atlas_payments"
  server_id = azurerm_postgresql_flexible_server.main.id
  collation = "en_US.utf8"
  charset   = "utf8"
}

resource "azurerm_postgresql_flexible_server_database" "cards" {
  name      = "atlas_cards"
  server_id = azurerm_postgresql_flexible_server.main.id
  collation = "en_US.utf8"
  charset   = "utf8"
}

resource "azurerm_postgresql_flexible_server_database" "kyc" {
  name      = "atlas_kyc"
  server_id = azurerm_postgresql_flexible_server.main.id
  collation = "en_US.utf8"
  charset   = "utf8"
}

resource "azurerm_postgresql_flexible_server_database" "risk" {
  name      = "atlas_risk"
  server_id = azurerm_postgresql_flexible_server.main.id
  collation = "en_US.utf8"
  charset   = "utf8"
}

resource "azurerm_postgresql_flexible_server_database" "loans" {
  name      = "atlas_loans"
  server_id = azurerm_postgresql_flexible_server.main.id
  collation = "en_US.utf8"
  charset   = "utf8"
}

resource "azurerm_postgresql_flexible_server_database" "fx" {
  name      = "atlas_fx"
  server_id = azurerm_postgresql_flexible_server.main.id
  collation = "en_US.utf8"
  charset   = "utf8"
}

resource "azurerm_postgresql_flexible_server_database" "reporting" {
  name      = "atlas_reporting"
  server_id = azurerm_postgresql_flexible_server.main.id
  collation = "en_US.utf8"
  charset   = "utf8"
}

resource "azurerm_postgresql_flexible_server_database" "identity" {
  name      = "atlas_identity"
  server_id = azurerm_postgresql_flexible_server.main.id
  collation = "en_US.utf8"
  charset   = "utf8"
}

# Firewall rules
resource "azurerm_postgresql_flexible_server_firewall_rule" "aks" {
  name             = "aks-access"
  server_id        = azurerm_postgresql_flexible_server.main.id
  start_ip_address = var.aks_subnet_cidr
  end_ip_address   = var.aks_subnet_cidr
}

# Diagnostic settings
resource "azurerm_monitor_diagnostic_setting" "postgresql" {
  name                       = "${var.server_name}-diagnostics"
  target_resource_id         = azurerm_postgresql_flexible_server.main.id
  log_analytics_workspace_id = var.log_analytics_workspace_id

  enabled_log {
    category = "PostgreSQLLogs"
  }

  metric {
    category = "AllMetrics"
    enabled  = true
  }
}
