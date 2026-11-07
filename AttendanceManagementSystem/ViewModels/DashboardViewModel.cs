using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AttendanceManagementSystem.Interfaces;
using AttendanceManagementSystem.Models;
using AttendanceManagementSystem.Services;

namespace AttendanceManagementSystem.ViewModels;

public sealed class DashboardViewModel : INotifyPropertyChanged
{
    private readonly IStatisticsService _statisticsService;
    private readonly IReportService _reportService;
    private readonly IPdfExportService _pdfExportService;
    private readonly IFileDialogService _fileDialogService;
    private readonly IUserDialogService _userDialogService;
    private readonly IDatabaseBackupService _databaseBackupService;
    private readonly IApplicationRestartService _applicationRestartService;
    private Course? _course;
    private bool _isBusy;
    private string? _errorMessage;
    private int _activeStudents;
    private int _completedLectures;
    private int _draftLectures;
    private int _totalPresent;
    private int _totalAbsent;
    private int _studentsWithWarnings;

    public DashboardViewModel(
        IStatisticsService statisticsService,
        IReportService reportService,
        IPdfExportService pdfExportService,
        IFileDialogService fileDialogService,
        IUserDialogService userDialogService,
        IDatabaseBackupService databaseBackupService,
        IApplicationRestartService applicationRestartService)
    {
        _statisticsService = statisticsService;
        _reportService = reportService;
        _pdfExportService = pdfExportService;
        _fileDialogService = fileDialogService;
        _userDialogService = userDialogService;
        _databaseBackupService = databaseBackupService;
        _applicationRestartService = applicationRestartService;
        EditCourseCommand = new RelayCommand(() => EditRequested?.Invoke(), () => Course is not null);
        ManageStudentsCommand = new RelayCommand(() => ManageStudentsRequested?.Invoke(), () => Course is not null);
        ManageLectureSessionsCommand = new RelayCommand(() => ManageLectureSessionsRequested?.Invoke(), () => Course is not null);
        RefreshStatisticsCommand = new AsyncRelayCommand(LoadStatisticsAsync, () => !IsBusy && Course is not null);
        ExportCourseReportCommand = new AsyncRelayCommand(ExportCourseReportAsync, () => !IsBusy && Course is not null);
        ExportWarningsReportCommand = new AsyncRelayCommand(ExportWarningsReportAsync, () => !IsBusy && Course is not null);
        CreateBackupCommand = new AsyncRelayCommand(CreateBackupAsync, () => !IsBusy);
        RestoreDatabaseCommand = new AsyncRelayCommand(RestoreDatabaseAsync, () => !IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? EditRequested;
    public event Action? ManageStudentsRequested;
    public event Action? ManageLectureSessionsRequested;
    public event Action<int>? WarningStudentRequested;

    public ObservableCollection<DashboardWarningViewModel> Warnings { get; } = [];

    public bool HasWarnings => Warnings.Count > 0;
    public ICommand EditCourseCommand { get; }
    public ICommand ManageStudentsCommand { get; }
    public ICommand ManageLectureSessionsCommand { get; }
    public ICommand RefreshStatisticsCommand { get; }
    public ICommand ExportCourseReportCommand { get; }
    public ICommand ExportWarningsReportCommand { get; }
    public ICommand CreateBackupCommand { get; }
    public ICommand RestoreDatabaseCommand { get; }

    public Course? Course
    {
        get => _course;
        private set
        {
            if (ReferenceEquals(_course, value)) return;
            _course = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CourseName));
            OnPropertyChanged(nameof(InstructorName));
            OnPropertyChanged(nameof(AcademicYear));
            OnPropertyChanged(nameof(Semester));
        }
    }

    public string CourseName => Course?.CourseName ?? string.Empty;
    public string InstructorName => Course?.InstructorName ?? string.Empty;
    public string AcademicYear => Course?.AcademicYear ?? string.Empty;
    public string Semester => Course?.Semester ?? string.Empty;
    public int ActiveStudents { get => _activeStudents; private set => SetField(ref _activeStudents, value); }
    public int CompletedLectures { get => _completedLectures; private set => SetField(ref _completedLectures, value); }
    public int DraftLectures { get => _draftLectures; private set => SetField(ref _draftLectures, value); }
    public int TotalPresent { get => _totalPresent; private set => SetField(ref _totalPresent, value); }
    public int TotalAbsent { get => _totalAbsent; private set => SetField(ref _totalAbsent, value); }
    public int StudentsWithWarnings { get => _studentsWithWarnings; private set => SetField(ref _studentsWithWarnings, value); }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetField(ref _isBusy, value)) return;
            ((AsyncRelayCommand)RefreshStatisticsCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)ExportCourseReportCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)ExportWarningsReportCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)CreateBackupCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)RestoreDatabaseCommand).RaiseCanExecuteChanged();
        }
    }

    public string? ErrorMessage { get => _errorMessage; private set => SetField(ref _errorMessage, value); }

    public void ShowCourse(Course course)
    {
        Course = course;
        ((RelayCommand)EditCourseCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ManageStudentsCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ManageLectureSessionsCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)RefreshStatisticsCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)ExportCourseReportCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)ExportWarningsReportCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)CreateBackupCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)RestoreDatabaseCommand).RaiseCanExecuteChanged();
        _ = LoadStatisticsAsync();
    }

    private async Task CreateBackupAsync()
    {
        var path = _fileDialogService.SelectBackupSaveFile($"AttendanceManagementSystem_{DateTime.Now:yyyy-MM-dd_HHmmss}.bak");
        if (path is null) return;

        IsBusy = true;
        try
        {
            var result = await _databaseBackupService.CreateBackupAsync(path);
            _userDialogService.ShowInformation($"The database backup was created successfully.\n\n{result.BackupPath}", "Database Backup");
        }
        catch (Exception exception)
        {
            _userDialogService.ShowError($"The database backup could not be created. SQL Server must be able to write to the selected folder.\n\n{exception.Message}", "Database Backup");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RestoreDatabaseAsync()
    {
        var backupPath = _fileDialogService.SelectBackupFile();
        if (backupPath is null) return;
        if (!_userDialogService.Confirm("The selected backup will replace the current application database if restoration succeeds. A safety backup will be created first, and the application must restart after restoration. Continue?", "Restore Database")) return;

        var safetyPath = _fileDialogService.SelectBackupSaveFile($"AttendanceManagementSystem_Safety_{DateTime.Now:yyyy-MM-dd_HHmmss}.bak");
        if (safetyPath is null) return;

        IsBusy = true;
        try
        {
            var result = await _databaseBackupService.RestoreAsync(backupPath, safetyPath);
            if (!result.Succeeded) return;

            if (_userDialogService.Confirm($"Database restored successfully. The application must restart to load the restored data. Restart now?\n\nSafety backup: {result.SafetyBackupPath}", "Restart Required"))
            {
                _applicationRestartService.Restart();
            }
            else
            {
                _userDialogService.ShowInformation("The database was restored, but the application must be restarted before normal work can continue.", "Restart Required");
            }
        }
        catch (Exception exception)
        {
            _userDialogService.ShowError(exception.Message, "Database Restore Failed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExportCourseReportAsync()
    {
        if (Course is null) return;
        var path = _fileDialogService.SelectPdfSaveFile($"Course_Attendance_Report_{DateTime.Now:yyyy}.pdf");
        if (path is null) return;
        try
        {
            var report = await _reportService.GetCourseReportAsync(Course.Id);
            await _pdfExportService.ExportCourseReportAsync(report, path);
            _userDialogService.ShowInformation("The course attendance report was exported successfully.", "Report Export");
        }
        catch (Exception exception)
        {
            _userDialogService.ShowError($"The course report could not be exported. {exception.Message}", "Report Export");
        }
    }

    private async Task ExportWarningsReportAsync()
    {
        if (Course is null) return;
        var path = _fileDialogService.SelectPdfSaveFile($"Students_With_Warnings_{DateTime.Now:yyyy}.pdf");
        if (path is null) return;
        try
        {
            var report = await _reportService.GetWarningsReportAsync(Course.Id);
            await _pdfExportService.ExportWarningsReportAsync(report, path);
            _userDialogService.ShowInformation("The students with warnings report was exported successfully.", "Report Export");
        }
        catch (Exception exception)
        {
            _userDialogService.ShowError($"The warnings report could not be exported. {exception.Message}", "Report Export");
        }
    }

    private async Task LoadStatisticsAsync()
    {
        if (Course is null) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var statistics = await _statisticsService.GetDashboardStatisticsAsync(Course.Id);
            ActiveStudents = statistics.ActiveStudents;
            CompletedLectures = statistics.CompletedLectures;
            DraftLectures = statistics.DraftLectures;
            TotalPresent = statistics.TotalPresent;
            TotalAbsent = statistics.TotalAbsent;
            StudentsWithWarnings = statistics.StudentsWithWarnings;
            Warnings.Clear();
            foreach (var warning in statistics.Warnings)
            {
                Warnings.Add(new DashboardWarningViewModel(warning, () => WarningStudentRequested?.Invoke(warning.StudentId)));
            }
            OnPropertyChanged(nameof(HasWarnings));
        }
        catch (Exception)
        {
            Warnings.Clear();
            OnPropertyChanged(nameof(HasWarnings));
            ErrorMessage = "Dashboard statistics could not be loaded. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}