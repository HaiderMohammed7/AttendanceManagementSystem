using AttendanceManagementSystem.Data;
using AttendanceManagementSystem.Interfaces;
using AttendanceManagementSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.IO;

namespace AttendanceManagementSystem.Services;

public sealed class StatisticsService(AttendanceDbContext dbContext,ILogger<StatisticsService> logger) : IStatisticsService
{
    private static readonly string DiagnosticLogPath = Path.Combine(AppContext.BaseDirectory, "statistics-diagnostics.log");

    public async Task<DashboardStatistics> GetDashboardStatisticsAsync(int courseId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Dashboard statistics started. CourseId={CourseId}, Database={Database}, DataSource={DataSource}", courseId, dbContext.Database.GetDbConnection().Database,dbContext.Database.GetDbConnection().DataSource);
        WriteDiagnostic($"Dashboard statistics started. CourseId={courseId}, Database={dbContext.Database.GetDbConnection().Database}, DataSource={dbContext.Database.GetDbConnection().DataSource}");

        var activeStudentsQuery = dbContext.Students.Where(student => student.CourseId == courseId && student.IsActive);
        var activeStudents = await ExecuteQueryAsync("Active students count",activeStudentsQuery, query => query.CountAsync(cancellationToken),cancellationToken);

        var sessionCountsQuery = dbContext.LectureSessions.Where(session => session.CourseId == courseId).GroupBy(session => session.Status).Select(group => new SessionCount(group.Key, group.Count()));
        var sessionCounts = await ExecuteQueryAsync("Lecture session counts",sessionCountsQuery, query => query.ToListAsync(cancellationToken),cancellationToken);

        var attendanceCountsQuery = dbContext.AttendanceRecords.Where(record => record.LectureSession.CourseId == courseId && record.LectureSession.Status == LectureSessionStatus.Completed).GroupBy(record => record.Status).Select(group => new AttendanceCount(group.Key, group.Count()));
        var attendanceCounts = await ExecuteQueryAsync("Completed attendance counts",attendanceCountsQuery,query => query.ToListAsync(cancellationToken),cancellationToken);

        var absenceCounts = await dbContext.AttendanceRecords
            .Where(record => record.LectureSession.CourseId == courseId && record.LectureSession.Status == LectureSessionStatus.Completed && record.Status == AttendanceStatus.Absent)
            .GroupBy(record => record.StudentId)
            .Select(group => new { StudentId = group.Key,AbsenceCount = group.Count() }).Where(item => item.AbsenceCount >= 3).ToListAsync(cancellationToken);
       
        var studentIds = absenceCounts.Select(item => item.StudentId).ToList();

        var students = await dbContext.Students
            .Where(student =>student.CourseId == courseId && student.IsActive &&studentIds.Contains(student.Id))
            .Select(student => new{ student.Id, student.UniversityId, student.FullName}).ToListAsync(cancellationToken);

        var warnings = students
            .Join(absenceCounts,student => student.Id,absence => absence.StudentId,
             (student, absence) => new StudentWarning(student.Id,student.UniversityId,student.FullName,absence.AbsenceCount))
            .OrderByDescending(warning => warning.AbsenceCount).ThenBy(warning => warning.FullName).ToList();

        logger.LogInformation("Dashboard statistics completed. CourseId={CourseId}, ActiveStudents={ActiveStudents}, Sessions={SessionCount}, AttendanceGroups={AttendanceGroupCount}, Warnings={WarningCount}", courseId, activeStudents, sessionCounts.Count, attendanceCounts.Count, warnings.Count);

        return new DashboardStatistics(activeStudents,
            sessionCounts.SingleOrDefault(item => item.Status == LectureSessionStatus.Completed)?.Count ?? 0,
            sessionCounts.SingleOrDefault(item => item.Status == LectureSessionStatus.Draft)?.Count ?? 0,
            attendanceCounts.SingleOrDefault(item => item.Status == AttendanceStatus.Present)?.Count ?? 0,
            attendanceCounts.SingleOrDefault(item => item.Status == AttendanceStatus.Absent)?.Count ?? 0,
            warnings.Count,warnings);
    }

    private async Task<TResult> ExecuteQueryAsync<TQuery, TResult>(string operation,IQueryable<TQuery> query,Func<IQueryable<TQuery>, Task<TResult>> execute, CancellationToken cancellationToken)
    {
        logger.LogInformation("Statistics query started. Operation={Operation}, SQL={Sql}", operation, query.ToQueryString());
        try
        {
            var result = await execute(query);
            logger.LogInformation("Statistics query succeeded. Operation={Operation}, Result={Result}", operation, FormatResult(result));
            return result;
        }
        catch (Exception exception)
        {
            var sql = GetSqlSafely(query, operation);
            logger.LogError(exception, "Statistics query failed. Operation={Operation}, SQL={Sql}", operation, sql);
            WriteDiagnostic($"QUERY FAILED: {operation}{Environment.NewLine}{exception}");
            throw;
        }
    }

    private static string GetSqlSafely<T>(IQueryable<T> query, string operation)
    {
        try
        {
            return query.ToQueryString();
        }
        catch (Exception exception)
        {
            return $"<SQL generation failed for {operation}: {exception}>";
        }
    }

    private static void WriteDiagnostic(string message)
    {
        try
        {
            File.AppendAllText(DiagnosticLogPath, $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private static string FormatResult<T>(T result) => result switch
    {
        System.Collections.ICollection collection => $"Count={collection.Count}",
        _ => result?.ToString() ?? "null"
    };

    private sealed record SessionCount(LectureSessionStatus Status, int Count);
    private sealed record AttendanceCount(AttendanceStatus? Status, int Count);

    public async Task<StudentAttendanceStatistics> GetStudentStatisticsAsync(int courseId, int studentId, CancellationToken cancellationToken = default)
    {
        var counts = await dbContext.AttendanceRecords
            .Where(record => record.StudentId == studentId && record.Student.CourseId == courseId && record.LectureSession.Status == LectureSessionStatus.Completed)
            .GroupBy(record => record.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return new StudentAttendanceStatistics(counts.SingleOrDefault(item => item.Status == AttendanceStatus.Present)?.Count ?? 0, counts.SingleOrDefault(item => item.Status == AttendanceStatus.Absent)?.Count ?? 0);
    }

    public async Task<IReadOnlyList<StudentAttendanceHistoryItem>> GetStudentHistoryAsync(int courseId, int studentId, CancellationToken cancellationToken = default)
    {
        return await dbContext.AttendanceRecords.AsNoTracking()
            .Where(record => record.StudentId == studentId && record.Student.CourseId == courseId)
            .OrderByDescending(record => record.LectureSession.LectureDate)
            .ThenByDescending(record => record.LectureSession.Id)
            .Select(record => new StudentAttendanceHistoryItem(record.LectureSession.LectureDate, record.LectureSession.Title ?? "Untitled lecture", record.LectureSession.Status, record.Status))
            .ToListAsync(cancellationToken);
    }
}