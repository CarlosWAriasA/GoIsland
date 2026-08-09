[CmdletBinding()]
param(
    [switch]$IncludeIntegration
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$frontendRoot = Join-Path $repositoryRoot 'frontend'
$backendTests = Join-Path $repositoryRoot 'backend\GoIsland.Api.Tests\GoIsland.Api.Tests.csproj'
$npmCommand = (Get-Command npm.cmd -ErrorAction Stop).Source

Push-Location $repositoryRoot
try {
    & dotnet build $backendTests --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'El build del backend falló.' }

    $testArguments = @('test', $backendTests, '--configuration', 'Release', '--no-build')
    if (-not $IncludeIntegration) {
        $testArguments += @('--filter', 'FullyQualifiedName!~Integration')
    }
    & dotnet @testArguments
    if ($LASTEXITCODE -ne 0) { throw 'Las pruebas del backend fallaron.' }

    Push-Location $frontendRoot
    try {
        & $npmCommand run lint
        if ($LASTEXITCODE -ne 0) { throw 'El lint del frontend falló.' }
        & $npmCommand test
        if ($LASTEXITCODE -ne 0) { throw 'Las pruebas del frontend fallaron.' }
        & $npmCommand run build
        if ($LASTEXITCODE -ne 0) { throw 'El build del frontend falló.' }
        & $npmCommand audit --audit-level=high
        if ($LASTEXITCODE -ne 0) { throw 'npm detectó vulnerabilidades altas o críticas.' }
    }
    finally {
        Pop-Location
    }

    Write-Host 'Verificación de release completada correctamente.' -ForegroundColor Green
}
finally {
    Pop-Location
}
