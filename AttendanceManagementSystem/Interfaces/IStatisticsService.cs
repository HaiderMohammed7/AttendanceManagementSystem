using AttendanceManagementSystem.Services;

namespace AttendanceManagementSystem.Interfaces;

public interface IStatisticsService
{
    Task<DashboardStatistics> GetDashboardStatisticsAsync(int courseId, CancellationToken cancellationToken = default);

    Task<StudentAttendanceStatistics> GetStudentStatisticsAsync(int courseId, int studentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudentAttendanceHistoryItem>> GetStudentHistoryAsync(int courseId, int studentId, CancellationToken cancellationToken = default);
}