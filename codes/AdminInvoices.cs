using System.Globalization;
using System.Text;
using System.Text.Json;
using HomeServices.Core;
using HomeServices.Models;
using SkyNet;

namespace HomeServices.codes
{
    public class AdminInvoices : WebPage
    {
        private const string AreaKey = "invoices";
        private const int Size = 15;

        private static readonly (string Key, string Name)[] Views =
        {
            ("ready", "To invoice"), ("draft", "Draft"), ("unpaid", "Unpaid"), ("overdue", "Overdue"), ("paid", "Paid"), ("void", "Void"), ("all", "All")
        };

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Invoices | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminInvoicesJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminInvoicesJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("ai-brand", Ui.Esc(site.Brand));
            response.SetElementContents("ai-menu", await Auth.Menu("ai", "AdminInvoices", c));
            response.SetElementContents("ai-who", Auth.Who("ai", c));
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"ai-panel\"><h1>No access</h1><p class=\"ai-hint\">You do not have access to " + what + ". Ask a manager if you need it.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c != null)
            {
                await Auth.Close(c.Tok);
                await Store.Log(c.U.Id, "sign out", c.U.Login, true);
            }
            response.ExecuteScript("AdminInvoicesJs.expired();");
            return response;
        }

        private static string ViewKey(string? v)
        {
            string k = (v ?? string.Empty).Trim();
            return Views.Any(x => x.Key == k) ? k : "unpaid";
        }

        private static bool InView(InvoiceRec i, string view)
        {
            return view switch
            {
                "draft" => i.Status == "draft",
                "unpaid" => i.Status == "issued",
                "overdue" => Work.Overdue(i),
                "paid" => i.Status == "paid",
                "void" => i.Status == "void",
                _ => true
            };
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            await Shell(response, c, site);
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                response.SetElementContents("ai-main", NoAccess("invoices"));
                return response;
            }
            string view = ViewKey(GetDataValue("view"));
            List<InvoiceRec> inv = await Store.Invoices();
            List<JobRec> jobs = await Store.Jobs();
            decimal unpaid = inv.Where(i => i.Status == "issued").Sum(i => i.Total);
            decimal month = inv.Where(i => i.Status == "paid" && i.PaidOn.StartsWith(DateTime.Today.ToString("yyyy-MM", CultureInfo.InvariantCulture), StringComparison.Ordinal)).Sum(i => i.Total);
            StringBuilder sb = new StringBuilder("<div class=\"ai-ph\"><div><h1>Invoices</h1><p>Invoices for finished jobs. Customers pay after the job by card, cash or check; record the payment here.</p></div><div class=\"ai-sums\"><span><small>Unpaid</small><b>" + Ui.Money(unpaid) + "</b></span><span><small>Paid this month</small><b>" + Ui.Money(month) + "</b></span></div></div>");
            sb.Append("<div class=\"ai-tools\"><div class=\"ai-chips\" id=\"ai-chips\">" + Chips(inv, jobs, view) + "</div><input type=\"search\" id=\"ai-term\" placeholder=\"Search name or invoice number\" maxlength=\"60\" oninput=\"AdminInvoicesJs.search()\" aria-label=\"Search invoices\"></div>");
            sb.Append("<div id=\"ai-list\">" + ListHtml(site, c, inv, jobs, view, string.Empty, 1) + "</div>");
            response.SetElementContents("ai-main", sb.ToString());
            response.ExecuteScript("AdminInvoicesJs.loaded('" + view + "');");
            string show = Ui.Clip(GetDataValue("show"), 20);
            string from = Ui.Clip(GetDataValue("from"), 20);
            if (show.Length > 0 && inv.Any(i => i.Id == show))
            {
                response.ExecuteScript("AdminInvoicesJs.edit('" + Ui.Js(show) + "');");
            }
            else if (from.Length > 0 && Access.Can(c.U, AreaKey, 'E'))
            {
                JobRec? j = jobs.FirstOrDefault(x => x.Id == from);
                if (j != null && j.InvoiceId.Length > 0)
                {
                    response.ExecuteScript("AdminInvoicesJs.edit('" + Ui.Js(j.InvoiceId) + "');");
                }
                else if (j != null && j.Status == "done")
                {
                    response.ExecuteScript("AdminInvoicesJs.make('" + Ui.Js(from) + "');");
                }
            }
            return response;
        }

        private static List<JobRec> Ready(List<JobRec> jobs)
        {
            return jobs.Where(j => j.Status == "done" && j.InvoiceId.Length == 0).OrderBy(j => j.Date).ToList();
        }

        private static string Chips(List<InvoiceRec> inv, List<JobRec> jobs, string view)
        {
            StringBuilder sb = new StringBuilder();
            foreach ((string key, string name) in Views)
            {
                int n = key == "ready" ? Ready(jobs).Count : key is "draft" or "unpaid" or "overdue" ? inv.Count(i => InView(i, key)) : 0;
                sb.Append("<button type=\"button\" class=\"ai-chip" + (key == view ? " ai-act" : string.Empty) + "\" onclick=\"AdminInvoicesJs.view('" + key + "')\">" + name + (n > 0 ? "<i>" + n + "</i>" : string.Empty) + "</button>");
            }
            return sb.ToString();
        }

        private static string ListHtml(SiteData site, Ctx c, List<InvoiceRec> inv, List<JobRec> jobs, string view, string term, int page)
        {
            string t = term.ToLowerInvariant();
            if (view == "ready")
            {
                List<JobRec> ready = Ready(jobs).Where(j => t.Length == 0 || j.Name.ToLowerInvariant().Contains(t) || j.Id.ToLowerInvariant().Contains(t)).ToList();
                if (ready.Count == 0)
                {
                    return "<div class=\"ai-panel ai-empty\">No finished jobs are waiting for an invoice.</div>";
                }
                StringBuilder r = new StringBuilder("<div class=\"ai-wrapt\"><table class=\"ai-table\"><thead><tr><th>Job</th><th>Done</th><th>Customer</th><th>Work</th><th>Technician</th><th></th></tr></thead><tbody>");
                foreach (JobRec j in ready)
                {
                    r.Append("<tr><td data-l=\"Job\"><b>" + j.Id + "</b></td><td data-l=\"Done\">" + Ui.Esc(Ui.Day(j.Date)) + "</td><td data-l=\"Customer\"><b>" + Ui.Esc(j.Name) + "</b><small>" + Ui.Esc(j.Town) + "</small></td><td data-l=\"Work\">" + Ui.Esc(j.Summary) + "</td><td data-l=\"Technician\">" + Ui.Esc(Work.TechName(site, j.TechId)) + "</td><td data-l=\"\">" + (Access.Can(c.U, AreaKey, 'E') ? "<button type=\"button\" class=\"ai-btn ai-btn-sm\" onclick=\"AdminInvoicesJs.make('" + j.Id + "')\">Create invoice</button>" : string.Empty) + "</td></tr>");
                }
                return r.Append("</tbody></table></div>").ToString();
            }
            List<InvoiceRec> rows = inv.Where(i => InView(i, view) && (t.Length == 0 || i.Name.ToLowerInvariant().Contains(t) || i.Number.ToLowerInvariant().Contains(t) || i.JobId.ToLowerInvariant().Contains(t))).OrderByDescending(i => i.Issued.Length == 0 ? "9999" : i.Issued).ThenByDescending(i => i.Created).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"ai-panel ai-empty\">" + (inv.Count == 0 ? "No invoices yet. Finished jobs appear under To invoice." : "No invoices match.") + "</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"ai-wrapt\"><table class=\"ai-table\"><thead><tr><th>Invoice</th><th>Customer</th><th>Issued</th><th>Due</th><th>Status</th><th class=\"ai-num\">Total</th></tr></thead><tbody>");
            foreach (InvoiceRec i in rows.Skip((page - 1) * Size).Take(Size))
            {
                sb.Append("<tr class=\"ai-row\" tabindex=\"0\" onclick=\"AdminInvoicesJs.edit('" + i.Id + "')\" onkeydown=\"if(event.key==='Enter'){AdminInvoicesJs.edit('" + i.Id + "')}\"><td data-l=\"Invoice\"><b>" + (i.Number.Length > 0 ? Ui.Esc(i.Number) : "Draft") + "</b><small>" + Ui.Esc(i.JobId) + "</small></td><td data-l=\"Customer\">" + Ui.Esc(i.Name) + "</td><td data-l=\"Issued\">" + (i.Issued.Length > 0 ? Ui.Esc(Ui.Day(i.Issued)) : "&ndash;") + "</td><td data-l=\"Due\">" + (i.Due.Length > 0 ? Ui.Esc(Ui.Day(i.Due)) : "&ndash;") + "</td><td data-l=\"Status\">" + Work.InvoicePill("ai", i) + (i.Status == "paid" ? "<small>" + Ui.Esc(Work.MethodName(i.Method)) + "</small>" : string.Empty) + "</td><td data-l=\"Total\" class=\"ai-num\">" + Ui.Money(i.Total) + "</td></tr>");
            }
            sb.Append("</tbody></table></div>" + Ui.Pager("ai", page, Size, rows.Count, "invoice", "invoices", "AdminInvoicesJs.page"));
            return sb.ToString();
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "list invoices", string.Empty, "You do not have access to invoices.");
            }
            await Refresh(response, c);
            return response;
        }

        private async Task Refresh(ApiResponse response, Ctx c)
        {
            SiteData site = await Store.LoadSite();
            List<InvoiceRec> inv = await Store.Invoices();
            List<JobRec> jobs = await Store.Jobs();
            string view = ViewKey(GetDataValue("st"));
            response.SetElementContents("ai-list", ListHtml(site, c, inv, jobs, view, Ui.Clip(GetDataValue("term"), 60), Ui.PageNo(GetDataValue("pg"))));
            response.SetElementContents("ai-chips", Chips(inv, jobs, view));
        }

        private static string LineRow(LineItem l)
        {
            return "<div class=\"ai-ln\"><input type=\"text\" class=\"ai-lt\" data-f=\"t\" maxlength=\"200\" value=\"" + Ui.Esc(l.Text) + "\" placeholder=\"Work or part\" aria-label=\"Description\"><input type=\"number\" class=\"ai-lq\" data-f=\"q\" min=\"0\" step=\"0.25\" value=\"" + Ui.Num(l.Qty) + "\" aria-label=\"Quantity\"><input type=\"number\" class=\"ai-lp\" data-f=\"p\" min=\"0\" step=\"0.01\" value=\"" + l.Price.ToString("0.00", CultureInfo.InvariantCulture) + "\" aria-label=\"Unit price\"><button type=\"button\" class=\"ai-ibtn ai-lx\" aria-label=\"Remove line\" onclick=\"AdminInvoicesJs.dropLine(this)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M6 6l12 12M18 6L6 18\"/></svg></button></div>";
        }

        private static string TotalsHtml(decimal sub, decimal rate, decimal tax, decimal total)
        {
            return "<div><span>Subtotal</span><b>" + Ui.Money(sub) + "</b></div><div><span>Tax " + Ui.Num(rate) + "%</span><b>" + Ui.Money(tax) + "</b></div><div class=\"ai-itot-g\"><span>Total</span><b>" + Ui.Money(total) + "</b></div>";
        }

        private static string FormHtml(SiteData site, InvoiceRec i, JobRec? j, Ctx c)
        {
            bool canE = Access.Can(c.U, AreaKey, 'E');
            bool canX = Access.Can(c.U, AreaKey, 'X');
            bool draft = i.Status == "draft" && canE;
            StringBuilder sb = new StringBuilder("<div class=\"ai-stack\" id=\"ai-form\" data-id=\"" + i.Id + "\"><div class=\"ai-inv\" id=\"ai-print\">");
            sb.Append("<div class=\"ai-inv-h\"><div><b class=\"ai-inv-brand\">" + Ui.Esc(site.Brand) + "</b><small>" + Ui.Esc(site.Address) + "<br>" + Ui.Esc(site.Phone) + " &middot; " + Ui.Esc(site.Email) + "</small></div><div class=\"ai-inv-no\"><span>Invoice</span><b>" + (i.Number.Length > 0 ? Ui.Esc(i.Number) : "Draft") + "</b>" + Work.InvoicePill("ai", i) + "</div></div>");
            sb.Append("<div class=\"ai-inv-meta\"><div><small>Bill to</small><b>" + Ui.Esc(i.Name) + "</b><span>" + Ui.Esc(i.Address) + "</span><span>" + Ui.Esc(i.Phone) + (i.Email.Length > 0 ? " &middot; " + Ui.Esc(i.Email) : string.Empty) + "</span></div><div><small>Job</small><b>" + Ui.Esc(i.JobId) + "</b><span>" + (j != null ? Ui.Esc(Ui.Day(j.Date)) + " &middot; " + Ui.Esc(Work.TechName(site, j.TechId)) : string.Empty) + "</span><span>Quote " + Ui.Esc(i.QuoteId) + "</span></div><div><small>Dates</small><span>Issued: " + (i.Issued.Length > 0 ? Ui.Esc(Ui.Day(i.Issued)) : "not yet") + "</span><span>Due: " + (i.Due.Length > 0 ? Ui.Esc(Ui.Day(i.Due)) : "&ndash;") + "</span></div></div>");
            if (draft)
            {
                sb.Append("<div class=\"ai-lnh\"><span>Description</span><span>Qty</span><span>Unit price</span><span></span></div><div class=\"ai-lines\" id=\"ai-lines\">" + string.Concat(i.Lines.Select(LineRow)) + "</div><template id=\"ai-lntpl\">" + LineRow(new LineItem { Qty = 1 }) + "</template><div class=\"ai-lnadd\"><button type=\"button\" class=\"ai-btn ai-btn-ghost ai-btn-sm\" onclick=\"AdminInvoicesJs.addLine()\">+ Add a line</button></div>");
                sb.Append("<div class=\"ai-qfoot\"><label class=\"ai-field ai-trip\" for=\"ai-trip\"><span>Trip fee</span><input type=\"number\" id=\"ai-trip\" min=\"0\" step=\"0.01\" value=\"" + i.TripFee.ToString("0.00", CultureInfo.InvariantCulture) + "\"></label><div class=\"ai-itot\" id=\"ai-itot\">" + TotalsHtml(i.Subtotal, i.TaxRate, i.Tax, i.Total) + "</div></div><em class=\"ai-err\" id=\"ai-e-lines\"></em>");
                sb.Append("<label class=\"ai-field\" for=\"ai-note\"><span>Note on the invoice</span><textarea id=\"ai-note\" rows=\"2\" maxlength=\"600\">" + Ui.Esc(i.Note) + "</textarea></label>");
            }
            else
            {
                sb.Append("<table class=\"ai-itab\"><thead><tr><th>Description</th><th class=\"ai-num\">Qty</th><th class=\"ai-num\">Price</th><th class=\"ai-num\">Amount</th></tr></thead><tbody>");
                foreach (LineItem l in i.Lines)
                {
                    sb.Append("<tr><td>" + Ui.Esc(l.Text) + "</td><td class=\"ai-num\">" + Ui.Num(l.Qty) + "</td><td class=\"ai-num\">" + Ui.Money(l.Price) + "</td><td class=\"ai-num\">" + Ui.Money(Math.Round(l.Qty * l.Price, 2, MidpointRounding.AwayFromZero)) + "</td></tr>");
                }
                if (i.TripFee > 0)
                {
                    sb.Append("<tr><td>Trip fee</td><td class=\"ai-num\">1</td><td class=\"ai-num\">" + Ui.Money(i.TripFee) + "</td><td class=\"ai-num\">" + Ui.Money(i.TripFee) + "</td></tr>");
                }
                sb.Append("</tbody></table><div class=\"ai-itot\">" + TotalsHtml(i.Subtotal, i.TaxRate, i.Tax, i.Total) + "</div>");
                if (i.Note.Length > 0)
                {
                    sb.Append("<p class=\"ai-hint\">" + Ui.Esc(i.Note) + "</p>");
                }
                sb.Append("<p class=\"ai-hint\">Pay by card, cash or check. Please write the invoice number on your check.</p>");
            }
            if (i.Status == "paid")
            {
                sb.Append("<div class=\"ai-note ai-note-ok\">Paid " + Ui.Esc(Ui.Day(i.PaidOn)) + " by " + Ui.Esc(Work.MethodName(i.Method).ToLowerInvariant()) + (i.Ref.Length > 0 ? ", reference " + Ui.Esc(i.Ref) : string.Empty) + ".</div>");
            }
            if (i.Status == "void")
            {
                sb.Append("<div class=\"ai-note\">Void" + (i.VoidWhy.Length > 0 ? ": " + Ui.Esc(i.VoidWhy) : ".") + "</div>");
            }
            sb.Append("</div>");
            if (i.Status == "issued" && canX)
            {
                sb.Append("<form class=\"ai-pay\" id=\"ai-pay\" onsubmit=\"return AdminInvoicesJs.pay('" + i.Id + "')\" novalidate><div class=\"ai-sect\">Record payment</div><div class=\"ai-grid3\"><label class=\"ai-field\" for=\"ai-f-method\"><span>Paid by</span><select id=\"ai-f-method\" data-k=\"method\"><option value=\"\">Choose</option>" + string.Concat(Work.Methods.Select(m => "<option value=\"" + m.Key + "\">" + m.Name + "</option>")) + "</select><em class=\"ai-err\" id=\"ai-e-method\"></em></label><label class=\"ai-field\" for=\"ai-f-ref\"><span>Reference</span><input type=\"text\" id=\"ai-f-ref\" data-k=\"ref\" maxlength=\"40\" placeholder=\"Check number or card slip\"><em class=\"ai-err\" id=\"ai-e-ref\"></em></label><label class=\"ai-field\" for=\"ai-f-paid\"><span>Paid on</span><input type=\"date\" id=\"ai-f-paid\" data-k=\"paid\" value=\"" + Work.Today() + "\" max=\"" + Work.Today() + "\"><em class=\"ai-err\" id=\"ai-e-paid\"></em></label></div><p class=\"ai-hint\">No card numbers here: only the last digits on the slip or the check number.</p><div class=\"ai-acts\"><button type=\"submit\" class=\"ai-btn\">Mark paid</button></div></form>");
            }
            sb.Append("<div class=\"ai-acts\">");
            if (draft)
            {
                sb.Append("<button type=\"button\" class=\"ai-btn ai-btn-ghost\" onclick=\"AdminInvoicesJs.save('" + i.Id + "',0)\">Save draft</button><button type=\"button\" class=\"ai-btn\" onclick=\"AdminInvoicesJs.save('" + i.Id + "',1)\">Issue invoice</button><button type=\"button\" class=\"ai-btn ai-btn-danger ai-push\" onclick=\"AdminInvoicesJs.discard('" + i.Id + "')\">Discard draft</button>");
            }
            if (i.Status != "draft")
            {
                sb.Append("<button type=\"button\" class=\"ai-btn ai-btn-ghost\" onclick=\"window.print()\">Print</button>");
            }
            if (i.Status == "issued" && canX)
            {
                sb.Append("<button type=\"button\" class=\"ai-btn ai-btn-danger ai-push\" onclick=\"AdminInvoicesJs.voidIt('" + i.Id + "')\">Void invoice</button>");
            }
            sb.Append("</div></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Edit()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 40);
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "open invoice", id, "You do not have access to invoices.");
            }
            InvoiceRec? i = (await Store.Invoices()).FirstOrDefault(x => x.Id == id);
            if (i == null)
            {
                response.ExecuteScript("AdminInvoicesJs.toast('That invoice was not found.');");
                return response;
            }
            SiteData site = await Store.LoadSite();
            JobRec? j = (await Store.Jobs()).FirstOrDefault(x => x.Id == i.JobId);
            response.SetElementContents("ai-mbody", FormHtml(site, i, j, c));
            response.ExecuteScript("AdminInvoicesJs.opened('" + Ui.Js((i.Number.Length > 0 ? "Invoice " + i.Number : "Draft invoice") + " · " + i.Name) + "');");
            return response;
        }

        public async Task<ApiResponse> Make()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string jid = Ui.Clip(GetDataValue("job"), 20);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "create invoice", jid, "You can look at invoices but not create them.");
            }
            string error = string.Empty;
            InvoiceRec? made = null;
            JobRec? job = null;
            using (await Store.Hold())
            {
                List<JobRec> jobs = await Store.Jobs();
                List<InvoiceRec> inv = await Store.Invoices();
                List<QuoteRec> quotes = await Store.Quotes();
                job = jobs.FirstOrDefault(x => x.Id == jid);
                if (job == null || job.Status != "done")
                {
                    error = "Only finished jobs can be invoiced.";
                }
                else if (job.InvoiceId.Length > 0)
                {
                    error = "This job already has an invoice.";
                }
                else
                {
                    QuoteRec? q = quotes.FirstOrDefault(x => x.Id == job.QuoteId);
                    made = new InvoiceRec { Id = "inv-" + Access.NewToken(6), JobId = job.Id, QuoteId = job.QuoteId, Status = "draft", Name = job.Name, Phone = job.Phone, Email = job.Email, Address = job.Address + ", " + job.Town + " " + job.Zip, Lines = q?.Lines.Select(l => new LineItem { Text = l.Text, Qty = l.Qty, Price = l.Price }).ToList() ?? new List<LineItem>(), TripFee = q?.TripFee ?? 0, TaxRate = q?.TaxRate ?? 0, Created = DateTime.UtcNow };
                    (made.Subtotal, made.Tax, made.Total) = Work.Totals(made.Lines, made.TripFee, made.TaxRate);
                    inv.Add(made);
                    job.InvoiceId = made.Id;
                    await Store.SaveInvoices(inv);
                    await Store.SaveJobs(jobs);
                }
            }
            if (made == null)
            {
                response.ExecuteScript("AdminInvoicesJs.toast('" + Ui.Js(error) + "');");
                return response;
            }
            await Store.Log(c.U.Id, "create invoice", made.JobId + " · " + made.Name, true);
            SiteData site = await Store.LoadSite();
            await Refresh(response, c);
            response.SetElementContents("ai-mbody", FormHtml(site, made, job, c));
            response.ExecuteScript("AdminInvoicesJs.opened('" + Ui.Js("Draft invoice · " + made.Name) + "');AdminInvoicesJs.toast('Draft created from the accepted quote. Check the lines, then issue it.');");
            return response;
        }

        private static List<LineItem>? ParseLines(string? json, out string error)
        {
            error = string.Empty;
            List<LineItem> list = new List<LineItem>();
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json))
                {
                    if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    {
                        error = "The lines could not be read.";
                        return null;
                    }
                    foreach (JsonElement e in doc.RootElement.EnumerateArray())
                    {
                        string t = e.TryGetProperty("t", out JsonElement te) && te.ValueKind == JsonValueKind.String ? (te.GetString() ?? string.Empty).Trim() : string.Empty;
                        string qs = e.TryGetProperty("q", out JsonElement qe) && qe.ValueKind == JsonValueKind.String ? qe.GetString() ?? string.Empty : string.Empty;
                        string ps = e.TryGetProperty("p", out JsonElement pe) && pe.ValueKind == JsonValueKind.String ? pe.GetString() ?? string.Empty : string.Empty;
                        if (t.Length == 0 && qs.Trim().Length == 0 && ps.Trim().Length == 0)
                        {
                            continue;
                        }
                        decimal? qty = Work.Amount(qs, 0.01m, 9999m);
                        decimal? price = Work.Amount(ps, 0m, 100000m);
                        if (t.Length == 0 || t.Length > 200)
                        {
                            error = "Every line needs a description of up to 200 characters.";
                            return null;
                        }
                        if (qty == null || price == null)
                        {
                            error = "Check the quantity and price on “" + t + "”.";
                            return null;
                        }
                        list.Add(new LineItem { Text = t, Qty = qty.Value, Price = price.Value });
                    }
                }
            }
            catch (JsonException)
            {
                error = "The lines could not be read.";
                return null;
            }
            if (list.Count > 40)
            {
                error = "An invoice can have up to 40 lines.";
                return null;
            }
            return list;
        }

        public async Task<ApiResponse> Calc()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null || !Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 40);
            InvoiceRec? i = (await Store.Invoices()).FirstOrDefault(x => x.Id == id);
            List<LineItem>? lines = ParseLines(GetDataValue("lines"), out string error);
            decimal trip = Work.Amount(GetDataValue("trip"), 0, 10000) ?? 0;
            response.SetElementContents("ai-e-lines", Ui.Esc(error));
            if (lines != null && i != null)
            {
                (decimal sub, decimal tax, decimal total) = Work.Totals(lines, trip, i.TaxRate);
                response.SetElementContents("ai-itot", TotalsHtml(sub, i.TaxRate, tax, total));
            }
            return response;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 40);
            bool issue = GetDataValue("issue") == "1";
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, issue ? "issue invoice" : "save invoice", id, "You can look at invoices but not change them.");
            }
            List<LineItem>? lines = ParseLines(GetDataValue("lines"), out string err);
            decimal? trip = Work.Amount(GetDataValue("trip"), 0, 10000);
            if (err.Length == 0 && trip == null)
            {
                err = "Enter a trip fee from 0 to 10,000.";
            }
            if (err.Length == 0 && issue && (lines == null || lines.Count == 0))
            {
                err = "Add at least one line before you issue the invoice.";
            }
            response.SetElementContents("ai-e-lines", Ui.Esc(err));
            if (err.Length > 0 || lines == null || trip == null)
            {
                return response;
            }
            string error = string.Empty;
            InvoiceRec? done = null;
            using (await Store.Hold())
            {
                List<InvoiceRec> inv = await Store.Invoices();
                InvoiceRec? i = inv.FirstOrDefault(x => x.Id == id);
                if (i == null || i.Status != "draft")
                {
                    error = "Only draft invoices can be changed.";
                }
                else
                {
                    i.Lines = lines;
                    i.TripFee = trip.Value;
                    (i.Subtotal, i.Tax, i.Total) = Work.Totals(lines, trip.Value, i.TaxRate);
                    i.Note = Ui.Clip(GetDataValue("note"), 600);
                    if (issue)
                    {
                        SiteData site = await Store.LoadSite();
                        int n = Math.Max(1, site.NextInvoice);
                        string prefix = site.InvoicePrefix.Length > 0 ? site.InvoicePrefix : "INV";
                        string number;
                        do
                        {
                            number = prefix + "-" + DateTime.Today.Year + "-" + n.ToString("0000", CultureInfo.InvariantCulture);
                            n++;
                        }
                        while (inv.Any(x => x.Number == number));
                        site.NextInvoice = n;
                        await Store.SaveSite(site);
                        i.Number = number;
                        i.Status = "issued";
                        i.Issued = Work.Today();
                        i.Due = Work.Iso(DateTime.Today.AddDays(Math.Max(0, site.PayDays)));
                    }
                    await Store.SaveInvoices(inv);
                    done = i;
                }
            }
            if (done == null)
            {
                response.ExecuteScript("AdminInvoicesJs.toast('" + Ui.Js(error) + "');");
                return response;
            }
            await Store.Log(c.U.Id, issue ? "issue invoice" : "save invoice", (done.Number.Length > 0 ? done.Number : done.JobId) + " · " + done.Name + " · " + Ui.Money(done.Total), true);
            SiteData s2 = await Store.LoadSite();
            JobRec? j = (await Store.Jobs()).FirstOrDefault(x => x.Id == done.JobId);
            await Refresh(response, c);
            response.SetElementContents("ai-mbody", FormHtml(s2, done, j, c));
            response.ExecuteScript("AdminInvoicesJs.saved('" + (issue ? "Invoice " + done.Number + " issued" : "Draft saved") + "');" + (issue ? "AdminInvoicesJs.opened('" + Ui.Js("Invoice " + done.Number + " · " + done.Name) + "');" : string.Empty));
            return response;
        }

        public async Task<ApiResponse> Pay()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 40);
            if (!Access.Can(c.U, AreaKey, 'X'))
            {
                return await Refuse(response, c, "mark invoice paid", id, "You are not allowed to record payments.");
            }
            string method = (GetDataValue("method") ?? string.Empty).Trim();
            string reference = Ui.Clip(GetDataValue("ref"), 60);
            DateTime? paid = Work.ParseDay(GetDataValue("paid"));
            string eMethod = Work.Methods.Any(m => m.Key == method) ? string.Empty : "Choose card, cash or check.";
            string eRef = method == "check" && reference.Length == 0 ? "Enter the check number." : reference.Length > 40 ? "Keep it under 40 characters." : System.Text.RegularExpressions.Regex.IsMatch(reference, "[0-9]{12,}") ? "Do not enter card numbers. Use the last 4 digits or the slip number." : string.Empty;
            string ePaid = paid == null ? "Enter the payment date." : paid.Value > DateTime.Today ? "The payment date cannot be in the future." : string.Empty;
            string error = string.Empty;
            InvoiceRec? done = null;
            if (eMethod + eRef + ePaid == string.Empty && paid != null)
            {
                using (await Store.Hold())
                {
                    List<InvoiceRec> inv = await Store.Invoices();
                    InvoiceRec? i = inv.FirstOrDefault(x => x.Id == id);
                    if (i == null || i.Status != "issued")
                    {
                        error = "Only unpaid invoices can be marked paid.";
                    }
                    else if (string.CompareOrdinal(Work.Iso(paid.Value), i.Issued) < 0)
                    {
                        ePaid = "The payment date cannot be before the invoice date.";
                    }
                    else
                    {
                        i.Status = "paid";
                        i.Method = method;
                        i.Ref = reference;
                        i.PaidOn = Work.Iso(paid.Value);
                        await Store.SaveInvoices(inv);
                        done = i;
                    }
                }
            }
            response.SetElementContents("ai-e-method", eMethod);
            response.SetElementContents("ai-e-ref", eRef);
            response.SetElementContents("ai-e-paid", ePaid);
            if (done == null)
            {
                if (error.Length > 0)
                {
                    response.ExecuteScript("AdminInvoicesJs.toast('" + Ui.Js(error) + "');");
                }
                return response;
            }
            await Store.Log(c.U.Id, "mark invoice paid", done.Number + " · " + Work.MethodName(done.Method) + " · " + Ui.Money(done.Total), true);
            SiteData site = await Store.LoadSite();
            JobRec? j = (await Store.Jobs()).FirstOrDefault(x => x.Id == done.JobId);
            await Refresh(response, c);
            response.SetElementContents("ai-mbody", FormHtml(site, done, j, c));
            response.ExecuteScript("AdminInvoicesJs.saved('" + Ui.Js(done.Number + " marked paid") + "');");
            return response;
        }

        public async Task<ApiResponse> VoidIt()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 40);
            string why = Ui.Clip(GetDataValue("why"), 300);
            if (!Access.Can(c.U, AreaKey, 'X'))
            {
                return await Refuse(response, c, "void invoice", id, "You are not allowed to void invoices.");
            }
            if (why.Length < 3)
            {
                response.ExecuteScript("AdminInvoicesJs.toast('Give a reason to void the invoice.');");
                return response;
            }
            InvoiceRec? done = null;
            using (await Store.Hold())
            {
                List<InvoiceRec> inv = await Store.Invoices();
                InvoiceRec? i = inv.FirstOrDefault(x => x.Id == id);
                if (i != null && i.Status == "issued")
                {
                    i.Status = "void";
                    i.VoidWhy = why;
                    await Store.SaveInvoices(inv);
                    done = i;
                }
            }
            if (done == null)
            {
                response.ExecuteScript("AdminInvoicesJs.toast('Only unpaid invoices can be voided.');");
                return response;
            }
            await Store.Log(c.U.Id, "void invoice", done.Number + " · " + why, true);
            SiteData site = await Store.LoadSite();
            JobRec? j = (await Store.Jobs()).FirstOrDefault(x => x.Id == done.JobId);
            await Refresh(response, c);
            response.SetElementContents("ai-mbody", FormHtml(site, done, j, c));
            response.ExecuteScript("AdminInvoicesJs.saved('Invoice voided');");
            return response;
        }

        public async Task<ApiResponse> Discard()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 40);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "discard invoice", id, "You can look at invoices but not change them.");
            }
            InvoiceRec? gone = null;
            using (await Store.Hold())
            {
                List<InvoiceRec> inv = await Store.Invoices();
                InvoiceRec? i = inv.FirstOrDefault(x => x.Id == id);
                if (i != null && i.Status == "draft")
                {
                    inv.Remove(i);
                    List<JobRec> jobs = await Store.Jobs();
                    foreach (JobRec j in jobs.Where(x => x.InvoiceId == i.Id))
                    {
                        j.InvoiceId = string.Empty;
                    }
                    await Store.SaveInvoices(inv);
                    await Store.SaveJobs(jobs);
                    gone = i;
                }
            }
            if (gone == null)
            {
                response.ExecuteScript("AdminInvoicesJs.toast('Only drafts can be discarded.');");
                return response;
            }
            await Store.Log(c.U.Id, "discard invoice", gone.JobId + " · " + gone.Name, true);
            await Refresh(response, c);
            response.ExecuteScript("AdminInvoicesJs.shut();AdminInvoicesJs.toast('Draft discarded. The job is back under To invoice.');");
            return response;
        }
    }
}
