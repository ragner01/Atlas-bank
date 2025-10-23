# Outputs for Event Hubs module

output "namespace_name" {
  description = "Name of the Event Hubs namespace"
  value       = azurerm_eventhub_namespace.main.name
}

output "namespace_id" {
  description = "ID of the Event Hubs namespace"
  value       = azurerm_eventhub_namespace.main.id
}

output "namespace_connection_string" {
  description = "Connection string for the Event Hubs namespace"
  value       = azurerm_eventhub_namespace.main.default_primary_connection_string
  sensitive   = true
}

output "ledger_eventhub_name" {
  description = "Name of the ledger event hub"
  value       = azurerm_eventhub.ledger.name
}

output "payments_eventhub_name" {
  description = "Name of the payments event hub"
  value       = azurerm_eventhub.payments.name
}

output "producer_connection_string" {
  description = "Connection string for producers"
  value       = azurerm_eventhub_authorization_rule.producer.primary_connection_string
  sensitive   = true
}

output "consumer_connection_string" {
  description = "Connection string for consumers"
  value       = azurerm_eventhub_authorization_rule.consumer.primary_connection_string
  sensitive   = true
}

output "kafka_bootstrap_servers" {
  description = "Kafka bootstrap servers for Event Hubs"
  value       = "${azurerm_eventhub_namespace.main.name}.servicebus.windows.net:9093"
}

output "private_endpoint_id" {
  description = "ID of the private endpoint"
  value       = azurerm_private_endpoint.eventhubs.id
}
