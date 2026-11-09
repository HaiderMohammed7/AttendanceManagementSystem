using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AttendanceManagementSystem.Interfaces;
using AttendanceManagementSystem.Models;
using AttendanceManagementSystem.Services;

namespace AttendanceManagementSystem.ViewModels;

public sealed class ReportsViewModel : INotifyPropertyChanged
{
    private readonly IReportService _reportService;
    private readonly IPdfExportService _pdfExportService;
    private readonly IFileDialogService _fileDialogService;
    private readonly IUserDialogService _userDialogService;
    private readonly IStudentService _studentService;
    private Course? _course;
    private StudentReportData? _selectedStudentReport;
    private Student? _selectedStudent;
    private string _studentSearchText = string.Empty;
    private bool _isBusy;
    private string? _errorMessage;
    private bool _hasLoadedStudents;

    public ReportsViewModel(IReportService reportService, IPdfExportService pdfExportService, IFileDialogService fileDialogService, IUserDialogService userDialogService, IStudentService studentService)
    {
        _reportService = reportService;
        _pdfExportService = pdfExportService;
        _fileDialogService = fileDialogService;
        _userDialogService = userDialogService;
        _studentService = studentService;
        ExportCourseReportCommand = new AsyncRelayCommand(ExportCourseReportAsync, () => !IsBusy && Course is not null);
        ExportWarningsReportCommand = new AsyncRelayCommand(ExportWarningsReportAsync, () => !IsBusy && Course is not null);
        SelectStudentCommand = new AsyncRelayCommand(SelectStudentAsync, () => !IsBusy && SelectedStudent is not null);
        ExportStudentReportCommand = new AsyncRelayCommand(ExportStudentReportAsync, () => !IsBusy && SelectedStudentReport is not null);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<Student> Students { get; } = [];
    public ObservableCollection<Student> FilteredStudents { get; } = [];
    public ICommand ExportCourseReportCommand { get; }
    public ICommand ExportWarningsReportCommand { get; }
    public ICommand SelectStudentCommand { get; }
    public ICommand ExportStudentReportCommand { get; }

    public Course? Course { get => _course; private set => SetField(ref _course, value); }
    public Student? SelectedStudent { get => _selectedStudent; set { if (ReferenceEquals(_selectedStudent, value)) return; _selectedStudent = value; OnPropertyChanged(); RaiseCanExecuteChanged(); } }
    public StudentReportData? SelectedStudentReport { get => _selectedStudentReport; private set => SetField(ref _selectedStudentReport, value); }
    public string StudentSearchText { get => _studentSearchText; set { if (SetField(ref _studentSearchText, value)) RefreshFilteredStudents(); } }
    public bool IsBusy { get => _isBusy; private set { if (SetField(ref _isBusy, value)) { RaiseCanExecuteChanged(); } } }
    public string? ErrorMessage { get => _errorMessage; private set => SetField(ref _errorMessage, value); }
    public bool HasStudents => FilteredStudents.Count > 0;

    public void ShowCourse(Course course)
    {
        Course = course;
        SelectedStudent = null;
        SelectedStudentReport = null;
        StudentSearchText = string.Empty;
        ErrorMessage = null;
        if (!_hasLoadedStudents)
        {
            _ = LoadStudentsAsync();
        }

        RaiseCanExecuteChanged();
    }

    private async Task ExportStudentReportAsync()
    {
        if (SelectedStudentReport is null) return;
        await ExportAsync($"Student_Report_{SelectedStudentReport.UniversityId}.pdf", path => _pdfExportService.ExportStudentReportAsync(SelectedStudentReport, path));
    }

    private async Task LoadStudentsAsync()
    {
        if (Course is null) return;
        IsBusy = true;
        try
        {
            var students = await _studentService.GetStudentsAsync(Course.Id);
            Students.Clear();
            foreach (var student in students) Students.Add(student);
            RefreshFilteredStudents();
            _hasLoadedStudents = true;
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Students could not be loaded. {exception.Message}";
        }
        finally { IsBusy = false; }
    }

    private void RefreshFilteredStudents()
    {
        var query = StudentSearchText.Trim();
        var results = Students.Where(student => string.IsNullOrWhiteSpace(query) || student.FullName.Contains(query, StringComparison.OrdinalIgnoreCase) || student.UniversityId.Contains(query, StringComparison.OrdinalIgnoreCase)).OrderBy(student => student.UniversityId).ToList();
        FilteredStudents.Clear();
        foreach (var student in results) FilteredStudents.Add(student);
        OnPropertyChanged(nameof(HasStudents));
    }

    private async Task SelectStudentAsync()
    {
        if (Course is null || SelectedStudent is null) return;
        IsBusy = true;
        try
        {
            SelectedStudentReport = await _reportService.GetStudentReportAsync(Course.Id, SelectedStudent.Id);
            if (SelectedStudentReport is null) ErrorMessage = "The selected student report is unavailable.";
        }
        catch (Exception exception) { ErrorMessage = $"The student report could not be loaded. {exception.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task ExportCourseReportAsync()
    {
        if (Course is null) return;
        await ExportAsync($"Course_Attendance_Report_{DateTime.Now:yyyy}.pdf", async path => await _pdfExportService.ExportCourseReportAsync(await _reportService.GetCourseReportAsync(Course.Id), path));
    }

    private async Task ExportWarningsReportAsync()
    {
        if (Course is null) return;
        await ExportAsync($"Students_With_Warnings_{DateTime.Now:yyyy}.pdf", async path => await _pdfExportService.ExportWarningsReportAsync(await _reportService.GetWarningsReportAsync(Course.Id), path));
    }

    private async Task ExportAsync(string suggestedName, Func<string, Task> export)
    {
        var path = _fileDialogService.SelectPdfSaveFile(suggestedName);
        if (path is null) return;
        IsBusy = true;
        try { await export(path); _userDialogService.ShowInformation("The report was exported successfully.", "Report Export"); }
        catch (Exception exception) { ErrorMessage = $"The report could not be exported. {exception.Message}"; _userDialogService.ShowError(ErrorMessage, "Report Export"); }
        finally { IsBusy = false; }
    }

    private void RaiseCanExecuteChanged()
    {
        ((AsyncRelayCommand)ExportCourseReportCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)ExportWarningsReportCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)SelectStudentCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)ExportStudentReportCommand).RaiseCanExecuteChanged();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) { if (EqualityComparer<T>.Default.Equals(field, value)) return false; field = value; OnPropertyChanged(propertyName); return true; }
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
