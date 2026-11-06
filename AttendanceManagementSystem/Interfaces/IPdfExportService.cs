using AttendanceManagementSystem.Services;

namespace AttendanceManagementSystem.Interfaces;

public interface IPdfExportService
{
    Task ExportCourseReportAsync(CourseReportData report, string filePath, CancellationToken cancellationToken = default);

    Task ExportStudentReportAsync(StudentReportData report, string filePath, CancellationToken cancellationToken = default);

    Task ExportWarningsReportAsync(WarningsReportData report, string filePath, CancellationToken cancellationToken = default);
}