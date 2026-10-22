$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path $PSScriptRoot) 'Start-LocalBackend.ps1')
$pipe = '\\.\pipe\LOCALDB#ABC12345\tsql\query'
$checks = 0
foreach ($info in @("Instance pipe name: np:$pipe", "Nom de canal de l'instance : $pipe")) {
    if ((Get-LocalBackendPipe $info) -cne $pipe) { throw 'Localized instance output was not parsed.' }
    $checks++
}
$connection = 'Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=LogisticsDb;Integrated Security=True;Encrypt=Optional;TrustServerCertificate=True;Application Name=Logistics.Api;Connect Timeout=15'
$result = New-LocalBackendConnection $connection $pipe
$builder = New-Object System.Data.Common.DbConnectionStringBuilder
$builder.set_ConnectionString($result)
foreach ($expected in @(
    @('Data Source', ('np:' + $pipe)),
    @('Initial Catalog', 'LogisticsDb'),
    @('Integrated Security', 'True'),
    @('Encrypt', 'Optional'),
    @('TrustServerCertificate', 'True'),
    @('Application Name', 'Logistics.Api'),
    @('Connect Timeout', '15')
)) {
    if ([string]$builder.get_Item($expected[0]) -cne $expected[1]) { throw "Connection option changed: $($expected[0])" }
    $checks++
}
foreach ($invalid in @(
    @{ Connection = $connection; Pipe = '\\remote\pipe\SQL' },
    @{ Connection = $connection.Replace('(localdb)\MSSQLLocalDB', 'another-server'); Pipe = $pipe },
    @{ Connection = $connection.Replace('Integrated Security=True', 'Integrated Security=False'); Pipe = $pipe }
)) {
    $rejected = $false
    try { $null = New-LocalBackendConnection $invalid.Connection $invalid.Pipe } catch { $rejected = $true }
    if (-not $rejected) { throw 'An invalid target or authentication mode was accepted.' }
    $checks++
}
$missingRejected = $false
try { $null = Get-LocalBackendPipe 'State: Stopped' } catch { $missingRejected = $true }
if (-not $missingRejected) { throw 'A stopped instance was accepted.' }
$checks++
Write-Output "$checks launcher checks passed; no SQL connection or backend process started."
