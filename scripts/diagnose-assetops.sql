-- Read-only health snapshot. Run with an account allowed to read server DMVs.
-- No SQL text, user credentials or application records are returned.
USE [master];
GO
SET NOCOUNT ON;
SELECT SYSDATETIMEOFFSET() AS CapturedAt;
SELECT name, state_desc, is_auto_close_on
FROM sys.databases WHERE name = N'AssetOps';
SELECT total_physical_memory_kb, available_physical_memory_kb,
       available_page_file_kb, system_memory_state_desc
FROM sys.dm_os_sys_memory;
SELECT physical_memory_in_use_kb, available_commit_limit_kb,
       memory_utilization_percentage, page_fault_count,
       process_physical_memory_low, process_virtual_memory_low
FROM sys.dm_os_process_memory;
SELECT name, value_in_use FROM sys.configurations
WHERE name IN ('min server memory (MB)', 'max server memory (MB)', 'user connections', 'max worker threads');
SELECT COUNT(*) AS UserSessions FROM sys.dm_exec_sessions WHERE is_user_process = 1;
SELECT session_id, status, command, wait_type, wait_time, blocking_session_id
FROM sys.dm_exec_requests
WHERE session_id <> @@SPID AND session_id > 50 AND status <> 'background';
-- Cumulative since SQL started; compare two captures to identify new waits.
SELECT wait_type, waiting_tasks_count, wait_time_ms
FROM sys.dm_os_wait_stats
WHERE wait_type IN ('THREADPOOL', 'RESOURCE_SEMAPHORE', 'RESOURCE_SEMAPHORE_QUERY_COMPILE',
                    'MEMORY_ALLOCATION_EXT', 'PAGEIOLATCH_SH', 'PAGEIOLATCH_EX', 'LCK_M_SCH_M');
