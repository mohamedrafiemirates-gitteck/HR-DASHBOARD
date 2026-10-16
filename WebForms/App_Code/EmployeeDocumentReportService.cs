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

        private const string DeptNameSql =
            "ISNULL(NULLIF(LTRIM(RTRIM((SELECT TOP 1 pd.DeptName FROM dbo.vw_PayDept AS pd WHERE pd.COMPID = m.CompID AND pd.DeptCode = m.Dept))), ''), " +
            "ISNULL(NULLIF(LTRIM(RTRIM(m.Dept)), ''), '(none)'))";
        private const string DesigNameSql =
            "ISNULL(NULLIF(LTRIM(RTRIM((SELECT TOP 1 pg.DesigName FROM dbo.vw_PayDesignation AS pg WHERE pg.CompID = m.CompID AND pg.DesigCode = m.Desig))), ''), " +
            "ISNULL(m.Desig, ''))";

        private static void AddCompany(SqlCommand command, string companyId)
        {
            command.Parameters.Add("@CompanyId", System.Data.SqlDbType.VarChar, 20).Value = companyId;
        }

        public void FillSalary(EmployeeDocumentReportViewModel model, string companyId)
        {
            const string employeeQuery =
                "SELECT m.EmpNo, m.Fname, m.Mname, m.LName, " + DeptNameSql + " AS Dept, " + DesigNameSql + " AS Desig, m.DOJ " +
                "FROM dbo.EmployMast AS m WHERE m.CompID = @CompanyId AND m.ResigDate IS NULL ORDER BY Dept, m.Fname, m.EmpNo;";
            const string earnQuery =
                "SELECT e.EmpNo, e.Earn_Code, e.Value FROM dbo.EmployeeEarnings AS e " +
                "INNER JOIN dbo.EmployMast AS m ON m.CompID = e.CompID AND m.EmpNo = e.EmpNo " +
                "WHERE e.CompID = @CompanyId AND m.ResigDate IS NULL;";

            var byNumber = new Dictionary<string, SalaryEmployeeViewModel>(StringComparer.OrdinalIgnoreCase);
            var groups = new Dictionary<string, SalaryDepartmentViewModel>(StringComparer.OrdinalIgnoreCase);
            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(employeeQuery, connection))
                {
                                    AddCompany(command, companyId);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var number = ReadText(reader, "EmpNo");
                            if (number.Length == 0 || byNumber.ContainsKey(number)) continue;
                            var parts = new[] { ReadText(reader, "Fname"), ReadText(reader, "Mname"), ReadText(reader, "LName") };
                            var employee = new SalaryEmployeeViewModel
                            {
                                EmployeeNumber = number,
                                FullName = string.Join(" ", parts.Where(part => part.Length > 0).ToArray()),
                                Designation = ReadText(reader, "Desig"),
                                JoinDate = ReadDate(reader, "DOJ")
                            };
                            byNumber.Add(number, employee);
                            var dept = ReadText(reader, "Dept");
                            SalaryDepartmentViewModel group;
                            if (!groups.TryGetValue(dept, out group))
                            {
                                group = new SalaryDepartmentViewModel { Department = dept };
                                groups.Add(dept, group);
                            }
                            group.Employees.Add(employee);
                        }
                    }
                }

                using (var command = new SqlCommand(earnQuery, connection))
                {
                                    AddCompany(command, companyId);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            SalaryEmployeeViewModel employee;
                            if (!byNumber.TryGetValue(ReadText(reader, "EmpNo"), out employee)) continue;
                            var code = ReadText(reader, "Earn_Code");
                            if (code.Length == 0 || reader.IsDBNull(reader.GetOrdinal("Value"))) continue;
                            var value = reader.GetDecimal(reader.GetOrdinal("Value"));
                            decimal existing;
                            employee.Earnings.TryGetValue(code, out existing);
                            employee.Earnings[code] = existing + value;
                            codes.Add(code);
                        }
                    }
                }
            }

            model.SalaryEarnCodes = codes
                .OrderBy(code => code.Equals("BASIC", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(code => code, StringComparer.OrdinalIgnoreCase)
                .ToList();
            model.SalaryDepartments = groups.Values.OrderBy(group => group.Department, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public void FillTurnover(EmployeeDocumentReportViewModel model, string companyId, DateTime from, DateTime to, string label)
        {
            model.TurnoverFrom = from.Date;
            model.TurnoverTo = to.Date;
            model.TurnoverLabel = label;

            const string deptQuery =
                "SELECT x.Dept, SUM(x.Op) AS Opening, SUM(x.Jn) AS Joiners, SUM(x.Lv) AS Leavers, SUM(x.Cl) AS Closing FROM (" +
                "SELECT " + DeptNameSql + " AS Dept, " +
                "  CASE WHEN m.DOJ < @From AND (m.ResigDate IS NULL OR m.ResigDate >= @From) THEN 1 ELSE 0 END AS Op, " +
                "  CASE WHEN m.DOJ >= @From AND m.DOJ < DATEADD(day, 1, @To) THEN 1 ELSE 0 END AS Jn, " +
                "  CASE WHEN m.ResigDate >= @From AND m.ResigDate < DATEADD(day, 1, @To) THEN 1 ELSE 0 END AS Lv, " +
                "  CASE WHEN m.DOJ < DATEADD(day, 1, @To) AND (m.ResigDate IS NULL OR m.ResigDate >= DATEADD(day, 1, @To)) THEN 1 ELSE 0 END AS Cl " +
                "FROM dbo.EmployMast AS m WHERE m.CompID = @CompanyId) AS x GROUP BY x.Dept ORDER BY x.Dept;";
            const string trendQuery =
                "SELECT YEAR(ResigDate) AS Y, MONTH(ResigDate) AS M, COUNT(*) AS N FROM dbo.EmployMast " +
                "WHERE CompID = @CompanyId AND ResigDate >= @From AND ResigDate < DATEADD(day, 1, @To) " +
                "GROUP BY YEAR(ResigDate), MONTH(ResigDate);";

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(deptQuery, connection))
                {
                                    AddCompany(command, companyId);
                    command.Parameters.AddWithValue("@From", model.TurnoverFrom);
                    command.Parameters.AddWithValue("@To", model.TurnoverTo);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var item = new TurnoverDepartmentViewModel
                            {
                                Department = reader.GetString(0),
                                Opening = reader.GetInt32(1),
                                Joiners = reader.GetInt32(2),
                                Leavers = reader.GetInt32(3),
                                Closing = reader.GetInt32(4)
                            };
                            if (item.Opening + item.Joiners + item.Leavers + item.Closing > 0) model.TurnoverDepartments.Add(item);
                        }
                    }
                }

                var byMonth = new Dictionary<int, int>();
                using (var command = new SqlCommand(trendQuery, connection))
                {
                                    AddCompany(command, companyId);
                    command.Parameters.AddWithValue("@From", model.TurnoverFrom);
                    command.Parameters.AddWithValue("@To", model.TurnoverTo);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read()) byMonth[reader.GetInt32(0) * 12 + reader.GetInt32(1) - 1] = reader.GetInt32(2);
                    }
                }
                var cursor = new DateTime(model.TurnoverFrom.Year, model.TurnoverFrom.Month, 1);
                while (cursor <= model.TurnoverTo)
                {
                    int count;
                    byMonth.TryGetValue(cursor.Year * 12 + cursor.Month - 1, out count);
                    model.TurnoverTrend.Add(new TurnoverSliceViewModel(cursor.ToString("MMM yyyy", CultureInfo.InvariantCulture), count));
                    cursor = cursor.AddMonths(1);
                }
            }
        }

        public EmployeeDocumentReportViewModel GetReport(string companyId, int expiryDays)
        {
            return GetReport(companyId, expiryDays, DateTime.Today.AddYears(-1), DateTime.Today);
        }

        public EmployeeDocumentReportViewModel GetReport(string companyId, int expiryDays, DateTime termFrom, DateTime termTo)
        {
            expiryDays = (expiryDays == 30 || expiryDays == 60 || expiryDays == 90 || expiryDays == 180) ? expiryDays : 90;
            var model = new EmployeeDocumentReportViewModel
            {
                SelectedCompany = companyId,
                ExpiryWindowDays = expiryDays,
                ReportDate = DateTime.Today,
                TerminationFrom = termFrom.Date,
                TerminationTo = termTo.Date
            };

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                const string typesQuery = "SELECT Code, Name FROM dbo.Document WHERE CompID = @CompanyId ORDER BY Code;";
                using (var command = new SqlCommand(typesQuery, connection))
                {
                                    AddCompany(command, companyId);
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

                var rowsQuery =
                    "SELECT e.EmpNo, e.Fname, e.Mname, e.LName, " + DeptNameSql.Replace("m.", "e.") + " AS Dept, e.Status, " +
                    "       d.DocCode, d.DocNum, d.IssueDt, d.ExpDt, d.AddInfo " +
                    "FROM dbo.EmployMast AS e " +
                    "LEFT JOIN dbo.vw_EmployeeExpiryDocuments AS d " +
                    "    ON d.CompID = e.CompID AND d.EmpNo = e.EmpNo " +
                    "WHERE e.CompID = @CompanyId AND e.ResigDate IS NULL " +
                    "ORDER BY e.Fname, e.LName, e.EmpNo, d.DocCode, d.ExpDt DESC;";
                var employees = new Dictionary<string, EmployeeDocumentRowViewModel>(StringComparer.OrdinalIgnoreCase);
                using (var command = new SqlCommand(rowsQuery, connection))
                {
                                    AddCompany(command, companyId);
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

                const string probationQuery =
                    "SELECT m.EmpNo, m.Fname, m.Mname, m.LName, " + DeptNameSql + " AS Dept, " + DesigNameSql + " AS Desig, m.DOJ, m.ProbationEndDate " +
                    "FROM dbo.EmployMast AS m " +
                    "WHERE m.CompID = @CompanyId AND m.ResigDate IS NULL AND m.ProbationEndDate >= @Today " +
                    "ORDER BY m.ProbationEndDate ASC, m.Fname, m.EmpNo;";
                using (var command = new SqlCommand(probationQuery, connection))
                {
                                    AddCompany(command, companyId);
                    command.Parameters.AddWithValue("@Today", DateTime.Today);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var parts = new[] { ReadText(reader, "Fname"), ReadText(reader, "Mname"), ReadText(reader, "LName") };
                            var end = ReadDate(reader, "ProbationEndDate").Value;
                            model.ProbationEmployees.Add(new ProbationEmployeeViewModel
                            {
                                EmployeeNumber = ReadText(reader, "EmpNo"),
                                FullName = string.Join(" ", parts.Where(part => part.Length > 0).ToArray()),
                                Department = ReadText(reader, "Dept"),
                                Designation = ReadText(reader, "Desig"),
                                JoinDate = ReadDate(reader, "DOJ"),
                                ProbationEndDate = end,
                                DaysRemaining = (int)Math.Min((end - DateTime.Today).TotalDays, int.MaxValue)
                            });
                        }
                    }
                }
                model.ActiveEmployeeCount = model.Employees.Count;

                const string terminationQuery =
                    "SELECT m.EmpNo, m.Fname, m.Mname, m.LName, " + DeptNameSql + " AS Dept, " + DesigNameSql + " AS Desig, m.DOJ, m.ResigDate, m.Status, m.Remarks " +
                    "FROM dbo.EmployMast AS m " +
                    "WHERE m.CompID = @CompanyId AND m.ResigDate >= @From AND m.ResigDate < DATEADD(day, 1, @To) " +
                    "ORDER BY m.ResigDate DESC, m.Fname, m.EmpNo;";
                using (var command = new SqlCommand(terminationQuery, connection))
                {
                                    AddCompany(command, companyId);
                    command.Parameters.AddWithValue("@From", model.TerminationFrom);
                    command.Parameters.AddWithValue("@To", model.TerminationTo);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var parts = new[] { ReadText(reader, "Fname"), ReadText(reader, "Mname"), ReadText(reader, "LName") };
                            model.TerminatedEmployees.Add(new TerminatedEmployeeViewModel
                            {
                                EmployeeNumber = ReadText(reader, "EmpNo"),
                                FullName = string.Join(" ", parts.Where(part => part.Length > 0).ToArray()),
                                Department = ReadText(reader, "Dept"),
                                Designation = ReadText(reader, "Desig"),
                                JoinDate = ReadDate(reader, "DOJ"),
                                TerminationDate = ReadDate(reader, "ResigDate").Value,
                                Status = ReadText(reader, "Status"),
                                Remarks = ReadText(reader, "Remarks")
                            });
                        }
                    }
                }
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
