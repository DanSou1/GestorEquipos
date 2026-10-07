-- Run once against the local SQLEXPRESS instance hosting AssetOps.
-- Operational database setting, deliberately not an EF schema migration.
-- No data changes and no forced disconnection of active users.
USE [master];
GO
IF EXISTS (SELECT 1 FROM sys.databases WHERE name = N'AssetOps' AND is_auto_close_on = 1)
    ALTER DATABASE [AssetOps] SET AUTO_CLOSE OFF;
GO
SELECT name, state_desc, is_auto_close_on
FROM sys.databases
WHERE name = N'AssetOps';
