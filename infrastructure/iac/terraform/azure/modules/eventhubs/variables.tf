# Variables for Event Hubs module

variable "namespace_name" {
  description = "Name of the Event Hubs namespace"
  type        = string
}

variable "location" {
  description = "Azure region for resources"
  type        = string
}

variable "resource_group_name" {
  description = "Name of the resource group"
  type        = string
}

variable "sku" {
  description = "SKU for Event Hubs namespace"
  type        = string
  default     = "Standard"
}

variable "capacity" {
  description = "Capacity units for Event Hubs namespace"
  type        = number
  default     = 1
}

variable "partition_count" {
  description = "Number of partitions for event hubs"
  type        = number
  default     = 4
}

variable "message_retention" {
  description = "Message retention period in days"
  type        = number
  default     = 7
}

variable "subnet_id" {
  description = "ID of the subnet for private endpoint"
  type        = string
}

variable "key_vault_key_id" {
  description = "ID of the Key Vault key for encryption"
  type        = string
}

variable "archive_container_name" {
  description = "Name of the storage container for archiving"
  type        = string
}

variable "storage_account_id" {
  description = "ID of the storage account for archiving"
  type        = string
}

variable "log_analytics_workspace_id" {
  description = "ID of the Log Analytics workspace"
  type        = string
}

variable "tags" {
  description = "Tags to apply to resources"
  type        = map(string)
  default     = {}
}
