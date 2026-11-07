using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using AttendanceManagementSystem.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AttendanceManagementSystem.Services;

public sealed class SqlServerDatabaseBackupService : IDatabaseBackupService
{
    private const string DatabaseName = "AttendanceManagementSystem";
    private static readonly Regex IdentifierPattern = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly string _connectionString;
    private readonly ILogger<SqlServerDatabaseBackupService> _logger;

    public SqlServerDatabaseBackupService(IConfiguration configuration, ILogger<SqlServerDatabaseBackupService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("The database connection string is not configured.");
        _logger = logger;
    }

    public async Task<BackupResult> CreateBackupAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        var path = ValidateNewBackupPath(outputPath);
        await BackupDatabaseAsync(DatabaseName, path, cancellationToken);
        return new BackupResult(path);
    }

    public async Task<RestoreResult> RestoreAsync(string backupPath, string safetyBackupPath, CancellationToken cancellationToken = default)
    {
        var sourcePath = ValidateExistingBackupPath(backupPath);
        var safetyPath = ValidateNewBackupPath(safetyBackupPath);
        var temporaryDatabase = $"{DatabaseName}_Restore_{Guid.NewGuid():N}";
        var previousDatabase = $"{DatabaseName}_PreRestore_{DateTime.UtcNow:yyyyMMddHHmmss}";
        var temporaryCreated = false;
        var switched = false;

        try
        {
            await BackupDatabaseAsync(DatabaseName, safetyPath, cancellationToken);
            await ValidateBackupAsync(sourcePath, cancellationToken);
            await RestoreTemporaryDatabaseAsync(sourcePath, temporaryDatabase, cancellationToken);
            temporaryCreated = true;
            await VerifyTemporaryDatabaseAsync(temporaryDatabase, cancellationToken);
            await SwitchDatabasesAsync(temporaryDatabase, previousDatabase, cancellationToken);
            await VerifyTemporaryDatabaseAsync(DatabaseName, cancellationToken);
            switched = true;

            return new RestoreResult(true, safetyPath, $"The previous database is preserved as {previousDatabase} until it is no longer needed.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Database restore failed. Temporary database: {TemporaryDatabase}", temporaryDatabase);
            if (temporaryCreated && !switched)
            {
                await TryRecoverDatabaseSwitchAsync(temporaryDatabase, previousDatabase);
                await TryDropDatabaseAsync(temporaryDatabase);
            }

            if (exception is OperationCanceledException)
            {
                throw;
            }

            throw new InvalidOperationException("The database restore could not be completed. The current database was preserved. Verify SQL Server permissions and that the SQL Server service account can access the selected folders.", exception);
        }
    }

    private async Task BackupDatabaseAsync(string databaseName, string outputPath, CancellationToken cancellationToken)
    {
        await using var connection = await OpenMasterConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 0;
        command.CommandText = $"BACKUP DATABASE {QuoteIdentifier(databaseName)} TO DISK = @path WITH INIT, FORMAT, CHECKSUM, STATS = 10;";
        command.Parameters.Add(new SqlParameter("@path", SqlDbType.NVarChar, 4000) { Value = outputPath });
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task ValidateBackupAsync(string backupPath, CancellationToken cancellationToken)
    {
        await using var connection = await OpenMasterConnectionAsync(cancellationToken);
        await using (var header = connection.CreateCommand())
        {
            header.CommandTimeout = 0;
            header.CommandText = "RESTORE HEADERONLY FROM DISK = @path;";
            header.Parameters.Add(new SqlParameter("@path", SqlDbType.NVarChar, 4000) { Value = backupPath });
            await using var reader = await header.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("The selected file does not contain a readable SQL Server backup.");
            }

            var databaseName = reader["DatabaseName"]?.ToString();
            if (!string.Equals(databaseName, DatabaseName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The selected backup does not belong to the Attendance Management System database.");
            }
        }

        await using var verify = connection.CreateCommand();
        verify.CommandTimeout = 0;
        verify.CommandText = "RESTORE VERIFYONLY FROM DISK = @path;";
        verify.Parameters.Add(new SqlParameter("@path", SqlDbType.NVarChar, 4000) { Value = backupPath });
        await verify.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task RestoreTemporaryDatabaseAsync(string backupPath, string temporaryDatabase, CancellationToken cancellationToken)
    {
        await using var connection = await OpenMasterConnectionAsync(cancellationToken);
        var files = new List<BackupFile>();
        await using (var fileList = connection.CreateCommand())
        {
            fileList.CommandTimeout = 0;
            fileList.CommandText = "RESTORE FILELISTONLY FROM DISK = @path;";
            fileList.Parameters.Add(new SqlParameter("@path", SqlDbType.NVarChar, 4000) { Value = backupPath });
            await using var reader = await fileList.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                files.Add(new BackupFile(reader["LogicalName"].ToString()!, Convert.ToString(reader["Type"])!));
            }
        }

        if (files.Count == 0 || files.Any(file => file.Type is not ("D" or "L")))
        {
            throw new InvalidOperationException("The selected backup does not contain valid database files.");
        }

        var dataDirectory = await GetDefaultDirectoryAsync(connection, "InstanceDefaultDataPath", cancellationToken);
        var logDirectory = await GetDefaultDirectoryAsync(connection, "InstanceDefaultLogPath", cancellationToken);
        var moves = files.Select((file, index) =>
        {
            var directory = file.Type == "L" ? logDirectory : dataDirectory;
            var extension = file.Type == "L" ? ".ldf" : index == 0 ? ".mdf" : $"_{index}.ndf";
            var physicalPath = Path.Combine(directory, $"{temporaryDatabase}{extension}");
            return $"MOVE {QuoteLiteral(file.LogicalName)} TO {QuoteLiteral(physicalPath)}";
        });

        await using var restore = connection.CreateCommand();
        restore.CommandTimeout = 0;
        restore.CommandText = $"RESTORE DATABASE {QuoteIdentifier(temporaryDatabase)} FROM DISK = @path WITH {string.Join(", ", moves)}, RECOVERY, STATS = 10;";
        restore.Parameters.Add(new SqlParameter("@path", SqlDbType.NVarChar, 4000) { Value = backupPath });
        await restore.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task VerifyTemporaryDatabaseAsync(string temporaryDatabase, CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(_connectionString)
        {
            InitialCatalog = temporaryDatabase,
            ConnectTimeout = 15
        };
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CASE WHEN DB_NAME() = @database AND OBJECT_ID(N'dbo.Courses', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Students', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.LectureSessions', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.AttendanceRecords', N'U') IS NOT NULL THEN 1 ELSE 0 END;";
        command.Parameters.Add(new SqlParameter("@database", SqlDbType.NVarChar, 128) { Value = temporaryDatabase });
        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) != 1)
        {
            throw new InvalidOperationException("The restored database is missing one or more required application tables.");
        }

        await using var query = connection.CreateCommand();
        query.CommandText = "SELECT TOP (0) Id FROM dbo.Courses; SELECT TOP (0) Id FROM dbo.Students; SELECT TOP (0) Id FROM dbo.LectureSessions; SELECT TOP (0) Id FROM dbo.AttendanceRecords;";
        await query.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task TryRecoverDatabaseSwitchAsync(string temporaryDatabase, string previousDatabase)
    {
        try
        {
            await using var connection = await OpenMasterConnectionAsync(CancellationToken.None);
            await using var state = connection.CreateCommand();
            state.CommandText = "SELECT DB_ID(@production), DB_ID(@temporary), DB_ID(@previous);";
            state.Parameters.Add(new SqlParameter("@production", SqlDbType.NVarChar, 128) { Value = DatabaseName });
            state.Parameters.Add(new SqlParameter("@temporary", SqlDbType.NVarChar, 128) { Value = temporaryDatabase });
            state.Parameters.Add(new SqlParameter("@previous", SqlDbType.NVarChar, 128) { Value = previousDatabase });
            await using var reader = await state.ExecuteReaderAsync(CommandBehavior.SingleRow);
            await reader.ReadAsync();
            var productionExists = !reader.IsDBNull(0);
            var temporaryExists = !reader.IsDBNull(1);
            var previousExists = !reader.IsDBNull(2);
            await reader.DisposeAsync();

            if (!productionExists && previousExists)
            {
                await using var restoreName = connection.CreateCommand();
                restoreName.CommandText = $"ALTER DATABASE {QuoteIdentifier(previousDatabase)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; ALTER DATABASE {QuoteIdentifier(previousDatabase)} MODIFY NAME = {QuoteIdentifier(DatabaseName)}; ALTER DATABASE {QuoteIdentifier(DatabaseName)} SET MULTI_USER;";
                await restoreName.ExecuteNonQueryAsync();
                _logger.LogWarning("Recovered the original database name after an incomplete restore switch.");
            }
            else if (productionExists && previousExists)
            {
                _logger.LogWarning("The restore switch completed before verification failed. The original database remains preserved as {PreviousDatabase}.", previousDatabase);
            }

            _ = temporaryExists;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not recover the database names after a failed restore switch.");
        }
    }

    private async Task SwitchDatabasesAsync(string temporaryDatabase, string previousDatabase, CancellationToken cancellationToken)
    {
        await using var connection = await OpenMasterConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 0;
        command.CommandText = $"ALTER DATABASE {QuoteIdentifier(DatabaseName)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
            $"ALTER DATABASE {QuoteIdentifier(DatabaseName)} MODIFY NAME = {QuoteIdentifier(previousDatabase)}; " +
            $"ALTER DATABASE {QuoteIdentifier(temporaryDatabase)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
            $"ALTER DATABASE {QuoteIdentifier(temporaryDatabase)} MODIFY NAME = {QuoteIdentifier(DatabaseName)}; " +
            $"ALTER DATABASE {QuoteIdentifier(DatabaseName)} SET MULTI_USER; ";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqlConnection> OpenMasterConnectionAsync(CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = "master" };
        var connection = new SqlConnection(builder.ConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static async Task<string> GetDefaultDirectoryAsync(SqlConnection connection, string propertyName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT CONVERT(nvarchar(4000), SERVERPROPERTY(N'{propertyName}'));";
        var value = (await command.ExecuteScalarAsync(cancellationToken))?.ToString();
        if (string.IsNullOrWhiteSpace(value) || !Directory.Exists(value))
        {
            throw new InvalidOperationException($"SQL Server did not provide a valid {propertyName}.");
        }

        return value;
    }

    private async Task TryDropDatabaseAsync(string databaseName)
    {
        try
        {
            await using var connection = await OpenMasterConnectionAsync(CancellationToken.None);
            await using var command = connection.CreateCommand();
            command.CommandText = $"IF DB_ID({QuoteLiteral(databaseName)}) IS NOT NULL BEGIN ALTER DATABASE {QuoteIdentifier(databaseName)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {QuoteIdentifier(databaseName)}; END;";
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not remove temporary restore database {DatabaseName}.", databaseName);
        }
    }

    private static string ValidateExistingBackupPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath) || !string.Equals(Path.GetExtension(fullPath), ".bak", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The selected backup file does not exist or is not a .bak file.");
        }

        return fullPath;
    }

    private static string ValidateNewBackupPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!string.Equals(Path.GetExtension(fullPath), ".bak", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Backup files must use the .bak extension.");
        }

        if (File.Exists(fullPath))
        {
            throw new InvalidOperationException("The selected backup file already exists.");
        }

        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            throw new InvalidOperationException("The selected backup directory does not exist.");
        }

        return fullPath;
    }

    private static string QuoteIdentifier(string value)
    {
        if (!IdentifierPattern.IsMatch(value))
        {
            throw new InvalidOperationException("An unsafe SQL identifier was generated.");
        }

        return $"[{value}]";
    }

    private static string QuoteLiteral(string value) => $"N'{value.Replace("'", "''", StringComparison.Ordinal)}'";

    private sealed record BackupFile(string LogicalName, string Type);
}