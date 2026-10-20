using System.Globalization;
using System.Text;
using HomeServices.Core;
using HomeServices.Models;
using SkyNet;

namespace HomeServices.codes
{
    public class AdminDispatch : WebPage
    {
        private const string AreaKey = "dispatch";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Dispatch board | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminDispatchJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminDispatchJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("ds-brand", Ui.Esc(site.Brand));
            response.SetElementContents("ds-menu", await Auth.Menu("ds", "AdminDispatch", c));
            response.SetElementContents("ds-who", Auth.Who("ds", c));
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"ds-panel\"><h1>No access</h1><p class=\"ds-hint\">You do not have access to " + what + ". Ask a manager if you need it.</p></div>";
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
            response.ExecuteScript("AdminDispatchJs.expired();");
            return response;
        }

        private static DateTime DayOf(string? v)
        {
            DateTime? d = Work.ParseDay(v);
            return d.HasValue && d.Value > DateTime.Today.AddYears(-2) && d.Value < DateTime.Today.AddYears(2) ? d.Value : DateTime.Today;
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
                response.SetElementContents("ds-main", NoAccess("the dispatch board"));
                return response;
            }
            DateTime day = DayOf(GetDataValue("view"));
            List<JobRec> jobs = await Store.Jobs();
            StringBuilder sb = new StringBuilder("<div class=\"ds-ph\"><div><h1>Dispatch board</h1><p>" + (c.Own.Length > 0 ? "Your day, window by window." : "One column per technician. Click a job to assign it or move it to another window.") + "</p></div></div>");
            sb.Append("<div class=\"ds-tools ds-daybar\"><button type=\"button\" class=\"ds-btn ds-btn-ghost ds-btn-sm\" onclick=\"AdminDispatchJs.shift(-1)\" aria-label=\"Previous day\">&larr; Previous</button><input type=\"date\" id=\"ds-day\" value=\"" + Work.Iso(day) + "\" onchange=\"AdminDispatchJs.go(this.value)\" aria-label=\"Day\"><button type=\"button\" class=\"ds-btn ds-btn-ghost ds-btn-sm\" onclick=\"AdminDispatchJs.shift(1)\" aria-label=\"Next day\">Next &rarr;</button><button type=\"button\" class=\"ds-btn ds-btn-ghost ds-btn-sm\" onclick=\"AdminDispatchJs.go('" + Work.Today() + "')\">Today</button></div>");
            sb.Append("<div id=\"ds-board\">" + BoardHtml(site, c, jobs, day) + "</div>");
            response.SetElementContents("ds-main", sb.ToString());
            return response;
        }

        public async Task<ApiResponse> Day()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "open dispatch", string.Empty, "You do not have access to the dispatch board.");
            }
            SiteData site = await Store.LoadSite();
            DateTime day = DayOf(GetDataValue("day"));
            response.SetElementContents("ds-board", BoardHtml(site, c, await Store.Jobs(), day));
            response.ExecuteScript("AdminDispatchJs.shown('" + Work.Iso(day) + "');");
            return response;
        }

        private static string Card(SiteData site, JobRec j)
        {
            return "<button type=\"button\" class=\"ds-card ds-st-" + j.Status + "\" style=\"--ds-tc:" + Work.TradeColor(site, j.Trade) + "\" onclick=\"AdminDispatchJs.card('" + j.Id + "')\"><span class=\"ds-card-w\">" + Ui.Esc(Work.WindowName(j.Window)) + "</span><b>" + Ui.Esc(j.Name) + "</b><small>" + Ui.Esc(j.Summary) + "</small><small>" + Ui.Esc(j.Town.Length > 0 ? j.Town : j.Zip) + "</small>" + Work.JobPill("ds", j.Status) + "</button>";
        }

        private static string BoardHtml(SiteData site, Ctx c, List<JobRec> all, DateTime day)
        {
            string iso = Work.Iso(day);
            int idx = Work.DayIndex(day);
            List<JobRec> jobs = all.Where(j => j.Date == iso && j.Status != "cancelled" && (c.Own.Length == 0 || j.TechId == c.Own)).ToList();
            List<(string Id, string Name, string Sub, string Photo, bool Off)> cols = new List<(string, string, string, string, bool)>();
            if (c.Own.Length == 0 && jobs.Any(j => j.TechId.Length == 0))
            {
                cols.Add((string.Empty, "Not assigned", "Click a job to assign it", string.Empty, false));
            }
            foreach (Technician t in site.Techs)
            {
                if (c.Own.Length > 0 && t.Id != c.Own)
                {
                    continue;
                }
                bool works = t.Active && t.Days.Contains(idx);
                bool has = jobs.Any(j => j.TechId == t.Id);
                if (works || has || (c.Own.Length > 0 && t.Id == c.Own))
                {
                    cols.Add((t.Id, t.Name, string.Join(", ", t.Trades.Select(x => Work.TradeName(site, x))), t.Photo, !works));
                }
            }
            CultureInfo us = CultureInfo.GetCultureInfo("en-US");
            StringBuilder sb = new StringBuilder("<h2 class=\"ds-dayh\">" + day.ToString("dddd, MMMM d, yyyy", us) + (iso == Work.Today() ? " <span class=\"ds-pill ds-pill-new\">Today</span>" : string.Empty) + " <small>" + Ui.Count(jobs.Count, "job", "jobs") + "</small></h2>");
            if (cols.Count == 0)
            {
                return sb.Append("<div class=\"ds-panel ds-empty\">No technician works on " + day.ToString("dddd", us) + "s and no jobs are booked.</div>").ToString();
            }
            sb.Append("<div class=\"ds-grid\" style=\"--ds-cols:" + cols.Count + "\"><div class=\"ds-gh ds-gcorner\"></div>");
            foreach ((string id, string name, string sub, string photo, bool off) in cols)
            {
                sb.Append("<div class=\"ds-gh" + (id.Length == 0 ? " ds-gun" : string.Empty) + "\">" + (photo.Length > 0 ? "<img src=\"" + Ui.Esc(photo) + "\" alt=\"\">" : "<span class=\"ds-gav\">" + (id.Length == 0 ? "?" : Ui.Esc(Work.Initials(name))) + "</span>") + "<span><b>" + Ui.Esc(name) + "</b><small>" + Ui.Esc(off ? "Not a working day" : sub) + "</small></span></div>");
            }
            foreach ((string wk, string wname, int _) in Work.Windows)
            {
                sb.Append("<div class=\"ds-gw\">" + Ui.Esc(wname) + "</div>");
                foreach ((string id, string _, string _, string _, bool _) in cols)
                {
                    sb.Append("<div class=\"ds-gc\">" + string.Concat(jobs.Where(j => j.TechId == id && j.Window == wk).Select(j => Card(site, j))) + "</div>");
                }
            }
            sb.Append("</div><div class=\"ds-stackv\">");
            foreach ((string id, string name, string sub, string _, bool off) in cols)
            {
                List<JobRec> mine = jobs.Where(j => j.TechId == id).OrderBy(j => j.Window).ToList();
                sb.Append("<section class=\"ds-sv\"><h3>" + Ui.Esc(name) + " <small>" + Ui.Esc(off ? "Not a working day" : Ui.Count(mine.Count, "job", "jobs")) + "</small></h3>" + (mine.Count == 0 ? "<p class=\"ds-hint\">No jobs.</p>" : "<div class=\"ds-svl\">" + string.Concat(mine.Select(j => Card(site, j))) + "</div>") + "</section>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Open()
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
                return await Refuse(response, c, "open job", id, "You do not have access to the dispatch board.");
            }
            JobRec? j = (await Store.Jobs()).FirstOrDefault(x => x.Id == id && (c.Own.Length == 0 || x.TechId == c.Own));
            if (j == null)
            {
                response.ExecuteScript("AdminDispatchJs.toast('That job was not found.');");
                return response;
            }
            SiteData site = await Store.LoadSite();
            bool canE = Access.Can(c.U, AreaKey, 'E') && c.Own.Length == 0 && j.Status == "scheduled";
            StringBuilder sb = new StringBuilder("<form class=\"ds-stack\" id=\"ds-aform\" onsubmit=\"return AdminDispatchJs.assign('" + j.Id + "')\" novalidate><div class=\"ds-jhead\">" + Work.JobPill("ds", j.Status) + "<b>" + Ui.Esc(Ui.LongDay(j.Date)) + "</b></div>");
            sb.Append("<dl class=\"ds-kv\"><dt>Customer</dt><dd><b>" + Ui.Esc(j.Name) + "</b>, <a href=\"" + Ui.Esc(Work.Tel(j.Phone)) + "\">" + Ui.Esc(j.Phone) + "</a></dd><dt>Address</dt><dd>" + Ui.Esc(j.Address) + ", " + Ui.Esc(j.Town) + " " + Ui.Esc(j.Zip) + "</dd><dt>Work</dt><dd>" + Ui.Esc(j.Summary) + "</dd>" + (j.Notes.Length > 0 ? "<dt>Notes</dt><dd>" + Ui.Esc(j.Notes).Replace("\n", "<br>") + "</dd>" : string.Empty) + "</dl>");
            if (canE)
            {
                sb.Append("<div class=\"ds-grid2\"><label class=\"ds-field\" for=\"ds-f-window\"><span>Arrival window</span><select id=\"ds-f-window\" data-k=\"window\">" + string.Concat(Work.Windows.Select(w => "<option value=\"" + w.Key + "\"" + (w.Key == j.Window ? " selected" : string.Empty) + ">" + Ui.Esc(w.Name) + "</option>")) + "</select><em class=\"ds-err\" id=\"ds-e-window\"></em></label>");
                sb.Append("<label class=\"ds-field\" for=\"ds-f-tech\"><span>Technician</span><select id=\"ds-f-tech\" data-k=\"tech\"><option value=\"\">Not assigned</option>" + string.Concat(site.Techs.Where(t => t.Active || t.Id == j.TechId).Select(t => "<option value=\"" + Ui.Esc(t.Id) + "\"" + (t.Id == j.TechId ? " selected" : string.Empty) + ">" + Ui.Esc(t.Name) + "</option>")) + "</select><em class=\"ds-err\" id=\"ds-e-tech\"></em></label></div>");
            }
            else if (Access.Can(c.U, AreaKey, 'E') && c.Own.Length == 0)
            {
                sb.Append("<p class=\"ds-hint\">Only scheduled jobs can be moved on the board.</p>");
            }
            sb.Append("<div class=\"ds-acts\">" + (canE ? "<button type=\"submit\" class=\"ds-btn\">Save</button>" : string.Empty) + (Access.Can(c.U, "jobs", 'V') ? "<a class=\"ds-btn ds-btn-ghost\" href=\"AdminJobs?show=" + j.Id + "\">Open in Jobs</a>" : string.Empty) + "<button type=\"button\" class=\"ds-btn ds-btn-ghost\" onclick=\"AdminDispatchJs.closeModal()\">Close</button></div></form>");
            response.SetElementContents("ds-mbody", sb.ToString());
            response.ExecuteScript("AdminDispatchJs.openModal('" + Ui.Js("Job " + j.Id + " · " + j.Name) + "');");
            return response;
        }

        public async Task<ApiResponse> Assign()
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
                return await Refuse(response, c, "assign job", id, "You are not allowed to assign jobs.");
            }
            string window = Ui.Clip(GetDataValue("window"), 5);
            string tech = Ui.Clip(GetDataValue("tech"), 20);
            SiteData site = await Store.LoadSite();
            string eWin = Work.Windows.Any(w => w.Key == window) ? string.Empty : "Pick an arrival window.";
            string eTech = string.Empty;
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
                else if (eWin.Length == 0)
                {
                    DateTime d = Work.ParseDay(j.Date) ?? DateTime.Today;
                    Technician? t = tech.Length == 0 ? null : site.Techs.FirstOrDefault(x => x.Id == tech);
                    if (tech.Length > 0 && (t == null || !t.Active))
                    {
                        eTech = "Pick an active technician.";
                    }
                    else if (t != null && !t.Days.Contains(Work.DayIndex(d)))
                    {
                        eTech = t.Name + " does not work on " + Work.LongDayNames[Work.DayIndex(d) - 1] + "s.";
                    }
                    else if (t != null && j.Trade.Length > 0 && !t.Trades.Contains(j.Trade))
                    {
                        eTech = t.Name + " does not do " + Work.TradeName(site, j.Trade) + " work.";
                    }
                    else if (t != null && jobs.Any(x => x.Id != j.Id && x.TechId == tech && x.Date == j.Date && x.Window == window && Work.Open(x.Status)))
                    {
                        eTech = t.Name + " already has a job in that window.";
                    }
                    else
                    {
                        j.TechId = tech;
                        j.Window = window;
                        done = j;
                        await Store.SaveJobs(jobs);
                    }
                }
            }
            response.SetElementContents("ds-e-window", eWin);
            response.SetElementContents("ds-e-tech", Ui.Esc(eTech));
            if (error.Length > 0)
            {
                response.ExecuteScript("AdminDispatchJs.toast('" + Ui.Js(error) + "');");
                return response;
            }
            if (done == null)
            {
                return response;
            }
            await Store.Log(c.U.Id, "assign job", done.Id + " · " + Work.WindowName(done.Window) + " · " + Work.TechName(site, done.TechId), true);
            response.SetElementContents("ds-board", BoardHtml(site, c, await Store.Jobs(), Work.ParseDay(done.Date) ?? DateTime.Today));
            response.ExecuteScript("AdminDispatchJs.shut();AdminDispatchJs.toast('Job " + done.Id + " moved');");
            return response;
        }
    }
}
