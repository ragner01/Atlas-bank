variable "prefix" { type = string }
variable "location" { type = string }
variable "rg_name" { type = string }

resource "azurerm_eventhub_namespace" "ns" {
  name                = "${var.prefix}-ehns"
  location            = var.location
  resource_group_name = var.rg_name
  sku                 = "Standard"
  capacity            = 2
  minimum_tls_version = "1.2"
  kafka_enabled       = true
}

resource "azurerm_eventhub" "ledger" {
  name                = "ledger-events"
  namespace_name      = azurerm_eventhub_namespace.ns.name
  resource_group_name = var.rg_name
  partition_count     = 2
  message_retention   = 3
}

resource "azurerm_eventhub_authorization_rule" "producer" {
  name                = "producer"
  namespace_name      = azurerm_eventhub_namespace.ns.name
  resource_group_name = var.rg_name
  listen              = false
  send                = true
  manage              = false
}

output "bootstrap_server" { value = "${azurerm_eventhub_namespace.ns.name}.servicebus.windows.net:9093" }
output "producer_connection" { value = azurerm_eventhub_authorization_rule.producer.primary_connection_string sensitive = true }
