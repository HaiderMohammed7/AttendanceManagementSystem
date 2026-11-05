namespace AttendanceManagementSystem.Interfaces;

public interface IUserDialogService
{
    bool Confirm(string message, string title);

    void ShowInformation(string message, string title);

    void ShowError(string message, string title);
}