/* Connect to (localdb)\MSSQLLocalDB with Windows authentication.
   The path below is the planned backup location. No backup was found/created during the initial audit.
   1. Set @BackupFile to a genuine full SQL Server backup, and select its @BackupSet.
   2. Run with @ExecuteRestore = 0; review HEADERONLY, FILELISTONLY and VERIFYONLY.
   3. Confirm the source database and expected data, then set @ExecuteRestore = 1.
   If LogisticsDb already exists, STOP: back it up and restore the incoming backup under a distinct
   @TargetDatabase (e.g. LogisticsDb_RestoreTest_20261012). Review both before a separately approved cutover.
   There is no REPLACE, DROP, SINGLE_USER, or forced disconnection in this script. */
USE [master];
SET NOCOUNT ON;
DECLARE @BackupFile nvarchar(4000) = N'C:\Users\User\Desktop\Project legacy\AI LOGISTICS PROJECT\AI LOGTISTICS BACKEND\Database\Backups\LogisticsDb_2026-10-12.bak';
DECLARE @BackupSet int = 1;
DECLARE @TargetDatabase sysname = N'LogisticsDb';
DECLARE @ExecuteRestore bit = 0;
DECLARE @DataDirectory nvarchar(260) = CONVERT(nvarchar(260), SERVERPROPERTY('InstanceDefaultDataPath'));
DECLARE @LogDirectory nvarchar(260) = CONVERT(nvarchar(260), SERVERPROPERTY('InstanceDefaultLogPath'));

IF ISNULL(CONVERT(int, SERVERPROPERTY('IsLocalDB')), 0) <> 1
    THROW 51100, 'Connect to the requested MSSQLLocalDB instance.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.dm_os_file_exists(@BackupFile) WHERE file_exists = 1)
    THROW 51101, 'The SQL engine cannot read the backup file. Supply a real .bak and check engine access.', 1;
IF @BackupSet < 1 OR @TargetDatabase IS NULL OR LEN(@TargetDatabase) = 0
    THROW 51102, 'Invalid backup set or target database name.', 1;
IF @TargetDatabase LIKE N'%[^a-zA-Z0-9_]%' COLLATE Latin1_General_100_BIN2
    THROW 51103, 'Use letters, numbers and underscores in the target name to ensure safe file paths.', 1;
IF DB_ID(@TargetDatabase) IS NOT NULL
    THROW 51104, 'Target already exists: preserve it and choose a distinct name for a reviewed test restore.', 1;
IF @DataDirectory IS NULL OR @LogDirectory IS NULL
    THROW 51105, 'Set valid LocalDB data and log directories.', 1;
IF RIGHT(@DataDirectory, 1) <> N'\' SET @DataDirectory += N'\';
IF RIGHT(@LogDirectory, 1) <> N'\' SET @LogDirectory += N'\';
IF NOT EXISTS (SELECT 1 FROM sys.dm_os_file_exists(@DataDirectory) WHERE file_is_a_directory = 1)
    OR NOT EXISTS (SELECT 1 FROM sys.dm_os_file_exists(@LogDirectory) WHERE file_is_a_directory = 1)
    THROW 51106, 'The SQL engine cannot access the destination directories.', 1;

RESTORE HEADERONLY FROM DISK = @BackupFile WITH FILE = @BackupSet;
CREATE TABLE #BackupFiles
(
    LogicalName nvarchar(128), PhysicalName nvarchar(260), Type char(1), FileGroupName nvarchar(128),
    Size numeric(20,0), MaxSize numeric(20,0), FileID bigint, CreateLSN numeric(25,0), DropLSN numeric(25,0),
    UniqueID uniqueidentifier, ReadOnlyLSN numeric(25,0), ReadWriteLSN numeric(25,0), BackupSizeInBytes bigint,
    SourceBlockSize int, FileGroupID int, LogGroupGUID uniqueidentifier, DifferentialBaseLSN numeric(25,0),
    DifferentialBaseGUID uniqueidentifier, IsReadOnly bit, IsPresent bit, TDEThumbprint varbinary(32),
    SnapshotURL nvarchar(360)
);
DECLARE @FileListCommand nvarchar(max) = N'RESTORE FILELISTONLY FROM DISK = N'''
    + REPLACE(@BackupFile, N'''', N'''''') + N''' WITH FILE = '
    + CAST(@BackupSet AS nvarchar(12)) + N';';
INSERT INTO #BackupFiles EXEC sys.sp_executesql @FileListCommand;
SELECT LogicalName, Type, FileID, PhysicalName, Size FROM #BackupFiles ORDER BY FileID;
IF EXISTS (SELECT 1 FROM #BackupFiles WHERE Type NOT IN ('D', 'L') OR IsPresent = 0)
    THROW 51107, 'Partial or FILESTREAM/full-text/container backup: obtain a reviewed engine-specific restore procedure.', 1;
IF NOT EXISTS (SELECT 1 FROM #BackupFiles WHERE Type = 'D')
    OR NOT EXISTS (SELECT 1 FROM #BackupFiles WHERE Type = 'L')
    THROW 51108, 'Expected data and log files are missing.', 1;

DECLARE @Restore nvarchar(max) = N'RESTORE DATABASE ' + QUOTENAME(@TargetDatabase)
    + N' FROM DISK = N''' + REPLACE(@BackupFile, N'''', N'''''')
    + N''' WITH FILE = ' + CAST(@BackupSet AS nvarchar(12)) + N', RECOVERY, CHECKSUM, STOP_ON_ERROR';
DECLARE @LogicalName nvarchar(128), @Type char(1), @FileID bigint, @Destination nvarchar(4000);
DECLARE files CURSOR LOCAL FAST_FORWARD FOR SELECT LogicalName, Type, FileID FROM #BackupFiles ORDER BY FileID;
OPEN files;
FETCH NEXT FROM files INTO @LogicalName, @Type, @FileID;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Destination = CASE WHEN @Type = 'L' THEN @LogDirectory ELSE @DataDirectory END
        + @TargetDatabase + N'_' + CAST(@FileID AS nvarchar(20))
        + CASE WHEN @Type = 'L' THEN N'.ldf' WHEN @FileID = 1 THEN N'.mdf' ELSE N'.ndf' END;
    IF LEN(@Destination) > 260 OR EXISTS (SELECT 1 FROM sys.dm_os_file_exists(@Destination) WHERE file_exists = 1 OR file_is_a_directory = 1)
        THROW 51109, 'Destination file exists or path is too long. No existing data file will be overwritten.', 1;
    SET @Restore += N', MOVE N''' + REPLACE(@LogicalName, N'''', N'''''')
        + N''' TO N''' + REPLACE(@Destination, N'''', N'''''') + N'''';
    FETCH NEXT FROM files INTO @LogicalName, @Type, @FileID;
END;
CLOSE files;
DEALLOCATE files;
RESTORE VERIFYONLY FROM DISK = @BackupFile WITH FILE = @BackupSet, CHECKSUM, STOP_ON_ERROR;
SELECT @Restore AS ReviewedRestoreCommand, @ExecuteRestore AS ExecuteRestore;
IF @ExecuteRestore = 0
BEGIN
    PRINT 'Preview only. Review the backup database name, full-backup type, logical files and command before setting ExecuteRestore=1.';
    RETURN;
END;
EXEC sys.sp_executesql @Restore;
IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = @TargetDatabase AND state_desc = N'ONLINE')
    THROW 51110, 'Restored database is not ONLINE.', 1;
SELECT name, state_desc FROM sys.databases WHERE name = @TargetDatabase;
SELECT name, type_desc, physical_name FROM sys.master_files WHERE database_id = DB_ID(@TargetDatabase);
DECLARE @Verify nvarchar(max) = N'USE ' + QUOTENAME(@TargetDatabase)
    + N'; DBCC CHECKDB (' + QUOTENAME(@TargetDatabase) + N') WITH NO_INFOMSGS;'
    + N' SELECT s.name AS SchemaName,t.name AS TableName,SUM(p.rows) AS MetadataRowCount'
    + N' FROM sys.tables t JOIN sys.schemas s ON t.schema_id=s.schema_id'
    + N' JOIN sys.partitions p ON t.object_id=p.object_id AND p.index_id IN (0,1)'
    + N' WHERE t.is_ms_shipped=0 GROUP BY s.name,t.name ORDER BY s.name,t.name;';
EXEC sys.sp_executesql @Verify;
PRINT 'Run Inspect-Database.sql against the restored database and compare counts/objects with the source audit.';
