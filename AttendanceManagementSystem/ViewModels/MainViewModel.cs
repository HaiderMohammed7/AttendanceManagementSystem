using System.ComponentModel;
using System.Windows.Input;
using AttendanceManagementSystem.Interfaces;
using AttendanceManagementSystem.Models;

namespace AttendanceManagementSystem.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ICourseService _courseService;
    private readonly CourseSetupViewModel _courseSetupViewModel;
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly StudentManagementViewModel _studentManagementViewModel;
    private readonly LectureSessionListViewModel _lectureSessionListViewModel;
    private readonly LectureSessionCreateViewModel _lectureSessionCreateViewModel;
    private readonly LectureSessionDetailsViewModel _lectureSessionDetailsViewModel;
    private readonly ReportsViewModel _reportsViewModel;
    private readonly DatabaseManagementViewModel _databaseManagementViewModel;
    private object? _currentViewModel;
    private bool _isSidebarExpanded = true;
    private readonly RelayCommand _showDashboardCommand;
    private readonly RelayCommand _showStudentsCommand;
    private readonly RelayCommand _showLectureSessionsCommand;
    private readonly RelayCommand _editCourseCommand;
    private readonly RelayCommand _toggleSidebarCommand;
    private readonly RelayCommand _showReportsCommand;
    private readonly RelayCommand _showDatabaseManagementCommand;

    public MainViewModel(ICourseService courseService, CourseSetupViewModel courseSetupViewModel, DashboardViewModel dashboardViewModel, StudentManagementViewModel studentManagementViewModel, LectureSessionListViewModel lectureSessionListViewModel, LectureSessionCreateViewModel lectureSessionCreateViewModel, LectureSessionDetailsViewModel lectureSessionDetailsViewModel,  ReportsViewModel reportsViewModel,  DatabaseManagementViewModel databaseManagementViewModel)
    {
        _courseService = courseService;
        _courseSetupViewModel = courseSetupViewModel;
        _dashboardViewModel = dashboardViewModel;
        _studentManagementViewModel = studentManagementViewModel;
        _lectureSessionListViewModel = lectureSessionListViewModel;
        _lectureSessionCreateViewModel = lectureSessionCreateViewModel;
        _lectureSessionDetailsViewModel = lectureSessionDetailsViewModel;
        _reportsViewModel = reportsViewModel;
        _databaseManagementViewModel = databaseManagementViewModel;

        _showDashboardCommand = new RelayCommand(ShowDashboardFromShell, CanNavigateToCoursePage);
        _showStudentsCommand = new RelayCommand(ShowStudentManagement, CanNavigateToCoursePage);
        _showLectureSessionsCommand = new RelayCommand(ShowLectureSessions, CanNavigateToCoursePage);
        _editCourseCommand = new RelayCommand(ShowCourseSetupForEdit, CanNavigateToCoursePage);
        _toggleSidebarCommand = new RelayCommand(ToggleSidebar, () => IsShellVisible);
        _showReportsCommand = new RelayCommand(ShowReports, CanNavigateToCoursePage);
        _showDatabaseManagementCommand = new RelayCommand(ShowDatabaseManagement, CanNavigateToCoursePage);

        _courseSetupViewModel.Saved += ShowDashboard;
        _courseSetupViewModel.Cancelled += ShowDashboardForCurrentCourse;
        _dashboardViewModel.EditRequested += ShowCourseSetupForEdit;
        _dashboardViewModel.ManageStudentsRequested += ShowStudentManagement;
        _dashboardViewModel.ManageLectureSessionsRequested += ShowLectureSessions;
        _dashboardViewModel.WarningStudentRequested += ShowStudentManagementForStudent;
        _studentManagementViewModel.BackRequested += ShowDashboardForCurrentCourse;
        _studentManagementViewModel.ConfigureBackCommand();
        _lectureSessionListViewModel.BackRequested += ShowDashboardForCurrentCourse;
        _lectureSessionListViewModel.CreateRequested += ShowLectureSessionCreate;
        _lectureSessionListViewModel.DetailsRequested += ShowLectureSessionDetails;
        _lectureSessionCreateViewModel.CancelRequested += ShowLectureSessions;
        _lectureSessionCreateViewModel.Saved += _ => ShowLectureSessions();
        _lectureSessionDetailsViewModel.BackRequested += ShowLectureSessions;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public object? CurrentViewModel
    {
        get => _currentViewModel;
        private set
        {
            if (ReferenceEquals(_currentViewModel, value))
            {
                return;
            }

            _currentViewModel = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentViewModel)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsShellVisible)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCourseSetupVisible)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentPageTitle)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedNavigationItem)));
            OnNavigationSelectionChanged();
            RaiseNavigationCanExecuteChanged();
        }
    }

    public bool IsShellVisible => CurrentViewModel is not CourseSetupViewModel;

    public bool IsCourseSetupVisible => !IsShellVisible;

    public bool IsSidebarExpanded
    {
        get => _isSidebarExpanded;
        set
        {
            if (_isSidebarExpanded == value)
            {
                return;
            }

            _isSidebarExpanded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSidebarExpanded)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SidebarWidth)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SidebarToggleLabel)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowSidebarLabels))); 
        }
    }

    public double SidebarWidth => IsShellVisible ? (IsSidebarExpanded ? 248 : 76) : 0;

    public string SidebarToggleLabel => IsSidebarExpanded ? "Collapse sidebar" : "Expand sidebar";
    public bool ShowSidebarLabels => IsSidebarExpanded;
    public bool IsDashboardSelected => SelectedNavigationItem == "Dashboard";
    public bool IsStudentsSelected => SelectedNavigationItem == "Students";
    public bool IsLectureSessionsSelected => SelectedNavigationItem == "Lecture Sessions";
    public bool IsReportsSelected => SelectedNavigationItem == "Reports";
    public bool IsDatabaseManagementSelected => SelectedNavigationItem == "Database Management";
    public bool IsCourseSettingsSelected => CurrentViewModel is CourseSetupViewModel;

    public string CurrentPageTitle => CurrentViewModel switch
    {
        DashboardViewModel => "Dashboard",
        StudentManagementViewModel => "Students",
        LectureSessionListViewModel => "Lecture Sessions",
        LectureSessionCreateViewModel => "Create Lecture Session",
        LectureSessionDetailsViewModel => "Lecture Session Details",
        ReportsViewModel => "Reports",
        DatabaseManagementViewModel => "Database Management",
        _ => string.Empty
    };

    public string CourseName => _dashboardViewModel.Course?.CourseName ?? string.Empty;

    public ICommand ShowDashboardCommand => _showDashboardCommand;
    public ICommand ShowStudentsCommand => _showStudentsCommand;
    public ICommand ShowLectureSessionsCommand => _showLectureSessionsCommand;
    public ICommand EditCourseCommand => _editCourseCommand;
    public ICommand ToggleSidebarCommand => _toggleSidebarCommand;
    public ICommand ShowReportsCommand => _showReportsCommand;
    public ICommand ShowDatabaseManagementCommand => _showDatabaseManagementCommand;

    public string SelectedNavigationItem => CurrentViewModel switch
    {
        DashboardViewModel => "Dashboard",
        StudentManagementViewModel => "Students",
        LectureSessionListViewModel or LectureSessionCreateViewModel or LectureSessionDetailsViewModel => "Lecture Sessions",
        ReportsViewModel => "Reports",
        DatabaseManagementViewModel => "Database Management",
        _ => ""
    };

    public void ToggleSidebar() => IsSidebarExpanded = !IsSidebarExpanded;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var course = await _courseService.GetCourseAsync(cancellationToken);

        if (course is null)
        {
            ShowCourseSetup(null);
            return;
        }

        ShowDashboard(course);
    }

    private void OnNavigationSelectionChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDashboardSelected)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsStudentsSelected)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLectureSessionsSelected)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsReportsSelected)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDatabaseManagementSelected)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCourseSettingsSelected)));
    }

    private bool CanNavigateToCoursePage() => IsShellVisible && _dashboardViewModel.Course is not null;

    private void ShowDashboardFromShell()
    {
        if (_dashboardViewModel.Course is not null)
        {
            ShowDashboard(_dashboardViewModel.Course);
        }
    }

    private void ShowCourseSetupForEdit()
    {
        ShowCourseSetup(_dashboardViewModel.Course);
    }

    private void ShowCourseSetup(Course? course)
    {
        _courseSetupViewModel.Initialize(course);
        CurrentViewModel = _courseSetupViewModel;
    }

    private void ShowDashboard(Course course)
    {
        _dashboardViewModel.ShowCourse(course);
        CurrentViewModel = _dashboardViewModel;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CourseName)));
    }

    private void RaiseNavigationCanExecuteChanged()
    {
        _showDashboardCommand.RaiseCanExecuteChanged();
        _showStudentsCommand.RaiseCanExecuteChanged();
        _showLectureSessionsCommand.RaiseCanExecuteChanged();
        _editCourseCommand.RaiseCanExecuteChanged();
        _toggleSidebarCommand.RaiseCanExecuteChanged();
        _showReportsCommand.RaiseCanExecuteChanged();
        _showDatabaseManagementCommand.RaiseCanExecuteChanged();
    }

    private void ShowReports()
    {
        if (_dashboardViewModel.Course is null) return;
        _reportsViewModel.ShowCourse(_dashboardViewModel.Course);
        CurrentViewModel = _reportsViewModel;
    }

    private void ShowDatabaseManagement()
    {
        if (_dashboardViewModel.Course is null) return;
        CurrentViewModel = _databaseManagementViewModel;
    }

    private void ShowDashboardForCurrentCourse()
    {
        if (_dashboardViewModel.Course is not null)
        {
            ShowDashboard(_dashboardViewModel.Course);
        }
    }

    private void ShowStudentManagement()
    {
        if (_dashboardViewModel.Course is null)
        {
            return;
        }
        _studentManagementViewModel.Initialize(_dashboardViewModel.Course);
        CurrentViewModel = _studentManagementViewModel;
    }

    private void ShowStudentManagementForStudent(int studentId)
    {
        if (_dashboardViewModel.Course is null)
        {
            return;
        }

        _studentManagementViewModel.Initialize(_dashboardViewModel.Course, studentId);
        CurrentViewModel = _studentManagementViewModel;
    }

    private void ShowLectureSessions()
    {
        if (_dashboardViewModel.Course is null)
        {
            return;
        }
        _lectureSessionListViewModel.Initialize(_dashboardViewModel.Course);
        CurrentViewModel = _lectureSessionListViewModel;
    }

    private void ShowLectureSessionCreate()
    {
        if (_dashboardViewModel.Course is null)
        {
            return;
        }
        _lectureSessionCreateViewModel.Initialize(_dashboardViewModel.Course);
        CurrentViewModel = _lectureSessionCreateViewModel;
    }

    private void ShowLectureSessionDetails(LectureSession session)
    {
        if (_dashboardViewModel.Course is null)
        {
            return;
        }
        _lectureSessionDetailsViewModel.Initialize(_dashboardViewModel.Course, session);
        CurrentViewModel = _lectureSessionDetailsViewModel;
    }
}