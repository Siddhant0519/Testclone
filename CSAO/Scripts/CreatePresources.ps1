Param
    (
        [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
        [string]$resourceGroup = "Pipeline-CICD-Test",
        [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
        [string]$location,
	[Parameter(Mandatory=$true, ValueFromPipeline=$true)]
        [string]$functionName,
	[Parameter(Mandatory=$true, ValueFromPipeline=$true)]
        [string]$storageName
     )

New-AzResourceGroup -Name $resourceGroup -Location $location

New-AzStorageAccount -ResourceGroupName $resourceGroup -Name $storageName -SkuName Standard_LRS -Location $location

New-AzFunctionApp -Name $functionName -ResourceGroupName $resourceGroup -StorageAccount $storageName -Runtime dotnet -FunctionsVersion 3 -Location $location