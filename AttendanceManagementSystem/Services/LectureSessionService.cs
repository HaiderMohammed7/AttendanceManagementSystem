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

    public async Task UpdateAttendanceAsync(
        int courseId,
        int sessionId,
        int attendanceRecordId,
        AttendanceStatus status,
        CancellationToken cancellationToken = default)
    {
        var record = await dbContext.AttendanceRecords
            .Include(record => record.LectureSession)
            .SingleOrDefaultAsync(
                record => record.Id == attendanceRecordId &&
                          record.LectureSessionId == sessionId &&
                          record.LectureSession.CourseId == courseId,
                cancellationToken);

        if (record is null)
        {
            throw new InvalidOperationException("The attendance record could not be found.");
        }

        if (record.LectureSession.Status != LectureSessionStatus.Draft)
        {
            throw new InvalidOperationException("Completed lecture sessions are read-only.");
        }

        record.Status = status;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteSessionAsync(int courseId, int sessionId, CancellationToken cancellationToken = default)
    {
        var session = await dbContext.LectureSessions
            .Include(session => session.AttendanceRecords)
            .SingleOrDefaultAsync(
                session => session.Id == sessionId && session.CourseId == courseId,
                cancellationToken);

        if (session is null)
        {
            throw new InvalidOperationException("The lecture session could not be found.");
        }

        if (session.Status != LectureSessionStatus.Draft)
        {
            throw new InvalidOperationException("The lecture session is already completed and read-only.");
        }

        if (session.AttendanceRecords.Any(record => record.Status is null))
        {
            throw new InvalidOperationException("Every student must be marked Present or Absent before completing the lecture.");
        }

        session.Status = LectureSessionStatus.Completed;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}