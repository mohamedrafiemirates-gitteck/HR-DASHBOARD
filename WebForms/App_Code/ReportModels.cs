using System;
using System.Collections.Generic;
using System.Linq;

namespace EmployeeDocumentTracker
{
    public sealed class EmployeeDocumentReportViewModel
    {
        public EmployeeDocumentReportViewModel()
        {
            Companies = new List<string>();
            ExpiryWindowDays = 90;
            ReportDate = DateTime.Today;
            DocumentTypes = new List<DocumentTypeViewModel>();
            Employees = new List<EmployeeDocumentRowViewModel>();
            StatusBreakdown = new List<DocumentStatusViewModel>();
            ExpiriesByMonth = new List<ExpiryMonthViewModel>();
            MissingByType = new List<MissingByTypeViewModel>();
            ProbationEmployees = new List<ProbationEmployeeViewModel>();
            TerminatedEmployees = new List<TerminatedEmployeeViewModel>();
            TerminationFrom = DateTime.Today.AddYears(-1);
            TerminationTo = DateTime.Today;
            TurnoverDepartments = new List<TurnoverDepartmentViewModel>();
            TurnoverTrend = new List<TurnoverSliceViewModel>();
            TurnoverFrom = DateTime.Today;
            TurnoverTo = DateTime.Today;
            TurnoverLabel = string.Empty;
            SalaryEarnCodes = new List<string>();
            SalaryDepartments = new List<SalaryDepartmentViewModel>();
        }

        public List<string> SalaryEarnCodes { get; set; }
        public List<SalaryDepartmentViewModel> SalaryDepartments { get; set; }
        public int SalaryEmployeeCount { get { return SalaryDepartments.Sum(d => d.Count); } }
        public decimal SalaryCodeTotal(string code) { return SalaryDepartments.Sum(d => d.CodeTotal(code)); }
        public decimal SalaryGrandTotal { get { return SalaryDepartments.Sum(d => d.Total); } }

        public List<TurnoverDepartmentViewModel> TurnoverDepartments { get; set; }
        public List<TurnoverSliceViewModel> TurnoverTrend { get; set; }
        public DateTime TurnoverFrom { get; set; }
        public DateTime TurnoverTo { get; set; }
        public string TurnoverLabel { get; set; }
        public int TurnoverOpening { get { return TurnoverDepartments.Sum(d => d.Opening); } }
        public int TurnoverJoiners { get { return TurnoverDepartments.Sum(d => d.Joiners); } }
        public int TurnoverLeavers { get { return TurnoverDepartments.Sum(d => d.Leavers); } }
        public int TurnoverClosing { get { return TurnoverDepartments.Sum(d => d.Closing); } }
        public double TurnoverPercent
        {
            get
            {
                var average = (TurnoverOpening + TurnoverClosing) / 2.0;
                return average <= 0 ? 0 : Math.Round(TurnoverLeavers * 100.0 / average, 1);
            }
        }

        public List<TerminatedEmployeeViewModel> TerminatedEmployees { get; set; }
        public DateTime TerminationFrom { get; set; }
        public DateTime TerminationTo { get; set; }
        public int TerminatedCount { get { return TerminatedEmployees.Count; } }
        public List<TerminatedEmployeeViewModel> ResignedEmployees { get { return TerminatedEmployees.Where(e => string.Equals(e.Status, "R", StringComparison.OrdinalIgnoreCase)).ToList(); } }
        public List<TerminatedEmployeeViewModel> DismissedEmployees { get { return TerminatedEmployees.Where(e => !string.Equals(e.Status, "R", StringComparison.OrdinalIgnoreCase)).ToList(); } }

        public List<ProbationEmployeeViewModel> ProbationEmployees { get; set; }
        public int ActiveEmployeeCount { get; set; }
        public int ProbationCount { get { return ProbationEmployees.Count; } }

        public string SelectedCompany { get; set; }
        public List<string> Companies { get; set; }
        public int ExpiryWindowDays { get; set; }
        public DateTime ReportDate { get; set; }
        public string ErrorMessage { get; set; }
        public List<DocumentTypeViewModel> DocumentTypes { get; set; }
        public List<EmployeeDocumentRowViewModel> Employees { get; set; }
        public List<DocumentStatusViewModel> StatusBreakdown { get; set; }
        public List<ExpiryMonthViewModel> ExpiriesByMonth { get; set; }
        public List<MissingByTypeViewModel> MissingByType { get; set; }
        public int MissingDocumentSlots { get; set; }
        public int EmployeesWithMissingDocuments { get; set; }
        public int ExpiringDocuments { get; set; }
        public int ExpiredDocuments { get; set; }
        public int EmployeeCount { get { return Employees.Count; } }
    }

    public sealed class SalaryEmployeeViewModel
    {
        public SalaryEmployeeViewModel()
        {
            Earnings = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        }

        public string EmployeeNumber { get; set; }
        public string FullName { get; set; }
        public string Designation { get; set; }
        public DateTime? JoinDate { get; set; }
        public Dictionary<string, decimal> Earnings { get; private set; }
        public decimal Total { get { return Earnings.Values.Sum(); } }
    }

    public sealed class SalaryDepartmentViewModel
    {
        public SalaryDepartmentViewModel()
        {
            Employees = new List<SalaryEmployeeViewModel>();
        }

        public string Department { get; set; }
        public List<SalaryEmployeeViewModel> Employees { get; private set; }
        public int Count { get { return Employees.Count; } }
        public decimal CodeTotal(string code)
        {
            return Employees.Sum(e => { decimal v; return e.Earnings.TryGetValue(code, out v) ? v : 0m; });
        }
        public decimal Total { get { return Employees.Sum(e => e.Total); } }
    }

    public sealed class TurnoverDepartmentViewModel
    {
        public string Department { get; set; }
        public int Opening { get; set; }
        public int Joiners { get; set; }
        public int Leavers { get; set; }
        public int Closing { get; set; }
        public double TurnoverPercent
        {
            get
            {
                var average = (Opening + Closing) / 2.0;
                return average <= 0 ? 0 : Math.Round(Leavers * 100.0 / average, 1);
            }
        }
    }

    public sealed class TurnoverSliceViewModel
    {
        public TurnoverSliceViewModel(string label, int leavers)
        {
            Label = label;
            Leavers = leavers;
        }

        public string Label { get; private set; }
        public int Leavers { get; private set; }
    }

    public sealed class TerminatedEmployeeViewModel
    {
        public string EmployeeNumber { get; set; }
        public string FullName { get; set; }
        public string Department { get; set; }
        public string Designation { get; set; }
        public DateTime? JoinDate { get; set; }
        public DateTime TerminationDate { get; set; }
        public string Status { get; set; }
        public string Remarks { get; set; }
        public string ServiceLength
        {
            get
            {
                if (!JoinDate.HasValue || TerminationDate < JoinDate.Value) return "-";
                var months = (TerminationDate.Year - JoinDate.Value.Year) * 12 + TerminationDate.Month - JoinDate.Value.Month;
                if (TerminationDate.Day < JoinDate.Value.Day) months--;
                if (months < 0) months = 0;
                return (months / 12) + "y " + (months % 12) + "m";
            }
        }
    }

    public sealed class ProbationEmployeeViewModel
    {
        public string EmployeeNumber { get; set; }
        public string FullName { get; set; }
        public string Department { get; set; }
        public string Designation { get; set; }
        public DateTime? JoinDate { get; set; }
        public DateTime ProbationEndDate { get; set; }
        public int DaysRemaining { get; set; }
    }

    public sealed class DocumentTypeViewModel
    {
        public DocumentTypeViewModel(string code, string name)
        {
            Code = code;
            Name = name;
        }

        public string Code { get; private set; }
        public string Name { get; private set; }
    }

    public sealed class EmployeeDocumentRowViewModel
    {
        public EmployeeDocumentRowViewModel()
        {
            EmployeeNumber = string.Empty;
            FullName = string.Empty;
            Department = string.Empty;
            EmployeeStatus = string.Empty;
            Documents = new Dictionary<string, EmployeeDocumentCellViewModel>(StringComparer.OrdinalIgnoreCase);
        }

        public string EmployeeNumber { get; set; }
        public string FullName { get; set; }
        public string Department { get; set; }
        public string EmployeeStatus { get; set; }
        public Dictionary<string, EmployeeDocumentCellViewModel> Documents { get; private set; }
    }

    public sealed class EmployeeDocumentCellViewModel
    {
        public EmployeeDocumentCellViewModel()
        {
            DocumentNumber = string.Empty;
            AddInfo = string.Empty;
            State = string.Empty;
        }

        public string DocumentNumber { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string AddInfo { get; set; }
        public string State { get; set; }
    }

    public sealed class DocumentStatusViewModel
    {
        public DocumentStatusViewModel(string label, int count, string color)
        {
            Label = label;
            Count = count;
            Color = color;
        }

        public string Label { get; private set; }
        public int Count { get; private set; }
        public string Color { get; private set; }
    }

    public sealed class ExpiryMonthViewModel
    {
        public ExpiryMonthViewModel(string label, int count)
        {
            Label = label;
            Count = count;
        }

        public string Label { get; private set; }
        public int Count { get; private set; }
    }

    public sealed class MissingByTypeViewModel
    {
        public MissingByTypeViewModel(string label, int count)
        {
            Label = label;
            Count = count;
        }

        public string Label { get; private set; }
        public int Count { get; private set; }
    }
}
