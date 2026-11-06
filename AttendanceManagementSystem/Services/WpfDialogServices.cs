using System.Windows;
using AttendanceManagementSystem.Interfaces;
using Microsoft.Win32;

namespace AttendanceManagementSystem.Services;

public sealed class WpfFileDialogService : IFileDialogService
{
    public string? SelectCsvFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            DefaultExt = ".csv",
            CheckFileExists = true,
            Multiselect = false,
            Title = "Select student CSV file"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SelectPdfSaveFile(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            AddExtension = true,
            FileName = suggestedFileName,
            OverwritePrompt = true,
            Title = "Save PDF report"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}

public sealed class WpfUserDialogService : IUserDialogService
{
    public bool Confirm(string message, string title)
    {
        return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    public void ShowInformation(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowError(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
