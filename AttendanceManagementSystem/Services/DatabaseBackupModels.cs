namespace AttendanceManagementSystem.Services;

public sealed record BackupResult(string BackupPath);

public sealed record RestoreResult(bool Succeeded, string SafetyBackupPath, string? Message = null);