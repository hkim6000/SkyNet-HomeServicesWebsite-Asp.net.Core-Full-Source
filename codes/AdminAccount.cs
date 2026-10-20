using System.Globalization;
using System.Text;
using HomeServices.Core;
using HomeServices.Models;
using SkyNet;

namespace HomeServices.codes
{
    public class AdminAccount : WebPage
    {
        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("My account | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAccountJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminAccountJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("ma-brand", Ui.Esc(site.Brand));
            response.SetElementContents("ma-menu", await Auth.Menu("ma", "AdminAccount", c));
            response.SetElementContents("ma-who", Auth.Who("ma", c));
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"ma-panel\"><h1>No access</h1><p class=\"ma-hint\">You do not have access to " + what + ". Ask a manager if you need it.</p></div>";
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
            response.ExecuteScript("AdminAccountJs.expired();");
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
            StringBuilder sb = new StringBuilder("<div class=\"ma-ph\"><div><h1>My account</h1><p>Signed in as <b>" + Ui.Esc(c.U.Login) + "</b>.</p></div></div>");
            if (c.U.Id == "u-demo")
            {
                sb.Append("<div class=\"ma-panel\"><h2>Demo manager</h2><p class=\"ma-hint\">This shared account exists only in Demo mode and has no password. Add real accounts under Users and rights, then set app.app.runmode to 1 in appConfig/application.cfg.</p></div>");
            }
            else
            {
                sb.Append("<form class=\"ma-panel ma-stack ma-narrow\" id=\"ma-form\" onsubmit=\"return AdminAccountJs.save()\" novalidate><h2>Change password</h2>");
                sb.Append("<label class=\"ma-field\" for=\"ma-f-cur\"><span>Current password</span><input type=\"password\" id=\"ma-f-cur\" data-k=\"cur\" maxlength=\"100\" autocomplete=\"current-password\"><em class=\"ma-err\" id=\"ma-e-cur\"></em></label>");
                sb.Append("<label class=\"ma-field\" for=\"ma-f-np1\"><span>New password</span><input type=\"password\" id=\"ma-f-np1\" data-k=\"np1\" maxlength=\"100\" autocomplete=\"new-password\"><small>8 characters or more.</small><em class=\"ma-err\" id=\"ma-e-np1\"></em></label>");
                sb.Append("<label class=\"ma-field\" for=\"ma-f-np2\"><span>New password again</span><input type=\"password\" id=\"ma-f-np2\" data-k=\"np2\" maxlength=\"100\" autocomplete=\"new-password\"><em class=\"ma-err\" id=\"ma-e-np2\"></em></label>");
                sb.Append("<p class=\"ma-hint\">Your other signed-in devices are signed out; this one stays.</p><div class=\"ma-acts\"><button type=\"submit\" class=\"ma-btn\">Change password</button></div></form>");
            }
            sb.Append("<section class=\"ma-panel\"><h2>What you can do</h2>" + (c.U.Admin ? "<p class=\"ma-hint\">You are a manager: every area, with every right.</p>" : string.Empty) + "<ul class=\"ma-mine\">");
            foreach (Area a in Access.Areas)
            {
                string lvl = Access.Level(c.U, a.Key);
                string text = lvl.Length == 0 ? "No access" : lvl == "V" ? "View" : lvl == "VE" ? "View and edit" : "View, edit and " + a.Extra.ToLowerInvariant();
                sb.Append("<li><b>" + Ui.Esc(a.Name) + "</b><span>" + text + (lvl.Length > 0 && a.Own && c.Own.Length > 0 ? ", own jobs only" : string.Empty) + "</span></li>");
            }
            sb.Append("</ul></section>");
            response.SetElementContents("ma-main", sb.ToString());
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
            if (c.U.Id == "u-demo")
            {
                return await Refuse(response, c, "change password", "demo", "The demo account has no password.");
            }
            string cur = GetDataValue("cur") ?? string.Empty;
            string np1 = GetDataValue("np1") ?? string.Empty;
            string np2 = GetDataValue("np2") ?? string.Empty;
            string eCur = !Access.Verify(cur, c.U.Hash) ? "That is not your current password." : string.Empty;
            string eNp1 = np1.Length < 8 ? "Use 8 characters or more." : np1.Length > 100 ? "Use 100 characters or fewer." : np1 == cur ? "Choose a different password." : string.Empty;
            string eNp2 = np1 != np2 ? "The two new passwords are not the same." : string.Empty;
            response.SetElementContents("ma-e-cur", eCur);
            response.SetElementContents("ma-e-np1", eNp1);
            response.SetElementContents("ma-e-np2", eNp2);
            if (eCur.Length > 0)
            {
                await Store.Log(c.U.Id, "change password", c.U.Login, false);
            }
            if (eCur + eNp1 + eNp2 != string.Empty)
            {
                return response;
            }
            using (await Store.Hold())
            {
                List<Staff> staff = await Store.LoadStaff();
                Staff? u = staff.FirstOrDefault(x => x.Id == c.U.Id);
                if (u != null)
                {
                    u.Hash = Access.HashPassword(np1);
                    await Store.SaveStaff(staff);
                }
            }
            await Auth.CloseAll(c.U.Id, c.Tok);
            await Store.Log(c.U.Id, "change password", c.U.Login, true);
            response.ExecuteScript("AdminAccountJs.changed();");
            return response;
        }
    }
}
