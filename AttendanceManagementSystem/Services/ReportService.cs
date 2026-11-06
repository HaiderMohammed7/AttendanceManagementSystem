using AttendanceManagementSystem.Data;
using AttendanceManagementSystem.Interfaces;
using AttendanceManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceManagementSystem.Services;

public sealed class ReportService(AttendanceDbContext dbContext) : IReportService
{
    public async Task<CourseReportData> GetCourseReportAsync(int courseId, CancellationToken cancellationToken = default)
    {
        var course = await GetCourseInfoAsync(courseId, cancellationToken);

        var attendanceCounts = await GetAttendanceCountsAsync(courseId, cancellationToken);

        var studentData = await dbContext.Students.AsNoTracking().Where(student => student.CourseId == courseId)
            .Select(student => new
            {
                student.Id,
                student.UniversityId,
                student.FullName,
                student.IsActive
            }).ToListAsync(cancellationToken);

        var students = studentData.Select(student =>
            {
                attendanceCounts.TryGetValue(student.Id, out var counts);

                return new ReportStudentAttendance(student.UniversityId,student.FullName, student.IsActive, counts.Present, counts.Absent);
            }).OrderBy(student => student.UniversityId).ToList();

        var completedLectures = await dbContext.LectureSessions.CountAsync(session => session.CourseId == courseId &&session.Status == LectureSessionStatus.Completed,cancellationToken);

        var totalPresent = students.Sum(student => student.PresentCount);
        var totalAbsent = students.Sum(student => student.AbsentCount);
        var warningsCount = students.Count(student => student.IsActive && student.AbsentCount >= 3);

        return new CourseReportData(course, DateTime.Now, students.Count, students.Count(student => student.IsActive), students.Count(student => !student.IsActive), completedLectures,totalPresent, totalAbsent, warningsCount, students);
    }

    public async Task<StudentReportData?> GetStudentReportAsync(int courseId, int studentId, CancellationToken cancellationToken = default)
    {
        var course = await GetCourseInfoAsync(courseId, cancellationToken);
        var student = await dbContext.Students
            .AsNoTracking()
            .Where(student => student.Id == studentId && student.CourseId == courseId)
            .Select(student => new
            {
                student.UniversityId,
                student.FullName,
                student.IsActive,
                PresentCount = student.AttendanceRecords.Count(record => record.LectureSession.CourseId == courseId && record.LectureSession.Status == LectureSessionStatus.Completed && record.Status == AttendanceStatus.Present),
                AbsentCount = student.AttendanceRecords.Count(record => record.LectureSession.CourseId == courseId && record.LectureSession.Status == LectureSessionStatus.Completed && record.Status == AttendanceStatus.Absent)
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (student is null)
        {
            return null;
        }

        var history = await dbContext.AttendanceRecords
            .AsNoTracking()
            .Where(record => record.StudentId == studentId && record.LectureSession.CourseId == courseId && record.LectureSession.Status == LectureSessionStatus.Completed && record.Status != null)
            .OrderByDescending(record => record.LectureSession.LectureDate)
            .ThenByDescending(record => record.LectureSession.Id)
            .Select(record => new StudentReportHistory(record.LectureSession.LectureDate, record.LectureSession.Title ?? "Untitled lecture", record.Status!.Value))
            .ToListAsync(cancellationToken);

        return new StudentReportData(course, DateTime.Now, student.UniversityId, student.FullName, student.IsActive, student.PresentCount, student.AbsentCount, history);
    }

    public async Task<WarningsReportData> GetWarningsReportAsync(int courseId, CancellationToken cancellationToken = default)
    {
        var course = await GetCourseInfoAsync(courseId, cancellationToken);

        var attendanceCounts = await GetAttendanceCountsAsync(courseId, cancellationToken);

        var studentData = await dbContext.Students.AsNoTracking().Where(student => student.CourseId == courseId &&student.IsActive)
            .Select(student => new
            {
                student.Id,
                student.UniversityId,
                student.FullName
            }).ToListAsync(cancellationToken);

        var students = studentData.Select(student =>
            {
                attendanceCounts.TryGetValue(student.Id, out var counts);

                return new WarningReportStudent(student.UniversityId, student.FullName,counts.Absent, counts.Present);
            }).Where(student => student.AbsenceCount >= 3).OrderByDescending(student => student.AbsenceCount).ThenBy(student => student.FullName).ToList();

        return new WarningsReportData(course, DateTime.Now, students);
    }

    private async Task<Dictionary<int, (int Present, int Absent)>> GetAttendanceCountsAsync(int courseId, CancellationToken cancellationToken)
    {
        var counts = await dbContext.AttendanceRecords.AsNoTracking()
            .Where(record => record.LectureSession.CourseId == courseId && record.LectureSession.Status == LectureSessionStatus.Completed)
            .GroupBy(record => new
            {
                record.StudentId,
                record.Status
            })
            .Select(group => new
            {
                group.Key.StudentId,
                group.Key.Status,
                Count = group.Count()
            }).ToListAsync(cancellationToken);

        return counts.GroupBy(item => item.StudentId).ToDictionary(group => group.Key, group => (Present: group.Where(x => x.Status == AttendanceStatus.Present).Sum(x => x.Count), Absent: group.Where(x => x.Status == AttendanceStatus.Absent).Sum(x => x.Count)));
    }

    private async Task<ReportCourseInfo> GetCourseInfoAsync(int courseId, CancellationToken cancellationToken)
    {
        var course = await dbContext.Courses.AsNoTracking().Where(course => course.Id == courseId).Select(course => new ReportCourseInfo(course.CourseName, course.InstructorName, course.AcademicYear, course.Semester)).SingleOrDefaultAsync(cancellationToken);

        return course ?? throw new InvalidOperationException("The configured course could not be found.");
    }
}