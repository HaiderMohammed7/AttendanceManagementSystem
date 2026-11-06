using System.Windows.Input;
using AttendanceManagementSystem.Services;

namespace AttendanceManagementSystem.ViewModels;

public sealed class DashboardWarningViewModel
{
    public DashboardWarningViewModel(StudentWarning warning, Action open)
    {
        StudentId = warning.StudentId;
        UniversityId = warning.UniversityId;
        FullName = warning.FullName;
        AbsenceCount = warning.AbsenceCount;
        OpenCommand = new RelayCommand(open);
    }

    public int StudentId { get; }
    public string UniversityId { get; }
    public string FullName { get; }
    public int AbsenceCount { get; }
    public ICommand OpenCommand { get; }
}
