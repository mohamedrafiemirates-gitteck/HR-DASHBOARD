using System.Diagnostics;
using EmployeeDocumentTracker.Models;
using EmployeeDocumentTracker.Services;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeDocumentTracker.Controllers;

public class HomeController(EmployeeDocumentReportService reportService, ILogger<HomeController> logger) : Controller
{
    public async Task<IActionResult> Index(string? companyId, int expiryDays = 90, CancellationToken cancellationToken = default)
    {
        try
        {
            var companies = await reportService.GetCompaniesAsync(cancellationToken);
            if (companies.Count == 0)
            {
                return View(new EmployeeDocumentReportViewModel { ErrorMessage = "No companies were found in PayrollDB.dbo.EmployMast." });
            }

            var selectedCompany = companies.Contains(companyId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                ? companyId!
                : companies[0];
            var model = await reportService.GetReportAsync(selectedCompany, expiryDays, cancellationToken);
            model.Companies = companies;
            return View(model);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not load employee document report from PayrollDB.");
            return View(new EmployeeDocumentReportViewModel
            {
                ErrorMessage = "PayrollDB could not be read. Check the PayrollDb connection string and database permissions."
            });
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
