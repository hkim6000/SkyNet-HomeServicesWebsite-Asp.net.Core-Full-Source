using System.Globalization;
using System.Text;
using HomeServices.Core;
using HomeServices.Models;
using SkyNet;

namespace HomeServices.codes
{
    public class AdminActivity : WebPage
    {
        private const string AreaKey = "activity";
        private const int Size = 25;

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Activity | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminActivityJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminActivityJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("av-brand", Ui.Esc(site.Brand));
            response.SetElementContents("av-menu", await Auth.Menu("av", "AdminActivity", c));
            response.SetElementContents("av-who", Auth.Who("av", c));
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"av-panel\"><h1>No access</h1><p class=\"av-hint\">You do not have access to " + what + ". Ask a manager if you need it.</p></div>";
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
            response.ExecuteScript("AdminActivityJs.expired();");
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
            bool all = Access.Can(c.U, AreaKey, 'V');
            List<LogEntry> log = await Store.LoadLog();
            StringBuilder sb = new StringBuilder("<div class=\"av-ph\"><div><h1>" + (all ? "Activity log" : "My activity") + "</h1><p>" + (all ? "Every sign-in, change and refused request, newest first. The latest 5,000 are kept." : "What you did, newest first.") + "</p></div></div><div class=\"av-tools\">");
            if (all)
            {
                sb.Append("<select id=\"av-person\" aria-label=\"Person\" onchange=\"AdminActivityJs.filter()\"><option value=\"\">Everyone</option>" + string.Concat(log.Select(l => (l.UserId, l.Who)).Distinct().OrderBy(x => x.Who).Select(x => "<option value=\"" + Ui.Esc(x.UserId) + "\">" + Ui.Esc(x.Who) + "</option>")) + "</select>");
            }
            sb.Append("<label class=\"av-check\"><input type=\"checkbox\" id=\"av-bad\" onchange=\"AdminActivityJs.filter()\"> Refused only</label><input type=\"search\" id=\"av-term\" placeholder=\"Search action or record\" maxlength=\"60\" oninput=\"AdminActivityJs.search()\" aria-label=\"Search activity\"></div>");
            sb.Append("<div id=\"av-list\">" + ListHtml(log, c, string.Empty, false, string.Empty, 1) + "</div>");
            response.SetElementContents("av-main", sb.ToString());
            return response;
        }

        private static string ListHtml(List<LogEntry> log, Ctx c, string who, bool bad, string term, int page)
        {
            bool all = Access.Can(c.U, AreaKey, 'V');
            List<LogEntry> rows = log.Where(l => (all ? (who.Length == 0 || l.UserId == who) : l.UserId == c.U.Id) && (!bad || !l.Ok) && (term.Length == 0 || Ui.Has(l.Action, term) || Ui.Has(l.Target, term))).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"av-panel av-empty\">Nothing to show.</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"av-wrapt\"><table class=\"av-table\"><thead><tr><th>When</th><th>Who</th><th>What</th><th>Record</th><th>Result</th></tr></thead><tbody>");
            foreach (LogEntry l in rows.Skip((page - 1) * Size).Take(Size))
            {
                sb.Append("<tr><td data-l=\"When\">" + Ui.Esc(Ui.When(l.When)) + "</td><td data-l=\"Who\">" + Ui.Esc(l.Who) + "</td><td data-l=\"What\">" + Ui.Esc(l.Action) + "</td><td data-l=\"Record\">" + Ui.Esc(l.Target) + "</td><td data-l=\"Result\">" + (l.Ok ? Work.Pill("av", "go", "Done") : Work.Pill("av", "bad", "Refused")) + "</td></tr>");
            }
            sb.Append("</tbody></table></div>" + Ui.Pager("av", page, Size, rows.Count, "entry", "entries", "AdminActivityJs.page"));
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
            response.SetElementContents("av-list", ListHtml(await Store.LoadLog(), c, Ui.Clip(GetDataValue("person"), 40), GetDataValue("bad") == "1", Ui.Clip(GetDataValue("term"), 60), Ui.PageNo(GetDataValue("pg"))));
            return response;
        }
    }
}
