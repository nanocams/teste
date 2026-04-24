#
# Global/Common variables
#-----------------------------------------------------------------
#
$UserInitialsLower              = 'ac'      # Aruna camará
$UserInitialsUpper              = ${UserInitialsLower}.ToUpper()
#
$ResourceGroupName              = "MicroServicesRG.${UserInitialsUpper}"
$ResourceLocation               = 'westeurope'      # West Europe
#
$LogAnalyticsWorkspaceName      = "${UserInitialsLower}-log-analytics"
$ContainerRegistryName          = "${UserInitialsLower}7registry"
$ContainerAppsEnvironmentName   = "${UserInitialsLower}-environment"
#
$ContainerAppNameTemplate       = "${UserInitialsLower}-capp-{0}"
$ContainerAppName_DataBase_Manager   = $ContainerAppNameTemplate -f 'database-manager'
$ContainerAppName_Data_Ingestion     = $ContainerAppNameTemplate -f 'data-ingestion'
$ContainerAppName_Internal_API       = $ContainerAppNameTemplate -f 'internal-api'
$ContainerAppName_Predictions       = $ContainerAppNameTemplate -f 'predictions'
$ContainerAppName_FrontEnd       = $ContainerAppNameTemplate -f 'front-end'
#
$KeyVaultName                   = 'KV-AIStockQuotes'
$SecretConnectionStringName     = 'fslab-connection-string'
$SecretConnectionStringId       = "https://${KeyVaultName}.vault.azure.net/secrets/${SecretConnectionStringName}"
$EnvVarConnectionStringName     = 'AZURE_SQL_CONNECTIONSTRING'


#
# Functions
#-----------------------------------------------------------------
#
function CreateLogAnalyticsWorkspace {
    #
    ''; '----------------------'
    "Creating the Log Analytics Workspace: [$LogAnalyticsWorkspaceName]"
    #-----------------------------------------------------------------
    #
    $script:newLogAnalyticsWorkspace = az monitor log-analytics workspace create `
        --workspace-name        $LogAnalyticsWorkspaceName `
        --resource-group        $ResourceGroupName `
        --location              $ResourceLocation `
        --sku                   'PerGB2018' `
    | ConvertFrom-Json
    #
    #return $script:newLogAnalyticsWorkspace
}
function GetLogAnalyticsWorkspaceKeys {

    "Retrieving the Log Analytics Workspace shared keys, from: [$LogAnalyticsWorkspaceName]"
    if( $script:newLogAnalyticsWorkspaceKeys )
    {
        '  ...value previoulsy retrieved.'
    }
    else
    {
        $script:newLogAnalyticsWorkspaceKeys = az monitor log-analytics workspace get-shared-keys `
            --workspace-name        $LogAnalyticsWorkspaceName `
            --resource-group        $ResourceGroupName `
        | ConvertFrom-Json
    }
    #
    #$script:newLogAnalyticsWorkspaceKeys
}
function DeleteLogAnalyticsWorkspace {
    #
    ''; '----------------------'
    "Deleting the Log Analytics Workspace: [$LogAnalyticsWorkspaceName]"
    #-----------------------------------------------------------------
    #
    az monitor log-analytics workspace delete `
        --workspace-name        $LogAnalyticsWorkspaceName `
        --resource-group        $ResourceGroupName `
        --force                 yes `
        --yes
}


function CreateContainerRegistry {
    #
    ''; '----------------------'
    "Creating the Container Registry: [$ContainerRegistryName]"
    #-----------------------------------------------------------------
    #
    $script:newContainerRegistry = az acr create `
        --name                  $ContainerRegistryName `
        --resource-group        $ResourceGroupName `
        --location              $ResourceLocation `
        --sku                   Basic `
        --admin-enabled         true `
        --dnl-scope             Unsecure `
        --workspace             $script:newLogAnalyticsWorkspace.id `
        --role-assignment-mode  'rbac' `
    | ConvertFrom-Json
    #
    #return $script:newContainerRegistry
}
function DeleteContainerRegistry {
    #
    ''; '----------------------'
    "Deleting the Container Registry: [$ContainerRegistryName]"
    #-----------------------------------------------------------------
    #
    az acr delete `
        --name                  $ContainerRegistryName `
        --resource-group        $ResourceGroupName `
        --yes
}


function CreateContainerAppsEnvironment {
    #
    GetLogAnalyticsWorkspaceKeys
    #
    ''; '----------------------'
    "Creating the Container Apps Environment: [$ContainerAppsEnvironmentName]"
    #-----------------------------------------------------------------
    #
    $script:newContainerAppsEnvironment = az containerapp env create `
        --name                  $ContainerAppsEnvironmentName `
        --resource-group        $ResourceGroupName `
        --location              $ResourceLocation `
        --logs-destination      'log-analytics' `
        --logs-workspace-id     $script:newLogAnalyticsWorkspace.customerId `
        --logs-workspace-key    $script:newLogAnalyticsWorkspaceKeys.primarySharedKey `
    | ConvertFrom-Json
    #
    #return $script:newContainerAppsEnvironment
}
function DeleteContainerAppsEnvironment {
    #
    ''; '----------------------'
    "Deleting the Container Apps Environment: [$ContainerAppsEnvironmentName]"
    #-----------------------------------------------------------------
    #
    az containerapp env delete `
        --name                  $ContainerAppsEnvironmentName `
        --resource-group        $ResourceGroupName `
        --yes
}

#-----*************************************************************************************------
function CreateContainerApp {
    param (
        [Parameter(Mandatory)]
        [string]
        $containerAppName,

        [string]
        $environmentVars
    )
    #
    ''; '----------------------'
    "Creating the Container App: [$containerAppName]"
    #-----------------------------------------------------------------
    #
    $newContainerApp = az containerapp create `
        --name                  $containerAppName `
        --resource-group        $ResourceGroupName `
        --environment           $script:newContainerAppsEnvironment.id `
        --registry-server       $script:newContainerRegistry.loginServer `
        --system-assigned `
        --ingress               external `
    | ConvertFrom-Json
    "  container app created $containerAppName."

    "  assign permission to read vault secrets, from [$KeyVaultName]..."
    $keyVaultId = az keyvault show `
        --name                  $KeyVaultName `
        --query                 'id' `
        --output                tsv
    #
	if (-not $newContainerApp.identity.principalId) {
        throw " Managed Identity not found for [$ContainerAppName]"
    }
    az role assignment create `
        --assignee              $newContainerApp.identity.principalId `
        --scope                 $keyVaultId `
        --role                  'Key Vault Secrets User' `
        --output                none
    #
    '    permission assigned, to principal ID'
    
    "  create secret, as a reference to key vault's [$SecretConnectionStringName]"
    az containerapp secret set `
        --ids                   $newContainerApp.id `
        --secrets               "${SecretConnectionStringName}=keyvaultref:${SecretConnectionStringId},identityref:system" `
        --output                none
    #
    '    secret created'

    "  create environment variable [$EnvVarConnectionStringName], from secret [$SecretConnectionStringName]"
    az containerapp update `
        --ids                   $newContainerApp.id `
        --set-env-vars          "${EnvVarConnectionStringName}=secretref:${SecretConnectionStringName}" `
        --output                none
    '    environment variable created'

    if( $environmentVars )
    {
        '  create aditional environment variables:'
        "    ${environmentVars}"
        az containerapp update `
            --ids                   $newContainerApp.id `
            --set-env-vars          $environmentVars `
            --output                none
        '    environment variables created'
    }
    #
    #return $newContainerApp
}
#
function CreateContainerAppSecret {
    param (

    )
}
function DeleteContainerApp {
    param (
        [Parameter(Mandatory)]
        [string]
        $containerAppName
    )
    #
    ''; '----------------------'
    "Deleting the Container App: [$containerAppName]"
    #-----------------------------------------------------------------
    #
    az containerapp delete `
        --name                  $containerAppName `
        --resource-group        $ResourceGroupName `
        --yes
}


function CreateAll {
    CreateLogAnalyticsWorkspace
    CreateContainerRegistry
    CreateContainerAppsEnvironment
    #
    $extraEnvironmentVars = "DATA_INGESTION_SERVICE_URL=http://${ContainerAppName_Data_Ingestion} PREDICTIONS_SERVICE_URL=http://${ContainerAppName_Predictions}"
    CreateContainerApp -containerAppName $ContainerAppName_Internal_API -environmentVars $extraEnvironmentVars
    #
    CreateContainerApp -containerAppName $ContainerAppName_DataBase_Manager
    #
	$extraEnvironmentVars = "DATABASE_MANAGER_SERVICE_URL=http://${ContainerAppName_DataBase_Manager}"
    CreateContainerApp -containerAppName $ContainerAppName_Data_Ingestion -environmentVars $extraEnvironmentVars
    # 
	$extraEnvironmentVars = "DATABASE_MANAGER_SERVICE_URL=http://${ContainerAppName_DataBase_Manager} INTERNALAPI_SERVICE_URL=http://${ContainerAppName_Internal_API}"
    CreateContainerApp -containerAppName $ContainerAppName_Predictions -environmentVars $extraEnvironmentVars
    #   
	$extraEnvironmentVars = "INTERNALAPI_SERVICE_URL=http://${ContainerAppName_Internal_API} DATA_INGESTION_SERVICE_URL=http://${ContainerAppName_Data_Ingestion}"
    CreateContainerApp -containerAppName $ContainerAppName_FrontEnd -environmentVars $extraEnvironmentVars
    #	
    ''; 'DONE'
}
function DeleteAll {
    DeleteContainerApp -containerAppName $ContainerAppName_Internal_API
    DeleteContainerApp -containerAppName $ContainerAppName_DataBase_Manager
	DeleteContainerApp -containerAppName $ContainerAppName_Data_Ingestion
    DeleteContainerApp -containerAppName $ContainerAppName_Predictions	
    DeleteContainerAppsEnvironment
    DeleteContainerRegistry
    DeleteLogAnalyticsWorkspace
    ''; 'DONE'
}


#
# Main execution
#-----------------------------------------------------------------
#

# DeleteContainerApp -containerAppName $ContainerAppName_Accounts
# DeleteContainerApp -containerAppName $ContainerAppName_Clients

CreateAll
# DeleteAll
