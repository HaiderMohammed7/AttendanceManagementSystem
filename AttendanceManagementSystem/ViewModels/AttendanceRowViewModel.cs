using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AttendanceManagementSystem.Models;

namespace AttendanceManagementSystem.ViewModels;

public sealed class AttendanceRowViewModel : INotifyPropertyChanged
{
    private readonly Func<AttendanceRowViewModel, AttendanceStatus, Task> _saveStatus;
    private AttendanceStatus? _status;
    private string? _errorMessage;
    private bool _isReadOnly;
    private bool _isSaving;

    public AttendanceRowViewModel(
        AttendanceRecord record,
        bool isReadOnly,
        Func<AttendanceRowViewModel, AttendanceStatus, Task> saveStatus)
    {
        Record = record;
        _status = record.Status;
        _isReadOnly = isReadOnly;
        _saveStatus = saveStatus;
        PresentCommand = new AsyncRelayCommand(() => SaveAsync(AttendanceStatus.Present), CanSave);
        AbsentCommand = new AsyncRelayCommand(() => SaveAsync(AttendanceStatus.Absent), CanSave);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AttendanceRecord Record { get; }
    public string UniversityId => Record.Student.UniversityId;
    public string FullName => Record.Student.FullName;
    public ICommand PresentCommand { get; }
    public ICommand AbsentCommand { get; }

    public AttendanceStatus? Status
    {
        get => _status;
        private set
        {
            if (SetField(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(IsPresent));
                OnPropertyChanged(nameof(IsAbsent));
                OnPropertyChanged(nameof(IsNotMarked));
            }
        }
    }

    public string StatusText => Status?.ToString() ?? "Not Marked";
    public bool IsPresent => Status == AttendanceStatus.Present;
    public bool IsAbsent => Status == AttendanceStatus.Absent;
    public bool IsNotMarked => Status is null;
    public bool IsReadOnly => _isReadOnly;

    private bool CanSave() => !IsReadOnly && !_isSaving;

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    public void SetReadOnly()
    {
        _isReadOnly = true;
        OnPropertyChanged(nameof(IsReadOnly));
        ((AsyncRelayCommand)PresentCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)AbsentCommand).RaiseCanExecuteChanged();
    }

    public void RestoreStatus(AttendanceStatus? status)
    {
        Status = status;
        Record.Status = status;
    }

    private async Task SaveAsync(AttendanceStatus status)
    {
        _isSaving = true;
        ((AsyncRelayCommand)PresentCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)AbsentCommand).RaiseCanExecuteChanged();
        ErrorMessage = null;
        var previousStatus = Status;
        try
        {
            await _saveStatus(this, status);
            Status = status;
            Record.Status = status;
        }
        catch (Exception exception)
        {
            RestoreStatus(previousStatus);
            ErrorMessage = exception.Message;
        }
        finally
        {
            _isSaving = false;
            ((AsyncRelayCommand)PresentCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)AbsentCommand).RaiseCanExecuteChanged();
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
