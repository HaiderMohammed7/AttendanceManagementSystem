using System.ComponentModel;
using AttendanceManagementSystem.Interfaces;
using AttendanceManagementSystem.Models;

namespace AttendanceManagementSystem.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ICourseService _courseService;
    private readonly CourseSetupViewModel _courseSetupViewModel;
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly StudentManagementViewModel _studentManagementViewModel;
    private object? _currentViewModel;

    public MainViewModel(
        ICourseService courseService,
        CourseSetupViewModel courseSetupViewModel,
        DashboardViewModel dashboardViewModel,
        StudentManagementViewModel studentManagementViewModel)
    {
        _courseService = courseService;
        _courseSetupViewModel = courseSetupViewModel;
        _dashboardViewModel = dashboardViewModel;
        _studentManagementViewModel = studentManagementViewModel;

        _courseSetupViewModel.Saved += ShowDashboard;
        _courseSetupViewModel.Cancelled += ShowDashboardForCurrentCourse;
        _dashboardViewModel.EditRequested += ShowCourseSetupForEdit;
        _dashboardViewModel.ManageStudentsRequested += ShowStudentManagement;
        _studentManagementViewModel.BackRequested += ShowDashboardForCurrentCourse;
        _studentManagementViewModel.ConfigureBackCommand();
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
        }
    }

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
}
