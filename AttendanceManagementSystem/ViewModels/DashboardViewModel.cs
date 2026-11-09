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
        IStatisticsService statisticsService)
    {
        _statisticsService = statisticsService;
        EditCourseCommand = new RelayCommand(() => EditRequested?.Invoke(), () => Course is not null);
        ManageStudentsCommand = new RelayCommand(() => ManageStudentsRequested?.Invoke(), () => Course is not null);
        ManageLectureSessionsCommand = new RelayCommand(() => ManageLectureSessionsRequested?.Invoke(), () => Course is not null);
        RefreshStatisticsCommand = new AsyncRelayCommand(LoadStatisticsAsync, () => !IsBusy && Course is not null);
        ViewAllWarningsCommand = new RelayCommand(() => ManageStudentsRequested?.Invoke(), () => Course is not null);
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
    public ICommand ViewAllWarningsCommand { get; }

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
    public int TotalAttendance => TotalPresent + TotalAbsent;
    public double PresentBarWidth => TotalAttendance == 0 ? 0 : TotalPresent * 260d / TotalAttendance;
    public double AbsentBarWidth => TotalAttendance == 0 ? 260 : TotalAbsent * 260d / TotalAttendance;
    public string AttendanceSummary => TotalAttendance == 0 ? "No completed attendance records" : $"{TotalPresent} present · {TotalAbsent} absent";

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetField(ref _isBusy, value)) return;
            ((AsyncRelayCommand)RefreshStatisticsCommand).RaiseCanExecuteChanged();
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
        _ = LoadStatisticsAsync();
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
            foreach (var warning in statistics.Warnings.Take(5))
            {
                Warnings.Add(new DashboardWarningViewModel(warning, () => WarningStudentRequested?.Invoke(warning.StudentId)));
            }
            OnPropertyChanged(nameof(TotalAttendance));
            OnPropertyChanged(nameof(PresentBarWidth));
            OnPropertyChanged(nameof(AbsentBarWidth));
            OnPropertyChanged(nameof(AttendanceSummary));
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