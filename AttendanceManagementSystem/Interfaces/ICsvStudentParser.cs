using AttendanceManagementSystem.Services;

namespace AttendanceManagementSystem.Interfaces;

public interface ICsvStudentParser
{
    CsvParseResult Parse(string filePath);
}