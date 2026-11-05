using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AttendanceManagementSystem.Interfaces;
using AttendanceManagementSystem.Models;

namespace AttendanceManagementSystem.ViewModels;

public sealed class LectureSessionCreateViewModel : INotifyPropertyChanged
{
    private readonly ILectureSessionService _lectureSessionService;
    private int _courseId;
    private string _courseName = string.Empty;
    private DateTime _lectureDate = DateTime.Today;
    private string _title = string.Empty;
    private bool _isBusy;
    private string? _errorMessage;
    private string? _lectureDateError;

    public LectureSessionCreateViewModel(ILectureSessionService lectureSessionService)
    {
        _lectureSessionService = lectureSessionService;
        SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        CancelCommand = new RelayCommand(() => CancelRequested?.Invoke(), () => !IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<LectureSession>? Saved;
    public event Action? CancelRequested;

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public string CourseName
    {
        get => _courseName;
        private set => SetField(ref _courseName, value);
    }

    public DateTime LectureDate
    {
        get => _lectureDate;
        set
        {
            if (SetField(ref _lectureDate, value))
            {
                LectureDateError = null;
                ((AsyncRelayCommand)SaveCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                ((AsyncRelayCommand)SaveCommand).RaiseCanExecuteChanged();
                ((RelayCommand)CancelCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    public string? LectureDateError
    {
        get => _lectureDateError;
        private set => SetField(ref _lectureDateError, value);
    }

    public void Initialize(Course course)
    {
        _courseId = course.Id;
        CourseName = course.CourseName;
        LectureDate = DateTime.Today;
        Title = string.Empty;
        ErrorMessage = null;
        LectureDateError = null;
    }

    private bool CanSave() => !IsBusy && _courseId != 0 && LectureDate != default;

    private async Task SaveAsync()
    {
        if (LectureDate == default)
        {
            LectureDateError = "Lecture date is required.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var session = await _lectureSessionService.CreateSessionAsync(_courseId, LectureDate, Title);
            Saved?.Invoke(session);
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
