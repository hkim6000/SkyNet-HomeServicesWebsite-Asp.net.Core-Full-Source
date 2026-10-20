using System.Text;
using HomeServices.Core;
using HomeServices.Models;
using SkyNet;

namespace HomeServices.codes
{
    public class AdminLogin : WebPage
    {
        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Staff sign in");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            await Store.EnsureFirstManager();
            SiteData site = await Store.LoadSite();
            bool demo = Store.Demo();
            string first = string.Empty;
            string pass = Store.InitialPassword();
            if (!demo && Local.Request() && pass.Length > 0)
            {
                first = "<div class=\"lg-first\"><b>First sign-in</b><dl><dt>User name</dt><dd>admin</dd><dt>Password</dt><dd>" + Ui.Esc(pass) + "</dd></dl><small>Shown only on this computer, until you choose your own password. Also in appConfig/initial-admin.txt.</small></div>";
            }
            string demoNote = demo ? "<p class=\"lg-note\">Demo mode: any user name and password signs in as a demo manager with every right.</p>" : string.Empty;
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_brand}", Ui.Esc(site.Brand))
                .Replace("{plhd_first}", first)
                .Replace("{plhd_demo}", demoNote)
                .Replace("{plhd_user}", demo ? "demo" : string.Empty)
                .Replace("{plhd_pass}", demo ? "demo1234" : string.Empty);
        }

        public async Task<ApiResponse> SignIn()
        {
            ApiResponse response = new ApiResponse();
            string login = Ui.Clip(GetDataValue("user"), 40).ToLowerInvariant();
            string pass = GetDataValue("pass") ?? string.Empty;
            List<Staff> staff = await Store.LoadStaff();
            Staff? u = staff.FirstOrDefault(x => x.Login == login && x.Active && x.Id != "u-demo");
            if (Store.Demo())
            {
                if (u != null && !u.MustChange && Access.Verify(pass, u.Hash))
                {
                    await Open(response, u);
                    return response;
                }
                Staff demo = await Store.DemoManager();
                await Open(response, demo);
                return response;
            }
            if (login.Length == 0 || pass.Length == 0)
            {
                response.SetElementContents("lg-msg", "Enter your user name and password.");
                return response;
            }
            if (Auth.Paused(login))
            {
                await Store.Log(u?.Id ?? "?", "sign in paused", login, false);
                response.SetElementContents("lg-msg", "Too many wrong passwords. Wait five minutes and try again.");
                return response;
            }
            if (u == null || !Access.Verify(pass, u.Hash))
            {
                Auth.Failed(login);
                await Store.Log(u?.Id ?? "?", "sign in", login, false);
                response.SetElementContents("lg-msg", "That user name and password do not match.");
                return response;
            }
            if (u.MustChange)
            {
                response.ExecuteScript("AdminLoginJs.choose();");
                return response;
            }
            await Open(response, u);
            return response;
        }

        public async Task<ApiResponse> SetPassword()
        {
            ApiResponse response = new ApiResponse();
            string login = Ui.Clip(GetDataValue("user"), 40).ToLowerInvariant();
            string pass = GetDataValue("pass") ?? string.Empty;
            string np1 = GetDataValue("np1") ?? string.Empty;
            string np2 = GetDataValue("np2") ?? string.Empty;
            if (Auth.Paused(login))
            {
                response.SetElementContents("lg-msg", "Too many wrong passwords. Wait five minutes and try again.");
                return response;
            }
            List<Staff> check = await Store.LoadStaff();
            Staff? found = check.FirstOrDefault(x => x.Login == login && x.Active && x.Id != "u-demo");
            if (found == null || !Access.Verify(pass, found.Hash))
            {
                Auth.Failed(login);
                await Store.Log(found?.Id ?? "?", "set password", login, false);
                response.SetElementContents("lg-msg", "That user name and password do not match.");
                return response;
            }
            string err = np1.Length < 8 ? "Use 8 characters or more." : np1.Length > 100 ? "Use 100 characters or fewer." : np1 == pass ? "Choose a password different from the one you were given." : np1 != np2 ? "The two new passwords are not the same." : string.Empty;
            if (err.Length > 0)
            {
                response.SetElementContents("lg-msg", err);
                return response;
            }
            Staff? u;
            using (await Store.Hold())
            {
                List<Staff> staff = await Store.LoadStaff();
                u = staff.FirstOrDefault(x => x.Id == found.Id);
                if (u != null)
                {
                    u.Hash = Access.HashPassword(np1);
                    u.MustChange = false;
                    await Store.SaveStaff(staff);
                }
            }
            if (u == null)
            {
                return response;
            }
            if (u.Admin && File.Exists(Store.InitialFile))
            {
                File.Delete(Store.InitialFile);
            }
            Auth.Cleared(login);
            await Auth.CloseAll(u.Id, string.Empty);
            await Store.Log(u.Id, "set own password", u.Login, true);
            await Open(response, u);
            return response;
        }

        private static async Task Open(ApiResponse response, Staff u)
        {
            string tok = await Auth.Open(u.Id);
            Auth.Cleared(u.Login);
            await Store.Log(u.Id, "sign in", u.Login, true);
            response.ExecuteScript("AdminLoginJs.signedIn('" + tok + "');");
        }
    }
}
