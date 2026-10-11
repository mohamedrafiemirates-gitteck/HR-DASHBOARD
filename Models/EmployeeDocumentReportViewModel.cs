namespace EmployeeDocumentTracker.Models;

public sealed class EmployeeDocumentReportViewModel
{
    public string SelectedCompany { get; set; } = string.Empty;
    public List<string> Companies { get; set; } = new();
    public int ExpiryWindowDays { get; set; } = 90;
    public DateTime ReportDate { get; set; } = DateTime.Today;
    public string? ErrorMessage { get; set; }
    public List<DocumentTypeViewModel> DocumentTypes { get; set; } = new();
    public List<EmployeeDocumentRowViewModel> Employees { get; set; } = new();
    public List<DocumentStatusViewModel> StatusBreakdown { get; set; } = new();
    public List<ExpiryMonthViewModel> ExpiriesByMonth { get; set; } = new();
    public List<MissingByTypeViewModel> MissingByType { get; set; } = new();
    public int MissingDocumentSlots { get; set; }
    public int EmployeesWithMissingDocuments { get; set; }
    public int ExpiringDocuments { get; set; }
    public int ExpiredDocuments { get; set; }
    public int EmployeeCount => Employees.Count;
}

public sealed record DocumentTypeViewModel(string Code, string Name);

public sealed class EmployeeDocumentRowViewModel
{
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string EmployeeStatus { get; set; } = string.Empty;
    public Dictionary<string, EmployeeDocumentCellViewModel> Documents { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class EmployeeDocumentCellViewModel
{
    public string DocumentNumber { get; set; } = string.Empty;
    public DateTime? IssueDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string AddInfo { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}

public sealed record DocumentStatusViewModel(string Label, int Count, string Color);
public sealed record ExpiryMonthViewModel(string Label, int Count);
public sealed record MissingByTypeViewModel(string Label, int Count);