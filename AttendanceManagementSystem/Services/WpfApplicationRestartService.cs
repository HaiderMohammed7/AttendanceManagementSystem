using System.Diagnostics;
using AttendanceManagementSystem.Interfaces;

namespace AttendanceManagementSystem.Services;

public sealed class WpfApplicationRestartService : IApplicationRestartService
{
    public void Restart()
    {
        var executablePath = Environment.ProcessPath ?? throw new InvalidOperationException("The application executable path is unavailable.");

        Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = true
        });

        System.Windows.Application.Current.Shutdown();
    }
}