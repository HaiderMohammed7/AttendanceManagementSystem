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
    private readonly IUserDialogService _userDialogService;
    private int _courseId;
    private int _sessionId;
    private string? _errorMessage;
    private bool _isBusy;
    private LectureSession? _session;

    public LectureSessionDetailsViewModel(
        ILectureSessionService lectureSessionService,
        IUserDialogService userDialogService)
    {
        _lectureSessionService = lectureSessionService;
        _userDialogService = userDialogService;
        BackCommand = new RelayCommand(() => BackRequested?.Invoke(), () => !IsBusy);
        CompleteCommand = new AsyncRelayCommand(CompleteAsync, CanComplete);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? BackRequested;
    public ICommand BackCommand { get; }
    public ICommand CompleteCommand { get; }
    public ObservableCollection<AttendanceRowViewModel> AttendanceRecords { get; } = [];

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
                OnPropertyChanged(nameof(IsCompleted));
                ((AsyncRelayCommand)CompleteCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public DateTime? LectureDate => Session?.LectureDate;
    public string Title => string.IsNullOrWhiteSpace(Session?.Title) ? "Untitled lecture" : Session.Title!;
    public string StatusText => Session?.Status.ToString() ?? string.Empty;
    public int StudentCount => AttendanceRecords.Count;
    public bool IsCompleted => Session?.Status == LectureSessionStatus.Completed;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                ((RelayCommand)BackCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)CompleteCommand).RaiseCanExecuteChanged();
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
                AttendanceRecords.Add(new AttendanceRowViewModel(record, IsCompleted, SaveAttendanceAsync));
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

    private async Task SaveAttendanceAsync(AttendanceRowViewModel row, AttendanceStatus status)
    {
        if (IsCompleted)
        {
            row.RestoreStatus(row.Record.Status);
            return;
        }

        await _lectureSessionService.UpdateAttendanceAsync(
            _courseId,
            _sessionId,
            row.Record.Id,
            status);
    }

    private bool CanComplete() => !IsBusy && Session?.Status == LectureSessionStatus.Draft;

    private async Task CompleteAsync()
    {
        if (AttendanceRecords.Any(record => record.Status is null))
        {
            ErrorMessage = "Every student must be marked Present or Absent before completing the lecture.";
            return;
        }

        if (!_userDialogService.Confirm(
                "Complete this lecture? Attendance can no longer be changed after completion.",
                "Complete Lecture"))
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _lectureSessionService.CompleteSessionAsync(_courseId, _sessionId);
            if (Session is not null)
            {
                Session.Status = LectureSessionStatus.Completed;
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(IsCompleted));
            }

            foreach (var record in AttendanceRecords)
            {
                record.SetReadOnly();
            }

            ((AsyncRelayCommand)CompleteCommand).RaiseCanExecuteChanged();
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}