-- Ejecuta este script en tu SQL Server local (SQLEXPRESS) para crear un login
-- dedicado que el contenedor Docker usará para conectarse, en vez de "sa".
-- También sirve contra el contenedor "db" de docker-compose.prod.yml (SQL Server
-- recién creado, sin la base AssetOps todavía) — por eso crea la base si falta.
--
-- La contraseña NO va escrita en este archivo: pásala como variable de sqlcmd
-- al invocarlo, por ejemplo:
--   sqlcmd -S DESKTOP-XXX\SQLEXPRESS -E -v Password="UnaClaveFuerte#2026" -i create-docker-sql-login.sql
-- o, contra el contenedor "db" (autenticación sa, no Windows -E):
--   sqlcmd -C -S localhost -U sa -P "$SA_PASSWORD" -v Password="UnaClaveFuerte#2026" -i create-docker-sql-login.sql
--
-- CHECK_POLICY se deja en ON: si la contraseña elegida no cumple la política
-- de complejidad de Windows, SQL Server rechazará la creación del login —
-- es la señal para elegir una contraseña más fuerte, no para apagar la política.

USE master;
GO

CREATE LOGIN gestorequipos_docker WITH PASSWORD = '$(Password)', CHECK_POLICY = ON;
GO

IF DB_ID('AssetOps') IS NULL
    CREATE DATABASE AssetOps;
GO

USE AssetOps;
GO

CREATE USER gestorequipos_docker FOR LOGIN gestorequipos_docker;
GO

-- db_datareader/db_datawriter para uso normal de la app, db_ddladmin porque
-- AuthBootstrapper.EnsureAdminAsync aplica migraciones EF Core automáticamente
-- al arrancar (ver CLAUDE.md, sección Configuration).
ALTER ROLE db_datareader ADD MEMBER gestorequipos_docker;
ALTER ROLE db_datawriter ADD MEMBER gestorequipos_docker;
ALTER ROLE db_ddladmin ADD MEMBER gestorequipos_docker;
GO
