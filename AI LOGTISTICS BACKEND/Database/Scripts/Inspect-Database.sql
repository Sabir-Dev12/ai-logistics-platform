/* Read-only inventory of the real database. Execute against the requested LocalDB instance.
   Set @Database to a distinct test-restore name if comparing a restored copy.
   Exact COUNT_BIG can be expensive on a large database; run during an appropriate window.
   No application rows or credentials are exported. */
USE [master];
SET NOCOUNT ON;
DECLARE @Database sysname = N'LogisticsDb';
IF ISNULL(CONVERT(int, SERVERPROPERTY('IsLocalDB')), 0) <> 1
    THROW 51200, 'Connect to the requested MSSQLLocalDB instance.', 1;
IF DB_ID(@Database) IS NULL
    THROW 51201, 'Database does not exist: a live schema/data inventory is unavailable.', 1;
SELECT @@SERVERNAME AS ServerName, ORIGINAL_LOGIN() AS WindowsLogin, name, state_desc, collation_name
FROM sys.databases WHERE name = @Database;
SELECT name, type_desc, physical_name, size * CONVERT(bigint, 8192) AS SizeBytes
FROM sys.master_files WHERE database_id = DB_ID(@Database);
DECLARE @Sql nvarchar(max) = N'USE ' + QUOTENAME(@Database) + N';
SELECT SCHEMA_NAME(schema_id) AS SchemaName,name,type_desc
FROM sys.objects WHERE is_ms_shipped=0 ORDER BY type_desc,name;
SELECT SCHEMA_NAME(t.schema_id) AS SchemaName,t.name AS TableName,c.name AS ColumnName,
       TYPE_NAME(c.user_type_id) AS DataType,c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity
FROM sys.tables t JOIN sys.columns c ON t.object_id=c.object_id
WHERE t.is_ms_shipped=0 ORDER BY SchemaName,TableName,c.column_id;
SELECT OBJECT_SCHEMA_NAME(i.object_id) AS SchemaName,OBJECT_NAME(i.object_id) AS TableName,
       i.name,i.type_desc,i.is_unique,i.is_primary_key,i.has_filter,i.filter_definition,
       c.name AS ColumnName,ic.key_ordinal,ic.is_included_column
FROM sys.indexes i JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id
JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
WHERE OBJECTPROPERTY(i.object_id,''IsUserTable'')=1 ORDER BY TableName,i.name,ic.index_column_id;
SELECT f.name AS ForeignKey,OBJECT_NAME(f.parent_object_id) AS ParentTable,
       COL_NAME(fc.parent_object_id,fc.parent_column_id) AS ParentColumn,
       OBJECT_NAME(f.referenced_object_id) AS ReferencedTable,
       COL_NAME(fc.referenced_object_id,fc.referenced_column_id) AS ReferencedColumn,
       f.delete_referential_action_desc,f.update_referential_action_desc,f.is_disabled,f.is_not_trusted
FROM sys.foreign_keys f JOIN sys.foreign_key_columns fc ON f.object_id=fc.constraint_object_id;
SELECT OBJECT_NAME(parent_object_id) AS TableName,name,type_desc
FROM sys.objects WHERE type IN (''PK'',''UQ'',''C'',''D'') AND is_ms_shipped=0;
SELECT name,parent_class_desc,OBJECT_NAME(parent_id) AS ParentObject,is_disabled FROM sys.triggers;
SELECT OBJECT_SCHEMA_NAME(referencing_id) AS SchemaName,OBJECT_NAME(referencing_id) AS ReferencingObject,
       referenced_server_name,referenced_database_name,referenced_schema_name,referenced_entity_name
FROM sys.sql_expression_dependencies
WHERE referenced_database_name IS NOT NULL OR referenced_server_name IS NOT NULL;
SELECT SCHEMA_NAME(schema_id) AS SchemaName,name,base_object_name FROM sys.synonyms;
SELECT o.name AS ObjectName,o.type_desc
FROM sys.objects o JOIN sys.sql_modules m ON m.object_id=o.object_id
WHERE m.definition IS NULL AND o.is_ms_shipped=0;
CREATE TABLE #RowCounts (SchemaName sysname,TableName sysname,RowCount bigint);
DECLARE @Schema sysname,@Table sysname,@CountSql nvarchar(max),@Count bigint;
DECLARE tables CURSOR LOCAL FAST_FORWARD FOR
    SELECT SCHEMA_NAME(schema_id),name FROM sys.tables WHERE is_ms_shipped=0 ORDER BY name;
OPEN tables;
FETCH NEXT FROM tables INTO @Schema,@Table;
WHILE @@FETCH_STATUS=0
BEGIN
    SET @CountSql=N''SELECT @Result=COUNT_BIG(*) FROM ''+QUOTENAME(@Schema)+N''.''+QUOTENAME(@Table)+N'';'';
    EXEC sys.sp_executesql @CountSql,N''@Result bigint OUTPUT'',@Result=@Count OUTPUT;
    INSERT #RowCounts VALUES (@Schema,@Table,@Count);
    FETCH NEXT FROM tables INTO @Schema,@Table;
END;
CLOSE tables;
DEALLOCATE tables;
SELECT SchemaName,TableName,RowCount FROM #RowCounts ORDER BY SchemaName,TableName;
SELECT SUM(RowCount) AS TotalRows FROM #RowCounts;
IF OBJECT_ID(N''dbo.__EFMigrationsHistory'',N''U'') IS NOT NULL
    SELECT MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
';
EXEC sys.sp_executesql @Sql;
SELECT name,product,provider,data_source FROM sys.servers WHERE is_linked = 1;
-- Static dependency metadata cannot discover every dynamic SQL/external integration dependency.
