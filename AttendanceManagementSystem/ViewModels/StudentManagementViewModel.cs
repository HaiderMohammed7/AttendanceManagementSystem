using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using AttendanceManagementSystem.Interfaces;
using AttendanceManagementSystem.Models;
using AttendanceManagementSystem.Services;

namespace AttendanceManagementSystem.ViewModels;

public sealed class StudentManagementViewModel : INotifyPropertyChanged
{
    private readonly IStudentService _studentService;
    private readonly ICsvStudentParser _csvStudentParser;
    private readonly IFileDialogService _fileDialogService;
    private readonly IUserDialogService _userDialogService;
    private readonly IStatisticsService _statisticsService;
    private readonly IReportService _reportService;
    private readonly IPdfExportService _pdfExportService;
    private readonly AsyncRelayCommand _importCommand;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _saveNameCommand;
    private readonly AsyncRelayCommand _deactivateCommand;
    private CancellationTokenSource? _searchCancellation;
    private int _courseId;
    private string _courseName = string.Empty;
    private string _searchText = string.Empty;
    private Student? _selectedStudent;
    private string _editedFullName = string.Empty;
    private bool _isBusy;
    private string? _errorMessage;
    private string? _selectedStudentError;
    private StudentAttendanceStatistics? _attendanceStatistics;
    private bool _isStatisticsBusy;
    private int? _requestedStudentId;

    public StudentManagementViewModel(
        IStudentService studentService,
        ICsvStudentParser csvStudentParser,
        IFileDialogService fileDialogService,
        IUserDialogService userDialogService,
        IStatisticsService statisticsService,
        IReportService reportService,
        IPdfExportService pdfExportService)
    {
        _studentService = studentService;
        _csvStudentParser = csvStudentParser;
        _fileDialogService = fileDialogService;
        _userDialogService = userDialogService;
        _statisticsService = statisticsService;
        _reportService = reportService;
        _pdfExportService = pdfExportService;

        _importCommand = new AsyncRelayCommand(ImportAsync, () => !IsBusy);
        _refreshCommand = new AsyncRelayCommand(LoadStudentsAsync, () => !IsBusy);
        _saveNameCommand = new AsyncRelayCommand(SaveNameAsync, CanSaveName);
        _deactivateCommand = new AsyncRelayCommand(DeactivateAsync, CanDeactivate);
        ExportStudentReportCommand = new AsyncRelayCommand(ExportStudentReportAsync, () => !IsBusy && SelectedStudent is not null);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action? BackRequested;

    public ObservableCollection<Student> Students { get; } = [];

    public string CourseName
    {
        get => _courseName;
        private set => SetField(ref _courseName, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
            {
                _ = RefreshForSearchAsync();
            }
        }
    }

    public Student? SelectedStudent
    {
        get => _selectedStudent;
        set
        {
            if (SetField(ref _selectedStudent, value))
            {
                EditedFullName = value?.FullName ?? string.Empty;
                SelectedStudentError = null;
                OnPropertyChanged(nameof(SelectedUniversityId));
                OnPropertyChanged(nameof(SelectedStatusText));
                AttendanceHistory.Clear();
                AttendanceStatistics = null;
                if (value is not null)
                {
                    _ = LoadAttendanceDetailsAsync(value.Id);
                }
                _saveNameCommand.RaiseCanExecuteChanged();
                _deactivateCommand.RaiseCanExecuteChanged();
                ((AsyncRelayCommand)ExportStudentReportCommand).RaiseCanExecuteChanged();
            }

        }
    }

    public string SelectedUniversityId => SelectedStudent?.UniversityId ?? string.Empty;

    public string SelectedStatusText => SelectedStudent?.IsActive == true ? "Active" : "Inactive";

    public string EditedFullName
    {
        get => _editedFullName;
        set
        {
            if (SetField(ref _editedFullName, value))
            {
                SelectedStudentError = null;
                _saveNameCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                _importCommand.RaiseCanExecuteChanged();
                _refreshCommand.RaiseCanExecuteChanged();
                _saveNameCommand.RaiseCanExecuteChanged();
                _deactivateCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    public string? SelectedStudentError
    {
        get => _selectedStudentError;
        private set => SetField(ref _selectedStudentError, value);
    }

    public StudentAttendanceStatistics? AttendanceStatistics
    {
        get => _attendanceStatistics;
        private set
        {
            if (SetField(ref _attendanceStatistics, value))
            {
                OnPropertyChanged(nameof(PresentCount));
                OnPropertyChanged(nameof(AbsentCount));
                OnPropertyChanged(nameof(AttendancePercentage));
                OnPropertyChanged(nameof(AbsencePercentage));
                OnPropertyChanged(nameof(WarningStatus));
            }
        }
    }

    public ObservableCollection<StudentAttendanceHistoryItem> AttendanceHistory { get; } = [];
    public int PresentCount => AttendanceStatistics?.PresentCount ?? 0;
    public int AbsentCount => AttendanceStatistics?.AbsentCount ?? 0;
    public string AttendancePercentage => AttendanceStatistics?.AttendancePercentage ?? "N/A";
    public string AbsencePercentage => AttendanceStatistics?.AbsencePercentage ?? "N/A";
    public string WarningStatus => AttendanceStatistics?.WarningStatus ?? "No Warning";

    public bool IsStatisticsBusy
    {
        get => _isStatisticsBusy;
        private set => SetField(ref _isStatisticsBusy, value);
    }

    public ICommand ImportCommand => _importCommand;

    public ICommand RefreshCommand => _refreshCommand;

    public ICommand SaveNameCommand => _saveNameCommand;

    public ICommand DeactivateCommand => _deactivateCommand;
    public ICommand ExportStudentReportCommand { get; }

    public ICommand BackCommand { get; private set; } = new RelayCommand(() => { });

    public void Initialize(Course course, int? studentId = null)
    {
        _courseId = course.Id;
        CourseName = course.CourseName;
        SearchText = string.Empty;
        _requestedStudentId = studentId;
        SelectedStudent = null;
        ErrorMessage = null;
        _ = LoadStudentsAsync();
    }

    private async Task LoadAttendanceDetailsAsync(int studentId)
    {
        IsStatisticsBusy = true;
        try
        {
            AttendanceStatistics = await _statisticsService.GetStudentStatisticsAsync(_courseId, studentId);
            var history = await _statisticsService.GetStudentHistoryAsync(_courseId, studentId);
            AttendanceHistory.Clear();
            foreach (var item in history)
            {
                AttendanceHistory.Add(item);
            }
        }
        catch (Exception exception)
        {
            SelectedStudentError = $"Attendance details could not be loaded. {exception.Message}";
        }
        finally
        {
            IsStatisticsBusy = false;
        }
    }

    public void ConfigureBackCommand()
    {
        BackCommand = new RelayCommand(() => BackRequested?.Invoke());
        OnPropertyChanged(nameof(BackCommand));
    }

    private async Task LoadStudentsAsync()
    {
        if (_courseId == 0)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var students = await _studentService.GetStudentsAsync(_courseId, SearchText);
            Students.Clear();
            foreach (var student in students)
            {
                Students.Add(student);
            }

            var studentId = _requestedStudentId ?? SelectedStudent?.Id;
            if (studentId.HasValue)
            {
                SelectedStudent = Students.FirstOrDefault(student => student.Id == studentId.Value);
                _requestedStudentId = null;
            }
            else if (SelectedStudent is not null)
            {
                SelectedStudent = Students.FirstOrDefault(student => student.Id == SelectedStudent.Id);
            }
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Students could not be loaded. {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshForSearchAsync()
    {
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = new CancellationTokenSource();

        try
        {
            await LoadStudentsAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ImportAsync()
    {
        var filePath = _fileDialogService.SelectCsvFile();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        CsvParseResult parseResult;
        try
        {
            parseResult = _csvStudentParser.Parse(filePath);
        }
        catch (Exception exception)
        {
            _userDialogService.ShowError(exception.Message, "CSV Import");
            return;
        }

        if (parseResult.Rows.Count == 0)
        {
            _userDialogService.ShowError(BuildErrorSummary(parseResult.Errors), "CSV Import");
            return;
        }

        var preview = new StringBuilder()
            .AppendLine("The following valid rows are ready to import:")
            .AppendLine($"Valid rows: {parseResult.Rows.Count}")
            .AppendLine($"Invalid or duplicate rows: {parseResult.Errors.Count}");

        if (parseResult.Errors.Count > 0)
        {
            preview.AppendLine().AppendLine(BuildErrorSummary(parseResult.Errors));
        }

        if (!_userDialogService.Confirm(preview.ToString(), "Preview Student Import"))
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var importResult = await _studentService.ImportAsync(_courseId, parseResult.Rows);
            var allErrors = parseResult.Errors.Concat(importResult.Errors).ToArray();
            var summary = new StringBuilder()
                .AppendLine("Student import completed.")
                .AppendLine($"New students added: {importResult.NewStudentsAdded}")
                .AppendLine($"Existing students updated: {importResult.ExistingStudentsUpdated}")
                .AppendLine($"Invalid or skipped rows: {allErrors.Length}");

            if (allErrors.Length > 0)
            {
                summary.AppendLine().AppendLine(BuildErrorSummary(allErrors));
            }

            _userDialogService.ShowInformation(summary.ToString(), "CSV Import");
            await LoadStudentsAsync();
        }
        catch (Exception exception)
        {
            ErrorMessage = $"The import could not be completed. {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveNameAsync()
    {
        if (SelectedStudent is null)
        {
            return;
        }

        var fullName = EditedFullName.Trim();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            SelectedStudentError = "Full name is required.";
            return;
        }

        IsBusy = true;
        SelectedStudentError = null;
        try
        {
            await _studentService.UpdateFullNameAsync(SelectedStudent.Id, fullName);
            await LoadStudentsAsync();
        }
        catch (Exception exception)
        {
            SelectedStudentError = $"The student could not be updated. {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeactivateAsync()
    {
        if (SelectedStudent is null || !SelectedStudent.IsActive)
        {
            return;
        }

        if (!_userDialogService.Confirm(
                $"Deactivate {SelectedStudent.FullName}? The student and attendance history will be retained.",
                "Deactivate Student"))
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _studentService.DeactivateAsync(SelectedStudent.Id);
            await LoadStudentsAsync();
        }
        catch (Exception exception)
        {
            ErrorMessage = $"The student could not be deactivated. {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSaveName() => !IsBusy && SelectedStudent is not null && !string.IsNullOrWhiteSpace(EditedFullName);

    private bool CanDeactivate() => !IsBusy && SelectedStudent?.IsActive == true;

    private async Task ExportStudentReportAsync()
    {
        if (SelectedStudent is null)
        {
            return;
        }

        var path = _fileDialogService.SelectPdfSaveFile($"Student_Report_{SelectedStudent.UniversityId}.pdf");
        if (path is null)
        {
            return;
        }

        try
        {
            var report = await _reportService.GetStudentReportAsync(_courseId, SelectedStudent.Id);
            if (report is null)
            {
                _userDialogService.ShowError("The selected student could not be found.", "Report Export");
                return;
            }

            await _pdfExportService.ExportStudentReportAsync(report, path);
            _userDialogService.ShowInformation("The student report was exported successfully.", "Report Export");
        }
        catch (Exception exception)
        {
            _userDialogService.ShowError($"The student report could not be exported. {exception.Message}", "Report Export");
        }
    }

    private static string BuildErrorSummary(IEnumerable<CsvRowError> errors)
    {
        var errorList = errors.ToArray();
        return errorList.Length == 0
            ? "No valid rows were found in the selected file."
            : string.Join(Environment.NewLine, errorList.Select(error => $"Row {error.RowNumber}: {error.Message}"));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
