# Azure Networking Terraform Module
# This module creates VNet, subnets, and network security groups for PCI compliance

terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~>3.0"
    }
  }
}

# Virtual Network
resource "azurerm_virtual_network" "main" {
  name                = var.vnet_name
  location            = var.location
  resource_group_name = var.resource_group_name
  address_space       = var.address_space

  tags = var.tags
}

# Subnet for AKS system nodes
resource "azurerm_subnet" "aks_system" {
  name                 = "aks-system"
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = var.aks_system_subnet_cidr
}

# Subnet for AKS application nodes
resource "azurerm_subnet" "aks_app" {
  name                 = "aks-app"
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = var.aks_app_subnet_cidr
}

# Subnet for databases
resource "azurerm_subnet" "database" {
  name                 = "database"
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = var.database_subnet_cidr
  
  # Enable service delegation for PostgreSQL
  delegation {
    name = "postgresql-delegation"
    service_delegation {
      name = "Microsoft.DBforPostgreSQL/flexibleServers"
      actions = [
        "Microsoft.Network/virtualNetworks/subnets/join/action",
      ]
    }
  }
}

# Subnet for Event Hubs
resource "azurerm_subnet" "eventhubs" {
  name                 = "eventhubs"
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = var.eventhubs_subnet_cidr
  
  # Enable service delegation for Event Hubs
  delegation {
    name = "eventhubs-delegation"
    service_delegation {
      name = "Microsoft.EventHub/namespaces"
      actions = [
        "Microsoft.Network/virtualNetworks/subnets/join/action",
      ]
    }
  }
}

# Subnet for Application Gateway
resource "azurerm_subnet" "appgw" {
  name                 = "appgw"
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = var.appgw_subnet_cidr
}

# Network Security Group for AKS system nodes
resource "azurerm_network_security_group" "aks_system" {
  name                = "aks-system-nsg"
  location            = var.location
  resource_group_name = var.resource_group_name

  # Allow inbound traffic from Application Gateway
  security_rule {
    name                       = "AllowAppGwInbound"
    priority                   = 100
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "80"
    source_address_prefix      = var.appgw_subnet_cidr[0]
    destination_address_prefix = "*"
  }

  security_rule {
    name                       = "AllowAppGwInboundHTTPS"
    priority                   = 110
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "443"
    source_address_prefix      = var.appgw_subnet_cidr[0]
    destination_address_prefix = "*"
  }

  # Allow outbound traffic to databases
  security_rule {
    name                       = "AllowDatabaseOutbound"
    priority                   = 100
    direction                  = "Outbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "5432"
    source_address_prefix      = "*"
    destination_address_prefix = var.database_subnet_cidr[0]
  }

  # Allow outbound traffic to Event Hubs
  security_rule {
    name                       = "AllowEventHubsOutbound"
    priority                   = 110
    direction                  = "Outbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "9093"
    source_address_prefix      = "*"
    destination_address_prefix = var.eventhubs_subnet_cidr[0]
  }

  tags = var.tags
}

# Network Security Group for databases
resource "azurerm_network_security_group" "database" {
  name                = "database-nsg"
  location            = var.location
  resource_group_name = var.resource_group_name

  # Allow inbound traffic from AKS
  security_rule {
    name                       = "AllowAKSInbound"
    priority                   = 100
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "5432"
    source_address_prefix      = var.aks_system_subnet_cidr[0]
    destination_address_prefix = "*"
  }

  security_rule {
    name                       = "AllowAKSAppInbound"
    priority                   = 110
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "5432"
    source_address_prefix      = var.aks_app_subnet_cidr[0]
    destination_address_prefix = "*"
  }

  tags = var.tags
}

# Associate NSGs with subnets
resource "azurerm_subnet_network_security_group_association" "aks_system" {
  subnet_id                 = azurerm_subnet.aks_system.id
  network_security_group_id = azurerm_network_security_group.aks_system.id
}

resource "azurerm_subnet_network_security_group_association" "database" {
  subnet_id                 = azurerm_subnet.database.id
  network_security_group_id = azurerm_network_security_group.database.id
}

# Private DNS Zone for PostgreSQL
resource "azurerm_private_dns_zone" "postgresql" {
  name                = "privatelink.postgres.database.azure.com"
  resource_group_name = var.resource_group_name
}

# Private DNS Zone for Event Hubs
resource "azurerm_private_dns_zone" "eventhubs" {
  name                = "privatelink.servicebus.windows.net"
  resource_group_name = var.resource_group_name
}

# Private DNS Zone for Key Vault
resource "azurerm_private_dns_zone" "keyvault" {
  name                = "privatelink.vaultcore.azure.net"
  resource_group_name = var.resource_group_name
}

# Link private DNS zones to VNet
resource "azurerm_private_dns_zone_virtual_network_link" "postgresql" {
  name                  = "postgresql-link"
  resource_group_name   = var.resource_group_name
  private_dns_zone_name = azurerm_private_dns_zone.postgresql.name
  virtual_network_id    = azurerm_virtual_network.main.id
}

resource "azurerm_private_dns_zone_virtual_network_link" "eventhubs" {
  name                  = "eventhubs-link"
  resource_group_name   = var.resource_group_name
  private_dns_zone_name = azurerm_private_dns_zone.eventhubs.name
  virtual_network_id    = azurerm_virtual_network.main.id
}

resource "azurerm_private_dns_zone_virtual_network_link" "keyvault" {
  name                  = "keyvault-link"
  resource_group_name   = var.resource_group_name
  private_dns_zone_name = azurerm_private_dns_zone.keyvault.name
  virtual_network_id    = azurerm_virtual_network.main.id
}
