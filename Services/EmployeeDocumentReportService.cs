using System.Globalization;
using EmployeeDocumentTracker.Models;
using Microsoft.Data.SqlClient;

namespace EmployeeDocumentTracker.Services;

public sealed class EmployeeDocumentReportService(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("PayrollDb")
        ?? throw new InvalidOperationException("Connection string 'PayrollDb' is not configured.");

    public async Task<List<string>> GetCompaniesAsync(CancellationToken cancellationToken)
    {
        const string query = """
            SELECT CompID
            FROM dbo.EmployMast
            WHERE CompID IS NOT NULL
            GROUP BY CompID
            ORDER BY SUM(CASE WHEN ResigDate IS NULL THEN 1 ELSE 0 END) DESC, CompID;
            """;
        var companies = new List<string>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var company = reader.GetString(0).Trim();
            if (!string.IsNullOrWhiteSpace(company)) companies.Add(company);
        }
        return companies;
    }

    public async Task<EmployeeDocumentReportViewModel> GetReportAsync(string companyId, int expiryDays, CancellationToken cancellationToken)
    {
        expiryDays = expiryDays is 30 or 60 or 90 or 180 ? expiryDays : 90;
        var model = new EmployeeDocumentReportViewModel
        {
            SelectedCompany = companyId,
            ExpiryWindowDays = expiryDays,
            ReportDate = DateTime.Today
        };
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string typesQuery = "SELECT Code, Name FROM dbo.Document WHERE CompID = @CompanyId ORDER BY Name, Code;";
        await using (var command = new SqlCommand(typesQuery, connection))
        {
            command.Parameters.AddWithValue("@CompanyId", companyId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var code = reader.GetString(0).Trim();
                if (!string.IsNullOrWhiteSpace(code) && !model.DocumentTypes.Any(type => type.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
                {
                    model.DocumentTypes.Add(new DocumentTypeViewModel(code, reader.GetString(1).Trim()));
                }
            }
        }

        const string rowsQuery = """
            SELECT e.EmpNo, e.Fname, e.Mname, e.LName, e.Dept, e.Status,
                   d.DocCode, d.DocNum, d.IssueDt, d.ExpDt, d.AddInfo
            FROM dbo.EmployMast AS e
            LEFT JOIN dbo.vw_EmployeeExpiryDocuments AS d
                ON d.CompID = e.CompID AND d.EmpNo = e.EmpNo
            WHERE e.CompID = @CompanyId AND e.ResigDate IS NULL
            ORDER BY e.Fname, e.LName, e.EmpNo, d.DocCode, d.ExpDt DESC;
            """;
        var employees = new Dictionary<string, EmployeeDocumentRowViewModel>(StringComparer.OrdinalIgnoreCase);
        await using (var command = new SqlCommand(rowsQuery, connection))
        {
            command.Parameters.AddWithValue("@CompanyId", companyId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var number = ReadText(reader, "EmpNo");
                if (string.IsNullOrWhiteSpace(number)) continue;
                if (!employees.TryGetValue(number, out var employee))
                {
                    var first = ReadText(reader, "Fname");
                    var middle = ReadText(reader, "Mname");
                    var last = ReadText(reader, "LName");
                    employee = new EmployeeDocumentRowViewModel
                    {
                        EmployeeNumber = number,
                        FullName = string.Join(' ', new[] { first, middle, last }.Where(part => part.Length > 0)),
                        Department = ReadText(reader, "Dept"),
                        EmployeeStatus = ReadText(reader, "Status")
                    };
                    employees.Add(number, employee);
                }

                var code = ReadText(reader, "DocCode");
                if (string.IsNullOrWhiteSpace(code)) continue;
                if (!model.DocumentTypes.Any(type => type.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
                {
                    model.DocumentTypes.Add(new DocumentTypeViewModel(code, code.ToUpperInvariant() switch
                    {
                        "CONT" => "Contract end",
                        "PB" => "Probation end",
                        _ => code
                    }));
                }
                var cell = new EmployeeDocumentCellViewModel
                {
                    DocumentNumber = ReadText(reader, "DocNum"),
                    IssueDate = ReadDate(reader, "IssueDt"),
                    ExpiryDate = ReadDate(reader, "ExpDt"),
                    AddInfo = ReadText(reader, "AddInfo")
                };
                if (!employee.Documents.TryGetValue(code, out var existing) || IsPreferred(cell, existing))
                {
                    employee.Documents[code] = cell;
                }
            }
        }

        model.DocumentTypes = model.DocumentTypes.OrderBy(type => type.Name, StringComparer.OrdinalIgnoreCase).ThenBy(type => type.Code).ToList();
        model.Employees = employees.Values.OrderBy(employee => employee.FullName, StringComparer.OrdinalIgnoreCase).ThenBy(employee => employee.EmployeeNumber).ToList();
        BuildDashboard(model);
        return model;
    }

    private static void BuildDashboard(EmployeeDocumentReportViewModel model)
    {
        var today = model.ReportDate.Date;
        var cutoff = today.AddDays(model.ExpiryWindowDays);
        var yearEnd = today.AddMonths(12);
        var counts = new Dictionary<string, (int Count, string Color)>
        {
            ["Current"] = (0, "#147d64"), ["Expiring"] = (0, "#e3a32e"), ["Expired"] = (0, "#cf5545"),
            ["Missing"] = (0, "#c8d2d0"), ["Incomplete"] = (0, "#5b7f92"), ["No expiry"] = (0, "#7e9a59")
        };
        var months = new SortedDictionary<DateTime, int>();
        var missingTypes = new List<MissingByTypeViewModel>();
        foreach (var type in model.DocumentTypes)
        {
            var missing = 0;
            foreach (var employee in model.Employees)
            {
                if (!employee.Documents.TryGetValue(type.Code, out var cell))
                {
                    missing++;
                    Increment(counts, "Missing");
                    continue;
                }
                cell.State = GetState(type.Code, cell, today, cutoff);
                Increment(counts, cell.State);
                if (cell.State == "Incomplete") missing++;
                if (cell.ExpiryDate is DateTime expiry && expiry.Date >= today && expiry.Date <= yearEnd)
                {
                    var month = new DateTime(expiry.Year, expiry.Month, 1);
                    months[month] = months.GetValueOrDefault(month) + 1;
                }
            }
            missingTypes.Add(new MissingByTypeViewModel(type.Name, missing));
        }

        model.StatusBreakdown = counts.Select(item => new DocumentStatusViewModel(item.Key, item.Value.Count, item.Value.Color)).ToList();
        model.MissingDocumentSlots = counts["Missing"].Count + counts["Incomplete"].Count;
        model.EmployeesWithMissingDocuments = model.Employees.Count(employee => model.DocumentTypes.Any(type =>
            !employee.Documents.TryGetValue(type.Code, out var cell) || cell.State is "Missing" or "Incomplete"));
        model.ExpiringDocuments = counts["Expiring"].Count;
        model.ExpiredDocuments = counts["Expired"].Count;
        model.ExpiriesByMonth = months.Select(item => new ExpiryMonthViewModel(item.Key.ToString("MMM yy", CultureInfo.InvariantCulture), item.Value)).ToList();
        model.MissingByType = missingTypes.Where(item => item.Count > 0).OrderByDescending(item => item.Count).ThenBy(item => item.Label).Take(8).ToList();
    }

    private static string GetState(string code, EmployeeDocumentCellViewModel cell, DateTime today, DateTime cutoff)
    {
        if (string.IsNullOrWhiteSpace(cell.DocumentNumber) && code is not ("CONT" or "PB")) return "Incomplete";
        if (cell.ExpiryDate is not DateTime expiry) return string.IsNullOrWhiteSpace(cell.DocumentNumber) ? "Incomplete" : "No expiry";
        if (expiry.Date < today) return "Expired";
        if (expiry.Date <= cutoff) return "Expiring";
        return "Current";
    }

    private static bool IsPreferred(EmployeeDocumentCellViewModel candidate, EmployeeDocumentCellViewModel existing)
    {
        if (candidate.ExpiryDate.HasValue != existing.ExpiryDate.HasValue) return candidate.ExpiryDate.HasValue;
        if (candidate.ExpiryDate.HasValue && candidate.ExpiryDate != existing.ExpiryDate) return candidate.ExpiryDate > existing.ExpiryDate;
        return !string.IsNullOrWhiteSpace(candidate.DocumentNumber) && string.IsNullOrWhiteSpace(existing.DocumentNumber);
    }

    private static void Increment(Dictionary<string, (int Count, string Color)> counts, string state)
    {
        var entry = counts[state];
        counts[state] = (entry.Count + 1, entry.Color);
    }

    private static string ReadText(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
    }

    private static DateTime? ReadDate(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }
}