using System.Globalization;
using System.Text;
using HomeServices.Core;
using HomeServices.Models;
using SkyNet;

namespace HomeServices.codes
{
    public class AdminJobs : WebPage
    {
        private const string AreaKey = "jobs";
        private const int Size = 15;

        private static readonly (string Key, string Name)[] Views =
        {
            ("toschedule", "To schedule"), ("today", "Today"), ("upcoming", "Upcoming"), ("done", "Done"), ("cancelled", "Cancelled"), ("all", "All")
        };

        private static readonly string[] Order = { "scheduled", "onway", "inprogress", "done" };

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Jobs | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminJobsJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminJobsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("aj-brand", Ui.Esc(site.Brand));
            response.SetElementContents("aj-menu", await Auth.Menu("aj", "AdminJobs", c));
            response.SetElementContents("aj-who", Auth.Who("aj", c));
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"aj-panel\"><h1>No access</h1><p class=\"aj-hint\">You do not have access to " + what + ". Ask a manager if you need it.</p></div>";
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
            response.ExecuteScript("AdminJobsJs.expired();");
            return response;
        }

        private static string ViewKey(string? v, Ctx c)
        {
            string k = (v ?? string.Empty).Trim();
            if (!Views.Any(x => x.Key == k) || (k == "toschedule" && c.Own.Length > 0))
            {
                return c.Own.Length > 0 ? "today" : "upcoming";
            }
            return k;
        }

        private static bool Mine(JobRec j, Ctx c)
        {
            return c.Own.Length == 0 || j.TechId == c.Own;
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
                response.SetElementContents("aj-main", NoAccess("jobs"));
                return response;
            }
            string view = ViewKey(GetDataValue("view"), c);
            List<JobRec> jobs = await Store.Jobs();
            List<QuoteRec> quotes = await Store.Quotes();
            StringBuilder sb = new StringBuilder("<div class=\"aj-ph\"><div><h1>" + (c.Own.Length > 0 ? "My jobs" : "Jobs") + "</h1><p>" + (c.Own.Length > 0 ? "Your jobs only. Move each one along as you go: on the way, in progress, done." : "Book accepted quotes, assign a technician and follow each job to done.") + "</p></div>" + (Access.Can(c.U, "dispatch", 'V') ? "<a class=\"aj-btn aj-btn-ghost\" href=\"AdminDispatch\">Dispatch board</a>" : string.Empty) + "</div>");
            sb.Append("<div class=\"aj-tools\"><div class=\"aj-chips\" id=\"aj-chips\">" + Chips(c, jobs, quotes, view) + "</div>");
            if (c.Own.Length == 0)
            {
                sb.Append("<select id=\"aj-tech\" aria-label=\"Technician\" onchange=\"AdminJobsJs.filter()\"><option value=\"\">All technicians</option><option value=\"-\">Not assigned</option>" + string.Concat(site.Techs.Select(t => "<option value=\"" + Ui.Esc(t.Id) + "\">" + Ui.Esc(t.Name) + "</option>")) + "</select>");
            }
            sb.Append("<input type=\"search\" id=\"aj-term\" placeholder=\"Search name, town or J- number\" maxlength=\"60\" oninput=\"AdminJobsJs.search()\" aria-label=\"Search jobs\"></div>");
            sb.Append("<div id=\"aj-list\">" + ListHtml(site, c, jobs, quotes, view, string.Empty, string.Empty, 1) + "</div>");
            response.SetElementContents("aj-main", sb.ToString());
            response.ExecuteScript("AdminJobsJs.loaded('" + view + "');");
            string show = Ui.Clip(GetDataValue("show"), 20);
            string from = Ui.Clip(GetDataValue("from"), 20);
            if (show.Length > 0 && jobs.Any(j => j.Id == show && Mine(j, c)))
            {
                response.ExecuteScript("AdminJobsJs.edit('" + Ui.Js(show) + "');");
            }
            else if (from.Length > 0 && c.Own.Length == 0 && Access.Can(c.U, AreaKey, 'E'))
            {
                response.ExecuteScript("AdminJobsJs.plan('" + Ui.Js(from) + "');");
            }
            return response;
        }

        private static List<QuoteRec> Waiting(List<QuoteRec> quotes)
        {
            return quotes.Where(q => q.Status == "accepted" && q.JobId.Length == 0).OrderBy(q => q.AnsweredAt).ToList();
        }

        private static bool InView(JobRec j, string view, string today)
        {
            return view switch
            {
                "today" => j.Date == today && j.Status != "cancelled",
                "upcoming" => Work.Open(j.Status) && string.CompareOrdinal(j.Date, today) >= 0,
                "done" => j.Status == "done",
                "cancelled" => j.Status == "cancelled",
                _ => true
            };
        }

        private static string Chips(Ctx c, List<JobRec> jobs, List<QuoteRec> quotes, string view)
        {
            string today = Work.Today();
            StringBuilder sb = new StringBuilder();
            foreach ((string key, string name) in Views)
            {
                if (key == "toschedule" && c.Own.Length > 0)
                {
                    continue;
                }
                int n = key == "toschedule" ? Waiting(quotes).Count : key is "today" or "upcoming" ? jobs.Count(j => Mine(j, c) && InView(j, key, today)) : 0;
                sb.Append("<button type=\"button\" class=\"aj-chip" + (key == view ? " aj-act" : string.Empty) + "\" onclick=\"AdminJobsJs.view('" + key + "')\">" + name + (n > 0 ? "<i>" + n + "</i>" : string.Empty) + "</button>");
            }
            return sb.ToString();
        }

        private static string ListHtml(SiteData site, Ctx c, List<JobRec> jobs, List<QuoteRec> quotes, string view, string tech, string term, int page)
        {
            string t = term.ToLowerInvariant();
            if (view == "toschedule")
            {
                List<QuoteRec> wait = Waiting(quotes).Where(q => t.Length == 0 || q.Name.ToLowerInvariant().Contains(t) || q.Id.ToLowerInvariant().Contains(t) || q.Town.ToLowerInvariant().Contains(t)).ToList();
                if (wait.Count == 0)
                {
                    return "<div class=\"aj-panel aj-empty\">No accepted quotes are waiting. When a customer accepts a quote, it appears here to book.</div>";
                }
                StringBuilder w = new StringBuilder("<div class=\"aj-wrapt\"><table class=\"aj-table\"><thead><tr><th>Quote</th><th>Customer</th><th>Services</th><th>Preferred</th><th class=\"aj-num\">Total</th><th></th></tr></thead><tbody>");
                foreach (QuoteRec q in wait)
                {
                    w.Append("<tr><td data-l=\"Quote\"><b>" + q.Id + "</b><small>accepted " + (q.AnsweredAt.HasValue ? Ui.Esc(Ui.When(q.AnsweredAt.Value)) : string.Empty) + "</small></td><td data-l=\"Customer\"><b>" + Ui.Esc(q.Name) + "</b><small>" + Ui.Esc(q.Town.Length > 0 ? q.Town : q.Zip) + " &middot; " + Ui.Esc(q.Phone) + "</small></td><td data-l=\"Services\">" + Ui.Esc(Work.ServiceNames(site, q.Services)) + "</td><td data-l=\"Preferred\">" + (q.Days.Count > 0 ? string.Join(", ", q.Days.Take(3).Select(d => Ui.Esc(Ui.Day(d)))) + (q.Days.Count > 3 ? " &hellip;" : string.Empty) : "&ndash;") + "<small>" + string.Join(", ", q.Times.Select(x => Ui.Esc(Work.TimeName(x).Split(',')[0]))) + "</small></td><td data-l=\"Total\" class=\"aj-num\">" + Ui.Money(q.Total) + "</td><td data-l=\"\">" + (Access.Can(c.U, AreaKey, 'E') ? "<button type=\"button\" class=\"aj-btn aj-btn-sm\" onclick=\"AdminJobsJs.plan('" + q.Id + "')\">Schedule</button>" : string.Empty) + "</td></tr>");
                }
                return w.Append("</tbody></table></div>").ToString();
            }
            string today = Work.Today();
            List<JobRec> rows = jobs.Where(j => Mine(j, c) && InView(j, view, today) && (tech.Length == 0 || (tech == "-" ? j.TechId.Length == 0 : j.TechId == tech)) && (t.Length == 0 || j.Name.ToLowerInvariant().Contains(t) || j.Id.ToLowerInvariant().Contains(t) || j.Town.ToLowerInvariant().Contains(t) || j.Zip.Contains(t))).ToList();
            rows = view is "done" or "cancelled" or "all" ? rows.OrderByDescending(j => j.Date).ThenByDescending(j => j.Window).ToList() : rows.OrderBy(j => j.Date).ThenBy(j => j.Window).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"aj-panel aj-empty\">" + (view == "today" ? "No jobs today." : "No jobs match.") + "</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"aj-wrapt\"><table class=\"aj-table\"><thead><tr><th>Job</th><th>When</th><th>Customer</th><th>Work</th><th>Technician</th><th>Status</th></tr></thead><tbody>");
            foreach (JobRec j in rows.Skip((page - 1) * Size).Take(Size))
            {
                sb.Append("<tr class=\"aj-row\" tabindex=\"0\" onclick=\"AdminJobsJs.edit('" + j.Id + "')\" onkeydown=\"if(event.key==='Enter'){AdminJobsJs.edit('" + j.Id + "')}\"><td data-l=\"Job\"><b>" + j.Id + "</b></td><td data-l=\"When\"><b>" + Ui.Esc(j.Date == today ? "Today" : Ui.Day(j.Date)) + "</b><small>" + Ui.Esc(Work.WindowName(j.Window)) + "</small></td><td data-l=\"Customer\"><b>" + Ui.Esc(j.Name) + "</b><small>" + Ui.Esc(j.Town.Length > 0 ? j.Town : j.Zip) + "</small></td><td data-l=\"Work\"><span class=\"aj-dot\" style=\"background:" + Work.TradeColor(site, j.Trade) + "\"></span>" + Ui.Esc(j.Summary) + "</td><td data-l=\"Technician\">" + Ui.Esc(Work.TechName(site, j.TechId)) + "</td><td data-l=\"Status\">" + Work.JobPill("aj", j.Status) + "</td></tr>");
            }
            sb.Append("</tbody></table></div>" + Ui.Pager("aj", page, Size, rows.Count, "job", "jobs", "AdminJobsJs.page"));
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
                return await Refuse(response, c, "list jobs", string.Empty, "You do not have access to jobs.");
            }
            await Refresh(response, c);
            return response;
        }

        private async Task Refresh(ApiResponse response, Ctx c)
        {
            SiteData site = await Store.LoadSite();
            List<JobRec> jobs = await Store.Jobs();
            List<QuoteRec> quotes = await Store.Quotes();
            string view = ViewKey(GetDataValue("st"), c);
            response.SetElementContents("aj-list", ListHtml(site, c, jobs, quotes, view, c.Own.Length > 0 ? string.Empty : Ui.Clip(GetDataValue("tech"), 20), Ui.Clip(GetDataValue("term"), 60), Ui.PageNo(GetDataValue("pg"))));
            response.SetElementContents("aj-chips", Chips(c, jobs, quotes, view));
        }

        private static string TechOptions(SiteData site, string trade, string current)
        {
            StringBuilder sb = new StringBuilder("<option value=\"\">Not assigned yet</option>");
            foreach (Technician t in site.Techs.Where(t => t.Active || t.Id == current))
            {
                bool fits = trade.Length == 0 || t.Trades.Contains(trade);
                sb.Append("<option value=\"" + Ui.Esc(t.Id) + "\"" + (t.Id == current ? " selected" : string.Empty) + ">" + Ui.Esc(t.Name) + " (" + Work.DaysText(t.Days) + ")" + (fits ? string.Empty : " &middot; other trade") + "</option>");
            }
            return sb.ToString();
        }

        private static string WindowOptions(string current)
        {
            return string.Concat(Work.Windows.Select(w => "<option value=\"" + w.Key + "\"" + (w.Key == current ? " selected" : string.Empty) + ">" + Ui.Esc(w.Name) + "</option>"));
        }

        private static string PlanFields(SiteData site, string trade, string date, string window, string tech)
        {
            return "<div class=\"aj-grid3\"><label class=\"aj-field\" for=\"aj-f-date\"><span>Day</span><input type=\"date\" id=\"aj-f-date\" data-k=\"date\" min=\"" + Work.Today() + "\" value=\"" + Ui.Esc(date) + "\"><em class=\"aj-err\" id=\"aj-e-date\"></em></label><label class=\"aj-field\" for=\"aj-f-window\"><span>Arrival window</span><select id=\"aj-f-window\" data-k=\"window\">" + WindowOptions(window) + "</select><em class=\"aj-err\" id=\"aj-e-window\"></em></label><label class=\"aj-field\" for=\"aj-f-tech\"><span>Technician</span><select id=\"aj-f-tech\" data-k=\"tech\">" + TechOptions(site, trade, tech) + "</select><em class=\"aj-err\" id=\"aj-e-tech\"></em></label></div>";
        }

        public async Task<ApiResponse> PlanForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string qid = Ui.Clip(GetDataValue("quote"), 20);
            if (!Access.Can(c.U, AreaKey, 'E') || c.Own.Length > 0)
            {
                return await Refuse(response, c, "schedule job", qid, "You are not allowed to book jobs.");
            }
            QuoteRec? q = (await Store.Quotes()).FirstOrDefault(x => x.Id == qid);
            if (q == null || q.Status != "accepted" || q.JobId.Length > 0)
            {
                response.ExecuteScript("AdminJobsJs.toast('That quote is not waiting to be scheduled.');");
                return response;
            }
            SiteData site = await Store.LoadSite();
            string trade = Work.MainTrade(site, q);
            StringBuilder sb = new StringBuilder("<form class=\"aj-stack\" id=\"aj-pform\" onsubmit=\"return AdminJobsJs.book('" + q.Id + "')\" novalidate>");
            sb.Append("<dl class=\"aj-kv\"><dt>Customer</dt><dd><b>" + Ui.Esc(q.Name) + "</b>, " + Ui.Esc(q.Phone) + "<br>" + Ui.Esc(q.Address) + ", " + Ui.Esc(q.Town) + " " + Ui.Esc(q.Zip) + "</dd><dt>Work</dt><dd>" + Ui.Esc(Work.ServiceNames(site, q.Services)) + " &middot; " + Ui.Money(q.Total) + "</dd>");
            if (q.Days.Count > 0)
            {
                sb.Append("<dt>Customer prefers</dt><dd>" + string.Join(", ", q.Days.Select(d => Ui.Esc(Ui.Day(d)))) + "<br>" + string.Join(", ", q.Times.Select(t => Ui.Esc(Work.TimeName(t)))) + "</dd>");
            }
            sb.Append("</dl>" + PlanFields(site, trade, q.Days.FirstOrDefault(d => string.CompareOrdinal(d, Work.Today()) >= 0) ?? string.Empty, q.Times.Contains("afternoon") && !q.Times.Contains("morning") ? "w12" : "w08", string.Empty));
            sb.Append("<label class=\"aj-field\" for=\"aj-f-notes\"><span>Notes for the technician</span><textarea id=\"aj-f-notes\" data-k=\"notes\" rows=\"3\" maxlength=\"1000\"></textarea><small>Access, parking, pets. Nothing sensitive.</small></label>");
            sb.Append("<div class=\"aj-acts\"><button type=\"submit\" class=\"aj-btn\">Book job</button><button type=\"button\" class=\"aj-btn aj-btn-ghost\" onclick=\"AdminJobsJs.closeModal()\">Cancel</button></div></form>");
            response.SetElementContents("aj-mbody", sb.ToString());
            response.ExecuteScript("AdminJobsJs.opened('" + Ui.Js("Schedule " + q.Id + " · " + q.Name) + "');");
            return response;
        }

        private static string CheckPlan(SiteData site, List<JobRec> jobs, string jobId, string trade, string date, string window, string tech, Dictionary<string, string> e)
        {
            DateTime? d = Work.ParseDay(date);
            e["date"] = d == null ? "Pick a day." : d.Value < DateTime.Today ? "Pick today or a later day." : d.Value > DateTime.Today.AddDays(366) ? "Pick a day within the next year." : string.Empty;
            e["window"] = Work.Windows.Any(w => w.Key == window) ? string.Empty : "Pick an arrival window.";
            e["tech"] = string.Empty;
            if (tech.Length > 0 && d != null)
            {
                Technician? t = site.Techs.FirstOrDefault(x => x.Id == tech);
                if (t == null || !t.Active)
                {
                    e["tech"] = "Pick an active technician.";
                }
                else if (!t.Days.Contains(Work.DayIndex(d.Value)))
                {
                    e["tech"] = t.Name + " does not work on " + Work.LongDayNames[Work.DayIndex(d.Value) - 1] + "s. Working days: " + Work.DaysText(t.Days) + ".";
                }
                else if (trade.Length > 0 && !t.Trades.Contains(trade))
                {
                    e["tech"] = t.Name + " does not do " + Work.TradeName(site, trade) + " work.";
                }
                else if (jobs.Any(j => j.Id != jobId && j.TechId == tech && j.Date == date && j.Window == window && Work.Open(j.Status)))
                {
                    e["tech"] = t.Name + " already has a job in that window.";
                }
            }
            return string.Concat(e.Values);
        }

        public async Task<ApiResponse> Book()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string qid = Ui.Clip(GetDataValue("quote"), 20);
            if (!Access.Can(c.U, AreaKey, 'E') || c.Own.Length > 0)
            {
                return await Refuse(response, c, "schedule job", qid, "You are not allowed to book jobs.");
            }
            string date = Ui.Clip(GetDataValue("date"), 10);
            string window = Ui.Clip(GetDataValue("window"), 5);
            string tech = Ui.Clip(GetDataValue("tech"), 20);
            string notes = Ui.Clip(GetDataValue("notes"), 1000);
            SiteData site = await Store.LoadSite();
            Dictionary<string, string> e = new Dictionary<string, string>();
            string error = string.Empty;
            JobRec? job = null;
            using (await Store.Hold())
            {
                List<QuoteRec> quotes = await Store.Quotes();
                List<JobRec> jobs = await Store.Jobs();
                QuoteRec? q = quotes.FirstOrDefault(x => x.Id == qid);
                if (q == null || q.Status != "accepted" || q.JobId.Length > 0)
                {
                    error = "That quote is not waiting to be scheduled.";
                }
                else if (CheckPlan(site, jobs, string.Empty, Work.MainTrade(site, q), date, window, tech, e).Length == 0)
                {
                    string id;
                    do
                    {
                        id = Work.Code("J");
                    }
                    while (jobs.Any(x => x.Id == id));
                    job = new JobRec { Id = id, QuoteId = q.Id, Date = date, Window = window, TechId = tech, Status = "scheduled", Name = q.Name, Phone = q.Phone, Email = q.Email, Address = q.Address, Zip = q.Zip, Town = q.Town, Trade = Work.MainTrade(site, q), Summary = Work.ServiceNames(site, q.Services), Notes = notes, Created = DateTime.UtcNow };
                    job.Steps.Add(new StepRec { When = DateTime.UtcNow, Status = "scheduled", By = c.U.Name });
                    jobs.Add(job);
                    q.JobId = id;
                    await Store.SaveJobs(jobs);
                    await Store.SaveQuotes(quotes);
                }
            }
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("aj-e-" + kv.Key, Ui.Esc(kv.Value));
            }
            if (error.Length > 0)
            {
                response.ExecuteScript("AdminJobsJs.toast('" + Ui.Js(error) + "');");
                return response;
            }
            if (job == null)
            {
                return response;
            }
            await Store.Log(c.U.Id, "schedule job", job.Id + " · " + job.Name + " · " + job.Date + " " + Work.WindowName(job.Window) + " · " + Work.TechName(site, job.TechId), true);
            await Refresh(response, c);
            response.SetElementContents("aj-mbody", FormHtml(site, job, c));
            response.ExecuteScript("AdminJobsJs.saved('Job " + job.Id + " booked');AdminJobsJs.opened('" + Ui.Js("Job " + job.Id + " · " + job.Name) + "');");
            return response;
        }

        private static string NextStatus(string s)
        {
            int i = Array.IndexOf(Order, s);
            return i >= 0 && i < Order.Length - 1 ? Order[i + 1] : string.Empty;
        }

        private static string PrevStatus(string s)
        {
            int i = Array.IndexOf(Order, s);
            return i > 0 ? Order[i - 1] : string.Empty;
        }

        private static string MoveBlock(JobRec j, string next)
        {
            string today = Work.Today();
            if (next is "onway" or "inprogress" && j.Date != today)
            {
                return string.CompareOrdinal(j.Date, today) > 0 ? "This job is on " + Ui.Day(j.Date) + ". You can mark it on the way on that day." : "This job was due on " + Ui.Day(j.Date) + ". Reschedule it to today first.";
            }
            return string.Empty;
        }

        private static string FormHtml(SiteData site, JobRec j, Ctx c)
        {
            bool canE = Access.Can(c.U, AreaKey, 'E');
            bool canX = Access.Can(c.U, AreaKey, 'X');
            bool own = c.Own.Length > 0;
            string next = NextStatus(j.Status);
            StringBuilder sb = new StringBuilder("<div class=\"aj-stack\" id=\"aj-form\" data-id=\"" + j.Id + "\">");
            sb.Append("<div class=\"aj-jtop\">" + Work.JobPill("aj", j.Status) + "<b>" + Ui.Esc(Ui.LongDay(j.Date)) + ", " + Ui.Esc(Work.WindowName(j.Window)) + "</b><span>" + Ui.Esc(Work.TechName(site, j.TechId)) + "</span></div>");
            sb.Append("<ol class=\"aj-steps\">");
            foreach (string s in Order)
            {
                StepRec? st = j.Steps.LastOrDefault(x => x.Status == s);
                bool reached = Array.IndexOf(Order, j.Status) >= Array.IndexOf(Order, s) && j.Status != "cancelled";
                sb.Append("<li class=\"" + (reached ? "aj-on" : string.Empty) + "\"><b>" + Work.JobStatusName(s) + "</b><small>" + (st != null && reached ? Ui.Esc(Ui.When(st.When)) + " &middot; " + Ui.Esc(st.By) : "&nbsp;") + "</small></li>");
            }
            sb.Append("</ol>");
            if (j.Status == "cancelled")
            {
                sb.Append("<div class=\"aj-note\">Cancelled" + (j.CancelWhy.Length > 0 ? ": " + Ui.Esc(j.CancelWhy) : ".") + "</div>");
            }
            sb.Append("<dl class=\"aj-kv\"><dt>Customer</dt><dd><b>" + Ui.Esc(j.Name) + "</b><br><a href=\"" + Ui.Esc(Work.Tel(j.Phone)) + "\">" + Ui.Esc(j.Phone) + "</a>" + (j.Email.Length > 0 ? " &middot; " + Ui.Esc(j.Email) : string.Empty) + "</dd><dt>Address</dt><dd>" + Ui.Esc(j.Address) + ", " + Ui.Esc(j.Town) + " " + Ui.Esc(j.Zip) + "</dd><dt>Work</dt><dd><span class=\"aj-dot\" style=\"background:" + Work.TradeColor(site, j.Trade) + "\"></span>" + Ui.Esc(j.Summary) + "</dd><dt>Quote</dt><dd>" + (Access.Can(c.U, "quotes", 'V') ? "<a class=\"aj-link\" href=\"AdminQuotes?show=" + Ui.Esc(j.QuoteId) + "\">" + Ui.Esc(j.QuoteId) + "</a>" : Ui.Esc(j.QuoteId)) + "</dd>");
            if (j.InvoiceId.Length > 0)
            {
                sb.Append("<dt>Invoice</dt><dd>" + (Access.Can(c.U, "invoices", 'V') ? "<a class=\"aj-link\" href=\"AdminInvoices?show=" + Ui.Esc(j.InvoiceId) + "\">Open invoice</a>" : "Invoiced") + "</dd>");
            }
            sb.Append("</dl>");
            sb.Append("<label class=\"aj-field\" for=\"aj-f-notes\"><span>Notes for the technician</span><textarea id=\"aj-f-notes\" rows=\"3\" maxlength=\"1000\"" + (canE ? string.Empty : " disabled") + ">" + Ui.Esc(j.Notes) + "</textarea><small>Access, parking, pets, what was found. Nothing sensitive.</small></label>");
            sb.Append("<div class=\"aj-acts\">");
            if (canE && next.Length > 0 && j.Status != "cancelled")
            {
                string block = MoveBlock(j, next);
                if (block.Length == 0)
                {
                    sb.Append("<button type=\"button\" class=\"aj-btn\" onclick=\"AdminJobsJs.move('" + j.Id + "',1,'" + Work.JobStatusName(next) + "')\">Mark " + Work.JobStatusName(next).ToLowerInvariant() + "</button>");
                }
                else
                {
                    sb.Append("<span class=\"aj-hint\">" + Ui.Esc(block) + "</span>");
                }
            }
            if (canE)
            {
                sb.Append("<button type=\"button\" class=\"aj-btn aj-btn-ghost\" onclick=\"AdminJobsJs.saveNotes('" + j.Id + "')\">Save notes</button>");
            }
            if (canX && PrevStatus(j.Status).Length > 0 && j.InvoiceId.Length == 0)
            {
                sb.Append("<button type=\"button\" class=\"aj-btn aj-btn-ghost\" onclick=\"AdminJobsJs.move('" + j.Id + "',-1,'" + Work.JobStatusName(PrevStatus(j.Status)) + "')\">Step back to " + Work.JobStatusName(PrevStatus(j.Status)).ToLowerInvariant() + "</button>");
            }
            if (j.Status == "done" && j.InvoiceId.Length == 0 && Access.Can(c.U, "invoices", 'E'))
            {
                sb.Append("<a class=\"aj-btn aj-btn-ink\" href=\"AdminInvoices?from=" + j.Id + "\">Create invoice</a>");
            }
            if (canE && !own && j.Status == "scheduled")
            {
                sb.Append("<button type=\"button\" class=\"aj-btn aj-btn-ghost\" onclick=\"AdminJobsJs.replan()\">Change day or technician</button>");
            }
            if (canE && !own && j.Status is "scheduled" or "onway")
            {
                sb.Append("<button type=\"button\" class=\"aj-btn aj-btn-danger aj-push\" onclick=\"AdminJobsJs.cancelJob('" + j.Id + "')\">Cancel job</button>");
            }
            sb.Append("</div>");
            if (canE && !own && j.Status == "scheduled")
            {
                sb.Append("<form class=\"aj-replan\" id=\"aj-replan\" hidden onsubmit=\"return AdminJobsJs.replanSave('" + j.Id + "')\" novalidate><div class=\"aj-sect\">Change day or technician</div>" + PlanFields(site, j.Trade, j.Date, j.Window, j.TechId) + "<div class=\"aj-acts\"><button type=\"submit\" class=\"aj-btn\">Save changes</button></div></form>");
            }
            sb.Append("</div>");
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
            string id = Ui.Clip(GetDataValue("id"), 20);
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "open job", id, "You do not have access to jobs.");
            }
            JobRec? j = (await Store.Jobs()).FirstOrDefault(x => x.Id == id);
            if (j == null || !Mine(j, c))
            {
                if (j != null)
                {
                    await Store.Log(c.U.Id, "open job", id, false);
                }
                response.ExecuteScript("AdminJobsJs.toast('That job was not found.');");
                return response;
            }
            SiteData site = await Store.LoadSite();
            response.SetElementContents("aj-mbody", FormHtml(site, j, c));
            response.ExecuteScript("AdminJobsJs.opened('" + Ui.Js("Job " + j.Id + " · " + j.Name) + "');");
            return response;
        }

        public async Task<ApiResponse> Move()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 20);
            bool back = GetDataValue("dir") == "-1";
            if (!Access.Can(c.U, AreaKey, back ? 'X' : 'E'))
            {
                return await Refuse(response, c, back ? "step job back" : "move job", id, back ? "You are not allowed to step jobs back." : "You can look at jobs but not change them.");
            }
            string error = string.Empty;
            JobRec? done = null;
            using (await Store.Hold())
            {
                List<JobRec> jobs = await Store.Jobs();
                JobRec? j = jobs.FirstOrDefault(x => x.Id == id);
                if (j == null || !Mine(j, c))
                {
                    error = "That job was not found.";
                }
                else
                {
                    string target = back ? PrevStatus(j.Status) : NextStatus(j.Status);
                    string block = back ? (j.InvoiceId.Length > 0 ? "This job already has an invoice." : string.Empty) : MoveBlock(j, target);
                    if (target.Length == 0 || j.Status == "cancelled")
                    {
                        error = "This job cannot move from " + Work.JobStatusName(j.Status).ToLowerInvariant() + ".";
                    }
                    else if (block.Length > 0)
                    {
                        error = block;
                    }
                    else
                    {
                        j.Status = target;
                        j.Steps.Add(new StepRec { When = DateTime.UtcNow, Status = target, By = c.U.Name + (back ? " (stepped back)" : string.Empty) });
                        done = j;
                        await Store.SaveJobs(jobs);
                    }
                }
            }
            if (done == null)
            {
                await Store.Log(c.U.Id, back ? "step job back" : "move job", id, false);
                response.ExecuteScript("AdminJobsJs.toast('" + Ui.Js(error) + "');");
                return response;
            }
            await Store.Log(c.U.Id, back ? "step job back" : "move job", done.Id + " · " + done.Name + " · " + Work.JobStatusName(done.Status), true);
            SiteData site = await Store.LoadSite();
            await Refresh(response, c);
            response.SetElementContents("aj-mbody", FormHtml(site, done, c));
            response.ExecuteScript("AdminJobsJs.saved('" + Ui.Js(done.Id + " is now " + Work.JobStatusName(done.Status).ToLowerInvariant()) + "');");
            return response;
        }

        public async Task<ApiResponse> SaveNotes()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 20);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "job notes", id, "You can look at jobs but not change them.");
            }
            bool ok = false;
            using (await Store.Hold())
            {
                List<JobRec> jobs = await Store.Jobs();
                JobRec? j = jobs.FirstOrDefault(x => x.Id == id);
                if (j != null && Mine(j, c))
                {
                    j.Notes = Ui.Clip(GetDataValue("notes"), 1000);
                    await Store.SaveJobs(jobs);
                    ok = true;
                }
            }
            if (ok)
            {
                await Store.Log(c.U.Id, "job notes", id, true);
            }
            response.ExecuteScript("AdminJobsJs.clean();AdminJobsJs.toast('" + (ok ? "Notes saved" : "That job was not found.") + "');");
            return response;
        }

        public async Task<ApiResponse> Replan()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 20);
            if (!Access.Can(c.U, AreaKey, 'E') || c.Own.Length > 0)
            {
                return await Refuse(response, c, "reschedule job", id, "You are not allowed to reschedule or reassign jobs.");
            }
            string date = Ui.Clip(GetDataValue("date"), 10);
            string window = Ui.Clip(GetDataValue("window"), 5);
            string tech = Ui.Clip(GetDataValue("tech"), 20);
            SiteData site = await Store.LoadSite();
            Dictionary<string, string> e = new Dictionary<string, string>();
            string error = string.Empty;
            JobRec? done = null;
            using (await Store.Hold())
            {
                List<JobRec> jobs = await Store.Jobs();
                JobRec? j = jobs.FirstOrDefault(x => x.Id == id);
                if (j == null || j.Status != "scheduled")
                {
                    error = "Only scheduled jobs can be moved.";
                }
                else if (CheckPlan(site, jobs, j.Id, j.Trade, date, window, tech, e).Length == 0)
                {
                    j.Date = date;
                    j.Window = window;
                    j.TechId = tech;
                    done = j;
                    await Store.SaveJobs(jobs);
                }
            }
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("aj-e-" + kv.Key, Ui.Esc(kv.Value));
            }
            if (done == null)
            {
                if (error.Length > 0)
                {
                    response.ExecuteScript("AdminJobsJs.toast('" + Ui.Js(error) + "');");
                }
                return response;
            }
            await Store.Log(c.U.Id, "reschedule job", done.Id + " · " + done.Date + " " + Work.WindowName(done.Window) + " · " + Work.TechName(site, done.TechId), true);
            await Refresh(response, c);
            response.SetElementContents("aj-mbody", FormHtml(site, done, c));
            response.ExecuteScript("AdminJobsJs.saved('Job updated');");
            return response;
        }

        public async Task<ApiResponse> CancelJob()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 20);
            string why = Ui.Clip(GetDataValue("why"), 300);
            if (!Access.Can(c.U, AreaKey, 'E') || c.Own.Length > 0)
            {
                return await Refuse(response, c, "cancel job", id, "You are not allowed to cancel jobs.");
            }
            JobRec? done = null;
            using (await Store.Hold())
            {
                List<JobRec> jobs = await Store.Jobs();
                JobRec? j = jobs.FirstOrDefault(x => x.Id == id);
                if (j != null && j.Status is "scheduled" or "onway")
                {
                    j.Status = "cancelled";
                    j.CancelWhy = why;
                    j.Steps.Add(new StepRec { When = DateTime.UtcNow, Status = "cancelled", By = c.U.Name });
                    done = j;
                    await Store.SaveJobs(jobs);
                    List<QuoteRec> quotes = await Store.Quotes();
                    foreach (QuoteRec q in quotes.Where(x => x.JobId == j.Id))
                    {
                        q.JobId = string.Empty;
                    }
                    await Store.SaveQuotes(quotes);
                }
            }
            if (done == null)
            {
                response.ExecuteScript("AdminJobsJs.toast('Only jobs that have not started can be cancelled.');");
                return response;
            }
            await Store.Log(c.U.Id, "cancel job", done.Id + " · " + done.Name + (why.Length > 0 ? " · " + why : string.Empty), true);
            SiteData site = await Store.LoadSite();
            await Refresh(response, c);
            response.SetElementContents("aj-mbody", FormHtml(site, done, c));
            response.ExecuteScript("AdminJobsJs.saved('Job cancelled');");
            return response;
        }
    }
}
