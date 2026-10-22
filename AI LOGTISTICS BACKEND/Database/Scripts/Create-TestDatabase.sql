-- ============================================================================================
--  03 — Creation de la base
-- ============================================================================================
--  A executer sur l'instance master, AVANT 01-schema.sql.
--
--      sqlcmd -S ".\SQLEXPRESS" -E -i 03-create-database.sql
--
--  Idempotent : relancer le script ne detruit rien.
-- ============================================================================================

SET NOCOUNT ON;

IF DB_ID(N'LogisticsDb') IS NULL
BEGIN
    PRINT 'Creation de LogisticsDb...';
    CREATE DATABASE [LogisticsDb];
END
ELSE
BEGIN
    PRINT 'LogisticsDb existe deja — rien a faire.';
END
GO

-- Mode de recuperation SIMPLE : c'est une base de test, et le journal complet ferait grossir le .bak
-- sans rien apporter.
IF DB_ID(N'LogisticsDb') IS NOT NULL
    ALTER DATABASE [LogisticsDb] SET RECOVERY SIMPLE;
GO

PRINT 'Base prete. Etape suivante :';
PRINT '  sqlcmd -S ".\SQLEXPRESS" -E -d LogisticsDb -i 01-schema.sql';
PRINT '  sqlcmd -S ".\SQLEXPRESS" -E -d LogisticsDb -i 02-seed.sql';
GO
