using System.Globalization;
using System.Text;
using HomeServices.Core;
using HomeServices.Models;
using SkyNet;

namespace HomeServices.codes
{
    public class AdminCustomers : WebPage
    {
        private const string AreaKey = "customers";
        private const int Size = 20;

        private sealed class Row
        {
            public string Key { get; init; } = string.Empty;
            public List<QuoteRec> Quotes { get; } = new List<QuoteRec>();
            public List<JobRec> Jobs { get; } = new List<JobRec>();
            public List<InvoiceRec> Invoices { get; } = new List<InvoiceRec>();
            public QuoteRec Last => Quotes.OrderByDescending(q => q.Created).First();
            public decimal Paid => Invoices.Where(i => i.Status == "paid").Sum(i => i.Total);
            public decimal Owed => Invoices.Where(i => i.Status == "issued").Sum(i => i.Total);
            public DateTime Seen => Quotes.Max(q => q.Created);
        }

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Customers | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminCustomersJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminCustomersJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("ac-brand", Ui.Esc(site.Brand));
            response.SetElementContents("ac-menu", await Auth.Menu("ac", "AdminCustomers", c));
            response.SetElementContents("ac-who", Auth.Who("ac", c));
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"ac-panel\"><h1>No access</h1><p class=\"ac-hint\">You do not have access to " + what + ". Ask a manager if you need it.</p></div>";
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
            response.ExecuteScript("AdminCustomersJs.expired();");
            return response;
        }

        private static async Task<List<Row>> Build()
        {
            List<QuoteRec> quotes = await Store.Quotes();
            List<JobRec> jobs = await Store.Jobs();
            List<InvoiceRec> inv = await Store.Invoices();
            Dictionary<string, Row> map = new Dictionary<string, Row>();
            Dictionary<string, Row> byQuote = new Dictionary<string, Row>();
            foreach (QuoteRec q in quotes)
            {
                string k = Work.CustomerKey(q.Email, q.Phone);
                if (!map.TryGetValue(k, out Row? r))
                {
                    r = new Row { Key = k };
                    map[k] = r;
                }
                r.Quotes.Add(q);
                byQuote[q.Id] = r;
            }
            foreach (JobRec j in jobs)
            {
                if (byQuote.TryGetValue(j.QuoteId, out Row? r))
                {
                    r.Jobs.Add(j);
                }
            }
            foreach (InvoiceRec i in inv)
            {
                if (byQuote.TryGetValue(i.QuoteId, out Row? r))
                {
                    r.Invoices.Add(i);
                }
            }
            return map.Values.OrderByDescending(r => r.Seen).ToList();
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
                response.SetElementContents("ac-main", NoAccess("customers"));
                return response;
            }
            List<Row> rows = await Build();
            StringBuilder sb = new StringBuilder("<div class=\"ac-ph\"><div><h1>Customers</h1><p>Everyone who asked for a quote, grouped by email or phone, with their quotes, jobs and invoices.</p></div></div>");
            sb.Append("<div class=\"ac-tools\"><select id=\"ac-show\" aria-label=\"Show\" onchange=\"AdminCustomersJs.filter()\"><option value=\"\">All customers</option><option value=\"owe\">With unpaid invoices</option><option value=\"jobs\">With jobs</option></select><input type=\"search\" id=\"ac-term\" placeholder=\"Search name, email, phone or town\" maxlength=\"60\" oninput=\"AdminCustomersJs.search()\" aria-label=\"Search customers\"></div>");
            sb.Append("<div id=\"ac-list\">" + ListHtml(rows, string.Empty, string.Empty, 1) + "</div>");
            response.SetElementContents("ac-main", sb.ToString());
            return response;
        }

        private static string ListHtml(List<Row> all, string show, string term, int page)
        {
            string t = term.ToLowerInvariant();
            string d = Work.Digits(term);
            List<Row> rows = all.Where(r => (show != "owe" || r.Owed > 0) && (show != "jobs" || r.Jobs.Count > 0) && (t.Length == 0 || r.Quotes.Any(q => q.Name.ToLowerInvariant().Contains(t) || q.Email.ToLowerInvariant().Contains(t) || q.Town.ToLowerInvariant().Contains(t) || (d.Length >= 3 && Work.Digits(q.Phone).Contains(d))))).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"ac-panel ac-empty\">" + (all.Count == 0 ? "No customers yet. They appear with their first quote request." : "No customers match.") + "</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"ac-wrapt\"><table class=\"ac-table\"><thead><tr><th>Customer</th><th>Contact</th><th>Quotes</th><th>Jobs</th><th class=\"ac-num\">Paid</th><th class=\"ac-num\">Owed</th></tr></thead><tbody>");
            foreach (Row r in rows.Skip((page - 1) * Size).Take(Size))
            {
                QuoteRec q = r.Last;
                sb.Append("<tr class=\"ac-row\" tabindex=\"0\" onclick=\"AdminCustomersJs.open('" + Ui.Js(r.Key) + "')\" onkeydown=\"if(event.key==='Enter'){AdminCustomersJs.open('" + Ui.Js(r.Key) + "')}\"><td data-l=\"Customer\"><b>" + Ui.Esc(q.Name) + "</b><small>" + Ui.Esc(q.Town.Length > 0 ? q.Town : q.Zip) + "</small></td><td data-l=\"Contact\">" + Ui.Esc(q.Phone) + "<small>" + Ui.Esc(q.Email) + "</small></td><td data-l=\"Quotes\">" + r.Quotes.Count + "</td><td data-l=\"Jobs\">" + r.Jobs.Count(j => j.Status != "cancelled") + "</td><td data-l=\"Paid\" class=\"ac-num\">" + Ui.Money(r.Paid) + "</td><td data-l=\"Owed\" class=\"ac-num\">" + (r.Owed > 0 ? "<b class=\"ac-owe\">" + Ui.Money(r.Owed) + "</b>" : "&ndash;") + "</td></tr>");
            }
            sb.Append("</tbody></table></div>" + Ui.Pager("ac", page, Size, rows.Count, "customer", "customers", "AdminCustomersJs.page"));
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
                return await Refuse(response, c, "list customers", string.Empty, "You do not have access to customers.");
            }
            string show = GetDataValue("show2") is "owe" or "jobs" ? GetDataValue("show2") : string.Empty;
            response.SetElementContents("ac-list", ListHtml(await Build(), show, Ui.Clip(GetDataValue("term"), 60), Ui.PageNo(GetDataValue("pg"))));
            return response;
        }

        public async Task<ApiResponse> Detail()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string key = Ui.Clip(GetDataValue("ck"), 200);
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "open customer", key, "You do not have access to customers.");
            }
            Row? r = (await Build()).FirstOrDefault(x => x.Key == key);
            if (r == null)
            {
                response.ExecuteScript("AdminCustomersJs.toast('That customer was not found.');");
                return response;
            }
            SiteData site = await Store.LoadSite();
            QuoteRec last = r.Last;
            bool vq = Access.Can(c.U, "quotes", 'V');
            bool vj = Access.Can(c.U, "jobs", 'V') && c.Own.Length == 0;
            bool vi = Access.Can(c.U, "invoices", 'V');
            StringBuilder sb = new StringBuilder("<div class=\"ac-stack\"><div class=\"ac-cstats\"><span><small>Quotes</small><b>" + r.Quotes.Count + "</b></span><span><small>Jobs</small><b>" + r.Jobs.Count(j => j.Status != "cancelled") + "</b></span><span><small>Paid</small><b>" + Ui.Money(r.Paid) + "</b></span><span><small>Owed</small><b>" + Ui.Money(r.Owed) + "</b></span></div>");
            sb.Append("<dl class=\"ac-kv\"><dt>Phone</dt><dd><a href=\"" + Ui.Esc(Work.Tel(last.Phone)) + "\">" + Ui.Esc(last.Phone) + "</a></dd><dt>Email</dt><dd>" + (last.Email.Length > 0 ? "<a href=\"mailto:" + Ui.Esc(last.Email) + "\">" + Ui.Esc(last.Email) + "</a>" : "&ndash;") + "</dd><dt>Address</dt><dd>" + string.Join("<br>", r.Quotes.Select(q => q.Address + ", " + q.Town + " " + q.Zip).Distinct().Select(Ui.Esc)) + "</dd></dl><div class=\"ac-sect\">History</div><ol class=\"ac-tl\">");
            List<(DateTime When, string Html)> items = new List<(DateTime, string)>();
            foreach (QuoteRec q in r.Quotes)
            {
                string link = vq ? "<a href=\"AdminQuotes?show=" + q.Id + "\">" + q.Id + "</a>" : q.Id;
                items.Add((q.Created, "<b>Quote request " + link + "</b> " + Work.QuotePill("ac", q.Status) + "<small>" + Ui.Esc(Work.ServiceNames(site, q.Services)) + (q.Lines.Count > 0 ? " &middot; " + Ui.Money(q.Total) : string.Empty) + "</small>"));
            }
            foreach (JobRec j in r.Jobs)
            {
                string link = vj ? "<a href=\"AdminJobs?show=" + j.Id + "\">" + j.Id + "</a>" : j.Id;
                DateTime when = Work.ParseDay(j.Date) ?? j.Created;
                items.Add((when.ToUniversalTime().AddHours(12), "<b>Job " + link + "</b> " + Work.JobPill("ac", j.Status) + "<small>" + Ui.Esc(Ui.Day(j.Date)) + ", " + Ui.Esc(Work.WindowName(j.Window)) + " &middot; " + Ui.Esc(Work.TechName(site, j.TechId)) + "</small>"));
            }
            foreach (InvoiceRec i in r.Invoices)
            {
                string label = i.Number.Length > 0 ? i.Number : "draft";
                string link = vi ? "<a href=\"AdminInvoices?show=" + i.Id + "\">" + Ui.Esc(label) + "</a>" : Ui.Esc(label);
                DateTime when = Work.ParseDay(i.Issued) ?? i.Created;
                items.Add((when.ToUniversalTime().AddHours(18), "<b>Invoice " + link + "</b> " + Work.InvoicePill("ac", i) + "<small>" + Ui.Money(i.Total) + (i.Status == "paid" ? " &middot; paid " + Ui.Esc(Ui.Day(i.PaidOn)) + " by " + Ui.Esc(Work.MethodName(i.Method).ToLowerInvariant()) : string.Empty) + "</small>"));
            }
            foreach ((DateTime when, string html) in items.OrderByDescending(x => x.When))
            {
                sb.Append("<li><span>" + when.ToLocalTime().ToString("MMM d, yyyy", CultureInfo.GetCultureInfo("en-US")) + "</span><div>" + html + "</div></li>");
            }
            sb.Append("</ol><div class=\"ac-acts\"><button type=\"button\" class=\"ac-btn ac-btn-ghost\" onclick=\"AdminCustomersJs.closeModal()\">Close</button></div></div>");
            response.SetElementContents("ac-mbody", sb.ToString());
            response.ExecuteScript("AdminCustomersJs.openModal('" + Ui.Js(last.Name) + "');");
            return response;
        }
    }
}
