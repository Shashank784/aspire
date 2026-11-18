@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param sku string = 'Standard'

resource servicebus 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: take('servicebus-${uniqueString(resourceGroup().id)}', 50)
  location: location
  properties: {
    disableLocalAuth: true
    publicNetworkAccess: 'Enabled'
  }
  sku: {
    name: sku
  }
  tags: {
    'aspire-resource-name': 'servicebus'
  }
}

resource order_events 'Microsoft.ServiceBus/namespaces/topics@2024-01-01' = {
  name: 'order-events'
  parent: servicebus
}

resource basket_order_events 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = {
  name: 'basket'
  parent: order_events
}

resource notification_order_events 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = {
  name: 'notification'
  parent: order_events
}

resource product_events 'Microsoft.ServiceBus/namespaces/topics@2024-01-01' = {
  name: 'product-events'
  parent: servicebus
}

resource basket_product_events 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = {
  name: 'basket'
  parent: product_events
}

output serviceBusEndpoint string = servicebus.properties.serviceBusEndpoint

output serviceBusHostName string = split(replace(servicebus.properties.serviceBusEndpoint, 'https://', ''), ':')[0]

output name string = servicebus.name

output id string = servicebus.id