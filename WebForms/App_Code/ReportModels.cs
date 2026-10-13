using System;
using System.Collections.Generic;

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
        }

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
