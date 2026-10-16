<%@ Page Title="HR Module Power BI Dashboard" Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeFile="Default.aspx.cs" Inherits="EmployeeDocumentTracker.DefaultPage" %>

<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
<section class="report-heading">
    <div>
        <p class="eyebrow">PAYROLL OPERATIONS / HR ANALYTICS</p>
        <h1>HR Module Power BI Dashboard</h1>
        <p class="heading-note"><%: Model.SelectedCompany %> <span aria-hidden="true">/</span> Updated <%: Model.ReportDate.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture) %></p>
    </div>
    <div class="report-filters" aria-label="Report filters">
        <label>
            <span>Company</span>
            <asp:DropDownList ID="ddlCompany" runat="server" CssClass="form-select" AutoPostBack="true" />
        </label>
    </div>
</section>

<% if (!string.IsNullOrWhiteSpace(Model.ErrorMessage)) { %>
    <div class="alert alert-danger report-error" role="alert"><%: Model.ErrorMessage %></div>
<% } else { %>
    <nav class="report-tabs" role="tablist">
        <button type="button" class="report-tab" role="tab" data-tab="turnover">Employee Turnover</button>
        <button type="button" class="report-tab" role="tab" data-tab="salary">Active Employee <span class="tab-count tab-count-green"><%: Model.SalaryEmployeeCount.ToString("N0") %></span></button>
        <button type="button" class="report-tab" role="tab" data-tab="probation">Probation Employee <span class="tab-count"><%: Model.ProbationCount.ToString("N0") %></span></button>
        <button type="button" class="report-tab" role="tab" data-tab="resigned">Resigned Employee <span class="tab-count tab-count-red"><%: Model.ResignedEmployees.Count.ToString("N0") %></span></button>
        <button type="button" class="report-tab" role="tab" data-tab="terminated">Terminated Employee <span class="tab-count tab-count-red"><%: Model.DismissedEmployees.Count.ToString("N0") %></span></button>
        <button type="button" class="report-tab" role="tab" data-tab="documents">Expiry Document Tracking</button>
    </nav>
    <div id="tab-documents" class="tab-panel" hidden>
    <section class="report-filters term-filters" aria-label="Expiry window">
        <label>
            <span>Expiry window</span>
            <asp:DropDownList ID="ddlExpiry" runat="server" CssClass="form-select" AutoPostBack="true" />
        </label>
    </section>
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
    </div>

    <div id="tab-probation" class="tab-panel" hidden>
    <section class="metric-grid" aria-label="Probation overview">
        <article class="metric metric-ink">
            <span class="metric-label">Active employees</span>
            <strong><%: Model.ActiveEmployeeCount.ToString("N0") %></strong>
            <span class="metric-foot">Resignation date not recorded</span>
        </article>
        <article class="metric metric-amber">
            <span class="metric-label">Under probation</span>
            <strong><%: Model.ProbationCount.ToString("N0") %></strong>
            <span class="metric-foot">Probation end date not yet reached</span>
        </article>
    </section>

    <section class="chart-grid" aria-label="Probation chart">
        <article class="chart-panel status-panel">
            <div class="panel-heading">
                <div><p class="eyebrow">HEADCOUNT</p><h2>Active vs probation</h2></div>
                <span class="panel-total"><%: Model.ActiveEmployeeCount.ToString("N0") %> active</span>
            </div>
            <div class="chart-wrap chart-donut"><canvas id="probationChart" aria-label="Active employees versus employees under probation"></canvas></div>
            <div class="status-legend">
                <span><i style="--legend-color:#2f7d6d"></i>Active (confirmed) <b><%: (Model.ActiveEmployeeCount - Model.ProbationCount).ToString("N0") %></b></span>
                <span><i style="--legend-color:#e3a32e"></i>Under probation <b><%: Model.ProbationCount.ToString("N0") %></b></span>
            </div>
        </article>
    </section>

    <section class="report-table-section">
        <div class="table-heading">
            <div>
                <p class="eyebrow">PROBATION REGISTER</p>
                <h2>Employees under probation <span><%: Model.ProbationCount.ToString("N0") %></span></h2>
            </div>
        </div>
        <div class="table-meta">Sorted by probation end date, earliest first.</div>
        <div class="table-scroll">
            <table class="table employee-table">
                <thead>
                    <tr>
                        <th scope="col">#</th>
                        <th scope="col">Employee</th>
                        <th scope="col">Department</th>
                        <th scope="col">Designation</th>
                        <th scope="col">Joined</th>
                        <th scope="col">Probation ends</th>
                        <th scope="col">Days left</th>
                    </tr>
                </thead>
                <tbody>
                    <% var probationNo = 0; foreach (var person in Model.ProbationEmployees) { probationNo++; %>
                        <tr>
                            <td><%: probationNo %></td>
                            <th class="employee-name" scope="row"><strong><%: person.FullName %></strong><small><%: person.EmployeeNumber %></small></th>
                            <td><%: string.IsNullOrWhiteSpace(person.Department) ? "-" : person.Department %></td>
                            <td><%: string.IsNullOrWhiteSpace(person.Designation) ? "-" : person.Designation %></td>
                            <td><%: person.JoinDate.HasValue ? person.JoinDate.Value.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture) : "-" %></td>
                            <td><%: person.ProbationEndDate.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture) %></td>
                            <td><%: person.DaysRemaining.ToString("N0") %></td>
                        </tr>
                    <% } %>
                </tbody>
            </table>
        </div>
        <% if (Model.ProbationCount == 0) { %><p class="no-results">No employees are currently under probation.</p><% } %>
    </section>
    </div>

    <section id="termFiltersBar" class="report-filters term-filters" aria-label="Period" hidden>
        <label><span>From</span><asp:TextBox ID="txtFrom" runat="server" TextMode="Date" CssClass="form-control" /></label>
        <label><span>To</span><asp:TextBox ID="txtTo" runat="server" TextMode="Date" CssClass="form-control" /></label>
        <asp:Button ID="btnApplyRange" runat="server" Text="Apply" CssClass="btn btn-dark term-apply" />
    </section>

    <% foreach (var kind in new[] { "resigned", "terminated" }) {
           var isTerm = kind == "terminated";
           var label = isTerm ? "Terminated" : "Resigned";
           var list = isTerm ? Model.DismissedEmployees : Model.ResignedEmployees;
           var listCount = list.Count; %>
    <div id="tab-<%= kind %>" class="tab-panel" hidden>
    <section class="metric-grid" aria-label="<%: label %> overview">
        <article class="metric metric-ink">
            <span class="metric-label">Active Employees</span>
            <strong><%: Model.ActiveEmployeeCount.ToString("N0") %></strong>
            <span class="metric-foot">Currently on payroll</span>
        </article>
        <article class="metric metric-coral">
            <span class="metric-label"><%: label %> In Period</span>
            <strong><%: listCount.ToString("N0") %></strong>
            <span class="metric-foot"><%: Model.TerminationFrom.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture) %> - <%: Model.TerminationTo.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture) %></span>
        </article>
    </section>

    <section class="chart-grid" aria-label="<%: label %> chart">
        <article class="chart-panel status-panel">
            <div class="panel-heading">
                <div><p class="eyebrow">HEADCOUNT</p><h2>Active vs <%: label %></h2></div>
                <span class="panel-total"><%: listCount.ToString("N0") %> <%: label.ToLowerInvariant() %></span>
            </div>
            <div class="chart-wrap chart-donut"><canvas id="<%= kind %>Chart" class="gone-chart" data-active="<%= Model.ActiveEmployeeCount %>" data-count="<%= listCount %>" data-label="<%: label %>"></canvas></div>
            <div class="status-legend">
                <span><i style="--legend-color:#2f7d6d"></i>Active <b><%: Model.ActiveEmployeeCount.ToString("N0") %></b></span>
                <span><i style="--legend-color:#cf5545"></i><%: label %> <b><%: listCount.ToString("N0") %></b></span>
            </div>
        </article>
    </section>

    <section class="report-table-section">
        <div class="table-heading">
            <div>
                <p class="eyebrow"><%: label.ToUpperInvariant() %> REGISTER</p>
                <h2><%: label %> Employees <span><%: listCount.ToString("N0") %></span></h2>
            </div>
        </div>
        <div class="table-meta">Employees whose last working date falls in the selected period, latest first.</div>
        <div class="table-scroll">
            <table class="table employee-table">
                <thead>
                    <tr>
                        <th scope="col">#</th>
                        <th scope="col">Employee</th>
                        <th scope="col">Department</th>
                        <th scope="col">Designation</th>
                        <th scope="col">Joined</th>
                        <th scope="col"><%: label %> On</th>
                        <th scope="col">Service</th>
                        <th scope="col">Status</th>
                        <th scope="col">Remarks</th>
                    </tr>
                </thead>
                <tbody>
                    <% var termNo = 0; foreach (var gone in list) { termNo++; %>
                        <tr>
                            <td><%: termNo %></td>
                            <th class="employee-name" scope="row"><strong><%: gone.FullName %></strong><small><%: gone.EmployeeNumber %></small></th>
                            <td><%: string.IsNullOrWhiteSpace(gone.Department) ? "-" : gone.Department %></td>
                            <td><%: string.IsNullOrWhiteSpace(gone.Designation) ? "-" : gone.Designation %></td>
                            <td><%: gone.JoinDate.HasValue ? gone.JoinDate.Value.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture) : "-" %></td>
                            <td><%: gone.TerminationDate.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture) %></td>
                            <td><%: gone.ServiceLength %></td>
                            <td><span class="employee-status"><%: string.IsNullOrWhiteSpace(gone.Status) ? "-" : gone.Status %></span></td>
                            <td><%: string.IsNullOrWhiteSpace(gone.Remarks) ? "-" : gone.Remarks %></td>
                        </tr>
                    <% } %>
                </tbody>
            </table>
        </div>
        <% if (listCount == 0) { %><p class="no-results">No records in this period.</p><% } %>
    </section>
    </div>
    <% } %>

    <div id="tab-turnover" class="tab-panel" hidden>
    <section class="report-filters term-filters" aria-label="Turnover period">
        <label><span>Year</span><asp:DropDownList ID="ddlTurnYear" runat="server" CssClass="form-select" AutoPostBack="true" /></label>
        <label><span>Period</span><asp:DropDownList ID="ddlTurnPeriod" runat="server" CssClass="form-select" AutoPostBack="true" /></label>
    </section>

    <section class="metric-grid" aria-label="Turnover overview">
        <article class="metric metric-ink">
            <span class="metric-label">Opening headcount</span>
            <strong><%: Model.TurnoverOpening.ToString("N0") %></strong>
            <span class="metric-foot"><%: Model.TurnoverLabel %></span>
        </article>
        <article class="metric metric-green">
            <span class="metric-label">Joiners</span>
            <strong><%: Model.TurnoverJoiners.ToString("N0") %></strong>
            <span class="metric-foot">Joined in period</span>
        </article>
        <article class="metric metric-coral">
            <span class="metric-label">Leavers</span>
            <strong><%: Model.TurnoverLeavers.ToString("N0") %></strong>
            <span class="metric-foot">Closing headcount <%: Model.TurnoverClosing.ToString("N0") %></span>
        </article>
        <article class="metric metric-amber">
            <span class="metric-label">Turnover rate</span>
            <strong><%: Model.TurnoverPercent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) %>%</strong>
            <span class="metric-foot">Leavers / average headcount</span>
        </article>
    </section>

    <section class="chart-grid turnover-charts" aria-label="Turnover charts">
        <article class="chart-panel">
            <div class="panel-heading"><div><p class="eyebrow">TREND</p><h2>Leavers by month</h2></div></div>
            <div class="chart-wrap"><canvas id="turnoverTrendChart" aria-label="Leavers by month"></canvas></div>
        </article>
        <article class="chart-panel">
            <div class="panel-heading"><div><p class="eyebrow">DEPARTMENTS</p><h2>Leavers by department</h2></div></div>
            <div class="chart-wrap"><canvas id="turnoverDeptChart" aria-label="Leavers by department"></canvas></div>
        </article>
    </section>

    <section class="report-table-section">
        <div class="table-heading">
            <div>
                <p class="eyebrow">DEPARTMENT TURNOVER</p>
                <h2>Turnover by department <span><%: Model.TurnoverLabel %></span></h2>
            </div>
        </div>
        <div class="table-meta">Turnover % = leavers / ((opening + closing headcount) / 2). Departments are shown by name.</div>
        <div class="table-scroll">
            <table class="table employee-table">
                <thead>
                    <tr>
                        <th scope="col">Department</th>
                        <th scope="col">Opening</th>
                        <th scope="col">Joiners</th>
                        <th scope="col">Leavers</th>
                        <th scope="col">Closing</th>
                        <th scope="col">Turnover %</th>
                    </tr>
                </thead>
                <tbody>
                    <% foreach (var dept in Model.TurnoverDepartments) { %>
                        <tr>
                            <th class="employee-name" scope="row"><strong><%: dept.Department %></strong></th>
                            <td><%: dept.Opening.ToString("N0") %></td>
                            <td><%: dept.Joiners.ToString("N0") %></td>
                            <td><%: dept.Leavers.ToString("N0") %></td>
                            <td><%: dept.Closing.ToString("N0") %></td>
                            <td><%: dept.TurnoverPercent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) %>%</td>
                        </tr>
                    <% } %>
                    <tr class="total-row">
                        <th scope="row"><strong>Total</strong></th>
                        <td><b><%: Model.TurnoverOpening.ToString("N0") %></b></td>
                        <td><b><%: Model.TurnoverJoiners.ToString("N0") %></b></td>
                        <td><b><%: Model.TurnoverLeavers.ToString("N0") %></b></td>
                        <td><b><%: Model.TurnoverClosing.ToString("N0") %></b></td>
                        <td><b><%: Model.TurnoverPercent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) %>%</b></td>
                    </tr>
                </tbody>
            </table>
        </div>
    </section>
    </div>

    <div id="tab-salary" class="tab-panel" hidden>
    <section class="metric-grid" aria-label="Active employees overview">
        <article class="metric metric-ink">
            <span class="metric-label">Active employees</span>
            <strong><%: Model.SalaryEmployeeCount.ToString("N0") %></strong>
            <span class="metric-foot"><%: Model.SalaryDepartments.Count %> departments</span>
        </article>
        <article class="metric metric-green">
            <span class="metric-label">Total monthly salary</span>
            <strong><%: Model.SalaryGrandTotal.ToString("N2") %></strong>
            <span class="metric-foot">Sum of all earning components</span>
        </article>
    </section>

    <section class="chart-grid chart-full" aria-label="Department headcount and salary">
        <article class="chart-panel">
            <div class="panel-heading"><div><p class="eyebrow">DEPARTMENTS</p><h2>Headcount and total salary by department</h2></div></div>
            <div class="chart-wrap"><canvas id="deptSalaryChart" aria-label="Active headcount and total salary by department"></canvas></div>
        </article>
    </section>

    <section class="report-table-section">
        <div class="table-heading">
            <div>
                <p class="eyebrow">DEPARTMENT SUMMARY</p>
                <h2>Department wise active employees <span><%: Model.SalaryDepartments.Count %> departments</span></h2>
            </div>
        </div>
        <div class="table-meta">Active headcount and total salary per department.</div>
        <div class="table-scroll">
            <table class="table employee-table">
                <thead>
                    <tr>
                        <th scope="col">Department</th>
                        <th scope="col" class="num">Active employees</th>
                        <th scope="col" class="num">Total salary</th>
                    </tr>
                </thead>
                <tbody>
                    <% foreach (var dept in Model.SalaryDepartments) { %>
                        <tr>
                            <th class="employee-name" scope="row"><strong><%: dept.Department %></strong></th>
                            <td class="num"><%: dept.Count.ToString("N0") %></td>
                            <td class="num"><%: dept.Total.ToString("N2") %></td>
                        </tr>
                    <% } %>
                    <tr class="total-row">
                        <th scope="row">Grand total</th>
                        <td class="num"><b><%: Model.SalaryEmployeeCount.ToString("N0") %></b></td>
                        <td class="num"><b><%: Model.SalaryGrandTotal.ToString("N2") %></b></td>
                    </tr>
                </tbody>
            </table>
        </div>
    </section>

    <section class="report-table-section">
        <div class="table-heading">
            <div>
                <p class="eyebrow">ACTIVE EMPLOYEE REGISTER</p>
                <h2>Employees with salary breakup <span><%: Model.SalaryEmployeeCount.ToString("N0") %></span></h2>
            </div>
            <label class="search-box">
                <span class="visually-hidden">Search active employees</span>
                <input id="salarySearch" type="search" class="form-control" placeholder="Find employee or designation" autocomplete="off" />
            </label>
        </div>
        <div class="table-meta">Grouped by department with sub totals. Amounts are the employee's fixed earnings (EmployeeEarnings) by earning code.</div>
        <div class="table-scroll">
            <table class="table employee-table salary-table">
                <thead>
                    <tr>
                        <th scope="col">Employee</th>
                        <th scope="col">Designation</th>
                        <th scope="col">Joined</th>
                        <% foreach (var code in Model.SalaryEarnCodes) { %><th scope="col" class="num"><%: code %></th><% } %>
                        <th scope="col" class="num">Total</th>
                    </tr>
                </thead>
                <tbody>
                    <% foreach (var group in Model.SalaryDepartments) { %>
                        <tr class="dept-row"><th colspan="<%: Model.SalaryEarnCodes.Count + 4 %>" scope="colgroup">Department <%: group.Department %></th></tr>
                        <% foreach (var person in group.Employees) { %>
                            <tr class="salary-row" data-search="<%: person.EmployeeNumber + " " + person.FullName + " " + person.Designation %>">
                                <th class="employee-name" scope="row"><strong><%: person.FullName %></strong><small><%: person.EmployeeNumber %></small></th>
                                <td><%: string.IsNullOrWhiteSpace(person.Designation) ? "-" : person.Designation %></td>
                                <td><%: person.JoinDate.HasValue ? person.JoinDate.Value.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture) : "-" %></td>
                                <% foreach (var code in Model.SalaryEarnCodes) {
                                       decimal amount;
                                       var has = person.Earnings.TryGetValue(code, out amount); %>
                                    <td class="num"><%: has ? amount.ToString("N2") : "-" %></td>
                                <% } %>
                                <td class="num"><b><%: person.Total.ToString("N2") %></b></td>
                            </tr>
                        <% } %>
                        <tr class="subtotal-row">
                            <th colspan="3" scope="row">Sub total - <%: group.Department %> (<%: group.Count.ToString("N0") %> employees)</th>
                            <% foreach (var code in Model.SalaryEarnCodes) { %><td class="num"><%: group.CodeTotal(code).ToString("N2") %></td><% } %>
                            <td class="num"><%: group.Total.ToString("N2") %></td>
                        </tr>
                    <% } %>
                    <tr class="total-row">
                        <th colspan="3" scope="row">Grand total (<%: Model.SalaryEmployeeCount.ToString("N0") %> employees)</th>
                        <% foreach (var code in Model.SalaryEarnCodes) { %><td class="num"><b><%: Model.SalaryCodeTotal(code).ToString("N2") %></b></td><% } %>
                        <td class="num"><b><%: Model.SalaryGrandTotal.ToString("N2") %></b></td>
                    </tr>
                </tbody>
            </table>
        </div>
        <p id="noSalaryResults" class="no-results" hidden>No employees match this search.</p>
    </section>
    </div>
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

        const probationData = { active: <%= Model.ActiveEmployeeCount - Model.ProbationCount %>, probation: <%= Model.ProbationCount %> };
        let probationChartDrawn = false;
        let terminationChartDrawn = false;
        let turnoverChartsDrawn = false;
        function drawTurnoverCharts() {
            if (turnoverChartsDrawn || !window.Chart) return;
            turnoverChartsDrawn = true;
            const trend = <%= ToJson(Model.TurnoverTrend) %>;
            const depts = <%= ToJson(Model.TurnoverDepartments) %>.filter(d => d.Leavers > 0).sort((a, b) => b.Leavers - a.Leavers);
            new Chart(document.getElementById("turnoverTrendChart"), {
                type: "bar",
                data: { labels: trend.map(r => r.Label), datasets: [{ label: "Leavers", data: trend.map(r => r.Leavers), backgroundColor: "#cf5545", borderRadius: 4, maxBarThickness: 34 }] },
                options: { maintainAspectRatio: false, plugins: { legend: { display: false } }, scales: { x: { grid: { display: false } }, y: { beginAtZero: true, ticks: { precision: 0 }, grid: { color: "#edf0ed" } } } }
            });
            new Chart(document.getElementById("turnoverDeptChart"), {
                type: "bar",
                data: { labels: depts.map(r => r.Department), datasets: [{ label: "Leavers", data: depts.map(r => r.Leavers), backgroundColor: "#e3a32e", borderRadius: 4, maxBarThickness: 26 }] },
                options: { indexAxis: "y", maintainAspectRatio: false, plugins: { legend: { display: false } }, scales: { x: { beginAtZero: true, ticks: { precision: 0 }, grid: { color: "#edf0ed" } }, y: { grid: { display: false } } } }
            });
        }
        let deptSalaryDrawn = false;
        function drawDeptSalaryChart() {
            if (deptSalaryDrawn || !window.Chart) return;
            deptSalaryDrawn = true;
            const depts = <%= ToJson(Model.SalaryDepartments.Select(d => new { Department = d.Department, Count = d.Count, Total = d.Total }).ToList()) %>;
            new Chart(document.getElementById("deptSalaryChart"), {
                type: "bar",
                data: {
                    labels: depts.map(d => d.Department),
                    datasets: [
                        { label: "Active employees", data: depts.map(d => d.Count), backgroundColor: "#2f7d6d", borderRadius: 4, maxBarThickness: 40, yAxisID: "y" },
                        { label: "Total salary", data: depts.map(d => d.Total), backgroundColor: "#e3a32e", borderRadius: 4, maxBarThickness: 40, yAxisID: "y1" }
                    ]
                },
                options: {
                    maintainAspectRatio: false,
                    scales: {
                        x: { grid: { display: false }, ticks: { autoSkip: false, maxRotation: 60, minRotation: 45 } },
                        y: { beginAtZero: true, ticks: { precision: 0 }, title: { display: true, text: "Employees" }, grid: { color: "#edf0ed" } },
                        y1: { beginAtZero: true, position: "right", title: { display: true, text: "Total salary" }, grid: { drawOnChartArea: false } }
                    }
                }
            });
        }
        function drawTerminationChart() {
            if (terminationChartDrawn || !window.Chart) return;
            terminationChartDrawn = true;
            document.querySelectorAll(".gone-chart").forEach(c => {
                if (c.offsetParent === null) return;
                new Chart(c, {
                    type: "pie",
                    data: {
                        labels: ["Active", c.dataset.label],
                        datasets: [{ data: [+c.dataset.active, +c.dataset.count], backgroundColor: ["#2f7d6d", "#cf5545"], borderWidth: 0, hoverOffset: 5 }]
                    },
                    options: { maintainAspectRatio: false, plugins: { legend: { display: false } } }
                });
            });
        }
        function drawProbationChart() {
            if (probationChartDrawn || !window.Chart) return;
            probationChartDrawn = true;
            new Chart(document.getElementById("probationChart"), {
                type: "pie",
                data: {
                    labels: ["Active (confirmed)", "Under probation"],
                    datasets: [{ data: [probationData.active, probationData.probation], backgroundColor: ["#2f7d6d", "#e3a32e"], borderWidth: 0, hoverOffset: 5 }]
                },
                options: { maintainAspectRatio: false, plugins: { legend: { display: false } } }
            });
        }
        function showTab(name) {
            document.querySelectorAll(".report-tab").forEach(t => t.classList.toggle("active", t.dataset.tab === name));
            document.querySelectorAll(".tab-panel").forEach(p => p.hidden = p.id !== "tab-" + name);
            try { sessionStorage.setItem("reportTab", name); } catch (e) { }
            document.getElementById("termFiltersBar").hidden = name !== "resigned" && name !== "terminated";
            if (name === "probation") drawProbationChart();
            if (name === "salary") drawDeptSalaryChart();
            if (name === "resigned" || name === "terminated") { terminationChartDrawn = false; drawTerminationChart(); }
            if (name === "turnover") drawTurnoverCharts();
        }
        document.querySelectorAll(".report-tab").forEach(t => t.addEventListener("click", () => showTab(t.dataset.tab)));
        let savedTab = "turnover";
        try { savedTab = sessionStorage.getItem("reportTab") || "turnover"; } catch (e) { }
        if (!document.querySelector('.report-tab[data-tab="' + savedTab + '"]')) savedTab = "turnover";
        showTab(savedTab);

        const salarySearch = document.getElementById("salarySearch");
        salarySearch.addEventListener("keydown", event => { if (event.key === "Enter") event.preventDefault(); });
        salarySearch.addEventListener("input", () => {
            const term = salarySearch.value.trim().toLocaleLowerCase();
            let visible = 0;
            for (const row of document.querySelectorAll(".salary-row")) {
                const show = row.dataset.search.toLocaleLowerCase().includes(term);
                row.hidden = !show;
                visible += show ? 1 : 0;
            }
            document.getElementById("noSalaryResults").hidden = visible > 0;
        });

        function exportName(section) {
            const panel = section.closest(".tab-panel");
            const title = (section.querySelector("h2").firstChild.textContent || "Report").trim().replace(/[^\w]+/g, "_");
            const company = (document.getElementById("<%= ddlCompany.ClientID %>") || {}).value || "";
            return "HR_" + title + (company ? "_" + company : "") + "_" + new Date().toISOString().slice(0, 10);
        }
        function tableClone(section) {
            const clone = section.querySelector("table").cloneNode(true);
            clone.querySelectorAll("[hidden]").forEach(el => el.remove());
            clone.querySelectorAll("small").forEach(el => el.textContent = " (" + el.textContent + ")");
            return clone;
        }
        function exportExcel(section) {
            const title = section.querySelector("h2").firstChild.textContent.trim();
            const html = '<html xmlns:x="urn:schemas-microsoft-com:office:excel"><head><meta charset="utf-8"></head><body><h3>' + title + '</h3>' + tableClone(section).outerHTML + '</body></html>';
            const link = document.createElement("a");
            link.href = URL.createObjectURL(new Blob(["\ufeff", html], { type: "application/vnd.ms-excel" }));
            link.download = exportName(section) + ".xls";
            document.body.appendChild(link); link.click(); link.remove();
        }
        function exportPdf(section) {
            const title = section.querySelector("h2").firstChild.textContent.trim();
            const company = document.querySelector(".heading-note");
            const w = window.open("", "_blank");
            if (!w) { alert("Please allow pop-ups for this site to export PDF."); return; }
            w.document.write('<html><head><meta charset="utf-8"><title>' + exportName(section) + '</title><style>' +
                '@page{size:A4 landscape;margin:10mm}body{font-family:Segoe UI,Arial,sans-serif;font-size:10px;color:#222}h2{margin:0 0 2px;font-size:15px}p{margin:0 0 8px;color:#666}' +
                'table{border-collapse:collapse;width:100%}th,td{border:1px solid #bbb;padding:3px 5px;text-align:left}th{background:#e6ebe8}tr{page-break-inside:avoid}thead{display:table-header-group}' +
                '.num,td.text-end,th.text-end{text-align:right}</style></head><body><h2>HR Module Power BI Dashboard - ' + title + '</h2><p>' +
                (company ? company.textContent.replace(/\s+/g, " ").trim() : "") + '</p>' + tableClone(section).outerHTML + '</body></html>');
            w.document.close(); w.focus();
            setTimeout(() => { w.print(); }, 400);
        }
        document.querySelectorAll(".report-table-section").forEach(section => {
            const heading = section.querySelector(".table-heading");
            if (!heading || !section.querySelector("table")) return;
            const bar = document.createElement("div");
            bar.className = "export-bar";
            [["Export Excel", exportExcel], ["Export PDF", exportPdf]].forEach(pair => {
                const b = document.createElement("button");
                b.type = "button"; b.className = "btn btn-outline-dark btn-sm export-btn"; b.textContent = pair[0];
                b.addEventListener("click", () => pair[1](section));
                bar.appendChild(b);
            });
            heading.appendChild(bar);
        });

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
