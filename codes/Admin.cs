using System.Globalization;
using System.Text;
using HomeServices.Core;
using HomeServices.Models;
using SkyNet;

namespace HomeServices.codes
{
    public class Admin : WebPage
    {
        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Dashboard | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("db-brand", Ui.Esc(site.Brand));
            response.SetElementContents("db-menu", await Auth.Menu("db", "Admin", c));
            response.SetElementContents("db-who", Auth.Who("db", c));
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"db-panel\"><h1>No access</h1><p class=\"db-hint\">You do not have access to " + what + ". Ask a manager if you need it.</p></div>";
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
            response.ExecuteScript("AdminJs.expired();");
            return response;
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
            response.SetElementContents("db-main", await MainHtml(c, site));
            return response;
        }

        private static string Tile(string href, string label, string value, string sub, bool hot)
        {
            return "<a class=\"db-tile" + (hot ? " db-hot" : string.Empty) + "\" href=\"" + href + "\"><span>" + Ui.Esc(label) + "</span><b>" + value + "</b><small>" + sub + "</small></a>";
        }

        private static async Task<string> MainHtml(Ctx c, SiteData site)
        {
            string today = Work.Today();
            string own = c.Own;
            List<QuoteRec> quotes = await Store.Quotes();
            List<JobRec> jobs = await Store.Jobs();
            List<InvoiceRec> inv = await Store.Invoices();
            List<MessageRec> msgs = await Store.Messages();
            CultureInfo us = CultureInfo.GetCultureInfo("en-US");
            StringBuilder sb = new StringBuilder("<div class=\"db-ph\"><div><h1>Good " + (DateTime.Now.Hour < 12 ? "morning" : DateTime.Now.Hour < 18 ? "afternoon" : "evening") + ", " + Ui.Esc(c.U.Name.Split(' ')[0]) + "</h1><p>" + DateTime.Now.ToString("dddd, MMMM d", us) + (own.Length > 0 ? " &middot; showing your own jobs" : string.Empty) + "</p></div></div><div class=\"db-tiles\">");
            if (Access.Can(c.U, "quotes", 'V'))
            {
                int n = quotes.Count(q => q.Status == "new");
                int sent = quotes.Count(q => q.Status == "sent");
                sb.Append(Tile("AdminQuotes?f=new", "New requests", n.ToString(us), "waiting for a quote", n > 0));
                sb.Append(Tile("AdminQuotes?f=sent", "Quotes sent", sent.ToString(us), "waiting for the customer", false));
            }
            if (Access.Can(c.U, "jobs", 'V'))
            {
                List<JobRec> mine = jobs.Where(j => own.Length == 0 || j.TechId == own).ToList();
                int tdy = mine.Count(j => j.Date == today && j.Status != "cancelled");
                int open = mine.Count(j => j.Date == today && Work.Open(j.Status));
                sb.Append(Tile("AdminJobs?f=today", "Jobs today", tdy.ToString(us), open == 0 ? "all finished" : Ui.Count(open, "still open", "still open"), open > 0));
                if (own.Length == 0)
                {
                    int ready = quotes.Count(q => q.Status == "accepted" && q.JobId.Length == 0);
                    sb.Append(Tile("AdminJobs?f=toschedule", "To schedule", ready.ToString(us), "accepted quotes", ready > 0));
                }
            }
            if (Access.Can(c.U, "invoices", 'V'))
            {
                int ready = jobs.Count(j => j.Status == "done" && j.InvoiceId.Length == 0);
                decimal unpaid = inv.Where(i => i.Status == "issued").Sum(i => i.Total);
                int overdue = inv.Count(Work.Overdue);
                sb.Append(Tile("AdminInvoices?f=ready", "To invoice", ready.ToString(us), "finished jobs", ready > 0));
                sb.Append(Tile("AdminInvoices?f=unpaid", "Unpaid", Ui.Money(unpaid), overdue > 0 ? Ui.Count(overdue, "overdue invoice", "overdue invoices") : "nothing overdue", overdue > 0));
            }
            if (Access.Can(c.U, "messages", 'V'))
            {
                int unread = msgs.Count(m => !m.Read);
                sb.Append(Tile("AdminMessages", "Messages", unread.ToString(us), "unread", unread > 0));
            }
            sb.Append("</div>");
            if (Access.Can(c.U, "jobs", 'V') || Access.Can(c.U, "dispatch", 'V'))
            {
                List<JobRec> list = jobs.Where(j => j.Date == today && j.Status != "cancelled" && (own.Length == 0 || j.TechId == own)).OrderBy(j => j.Window).ToList();
                sb.Append("<section class=\"db-panel\"><div class=\"db-panel-h\"><h2>Today&rsquo;s jobs</h2>" + (Access.Can(c.U, "dispatch", 'V') ? "<a class=\"db-btn db-btn-ghost db-btn-sm\" href=\"AdminDispatch\">Dispatch board</a>" : string.Empty) + "</div>");
                if (list.Count == 0)
                {
                    sb.Append("<p class=\"db-empty\">No jobs booked for today.</p>");
                }
                else
                {
                    sb.Append("<ul class=\"db-today\">");
                    foreach (JobRec j in list)
                    {
                        sb.Append("<li style=\"--db-tc:" + Work.TradeColor(site, j.Trade) + "\"><a href=\"AdminJobs?show=" + Ui.Esc(j.Id) + "\"><span class=\"db-tw\">" + Ui.Esc(Work.WindowName(j.Window)) + "</span><span class=\"db-tm\"><b>" + Ui.Esc(j.Name) + "</b><small>" + Ui.Esc(j.Summary) + " &middot; " + Ui.Esc(j.Town.Length > 0 ? j.Town : j.Zip) + "</small></span><span class=\"db-tt\">" + Ui.Esc(Work.TechName(site, j.TechId)) + "</span>" + Work.JobPill("db", j.Status) + "</a></li>");
                    }
                    sb.Append("</ul>");
                }
                sb.Append("</section>");
            }
            List<LogEntry> log = (await Store.LoadLog()).Where(l => Access.Can(c.U, "activity", 'V') || l.UserId == c.U.Id).Take(8).ToList();
            sb.Append("<section class=\"db-panel\"><div class=\"db-panel-h\"><h2>" + (Access.Can(c.U, "activity", 'V') ? "Latest activity" : "Your latest activity") + "</h2><a class=\"db-btn db-btn-ghost db-btn-sm\" href=\"AdminActivity\">See all</a></div>");
            if (log.Count == 0)
            {
                sb.Append("<p class=\"db-empty\">Nothing yet.</p>");
            }
            else
            {
                sb.Append("<ul class=\"db-log\">");
                foreach (LogEntry l in log)
                {
                    sb.Append("<li><span>" + Ui.Esc(Ui.When(l.When)) + "</span><b>" + Ui.Esc(l.Who) + "</b><span>" + Ui.Esc(l.Action) + (l.Target.Length > 0 ? ": " + Ui.Esc(l.Target) : string.Empty) + "</span>" + (l.Ok ? string.Empty : Work.Pill("db", "bad", "Refused")) + "</li>");
                }
                sb.Append("</ul>");
            }
            sb.Append("</section>");
            return sb.ToString();
        }
    }
}
