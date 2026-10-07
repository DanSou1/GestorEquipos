param([switch]$Restore)
$ErrorActionPreference = 'Stop'
$connection = New-Object System.Data.SqlClient.SqlConnection('Server=.\SQLEXPRESS;Database=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=30')
try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandTimeout = 60
    $command.CommandText = @'
IF CONVERT(nvarchar(128), SERVERPROPERTY('MachineName')) <> HOST_NAME()
    THROW 50000, 'Only the local SQL Server instance is supported.', 1;
IF CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')) <> N'SQLEXPRESS'
    THROW 50000, 'Only SQLEXPRESS is supported.', 1;
SELECT name,value_in_use FROM sys.configurations
WHERE name IN ('min server memory (MB)','max server memory (MB)','show advanced options');
'@
    $before = New-Object System.Data.DataTable
    $before.Load($command.ExecuteReader())
    $before | Format-Table -AutoSize | Out-String | Write-Output
    $advanced = if ($Restore) { 0 } else { [int]($before.Select("name = 'show advanced options'")[0].value_in_use) }
    $minimum = if ($Restore) { 16 } else { 128 }
    $maximum = if ($Restore) { 2147483647 } else { 1024 }
    $command.CommandText = "EXEC sp_configure 'show advanced options', 1; RECONFIGURE; EXEC sp_configure 'max server memory (MB)', $maximum; RECONFIGURE; EXEC sp_configure 'min server memory (MB)', $minimum; RECONFIGURE; EXEC sp_configure 'show advanced options', $advanced; RECONFIGURE;"
    $null = $command.ExecuteNonQuery()
    $command.CommandText = "SELECT name,value_in_use FROM sys.configurations WHERE name IN ('min server memory (MB)','max server memory (MB)');"
    $after = New-Object System.Data.DataTable
    $after.Load($command.ExecuteReader())
    $after | Format-Table -AutoSize | Out-String | Write-Output
} finally { $connection.Dispose() }
