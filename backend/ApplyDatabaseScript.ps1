param(
    [Parameter(Mandatory = $true)]
    [string]$ScriptPath
)

$ErrorActionPreference = 'Stop'
$backendDirectory = $PSScriptRoot
$apiDirectory = Join-Path $backendDirectory 'GoIsland.Api'
$rawConnectionString = [Environment]::GetEnvironmentVariable('ConnectionStrings__DefaultConnection')
if ([string]::IsNullOrWhiteSpace($rawConnectionString)) {
    $secretPrefix = 'ConnectionStrings:DefaultConnection = '
    $secretLine = & dotnet user-secrets list --project $apiDirectory 2>$null |
        Where-Object { $_.StartsWith($secretPrefix, [StringComparison]::Ordinal) } |
        Select-Object -First 1

    if (-not [string]::IsNullOrWhiteSpace($secretLine)) {
        $rawConnectionString = $secretLine.Substring($secretPrefix.Length)
    }
}

if ([string]::IsNullOrWhiteSpace($rawConnectionString)) {
    throw 'Configura ConnectionStrings__DefaultConnection o el user-secret ConnectionStrings:DefaultConnection.'
}

$npgsqlAssembly = Get-ChildItem -Path @(
    (Join-Path $apiDirectory 'bin'),
    'C:\tmp\GoIsland-test-build',
    'C:\tmp\GoIsland-build'
) -Filter 'Npgsql.dll' -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1

if ($null -eq $npgsqlAssembly) {
    throw 'No se encontro Npgsql.dll. Compila la solucion antes de aplicar el script.'
}

Add-Type -Path $npgsqlAssembly.FullName

$connectionString = $rawConnectionString
if ($rawConnectionString -match '^postgres(ql)?://') {
    $uri = [Uri]$rawConnectionString
    $userInfoSeparator = $uri.UserInfo.IndexOf(':')
    $username = if ($userInfoSeparator -ge 0) {
        $uri.UserInfo.Substring(0, $userInfoSeparator)
    } else {
        $uri.UserInfo
    }
    $password = if ($userInfoSeparator -ge 0) {
        $uri.UserInfo.Substring($userInfoSeparator + 1)
    } else {
        ''
    }

    $builder = [Npgsql.NpgsqlConnectionStringBuilder]::new()
    $builder.Host = $uri.Host
    $builder.Port = if ($uri.Port -gt 0) { $uri.Port } else { 5432 }
    $builder.Database = $uri.AbsolutePath.TrimStart('/')
    $builder.Username = [Uri]::UnescapeDataString($username)
    $builder.Password = [Uri]::UnescapeDataString($password)

    foreach ($part in $uri.Query.TrimStart('?').Split('&', [StringSplitOptions]::RemoveEmptyEntries)) {
        $pair = $part.Split('=', 2)
        if ($pair.Length -ne 2) {
            continue
        }

        $key = [Uri]::UnescapeDataString($pair[0]).ToLowerInvariant()
        $value = [Uri]::UnescapeDataString($pair[1])
        if ($key -eq 'sslmode') {
            $builder.SslMode = [Enum]::Parse([Npgsql.SslMode], $value, $true)
        } elseif ($key -eq 'channel_binding') {
            $builder.ChannelBinding = [Enum]::Parse([Npgsql.ChannelBinding], $value, $true)
        }
    }

    $connectionString = $builder.ConnectionString
}

$resolvedScriptPath = (Resolve-Path -LiteralPath $ScriptPath).Path
$sql = Get-Content -Raw -LiteralPath $resolvedScriptPath
$connection = [Npgsql.NpgsqlConnection]::new($connectionString)

try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandText = $sql
    $null = $command.ExecuteNonQuery()
    Write-Output "Applied database script: $([IO.Path]::GetFileName($resolvedScriptPath))"
} finally {
    $connection.Dispose()
}
