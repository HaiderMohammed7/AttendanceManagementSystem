using AttendanceManagementSystem.Data;
using AttendanceManagementSystem.Interfaces;
using AttendanceManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceManagementSystem.Services;

public sealed class CourseService(AttendanceDbContext dbContext) : ICourseService
{
    public Task<Course?> GetCourseAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.Courses.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<Course> SaveCourseAsync(int? courseId, string courseName, string instructorName, string academicYear, string semester, CancellationToken cancellationToken = default)
    {
        Course? course = courseId.HasValue ? await dbContext.Courses.SingleOrDefaultAsync(course => course.Id == courseId.Value, cancellationToken) : await dbContext.Courses.SingleOrDefaultAsync(cancellationToken);

        if (course is null)
        {
            course = new Course();
            dbContext.Courses.Add(course);
        }

        course.CourseName = courseName.Trim();
        course.InstructorName = instructorName.Trim();
        course.AcademicYear = academicYear.Trim();
        course.Semester = semester.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);
        return course;
    }
}