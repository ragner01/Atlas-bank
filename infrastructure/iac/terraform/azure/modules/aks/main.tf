# Azure AKS Terraform Module
# This module creates AKS cluster with PCI compliance features

terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~>3.0"
    }
  }
}

# AKS Cluster
resource "azurerm_kubernetes_cluster" "main" {
  name                = var.cluster_name
  location            = var.location
  resource_group_name = var.resource_group_name
  dns_prefix          = var.dns_prefix
  kubernetes_version  = var.kubernetes_version

  # Node pool configuration
  default_node_pool {
    name                = "system"
    node_count          = var.node_count
    vm_size             = var.vm_size
    vnet_subnet_id      = var.subnet_id
    enable_auto_scaling = true
    min_count          = var.min_count
    max_count          = var.max_count
    
    # Security settings
    only_critical_addons_enabled = true
  }

  # Identity
  identity {
    type = "SystemAssigned"
  }

  # Network profile
  network_profile {
    network_plugin    = "azure"
    network_policy    = "azure"
    service_cidr      = var.service_cidr
    dns_service_ip    = var.dns_service_ip
  }

  # Security features
  role_based_access_control_enabled = true
  
  azure_active_directory_role_based_access_control {
    managed                = true
    admin_group_object_ids = var.admin_group_object_ids
  }

  # Monitoring
  oms_agent {
    log_analytics_workspace_id = var.log_analytics_workspace_id
  }

  # Add-ons
  addon_profile {
    aci_connector_linux {
      enabled = false
    }
    
    azure_policy {
      enabled = true
    }
    
    http_application_routing {
      enabled = false
    }
    
    kube_dashboard {
      enabled = false
    }
    
    oms_agent {
      enabled                    = true
      log_analytics_workspace_id = var.log_analytics_workspace_id
    }
  }

  tags = var.tags
}

# Additional node pool for applications
resource "azurerm_kubernetes_cluster_node_pool" "app" {
  name                  = "app"
  kubernetes_cluster_id = azurerm_kubernetes_cluster.main.id
  vm_size              = var.app_vm_size
  node_count           = var.app_node_count
  vnet_subnet_id       = var.app_subnet_id
  enable_auto_scaling  = true
  min_count           = var.app_min_count
  max_count           = var.app_max_count
  
  # Security settings
  node_taints = ["workload=app:NoSchedule"]
  
  tags = var.tags
}

# Diagnostic settings
resource "azurerm_monitor_diagnostic_setting" "aks" {
  name                       = "${var.cluster_name}-diagnostics"
  target_resource_id         = azurerm_kubernetes_cluster.main.id
  log_analytics_workspace_id = var.log_analytics_workspace_id

  enabled_log {
    category = "kube-apiserver"
  }

  enabled_log {
    category = "kube-controller-manager"
  }

  enabled_log {
    category = "kube-scheduler"
  }

  enabled_log {
    category = "kube-audit"
  }

  enabled_log {
    category = "cluster-autoscaler"
  }

  metric {
    category = "AllMetrics"
    enabled  = true
  }
}
