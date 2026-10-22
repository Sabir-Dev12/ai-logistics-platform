param(
    [ValidateSet('Debug', 'Release', 'AuditFixesValidation')]
    [string]$Configuration = 'Debug',
    [switch]$CheckOnly
)

function Get-LocalBackendPipe {
    param([Parameter(Mandatory = $true)][string]$InstanceInfo)
    $match = [regex]::Match($InstanceInfo, '\\\\\.\\pipe\\LOCALDB#[A-Za-z0-9]+\\tsql\\query')
    if (-not $match.Success) {
        throw 'No running private LocalDB pipe found. Run SqlLocalDB start MSSQLLocalDB in this Windows PowerShell window first.'
    }
    return $match.Value
}

function New-LocalBackendConnection {
    param(
        [Parameter(Mandatory = $true)][string]$ConnectionString,
        [Parameter(Mandatory = $true)][string]$Pipe
    )
    if ($Pipe -notmatch '^\\\\\.\\pipe\\LOCALDB#[A-Za-z0-9]+\\tsql\\query$') {
        throw 'Invalid private LocalDB pipe.'
    }
    $builder = New-Object System.Data.Common.DbConnectionStringBuilder
    $builder.set_ConnectionString($ConnectionString)
    $sourceKeys = @($builder.Keys | Where-Object { $_ -in @('Server', 'Data Source', 'Address', 'Addr', 'Network Address') })
    if ($sourceKeys.Count -ne 1 -or $builder.get_Item($sourceKeys[0]) -ine '(localdb)\MSSQLLocalDB') {
        throw 'This launcher only supports the existing MSSQLLocalDB instance.'
    }
    $authKeys = @($builder.Keys | Where-Object { $_ -in @('Integrated Security', 'Trusted_Connection') })
    if ($authKeys.Count -ne 1 -or [string]$builder.get_Item($authKeys[0]) -notin @('True', 'Yes', 'SSPI')) {
        throw 'Windows authentication must remain enabled.'
    }
    # Change only the transport. Preserve the database, encryption and every other option.
    $builder.set_Item($sourceKeys[0], ('np:' + $Pipe))
    return $builder.get_ConnectionString()
}

# Dot-sourcing loads the pure functions for tests without contacting SQL or starting the API.
if ($MyInvocation.InvocationName -eq '.') { return }

$ErrorActionPreference = 'Stop'
$apiDirectory = Join-Path $PSScriptRoot 'src\Logistics.Api'
$connectionString = $null
foreach ($name in @('appsettings.json', 'appsettings.Development.json')) {
    $settings = Get-Content -LiteralPath (Join-Path $apiDirectory $name) -Raw | ConvertFrom-Json
    if ($settings.ConnectionStrings.LogisticsDatabase) {
        $connectionString = [string]$settings.ConnectionStrings.LogisticsDatabase
    }
}
$previousConnection = [Environment]::GetEnvironmentVariable('ConnectionStrings__LogisticsDatabase', 'Process')
if (-not [string]::IsNullOrWhiteSpace($previousConnection)) { $connectionString = $previousConnection }
if (-not $connectionString) { throw 'ConnectionStrings:LogisticsDatabase is missing.' }

$localDb = Get-Command SqlLocalDB -ErrorAction Stop
$instanceInfo = (& $localDb.Source info MSSQLLocalDB 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0) { throw 'LocalDB info failed. Use a Windows PowerShell window under the database owner account.' }
$pipe = Get-LocalBackendPipe -InstanceInfo $instanceInfo
$runtimeConnection = New-LocalBackendConnection -ConnectionString $connectionString -Pipe $pipe
Write-Output 'Existing LocalDB private pipe resolved; database and Windows authentication preserved.'
if ($CheckOnly) { return }

$backendExitCode = 0
try {
    # Process-scoped override, inherited by dotnet only. No persistent settings or instance changes.
    [Environment]::SetEnvironmentVariable('ConnectionStrings__LogisticsDatabase', $runtimeConnection, 'Process')
    & dotnet run --project $apiDirectory --configuration $Configuration --launch-profile http 2>&1 |
        Tee-Object -FilePath (Join-Path $PSScriptRoot 'audit-local-backend.log')
    $backendExitCode = $LASTEXITCODE
}
finally {
    [Environment]::SetEnvironmentVariable('ConnectionStrings__LogisticsDatabase', $previousConnection, 'Process')
}
if ($backendExitCode -ne 0) { throw "Backend exited with code $backendExitCode. See audit-local-backend.log." }
