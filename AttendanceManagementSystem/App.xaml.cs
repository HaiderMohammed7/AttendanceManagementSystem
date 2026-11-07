using System.Windows;
using AttendanceManagementSystem.Data;
using AttendanceManagementSystem.ViewModels;
using AttendanceManagementSystem.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;

namespace AttendanceManagementSystem;

public partial class App : Application
{
    private IHost? _host;
    private IServiceScope? _applicationScope;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory).ConfigureLogging(logging => logging.AddDebug())
            .ConfigureServices((context, services) =>
            {
                services.AddDbContext<AttendanceDbContext>(options =>
                    options.UseSqlServer(context.Configuration.GetConnectionString("DefaultConnection")));
                services.AddScoped<Interfaces.ICourseService, Services.CourseService>();
                services.AddScoped<Interfaces.IStudentService, Services.StudentService>();
                services.AddScoped<Interfaces.ILectureSessionService, Services.LectureSessionService>();
                services.AddScoped<Interfaces.IStatisticsService, Services.StatisticsService>();
                services.AddScoped<Interfaces.IReportService, Services.ReportService>();
                services.AddSingleton<Interfaces.IPdfExportService, Services.QuestPdfExportService>();
                services.AddSingleton<Interfaces.ICsvStudentParser, Services.CsvStudentParser>();
                services.AddSingleton<Interfaces.IFileDialogService, Services.WpfFileDialogService>();
                services.AddSingleton<Interfaces.IUserDialogService, Services.WpfUserDialogService>();
                services.AddSingleton<Interfaces.IDatabaseBackupService, Services.SqlServerDatabaseBackupService>();
                services.AddSingleton<Interfaces.IApplicationRestartService, Services.WpfApplicationRestartService>();
                services.AddScoped<CourseSetupViewModel>();
                services.AddScoped<DashboardViewModel>();
                services.AddScoped<StudentManagementViewModel>();
                services.AddScoped<LectureSessionListViewModel>();
                services.AddScoped<LectureSessionCreateViewModel>();
                services.AddScoped<LectureSessionDetailsViewModel>();
                services.AddScoped<MainViewModel>();
                services.AddScoped<MainWindow>();
            }).Build();
        await _host.StartAsync();

        try
        {
            await using var scope = _host.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AttendanceDbContext>();
            await dbContext.Database.MigrateAsync();
        }
        catch (Exception exception)
        {
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "startup-diagnostics.log"), $"[{DateTimeOffset.Now:O}] Database initialization failed.{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
            MessageBox.Show($"The database could not be initialized. Verify that SQL Server is installed, running, and that your Windows account has access.\n\nDetails: {exception.Message}","Database Initialization Failed", MessageBoxButton.OK, MessageBoxImage.Error);

            Shutdown(1);
            return;
        }

        try
        {
            _applicationScope = _host.Services.CreateScope();
            var mainViewModel = _applicationScope.ServiceProvider.GetRequiredService<MainViewModel>();
            await mainViewModel.InitializeAsync();
            _applicationScope.ServiceProvider.GetRequiredService<MainWindow>().Show();
        }
        catch (Exception exception)
        {
            MessageBox.Show($"The course information could not be loaded. Please try again.\n\nDetails: {exception.Message}","Course Loading Failed", MessageBoxButton.OK, MessageBoxImage.Error);

            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            _applicationScope?.Dispose();
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}