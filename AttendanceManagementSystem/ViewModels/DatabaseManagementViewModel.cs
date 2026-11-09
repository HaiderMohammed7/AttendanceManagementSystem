using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AttendanceManagementSystem.Interfaces;

namespace AttendanceManagementSystem.ViewModels;

public sealed class DatabaseManagementViewModel : INotifyPropertyChanged
{
    private readonly IDatabaseBackupService _databaseBackupService;
    private readonly IApplicationRestartService _applicationRestartService;
    private readonly IFileDialogService _fileDialogService;
    private readonly IUserDialogService _userDialogService;
    private bool _isBusy;
    private string? _statusMessage;
    private string? _errorMessage;

    public DatabaseManagementViewModel(IDatabaseBackupService databaseBackupService, IApplicationRestartService applicationRestartService, IFileDialogService fileDialogService, IUserDialogService userDialogService)
    {
        _databaseBackupService = databaseBackupService;
        _applicationRestartService = applicationRestartService;
        _fileDialogService = fileDialogService;
        _userDialogService = userDialogService;
        CreateBackupCommand = new AsyncRelayCommand(CreateBackupAsync, () => !IsBusy);
        RestoreDatabaseCommand = new AsyncRelayCommand(RestoreDatabaseAsync, () => !IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ICommand CreateBackupCommand { get; }
    public ICommand RestoreDatabaseCommand { get; }
    public bool IsBusy { get => _isBusy; private set { if (SetField(ref _isBusy, value)) { ((AsyncRelayCommand)CreateBackupCommand).RaiseCanExecuteChanged(); ((AsyncRelayCommand)RestoreDatabaseCommand).RaiseCanExecuteChanged(); } } }
    public string? StatusMessage { get => _statusMessage; private set => SetField(ref _statusMessage, value); }
    public string? ErrorMessage { get => _errorMessage; private set => SetField(ref _errorMessage, value); }

    private async Task CreateBackupAsync()
    {
        var path = _fileDialogService.SelectBackupSaveFile($"AttendanceManagementSystem_{DateTime.Now:yyyy-MM-dd_HHmmss}.bak");
        if (path is null) return;
        await RunAsync(async () =>
        {
            var result = await _databaseBackupService.CreateBackupAsync(path);
            StatusMessage = $"Backup created successfully: {result.BackupPath}";
            _userDialogService.ShowInformation(StatusMessage, "Database Backup");
        });
    }

    private async Task RestoreDatabaseAsync()
    {
        var backupPath = _fileDialogService.SelectBackupFile();
        if (backupPath is null || !_userDialogService.Confirm("The selected backup will replace the current application database if restoration succeeds. A safety backup will be created first, and the application must restart after restoration. Continue?", "Restore Database")) return;
        var safetyPath = _fileDialogService.SelectBackupSaveFile($"AttendanceManagementSystem_Safety_{DateTime.Now:yyyy-MM-dd_HHmmss}.bak");
        if (safetyPath is null) return;
        await RunAsync(async () =>
        {
            var result = await _databaseBackupService.RestoreAsync(backupPath, safetyPath);
            StatusMessage = $"Database restored. Safety backup: {result.SafetyBackupPath}";
            if (_userDialogService.Confirm("Database restored successfully. The application must restart to load the restored data. Restart now?", "Restart Required")) _applicationRestartService.Restart();
            else _userDialogService.ShowInformation("Restart is required before normal database operations can continue.", "Restart Required");
        });
    }

    private async Task RunAsync(Func<Task> operation)
    {
        IsBusy = true; ErrorMessage = null; StatusMessage = null;
        try { await operation(); }
        catch (Exception exception) { ErrorMessage = exception.Message; _userDialogService.ShowError(ErrorMessage, "Database Management"); }
        finally { IsBusy = false; }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) { if (EqualityComparer<T>.Default.Equals(field, value)) return false; field = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); return true; }
}
