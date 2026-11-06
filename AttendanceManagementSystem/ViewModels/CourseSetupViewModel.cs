using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using AttendanceManagementSystem.Interfaces;
using AttendanceManagementSystem.Models;

namespace AttendanceManagementSystem.ViewModels;

public sealed class CourseSetupViewModel : INotifyPropertyChanged
{
    private readonly ICourseService _courseService;
    private readonly AsyncRelayCommand _saveCommand;
    private readonly RelayCommand _cancelCommand;
    private int? _courseId;
    private string _courseName = string.Empty;
    private string _instructorName = string.Empty;
    private string _academicYear = string.Empty;
    private string _semester = string.Empty;
    private string? _courseNameError;
    private string? _instructorNameError;
    private string? _academicYearError;
    private string? _semesterError;
    private string? _saveError;
    private bool _isSaving;

    public CourseSetupViewModel(ICourseService courseService)
    {
        _courseService = courseService;
        _saveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        _cancelCommand = new RelayCommand(() => Cancelled?.Invoke(), () => IsEditing);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action<Course>? Saved;

    public event Action? Cancelled;

    public IReadOnlyList<string> SemesterOptions { get; } = ["First", "Second"];

    public string FormTitle => IsEditing ? "Edit Course" : "Set Up Your Course";

    public string FormDescription => IsEditing ? "Update the course information used by your attendance workspace." : "Enter the course information to get started with Attendance Management System.";

    public Visibility CancelVisibility => IsEditing ? Visibility.Visible : Visibility.Collapsed;

    public bool IsEditing => _courseId.HasValue;

    public bool IsSaving
    {
        get => _isSaving;
        private set
        {
            if (_isSaving == value)
            {
                return;
            }

            _isSaving = value;
            OnPropertyChanged();
            _saveCommand.RaiseCanExecuteChanged();
        }
    }

    public string CourseName
    {
        get => _courseName;
        set
        {
            if (SetField(ref _courseName, value))
            {
                ValidateCourseName();
                ClearSaveError();
            }
        }
    }

    public string InstructorName
    {
        get => _instructorName;
        set
        {
            if (SetField(ref _instructorName, value))
            {
                ValidateInstructorName();
                ClearSaveError();
            }
        }
    }

    public string AcademicYear
    {
        get => _academicYear;
        set
        {
            if (SetField(ref _academicYear, value))
            {
                ValidateAcademicYear();
                ClearSaveError();
            }
        }
    }

    public string Semester
    {
        get => _semester;
        set
        {
            if (SetField(ref _semester, value))
            {
                ValidateSemester();
                ClearSaveError();
            }
        }
    }

    public string? CourseNameError
    {
        get => _courseNameError;
        private set => SetField(ref _courseNameError, value);
    }

    public string? InstructorNameError
    {
        get => _instructorNameError;
        private set => SetField(ref _instructorNameError, value);
    }

    public string? AcademicYearError
    {
        get => _academicYearError;
        private set => SetField(ref _academicYearError, value);
    }

    public string? SemesterError
    {
        get => _semesterError;
        private set => SetField(ref _semesterError, value);
    }

    public string? SaveError
    {
        get => _saveError;
        private set => SetField(ref _saveError, value);
    }

    public ICommand SaveCommand => _saveCommand;

    public ICommand CancelCommand => _cancelCommand;

    public void Initialize(Course? course)
    {
        _courseId = course?.Id;
        CourseName = course?.CourseName ?? string.Empty;
        InstructorName = course?.InstructorName ?? string.Empty;
        AcademicYear = course?.AcademicYear ?? string.Empty;
        Semester = course?.Semester ?? string.Empty;
        ClearValidation();
        ClearSaveError();
        OnPropertyChanged(nameof(FormTitle));
        OnPropertyChanged(nameof(FormDescription));
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(CancelVisibility));
        _cancelCommand.RaiseCanExecuteChanged();
        _saveCommand.RaiseCanExecuteChanged();
    }

    private bool CanSave()
    {
        return !IsSaving
            && string.IsNullOrWhiteSpace(CourseNameError)
            && string.IsNullOrWhiteSpace(InstructorNameError)
            && string.IsNullOrWhiteSpace(AcademicYearError)
            && string.IsNullOrWhiteSpace(SemesterError)
            && !string.IsNullOrWhiteSpace(CourseName)
            && !string.IsNullOrWhiteSpace(InstructorName)
            && !string.IsNullOrWhiteSpace(AcademicYear)
            && !string.IsNullOrWhiteSpace(Semester);
    }

    private async Task SaveAsync()
    {
        ValidateAll();
        if (!CanSave())
        {
            return;
        }

        IsSaving = true;
        ClearSaveError();

        try
        {
            var course = await _courseService.SaveCourseAsync(
                _courseId,
                CourseName,
                InstructorName,
                AcademicYear,
                Semester);

            _courseId = course.Id;
            Saved?.Invoke(course);
        }
        catch (Exception exception)
        {
            SaveError = $"The course could not be saved. {exception.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void ValidateAll()
    {
        ValidateCourseName();
        ValidateInstructorName();
        ValidateAcademicYear();
        ValidateSemester();
        _saveCommand.RaiseCanExecuteChanged();
    }

    private void ValidateCourseName() => CourseNameError = RequiredMessage(CourseName);

    private void ValidateInstructorName() => InstructorNameError = RequiredMessage(InstructorName);

    private void ValidateAcademicYear() => AcademicYearError = RequiredMessage(AcademicYear);

    private void ValidateSemester() => SemesterError = RequiredMessage(Semester);

    private static string? RequiredMessage(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "This field is required." : null;
    }

    private void ClearValidation()
    {
        CourseNameError = null;
        InstructorNameError = null;
        AcademicYearError = null;
        SemesterError = null;
    }

    private void ClearSaveError() => SaveError = null;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        _saveCommand.RaiseCanExecuteChanged();
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}