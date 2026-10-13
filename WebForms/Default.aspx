<%@ Page Title="Employee document tracker" Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeFile="Default.aspx.cs" Inherits="EmployeeDocumentTracker.DefaultPage" %>

<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
<section class="report-heading">
    <div>
        <p class="eyebrow">PAYROLL OPERATIONS / DOCUMENT CONTROL</p>
        <h1>Employee document tracker</h1>
        <p class="heading-note"><%: Model.SelectedCompany %> <span aria-hidden="true">/</span> Updated <%: Model.ReportDate.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture) %></p>
    </div>
    <div class="report-filters" aria-label="Report filters">
        <label>
            <span>Company</span>
            <asp:DropDownList ID="ddlCompany" runat="server" CssClass="form-select" AutoPostBack="true" />
        </label>
        <label>
            <span>Expiry window</span>
            <asp:DropDownList ID="ddlExpiry" runat="server" CssClass="form-select" AutoPostBack="true" />
        </label>
    </div>
</section>

<% if (!string.IsNullOrWhiteSpace(Model.ErrorMessage)) { %>
    <div class="alert alert-danger report-error" role="alert"><%: Model.ErrorMessage %></div>
<% } else { %>
    <section class="metric-grid" aria-label="Document overview">
        <article class="metric metric-ink">
            <span class="metric-label">Employees on file</span>
            <strong><%: Model.EmployeeCount.ToString("N0") %></strong>
            <span class="metric-foot">Resignation date not recorded</span>
        </article>
        <article class="metric metric-coral">
            <span class="metric-label">Employees with gaps</span>
            <strong><%: Model.EmployeesWithMissingDocuments.ToString("N0") %></strong>
            <span class="metric-foot">Missing or incomplete documents</span>
        </article>
        <article class="metric metric-amber">
            <span class="metric-label">Expiring soon</span>
            <strong><%: Model.ExpiringDocuments.ToString("N0") %></strong>
            <span class="metric-foot">Within <%: Model.ExpiryWindowDays %> days</span>
        </article>
        <article class="metric metric-green">
            <span class="metric-label">Already expired</span>
            <strong><%: Model.ExpiredDocuments.ToString("N0") %></strong>
            <span class="metric-foot">Based on recorded expiry dates</span>
        </article>
    </section>

    <section class="chart-grid" aria-label="Document charts">
        <article class="chart-panel status-panel">
            <div class="panel-heading">
                <div><p class="eyebrow">PORTFOLIO</p><h2>Document status</h2></div>
                <span class="panel-total"><%: Model.DocumentTypes.Count %> types</span>
            </div>
            <div class="chart-wrap chart-donut"><canvas id="statusChart" aria-label="Document status breakdown"></canvas></div>
            <div class="status-legend">
                <% foreach (var status in Model.StatusBreakdown) { %>
                    <span><i style="--legend-color:<%: status.Color %>"></i><%: status.Label %> <b><%: status.Count.ToString("N0") %></b></span>
                <% } %>
            </div>
        </article>
        <article class="chart-panel">
            <div class="panel-heading">
                <div><p class="eyebrow">NEXT 12 MONTHS</p><h2>Expiry schedule</h2></div>
                <span class="panel-total"><%: Model.ExpiriesByMonth.Sum(item => item.Count).ToString("N0") %> records</span>
            </div>
            <div class="chart-wrap"><canvas id="expiryChart" aria-label="Upcoming document expiries by month"></canvas></div>
        </article>
        <article class="chart-panel missing-panel">
            <div class="panel-heading">
                <div><p class="eyebrow">FOLLOW-UP</p><h2>Gaps by document</h2></div>
                <span class="panel-total">Top 8</span>
            </div>
            <div class="chart-wrap"><canvas id="missingChart" aria-label="Missing and incomplete documents by type"></canvas></div>
        </article>
    </section>

    <section class="report-table-section">
        <div class="table-heading">
            <div>
                <p class="eyebrow">EMPLOYEE REGISTER</p>
                <h2>Documents by employee <span><%: Model.EmployeeCount.ToString("N0") %></span></h2>
            </div>
            <label class="search-box">
                <span class="visually-hidden">Search employees</span>
                <input id="employeeSearch" type="search" class="form-control" placeholder="Find employee or department" autocomplete="off" />
            </label>
        </div>
        <div class="table-meta">Rows with no resignation date. Document columns follow the PayrollDB document master.</div>
        <div class="table-scroll">
            <table class="table employee-table">
                <thead>
                    <tr>
                        <th class="employee-col" scope="col">Employee</th>
                        <th scope="col">Department</th>
                        <th scope="col">Status</th>
                        <% for (var i = 0; i < Model.DocumentTypes.Count; i++) { %>
                            <th class="document-col" scope="col" title="<%: Model.DocumentTypes[i].Name %>"><span>DOC<%: i + 1 %></span><small><%: Model.DocumentTypes[i].Name %></small></th>
                        <% } %>
                    </tr>
                </thead>
                <tbody id="employeeRows">
                    <% foreach (var employee in Model.Employees) { %>
                        <tr class="employee-row" data-search="<%: employee.EmployeeNumber + " " + employee.FullName + " " + employee.Department %>">
                            <th class="employee-col employee-name" scope="row"><strong><%: employee.FullName %></strong><small><%: employee.EmployeeNumber %></small></th>
                            <td><%: string.IsNullOrWhiteSpace(employee.Department) ? "-" : employee.Department %></td>
                            <td><span class="employee-status"><%: string.IsNullOrWhiteSpace(employee.EmployeeStatus) ? "-" : employee.EmployeeStatus %></span></td>
                            <% foreach (var type in Model.DocumentTypes) {
                                   EmployeeDocumentTracker.EmployeeDocumentCellViewModel document;
                                   employee.Documents.TryGetValue(type.Code, out document); %>
                                <td class="document-cell">
                                    <% if (document == null) { %>
                                        <span class="state state-missing">Missing</span>
                                    <% } else { %>
                                        <span class="doc-number"><%: string.IsNullOrWhiteSpace(document.DocumentNumber) ? "No number" : document.DocumentNumber %></span>
                                        <span class="state <%: StateClass(document.State) %>"><%: document.State %></span>
                                        <% if (document.IssueDate.HasValue) { %>
                                            <small class="doc-date">Issued <%: document.IssueDate.Value.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture) %></small>
                                        <% } %>
                                        <% if (document.ExpiryDate.HasValue) { %>
                                            <small class="doc-date">Expires <%: document.ExpiryDate.Value.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture) %></small>
                                        <% } %>
                                        <% if (!string.IsNullOrWhiteSpace(document.AddInfo)) { %>
                                            <small class="doc-date"><%: document.AddInfo %></small>
                                        <% } %>
                                    <% } %>
                                </td>
                            <% } %>
                        </tr>
                    <% } %>
                </tbody>
            </table>
        </div>
        <p id="noSearchResults" class="no-results" hidden>No employees match this search.</p>
    </section>
<% } %>
</asp:Content>

<asp:Content ID="Scripts" ContentPlaceHolderID="ScriptsContent" runat="server">
<% if (string.IsNullOrWhiteSpace(Model.ErrorMessage)) { %>
    <script src="<%: ResolveUrl("~/Scripts/chart.umd.min.js") %>"></script>
    <script>
        const statusRows = <%= ToJson(Model.StatusBreakdown) %>;
        const expiryRows = <%= ToJson(Model.ExpiriesByMonth) %>;
        const missingRows = <%= ToJson(Model.MissingByType) %>;

        if (window.Chart) {
            Chart.defaults.font.family = "DM Sans, sans-serif";
            Chart.defaults.color = "#64716f";
            Chart.defaults.animation.duration = 650;

            new Chart(document.getElementById("statusChart"), {
                type: "doughnut",
                data: {
                    labels: statusRows.map(row => row.Label),
                    datasets: [{ data: statusRows.map(row => row.Count), backgroundColor: statusRows.map(row => row.Color), borderWidth: 0, hoverOffset: 5 }]
                },
                options: { maintainAspectRatio: false, cutout: "70%", plugins: { legend: { display: false } } }
            });

            new Chart(document.getElementById("expiryChart"), {
                type: "bar",
                data: {
                    labels: expiryRows.map(row => row.Label),
                    datasets: [{ label: "Expiring documents", data: expiryRows.map(row => row.Count), backgroundColor: "#e3a32e", borderRadius: 4, maxBarThickness: 38 }]
                },
                options: {
                    maintainAspectRatio: false,
                    plugins: { legend: { display: false } },
                    scales: { x: { grid: { display: false } }, y: { beginAtZero: true, ticks: { precision: 0 }, grid: { color: "#edf0ed" } } }
                }
            });

            new Chart(document.getElementById("missingChart"), {
                type: "bar",
                data: {
                    labels: missingRows.map(row => row.Label),
                    datasets: [{ label: "Employees with gaps", data: missingRows.map(row => row.Count), backgroundColor: "#cf5545", borderRadius: 4, maxBarThickness: 28 }]
                },
                options: {
                    indexAxis: "y",
                    maintainAspectRatio: false,
                    plugins: { legend: { display: false } },
                    scales: { x: { beginAtZero: true, ticks: { precision: 0 }, grid: { color: "#edf0ed" } }, y: { grid: { display: false } } }
                }
            });
        }

        const employeeSearch = document.getElementById("employeeSearch");
        const employeeRows = [...document.querySelectorAll(".employee-row")];
        const noSearchResults = document.getElementById("noSearchResults");
        employeeSearch.addEventListener("keydown", event => { if (event.key === "Enter") event.preventDefault(); });
        employeeSearch.addEventListener("input", () => {
            const term = employeeSearch.value.trim().toLocaleLowerCase();
            let visibleRows = 0;
            for (const row of employeeRows) {
                const visible = row.dataset.search.toLocaleLowerCase().includes(term);
                row.hidden = !visible;
                visibleRows += visible ? 1 : 0;
            }
            noSearchResults.hidden = visibleRows > 0;
        });
    </script>
<% } %>
</asp:Content>
