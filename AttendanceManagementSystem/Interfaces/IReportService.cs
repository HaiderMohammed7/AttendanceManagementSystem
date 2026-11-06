using AttendanceManagementSystem.Services;

namespace AttendanceManagementSystem.Interfaces;

public interface IReportService
{
    Task<CourseReportData> GetCourseReportAsync(int courseId, CancellationToken cancellationToken = default);

    Task<StudentReportData?> GetStudentReportAsync(int courseId, int studentId, CancellationToken cancellationToken = default);

    Task<WarningsReportData> GetWarningsReportAsync(int courseId, CancellationToken cancellationToken = default);
}