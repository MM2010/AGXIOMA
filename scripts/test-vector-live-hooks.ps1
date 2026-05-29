param(
    [switch]$OnlyRocksDb,
    [switch]$OnlyManaged,
    [switch]$OnlyScaNN
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir

$allFilters = @(
    'FullyQualifiedName~MongoDbAtlasVectorStoreLiveTests',
    'FullyQualifiedName~CosmosDbVCoreStoreTests',
    'FullyQualifiedName~ScaNNStoreLiveTests',
    'FullyQualifiedName~RocksDbHnswStoreTests',
    'FullyQualifiedName~SqlServerVectorStoreTests'
)

$rocksDbFilters = @('FullyQualifiedName~RocksDbHnswStoreTests')
$managedFilters = @(
    'FullyQualifiedName~MongoDbAtlasVectorStoreLiveTests',
    'FullyQualifiedName~CosmosDbVCoreStoreTests',
    'FullyQualifiedName~SqlServerVectorStoreTests'
)
$scannFilters = @('FullyQualifiedName~ScaNNStoreLiveTests')

$filters = if ($OnlyRocksDb) {
    $rocksDbFilters
} elseif ($OnlyManaged) {
    $managedFilters
} elseif ($OnlyScaNN) {
    $scannFilters
} else {
    $allFilters
}

$filterArg = ($filters -join '|')

$requiredEnv = @{
    'AGXIOME_MONGODB_ATLAS_CONNECTION_STRING' = 'MongoDB Atlas live runtime';
    'AGXIOME_MONGODB_ATLAS_DATABASE' = 'MongoDB Atlas live runtime';
    'AGXIOME_MONGODB_ATLAS_COLLECTION' = 'MongoDB Atlas live runtime';
    'AGXIOME_COSMOS_VCORE_CONNECTION_STRING' = 'Cosmos DB vCore live runtime';
    'AGXIOME_COSMOS_VCORE_DATABASE' = 'Cosmos DB vCore live runtime';
    'AGXIOME_COSMOS_VCORE_COLLECTION' = 'Cosmos DB vCore live runtime';
    'AGXIOME_SQLSERVER_VECTOR_CONNECTION_STRING' = 'SQL Server VECTOR live runtime';
    'AGXIOME_SCANN_ENDPOINT' = 'ScaNN HTTP wrapper runtime';
}

Write-Host '[AGXIOME] Vector live-hook prerequisite snapshot:' -ForegroundColor Cyan
foreach ($entry in $requiredEnv.GetEnumerator() | Sort-Object Name) {
    $present = -not [string]::IsNullOrWhiteSpace((Get-Item -Path ("Env:" + $entry.Key) -ErrorAction SilentlyContinue).Value)
    $status = if ($present) { 'set' } else { 'missing' }
    Write-Host ("  {0,-42} {1}" -f $entry.Key, $status)
}

Push-Location $repoRoot
try {
    & (Join-Path $scriptDir 'test-agxiome-tests-isolated.ps1') -Filter $filterArg
}
finally {
    Pop-Location
}