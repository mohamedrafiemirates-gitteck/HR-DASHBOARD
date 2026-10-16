using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace EmployeeDocumentTracker
{
    public partial class DefaultPage : Page
    {
        private static readonly int[] ExpiryOptions = { 30, 60, 90, 180 };

        protected EmployeeDocumentReportViewModel Model { get; private set; }

        protected void Page_Load(object sender, EventArgs e)
        {
            Model = new EmployeeDocumentReportViewModel();
            try
            {
                var service = new EmployeeDocumentReportService();
                var companies = service.GetCompanies();
                var allowedSetting = System.Configuration.ConfigurationManager.AppSettings["AllowedCompanies"];
                if (!string.IsNullOrWhiteSpace(allowedSetting))
                {
                    var allowed = new List<string>();
                    foreach (var part in allowedSetting.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        allowed.Add(part.Trim());
                    }
                    companies = companies.FindAll(c => allowed.Exists(a => string.Equals(a, c, StringComparison.OrdinalIgnoreCase)));
                }
                if (companies.Count == 0)
                {
                    Model.ErrorMessage = "No companies are available. Check AllowedCompanies in AppSettings.config.";
                    return;
                }

                if (!IsPostBack)
                {
                    ddlCompany.DataSource = companies;
                    ddlCompany.DataBind();
                    foreach (var days in ExpiryOptions)
                    {
                        ddlExpiry.Items.Add(new ListItem(days + " days", days.ToString(CultureInfo.InvariantCulture)));
                    }

                    var requested = Request.QueryString["companyId"] ?? string.Empty;
                    var match = companies.Find(c => string.Equals(c, requested, StringComparison.OrdinalIgnoreCase));
                    ddlCompany.SelectedValue = match ?? companies[0];

                    int queryDays;
                    if (int.TryParse(Request.QueryString["expiryDays"], out queryDays) && Array.IndexOf(ExpiryOptions, queryDays) >= 0)
                    {
                        ddlExpiry.SelectedValue = queryDays.ToString(CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        ddlExpiry.SelectedValue = "90";
                    }
                }

                var termFrom = DateTime.Today.AddYears(-1);
                var termTo = DateTime.Today;
                if (!IsPostBack)
                {
                    txtFrom.Text = termFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    txtTo.Text = termTo.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }
                else
                {
                    DateTime parsed;
                    if (DateTime.TryParseExact(txtFrom.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)) termFrom = parsed;
                    if (DateTime.TryParseExact(txtTo.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)) termTo = parsed;
                    if (termFrom > termTo) { var swap = termFrom; termFrom = termTo; termTo = swap; }
                    txtFrom.Text = termFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    txtTo.Text = termTo.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }

                if (!IsPostBack)
                {
                    for (var y = DateTime.Today.Year; y >= DateTime.Today.Year - 9; y--)
                    {
                        ddlTurnYear.Items.Add(new ListItem(y.ToString(CultureInfo.InvariantCulture), y.ToString(CultureInfo.InvariantCulture)));
                    }
                    ddlTurnPeriod.Items.Add(new ListItem("Full year", "0"));
                    ddlTurnPeriod.Items.Add(new ListItem("Q1 (Jan - Mar)", "1"));
                    ddlTurnPeriod.Items.Add(new ListItem("Q2 (Apr - Jun)", "2"));
                    ddlTurnPeriod.Items.Add(new ListItem("Q3 (Jul - Sep)", "3"));
                    ddlTurnPeriod.Items.Add(new ListItem("Q4 (Oct - Dec)", "4"));
                }

                Model = service.GetReport(ddlCompany.SelectedValue, int.Parse(ddlExpiry.SelectedValue, CultureInfo.InvariantCulture), termFrom, termTo);
                var turnYear = int.Parse(ddlTurnYear.SelectedValue, CultureInfo.InvariantCulture);
                var turnQuarter = int.Parse(ddlTurnPeriod.SelectedValue, CultureInfo.InvariantCulture);
                var turnFrom = turnQuarter == 0 ? new DateTime(turnYear, 1, 1) : new DateTime(turnYear, (turnQuarter - 1) * 3 + 1, 1);
                var turnTo = turnQuarter == 0 ? new DateTime(turnYear, 12, 31) : turnFrom.AddMonths(3).AddDays(-1);
                service.FillSalary(Model, ddlCompany.SelectedValue);
                service.FillTurnover(Model, ddlCompany.SelectedValue, turnFrom, turnTo,
                    turnQuarter == 0 ? "Year " + turnYear : "Q" + turnQuarter + " " + turnYear);
                Model.Companies = companies;
            }
            catch (Exception exception)
            {
                LogError(exception);
                Model = new EmployeeDocumentReportViewModel
                {
                    ErrorMessage = "PayrollDB could not be read. Check the PayrollDb connection string and database permissions."
                };
                if (Request.IsLocal)
                {
                    Model.ErrorMessage += " " + exception.Message;
                }
            }
        }

        protected string StateClass(string state)
        {
            return "state-" + state.ToLowerInvariant().Replace(' ', '-');
        }

        protected string ToJson(object value)
        {
            return new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(value).Replace("<", "\\u003c");
        }

        private void LogError(Exception exception)
        {
            try
            {
                var path = Server.MapPath("~/App_Data/errors.log");
                File.AppendAllText(path, DateTime.Now.ToString("s", CultureInfo.InvariantCulture) + " " + exception + Environment.NewLine + Environment.NewLine);
            }
            catch (Exception)
            {
                // Logging is best effort when the app pool cannot write to App_Data.
            }
        }
    }
}
