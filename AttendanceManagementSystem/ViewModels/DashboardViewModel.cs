using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AttendanceManagementSystem.Models;

namespace AttendanceManagementSystem.ViewModels;

public sealed class DashboardViewModel : INotifyPropertyChanged
{
    private Course? _course;

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action? EditRequested;

    public event Action? ManageStudentsRequested;

    public Course? Course
    {
        get => _course;
        private set
        {
            if (ReferenceEquals(_course, value))
            {
                return;
            }

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

    public ICommand EditCourseCommand { get; }

    public ICommand ManageStudentsCommand { get; }

    public DashboardViewModel()
    {
        EditCourseCommand = new RelayCommand(() => EditRequested?.Invoke(), () => Course is not null);
        ManageStudentsCommand = new RelayCommand(() => ManageStudentsRequested?.Invoke(), () => Course is not null);
    }

    public void ShowCourse(Course course)
    {
        Course = course;
        ((RelayCommand)EditCourseCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ManageStudentsCommand).RaiseCanExecuteChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
