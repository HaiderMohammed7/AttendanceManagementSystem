using System.Globalization;
using System.IO;
using System.Text;
using AttendanceManagementSystem.Interfaces;
using CsvHelper;
using CsvHelper.Configuration;

namespace AttendanceManagementSystem.Services;

public sealed class CsvStudentParser : ICsvStudentParser
{
    private static readonly string[] RequiredHeaders = ["UniversityId", "FullName"];

    public CsvParseResult Parse(string filePath)
    {
        using var reader = new StreamReader(filePath, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), detectEncodingFromByteOrderMarks: true);

        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        csv.Context.Configuration.TrimOptions = TrimOptions.Trim;
        csv.Context.Configuration.HeaderValidated = null;
        csv.Context.Configuration.MissingFieldFound = null;
        csv.Context.Configuration.BadDataFound = null;
        if (!csv.Read()) throw new InvalidDataException("The CSV file is empty.");

        csv.ReadHeader();
        var headers = (csv.HeaderRecord ?? []).Select(header => header.Trim().TrimStart('\uFEFF')).ToArray();

        var missingHeaders = RequiredHeaders.Where(required => !headers.Contains(required, StringComparer.OrdinalIgnoreCase)).ToArray();

        if (missingHeaders.Length > 0)
        {
            throw new InvalidDataException( $"The CSV file must contain these columns: {string.Join(", ", RequiredHeaders)}.");
        }

        var rows = new List<CsvStudentRow>();
        var errors = new List<CsvRowError>();

        while (csv.Read())
        {
            var rowNumber = csv.Parser.Row;
            try
            {
                var universityId = csv.GetField("UniversityId")?.Trim() ?? string.Empty;
                var fullName = csv.GetField("FullName")?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(universityId))
                {
                    errors.Add(new CsvRowError(rowNumber, "UniversityId is required."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(fullName))
                {
                    errors.Add(new CsvRowError(rowNumber, "FullName is required."));
                    continue;
                }

                rows.Add(new CsvStudentRow(rowNumber, universityId, fullName));
            }
            catch (Exception exception) when (exception is CsvHelperException or FormatException)
            {
                errors.Add(new CsvRowError(rowNumber, $"The row could not be read: {exception.Message}"));
            }
        }

        var duplicateRows = rows
            .GroupBy(row => row.UniversityId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group)
            .ToHashSet();

        foreach (var duplicateRow in duplicateRows)
        {
            errors.Add(new CsvRowError(
                duplicateRow.RowNumber,
                $"UniversityId '{duplicateRow.UniversityId}' appears more than once in the file."));
        }

        return new CsvParseResult
        {
            Rows = rows.Where(row => !duplicateRows.Contains(row)).ToArray(),
            Errors = errors.OrderBy(error => error.RowNumber).ToArray()
        };
    }
}