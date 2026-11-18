targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment that can be used as part of naming resource convention, the name of the resource group for your application will use this name, prefixed with rg-')
param environmentName string

@minLength(1)
@description('The location used for all deployed resources')
param location string

@description('Id of the user or app to assign application roles')
param principalId string = ''

@secure()
param jwt_signing_key string
@metadata({azd: {
  type: 'generate'
  config: {length:22}
  }
})
@secure()
param postgres_password string
@metadata({azd: {
  type: 'generate'
  config: {length:10,noNumeric:true,noSpecial:true}
  }
})
param postgres_username string
@secure()
param sendgrid_api_key string
param sendgrid_from_email string

var tags = {
  'azd-env-name': environmentName
}

resource rg 'Microsoft.Resources/resourceGroups@2022-09-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}
module resources 'resources.bicep' = {
  scope: rg
  name: 'resources'
  params: {
    location: location
    tags: tags
    principalId: principalId
  }
}

module cache 'cache/cache.module.bicep' = {
  name: 'cache'
  scope: rg
  params: {
    cache_kv_outputs_name: cache_kv.outputs.name
    location: location
  }
}
module cache_kv 'cache-kv/cache-kv.module.bicep' = {
  name: 'cache-kv'
  scope: rg
  params: {
    location: location
  }
}
module cache_kv_roles 'cache-kv-roles/cache-kv-roles.module.bicep' = {
  name: 'cache-kv-roles'
  scope: rg
  params: {
    cache_kv_outputs_name: cache_kv.outputs.name
    location: location
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalType: 'ServicePrincipal'
  }
}
module postgres 'postgres/postgres.module.bicep' = {
  name: 'postgres'
  scope: rg
  params: {
    administratorLogin: postgres_username
    administratorLoginPassword: postgres_password
    location: location
    postgres_kv_outputs_name: postgres_kv.outputs.name
  }
}
module postgres_kv 'postgres-kv/postgres-kv.module.bicep' = {
  name: 'postgres-kv'
  scope: rg
  params: {
    location: location
  }
}
module postgres_kv_roles 'postgres-kv-roles/postgres-kv-roles.module.bicep' = {
  name: 'postgres-kv-roles'
  scope: rg
  params: {
    location: location
    postgres_kv_outputs_name: postgres_kv.outputs.name
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalType: 'ServicePrincipal'
  }
}
module servicebus 'servicebus/servicebus.module.bicep' = {
  name: 'servicebus'
  scope: rg
  params: {
    location: location
  }
}
module servicebus_roles 'servicebus-roles/servicebus-roles.module.bicep' = {
  name: 'servicebus-roles'
  scope: rg
  params: {
    location: location
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalType: 'ServicePrincipal'
    servicebus_outputs_name: servicebus.outputs.name
  }
}
module storage 'storage/storage.module.bicep' = {
  name: 'storage'
  scope: rg
  params: {
    location: location
  }
}
module storage_roles 'storage-roles/storage-roles.module.bicep' = {
  name: 'storage-roles'
  scope: rg
  params: {
    location: location
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalType: 'ServicePrincipal'
    storage_outputs_name: storage.outputs.name
  }
}

output MANAGED_IDENTITY_CLIENT_ID string = resources.outputs.MANAGED_IDENTITY_CLIENT_ID
output MANAGED_IDENTITY_NAME string = resources.outputs.MANAGED_IDENTITY_NAME
output AZURE_LOG_ANALYTICS_WORKSPACE_NAME string = resources.outputs.AZURE_LOG_ANALYTICS_WORKSPACE_NAME
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = resources.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT
output AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID string = resources.outputs.AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID
output AZURE_CONTAINER_REGISTRY_NAME string = resources.outputs.AZURE_CONTAINER_REGISTRY_NAME
output AZURE_CONTAINER_APPS_ENVIRONMENT_NAME string = resources.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_NAME
output AZURE_CONTAINER_APPS_ENVIRONMENT_ID string = resources.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_ID
output AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN string = resources.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN
output CACHE_KV_VAULTURI string = cache_kv.outputs.vaultUri
output CACHE_HOSTNAME string = cache.outputs.hostName
output POSTGRES_KV_VAULTURI string = postgres_kv.outputs.vaultUri
output POSTGRES_HOSTNAME string = postgres.outputs.hostName
output SERVICEBUS_SERVICEBUSENDPOINT string = servicebus.outputs.serviceBusEndpoint
output SERVICEBUS_SERVICEBUSHOSTNAME string = servicebus.outputs.serviceBusHostName
output STORAGE_BLOBENDPOINT string = storage.outputs.blobEndpoint
