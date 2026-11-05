using AttendanceManagementSystem.Models;

namespace AttendanceManagementSystem.Interfaces;

public interface ILectureSessionService
{
    Task<IReadOnlyList<LectureSession>> GetSessionsAsync(int courseId, CancellationToken cancellationToken = default);

    Task<LectureSession?> GetSessionAsync(int courseId, int sessionId, CancellationToken cancellationToken = default);

    Task<LectureSession> CreateSessionAsync(int courseId, DateTime lectureDate, string? title,CancellationToken cancellationToken = default);
}