$ErrorActionPreference = 'Stop'
$connection = New-Object System.Data.SqlClient.SqlConnection('Server=.\SQLEXPRESS;Database=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=30')
try {
    $connection.Open()
    foreach ($query in @(
        'SELECT total_physical_memory_kb, available_physical_memory_kb, system_memory_state_desc FROM sys.dm_os_sys_memory',
        'SELECT physical_memory_in_use_kb, locked_page_allocations_kb, memory_utilization_percentage, process_physical_memory_low, process_virtual_memory_low FROM sys.dm_os_process_memory',
        "SELECT wait_type, waiting_tasks_count, wait_time_ms FROM sys.dm_os_wait_stats WHERE wait_type IN ('RESOURCE_SEMAPHORE','RESOURCE_SEMAPHORE_QUERY_COMPILE','THREADPOOL','PAGEIOLATCH_SH','MEMORY_ALLOCATION_EXT')",
        "SELECT counter_name,cntr_value FROM sys.dm_os_performance_counters WHERE counter_name IN ('Total Server Memory (KB)','Target Server Memory (KB)','Memory Grants Pending')",
        "SELECT name FROM sys.databases WHERE database_id>4"
    )) {
        $command = $connection.CreateCommand()
        $command.CommandTimeout = 30
        $command.CommandText = $query
        try {
            $table = New-Object System.Data.DataTable
            $table.Load($command.ExecuteReader())
            $table | Format-Table -AutoSize | Out-String | Write-Output
        } catch { Write-Output $_.Exception.Message }
    }
} finally { $connection.Dispose() }
