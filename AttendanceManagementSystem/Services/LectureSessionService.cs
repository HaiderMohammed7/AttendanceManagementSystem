using AttendanceManagementSystem.Data;
using AttendanceManagementSystem.Interfaces;
using AttendanceManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceManagementSystem.Services;

public sealed class LectureSessionService(AttendanceDbContext dbContext) : ILectureSessionService
{
    public async Task<IReadOnlyList<LectureSession>> GetSessionsAsync(int courseId, CancellationToken cancellationToken = default)
    {
        return await dbContext.LectureSessions
            .AsNoTracking()
            .Where(session => session.CourseId == courseId)
            .Include(session => session.AttendanceRecords)
            .OrderByDescending(session => session.LectureDate)
            .ThenByDescending(session => session.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<LectureSession?> GetSessionAsync(int courseId, int sessionId, CancellationToken cancellationToken = default)
    {
        return dbContext.LectureSessions
            .AsNoTracking()
            .Where(session => session.CourseId == courseId && session.Id == sessionId)
            .Include(session => session.AttendanceRecords)
                .ThenInclude(record => record.Student)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<LectureSession> CreateSessionAsync(int courseId, DateTime lectureDate, string? title, CancellationToken cancellationToken = default)
    {
        var activeStudents = await dbContext.Students
            .Where(student => student.CourseId == courseId && student.IsActive)
            .OrderBy(student => student.FullName)
            .ThenBy(student => student.UniversityId)
            .ToListAsync(cancellationToken);

        if (activeStudents.Count == 0)
        {
            throw new InvalidOperationException("No active students are available for this course. Add or activate students before creating a lecture session.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var session = new LectureSession
        {
            CourseId = courseId,
            LectureDate = lectureDate.Date,
            Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            Status = LectureSessionStatus.Draft
        };

        foreach (var student in activeStudents)
        {
            session.AttendanceRecords.Add(new AttendanceRecord
            {
                StudentId = student.Id,
                Status = null
            });
        }

        dbContext.LectureSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return session;
    }
}