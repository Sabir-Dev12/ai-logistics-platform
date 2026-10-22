/* Run in SSMS against (localdb)\MSSQLLocalDB using Windows authentication.
   This is a prepared procedure, not proof that a backup already exists.
   Run Inspect-Database.sql first and review dependencies and data counts.
   Never creates a source database, overwrites a backup, or changes data. */
USE [master];
SET NOCOUNT ON;
DECLARE @Database sysname = N'LogisticsDb';
DECLARE @BackupFile nvarchar(4000) = N'C:\Users\User\Desktop\Project legacy\AI LOGISTICS PROJECT\AI LOGTISTICS BACKEND\Database\Backups\LogisticsDb_2026-10-12.bak';
DECLARE @BackupDirectory nvarchar(4000) = N'C:\Users\User\Desktop\Project legacy\AI LOGISTICS PROJECT\AI LOGTISTICS BACKEND\Database\Backups';

IF ISNULL(CONVERT(int, SERVERPROPERTY('IsLocalDB')), 0) <> 1
    THROW 51000, 'Connect to the requested MSSQLLocalDB instance.', 1;
IF DB_ID(@Database) IS NULL
    THROW 51001, 'Source LogisticsDb does not exist. Supply the original database/backup; no backup was created.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = @Database AND state_desc = N'ONLINE')
    THROW 51002, 'The source database must be ONLINE.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.dm_os_file_exists(@BackupDirectory) WHERE file_is_a_directory = 1)
    THROW 51003, 'The SQL engine cannot see the backup directory. Check its Windows account and permissions.', 1;
IF EXISTS (SELECT 1 FROM sys.dm_os_file_exists(@BackupFile) WHERE file_exists = 1)
    THROW 51004, 'The backup file already exists. Choose a new filename; it will not be overwritten.', 1;

SELECT ORIGINAL_LOGIN() AS WindowsLogin, @Database AS SourceDatabase, @BackupFile AS BackupFile;
-- COPY_ONLY preserves any existing differential backup chain. CHECKSUM aborts on checksum errors.
-- The BACKUP command is also the definitive test of the engine's write permission.
BACKUP DATABASE @Database TO DISK = @BackupFile
    WITH COPY_ONLY, CHECKSUM, NOINIT, STATS = 10;
RESTORE VERIFYONLY FROM DISK = @BackupFile WITH FILE = 1, CHECKSUM, STOP_ON_ERROR;
RESTORE HEADERONLY FROM DISK = @BackupFile WITH FILE = 1;
RESTORE FILELISTONLY FROM DISK = @BackupFile WITH FILE = 1;
