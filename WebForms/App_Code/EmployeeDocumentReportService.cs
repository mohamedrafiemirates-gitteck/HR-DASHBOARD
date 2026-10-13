using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;

namespace EmployeeDocumentTracker
{
    public sealed class EmployeeDocumentReportService
    {
        private readonly string _connectionString;

        public EmployeeDocumentReportService()
        {
            var setting = ConfigurationManager.ConnectionStrings["PayrollDb"];
            var full = setting == null ? null : setting.ConnectionString;
            if (string.IsNullOrWhiteSpace(full) || full.IndexOf("SQLSERVER", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException("Set the PayrollDb connection string in ConnectionStrings.config.");
            _connectionString = full;
        }

        public List<string> GetCompanies()
        {
            const string query =
                "SELECT CompID FROM dbo.EmployMast WHERE CompID IS NOT NULL GROUP BY CompID " +
                "ORDER BY SUM(CASE WHEN ResigDate IS NULL THEN 1 ELSE 0 END) DESC, CompID;";
            var companies = new List<string>();
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(query, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var company = reader.GetString(0).Trim();
                        if (!string.IsNullOrWhiteSpace(company)) companies.Add(company);
                    }
                }
            }
            return companies;
        }

        public EmployeeDocumentReportViewModel GetReport(string companyId, int expiryDays)
        {
            expiryDays = (expiryDays == 30 || expiryDays == 60 || expiryDays == 90 || expiryDays == 180) ? expiryDays : 90;
            var model = new EmployeeDocumentReportViewModel
            {
                SelectedCompany = companyId,
                ExpiryWindowDays = expiryDays,
                ReportDate = DateTime.Today
            };

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                const string typesQuery = "SELECT Code, Name FROM dbo.Document WHERE CompID = @CompanyId ORDER BY Code;";
                using (var command = new SqlCommand(typesQuery, connection))
                {
                    command.Parameters.AddWithValue("@CompanyId", companyId);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var code = reader.GetString(0).Trim();
                            if (!string.IsNullOrWhiteSpace(code) && !HasType(model, code))
                            {
                                model.DocumentTypes.Add(new DocumentTypeViewModel(code, reader.GetString(1).Trim()));
                            }
                        }
                    }
                }

                const string rowsQuery =
                    "SELECT e.EmpNo, e.Fname, e.Mname, e.LName, e.Dept, e.Status, " +
                    "       d.DocCode, d.DocNum, d.IssueDt, d.ExpDt, d.AddInfo " +
                    "FROM dbo.EmployMast AS e " +
                    "LEFT JOIN dbo.vw_EmployeeExpiryDocuments AS d " +
                    "    ON d.CompID = e.CompID AND d.EmpNo = e.EmpNo " +
                    "WHERE e.CompID = @CompanyId AND e.ResigDate IS NULL " +
                    "ORDER BY e.Fname, e.LName, e.EmpNo, d.DocCode, d.ExpDt DESC;";
                var employees = new Dictionary<string, EmployeeDocumentRowViewModel>(StringComparer.OrdinalIgnoreCase);
                using (var command = new SqlCommand(rowsQuery, connection))
                {
                    command.Parameters.AddWithValue("@CompanyId", companyId);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var number = ReadText(reader, "EmpNo");
                            if (string.IsNullOrWhiteSpace(number)) continue;

                            EmployeeDocumentRowViewModel employee;
                            if (!employees.TryGetValue(number, out employee))
                            {
                                var parts = new[] { ReadText(reader, "Fname"), ReadText(reader, "Mname"), ReadText(reader, "LName") };
                                employee = new EmployeeDocumentRowViewModel
                                {
                                    EmployeeNumber = number,
                                    FullName = string.Join(" ", parts.Where(part => part.Length > 0).ToArray()),
                                    Department = ReadText(reader, "Dept"),
                                    EmployeeStatus = ReadText(reader, "Status")
                                };
                                employees.Add(number, employee);
                            }

                            var docCode = ReadText(reader, "DocCode");
                            if (string.IsNullOrWhiteSpace(docCode)) continue;
                            if (!HasType(model, docCode))
                            {
                                model.DocumentTypes.Add(new DocumentTypeViewModel(docCode, docCode));
                            }

                            var cell = new EmployeeDocumentCellViewModel
                            {
                                DocumentNumber = ReadText(reader, "DocNum"),
                                IssueDate = ReadDate(reader, "IssueDt"),
                                ExpiryDate = ReadDate(reader, "ExpDt"),
                                AddInfo = ReadText(reader, "AddInfo")
                            };
                            EmployeeDocumentCellViewModel existing;
                            if (!employee.Documents.TryGetValue(docCode, out existing) || IsPreferred(cell, existing))
                            {
                                employee.Documents[docCode] = cell;
                            }
                        }
                    }
                }

                model.DocumentTypes = model.DocumentTypes.OrderBy(type => type.Code, StringComparer.OrdinalIgnoreCase).ToList();
                model.Employees = employees.Values
                    .OrderBy(employee => employee.FullName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(employee => employee.EmployeeNumber, StringComparer.Ordinal)
                    .ToList();
            }

            BuildDashboard(model);
            return model;
        }

        private static bool HasType(EmployeeDocumentReportViewModel model, string code)
        {
            return model.DocumentTypes.Any(type => type.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        }

        private static void BuildDashboard(EmployeeDocumentReportViewModel model)
        {
            var today = model.ReportDate.Date;
            var cutoff = today.AddDays(model.ExpiryWindowDays);
            var yearEnd = today.AddMonths(12);

            var labels = new[] { "Current", "Expiring", "Expired", "Missing", "Incomplete", "No expiry" };
            var colors = new[] { "#147d64", "#e3a32e", "#cf5545", "#c8d2d0", "#5b7f92", "#7e9a59" };
            var counts = new Dictionary<string, int>();
            foreach (var label in labels) counts[label] = 0;

            var months = new SortedDictionary<DateTime, int>();
            var missingTypes = new List<MissingByTypeViewModel>();
            foreach (var type in model.DocumentTypes)
            {
                var missing = 0;
                foreach (var employee in model.Employees)
                {
                    EmployeeDocumentCellViewModel cell;
                    if (!employee.Documents.TryGetValue(type.Code, out cell))
                    {
                        missing++;
                        counts["Missing"]++;
                        continue;
                    }

                    cell.State = GetState(cell, today, cutoff);
                    counts[cell.State]++;
                    if (cell.State == "Incomplete") missing++;
                    if (cell.ExpiryDate.HasValue && cell.ExpiryDate.Value.Date >= today && cell.ExpiryDate.Value.Date <= yearEnd)
                    {
                        var expiry = cell.ExpiryDate.Value;
                        var month = new DateTime(expiry.Year, expiry.Month, 1);
                        int current;
                        months.TryGetValue(month, out current);
                        months[month] = current + 1;
                    }
                }
                missingTypes.Add(new MissingByTypeViewModel(type.Name, missing));
            }

            model.StatusBreakdown = labels.Select((label, index) => new DocumentStatusViewModel(label, counts[label], colors[index])).ToList();
            model.MissingDocumentSlots = counts["Missing"] + counts["Incomplete"];
            model.EmployeesWithMissingDocuments = model.Employees.Count(employee => model.DocumentTypes.Any(type =>
            {
                EmployeeDocumentCellViewModel cell;
                return !employee.Documents.TryGetValue(type.Code, out cell) || cell.State == "Missing" || cell.State == "Incomplete";
            }));
            model.ExpiringDocuments = counts["Expiring"];
            model.ExpiredDocuments = counts["Expired"];
            model.ExpiriesByMonth = months.Select(item => new ExpiryMonthViewModel(item.Key.ToString("MMM yy", CultureInfo.InvariantCulture), item.Value)).ToList();
            model.MissingByType = missingTypes.Where(item => item.Count > 0).OrderByDescending(item => item.Count).ThenBy(item => item.Label, StringComparer.Ordinal).Take(8).ToList();
        }

        private static string GetState(EmployeeDocumentCellViewModel cell, DateTime today, DateTime cutoff)
        {
            if (!cell.ExpiryDate.HasValue) return string.IsNullOrWhiteSpace(cell.DocumentNumber) ? "Incomplete" : "No expiry";
            var expiry = cell.ExpiryDate.Value;
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

        private static string ReadText(SqlDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal)) return string.Empty;
            var text = Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
            return text == null ? string.Empty : text.Trim();
        }

        private static DateTime? ReadDate(SqlDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal)) return null;
            return Convert.ToDateTime(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }
    }
}
