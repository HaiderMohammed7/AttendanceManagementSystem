using AttendanceManagementSystem.Services;

namespace AttendanceManagementSystem.Interfaces;

public interface IDatabaseBackupService
{
    Task<BackupResult> CreateBackupAsync(string outputPath, CancellationToken cancellationToken = default);

    Task<RestoreResult> RestoreAsync(string backupPath, string safetyBackupPath, CancellationToken cancellationToken = default);
}