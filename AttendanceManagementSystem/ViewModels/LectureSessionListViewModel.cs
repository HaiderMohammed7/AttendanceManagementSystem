using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AttendanceManagementSystem.Interfaces;
using AttendanceManagementSystem.Models;

namespace AttendanceManagementSystem.ViewModels;

public sealed class LectureSessionListViewModel : INotifyPropertyChanged
{
    private readonly ILectureSessionService _lectureSessionService;
    private bool _isBusy;
    private string? _errorMessage;
    private string _courseName = string.Empty;
    private LectureSession? _selectedSession;

    public LectureSessionListViewModel(ILectureSessionService lectureSessionService)
    {
        _lectureSessionService = lectureSessionService;
        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        CreateCommand = new RelayCommand(() => CreateRequested?.Invoke(), () => !IsBusy);
        BackCommand = new RelayCommand(() => BackRequested?.Invoke());
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? BackRequested;
    public event Action? CreateRequested;
    public event Action<LectureSession>? DetailsRequested;

    public ObservableCollection<LectureSession> Sessions { get; } = [];
    public ICommand RefreshCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand BackCommand { get; }

    public LectureSession? SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (SetField(ref _selectedSession, value) && value is not null)
            {
                DetailsRequested?.Invoke(value);
                SelectedSession = null;
            }
        }
    }

    public string CourseName
    {
        get => _courseName;
        private set => SetField(ref _courseName, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                ((AsyncRelayCommand)RefreshCommand).RaiseCanExecuteChanged();
                ((RelayCommand)CreateCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    public void Initialize(Course course)
    {
        _courseId = course.Id;
        CourseName = course.CourseName;
        ErrorMessage = null;
        _ = LoadAsync();
    }

    private int _courseId;

    private async Task LoadAsync()
    {
        if (_courseId == 0)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var sessions = await _lectureSessionService.GetSessionsAsync(_courseId);
            Sessions.Clear();
            foreach (var session in sessions)
            {
                Sessions.Add(session);
            }
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Lecture sessions could not be loaded. {exception.Message}";
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
