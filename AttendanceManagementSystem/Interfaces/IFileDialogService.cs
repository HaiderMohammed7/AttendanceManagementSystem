namespace AttendanceManagementSystem.Interfaces;

public interface IFileDialogService
{
    string? SelectCsvFile();

    string? SelectPdfSaveFile(string suggestedFileName);
}