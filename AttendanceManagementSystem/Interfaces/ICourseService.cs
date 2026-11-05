using AttendanceManagementSystem.Models;

namespace AttendanceManagementSystem.Interfaces;

public interface ICourseService
{
    Task<Course?> GetCourseAsync(CancellationToken cancellationToken = default);

    Task<Course> SaveCourseAsync(int? courseId, string courseName, string instructorName, string academicYear, string semester, CancellationToken cancellationToken = default);
}