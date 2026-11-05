using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AttendanceManagementSystem.Interfaces;
using AttendanceManagementSystem.Models;

namespace AttendanceManagementSystem.ViewModels;

public sealed class LectureSessionDetailsViewModel : INotifyPropertyChanged
{
    private readonly ILectureSessionService _lectureSessionService;
    private int _courseId;
    private int _sessionId;
    private string? _errorMessage;
    private bool _isBusy;
    private LectureSession? _session;

    public LectureSessionDetailsViewModel(ILectureSessionService lectureSessionService)
    {
        _lectureSessionService = lectureSessionService;
        BackCommand = new RelayCommand(() => BackRequested?.Invoke(), () => !IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? BackRequested;
    public ICommand BackCommand { get; }
    public ObservableCollection<AttendanceRecord> AttendanceRecords { get; } = [];

    public LectureSession? Session
    {
        get => _session;
        private set
        {
            if (SetField(ref _session, value))
            {
                OnPropertyChanged(nameof(LectureDate));
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StudentCount));
            }
        }
    }

    public DateTime? LectureDate => Session?.LectureDate;
    public string Title => string.IsNullOrWhiteSpace(Session?.Title) ? "Untitled lecture" : Session.Title!;
    public string StatusText => Session?.Status.ToString() ?? string.Empty;
    public int StudentCount => AttendanceRecords.Count;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                ((RelayCommand)BackCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    public void Initialize(Course course, LectureSession session)
    {
        _courseId = course.Id;
        _sessionId = session.Id;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            Session = await _lectureSessionService.GetSessionAsync(_courseId, _sessionId);
            AttendanceRecords.Clear();
            if (Session is null)
            {
                ErrorMessage = "The lecture session could not be found.";
                return;
            }

            foreach (var record in Session.AttendanceRecords.OrderBy(record => record.Student.FullName))
            {
                AttendanceRecords.Add(record);
            }

            OnPropertyChanged(nameof(StudentCount));
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Lecture session details could not be loaded. {exception.Message}";
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
