using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HomeServices.Core;
using HomeServices.Models;
using SkyNet;

namespace HomeServices.codes
{
    public class AdminUsers : WebPage
    {
        private const string AreaKey = "users";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Users and rights | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminUsersJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminUsersJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("au-brand", Ui.Esc(site.Brand));
            response.SetElementContents("au-menu", await Auth.Menu("au", "AdminUsers", c));
            response.SetElementContents("au-who", Auth.Who("au", c));
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"au-panel\"><h1>No access</h1><p class=\"au-hint\">You do not have access to " + what + ". Ask a manager if you need it.</p></div>";
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
            response.ExecuteScript("AdminUsersJs.expired();");
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
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                response.SetElementContents("au-main", NoAccess("users and rights"));
                return response;
            }
            response.SetElementContents("au-main", "<div class=\"au-ph\"><div><h1>Users and rights</h1><p>Each person signs in with their own account. Give each one View, Edit or Extra per area. A technician login set to Own jobs only sees just their own jobs and dispatch column.</p></div><button type=\"button\" class=\"au-btn\" onclick=\"AdminUsersJs.edit('')\">Add user</button></div><div id=\"au-list\">" + ListHtml(site, await Store.LoadStaff()) + "</div>");
            return response;
        }

        private static string Summary(Staff u)
        {
            if (u.Admin)
            {
                return "Manager: every area";
            }
            List<string> parts = Access.Areas.Where(a => Access.Level(u, a.Key).Length > 0).Select(a => a.Name + " " + Access.Level(u, a.Key)).ToList();
            return parts.Count == 0 ? "No areas yet" : string.Join(", ", parts);
        }

        private static string ListHtml(SiteData site, List<Staff> staff)
        {
            List<Staff> rows = staff.Where(s => s.Id != "u-demo").OrderBy(s => !s.Active).ThenBy(s => s.Name).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"au-panel au-empty\">No staff accounts yet. In Demo mode everyone signs in as the demo manager; add real accounts here before switching to Production.</div>";
            }
            StringBuilder sb = new StringBuilder("<div class=\"au-wrapt\"><table class=\"au-table\"><thead><tr><th>Person</th><th>Title</th><th>Rights</th><th>Technician</th><th>Status</th></tr></thead><tbody>");
            foreach (Staff s in rows)
            {
                sb.Append("<tr class=\"au-row\" tabindex=\"0\" onclick=\"AdminUsersJs.edit('" + Ui.Js(s.Id) + "')\" onkeydown=\"if(event.key==='Enter'){AdminUsersJs.edit('" + Ui.Js(s.Id) + "')}\"><td data-l=\"Person\"><b>" + Ui.Esc(s.Name) + "</b><small>" + Ui.Esc(s.Login) + "</small></td><td data-l=\"Title\">" + Ui.Esc(s.Title) + "</td><td data-l=\"Rights\"><small>" + Ui.Esc(Summary(s)) + "</small></td><td data-l=\"Technician\">" + (s.TechId.Length > 0 ? Ui.Esc(Work.TechName(site, s.TechId)) + (s.Own ? "<small>own jobs only</small>" : string.Empty) : "&ndash;") + "</td><td data-l=\"Status\">" + (!s.Active ? Work.Pill("au", "off", "Switched off") : s.MustChange ? Work.Pill("au", "wait", "Must set password") : s.Admin ? Work.Pill("au", "info", "Manager") : Work.Pill("au", "go", "Active")) + "</td></tr>");
            }
            return sb.Append("</tbody></table></div>").ToString();
        }

        private static string Grid(Staff u)
        {
            StringBuilder sb = new StringBuilder("<div class=\"au-rights\" id=\"au-rights\"><div class=\"au-rh\"><span>Area</span><span>None</span><span>View</span><span>Edit</span><span>Extra</span></div>");
            foreach (Area a in Access.Areas.Where(a => !a.AdminOnly))
            {
                string lvl = u.Admin ? string.Empty : Access.Level(u, a.Key);
                sb.Append("<div class=\"au-rr\" data-area=\"" + a.Key + "\"><span class=\"au-ra\"><b>" + Ui.Esc(a.Name) + "</b><small>" + Ui.Esc(a.Hint) + "</small></span>");
                foreach ((string v, string label) in new[] { ("", "None"), ("V", "View"), ("VE", "Edit"), ("VEX", a.Extra.Length > 0 ? a.Extra : "Extra") })
                {
                    bool na = (v == "VE" && a.NoEdit) || (v == "VEX" && (a.NoEdit || a.Extra.Length == 0));
                    sb.Append("<label class=\"au-rc\"" + (na ? " aria-hidden=\"true\"" : string.Empty) + ">" + (na ? "<i>&ndash;</i>" : "<input type=\"radio\" name=\"au-r-" + a.Key + "\" value=\"" + v + "\"" + (lvl == v ? " checked" : string.Empty) + " aria-label=\"" + Ui.Esc(a.Name + ": " + label) + "\"><span>" + Ui.Esc(label) + "</span>") + "</label>");
                }
                sb.Append("</div>");
            }
            sb.Append("<p class=\"au-hint\">Settings and Users and rights are for managers only.</p></div>");
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
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "open user", id, "Only managers can manage users.");
            }
            SiteData site = await Store.LoadSite();
            Staff? u = id.Length == 0 ? new Staff() : (await Store.LoadStaff()).FirstOrDefault(x => x.Id == id && x.Id != "u-demo");
            if (u == null)
            {
                response.ExecuteScript("AdminUsersJs.toast('That user was not found.');");
                return response;
            }
            bool self = u.Id == c.U.Id;
            StringBuilder sb = new StringBuilder("<form class=\"au-stack\" id=\"au-form\" onsubmit=\"return AdminUsersJs.save('" + Ui.Js(u.Id) + "')\" novalidate><div class=\"au-grid3\">");
            sb.Append("<label class=\"au-field\" for=\"au-f-login\"><span>User name</span><input type=\"text\" id=\"au-f-login\" data-k=\"login\" maxlength=\"30\" autocomplete=\"off\" value=\"" + Ui.Esc(u.Login) + "\"><small>Lower-case letters, digits, dot or dash.</small><em class=\"au-err\" id=\"au-e-login\"></em></label>");
            sb.Append("<label class=\"au-field\" for=\"au-f-name\"><span>Full name</span><input type=\"text\" id=\"au-f-name\" data-k=\"name\" maxlength=\"60\" value=\"" + Ui.Esc(u.Name) + "\"><em class=\"au-err\" id=\"au-e-name\"></em></label>");
            sb.Append("<label class=\"au-field\" for=\"au-f-title\"><span>Job title</span><input type=\"text\" id=\"au-f-title\" data-k=\"title\" maxlength=\"40\" value=\"" + Ui.Esc(u.Title) + "\"><em class=\"au-err\" id=\"au-e-title\"></em></label></div>");
            sb.Append("<div class=\"au-checks\"><label class=\"au-check\"><input type=\"checkbox\" data-k=\"admin\" id=\"au-f-admin\" onchange=\"AdminUsersJs.adminToggle()\"" + (u.Admin ? " checked" : string.Empty) + (self ? " disabled" : string.Empty) + "> Manager: every right, including Settings and Users</label><label class=\"au-check\"><input type=\"checkbox\" data-k=\"active\"" + (u.Active || u.Id.Length == 0 ? " checked" : string.Empty) + (self ? " disabled" : string.Empty) + "> Active: may sign in</label></div>");
            if (self)
            {
                sb.Append("<p class=\"au-hint\">You cannot remove your own manager right or switch yourself off.</p>");
            }
            sb.Append("<div class=\"au-grid2\"><label class=\"au-field\" for=\"au-f-tech\"><span>Technician record</span><select id=\"au-f-tech\" data-k=\"tech\"><option value=\"\">Not a technician</option>" + string.Concat(site.Techs.Select(t => "<option value=\"" + Ui.Esc(t.Id) + "\"" + (t.Id == u.TechId ? " selected" : string.Empty) + ">" + Ui.Esc(t.Name) + "</option>")) + "</select><em class=\"au-err\" id=\"au-e-tech\"></em></label><label class=\"au-check au-own\"><input type=\"checkbox\" data-k=\"own\"" + (u.Own ? " checked" : string.Empty) + "> Own jobs only: Jobs, Dispatch and the dashboard show only this technician&rsquo;s jobs, and they cannot reassign or reschedule</label></div>");
            sb.Append("<div class=\"au-presets\" id=\"au-presets\"" + (u.Admin ? " hidden" : string.Empty) + "><span>Quick start:</span><button type=\"button\" class=\"au-btn au-btn-ghost au-btn-sm\" onclick=\"AdminUsersJs.preset('tech')\">Technician</button><button type=\"button\" class=\"au-btn au-btn-ghost au-btn-sm\" onclick=\"AdminUsersJs.preset('office')\">Office</button><button type=\"button\" class=\"au-btn au-btn-ghost au-btn-sm\" onclick=\"AdminUsersJs.preset('books')\">Bookkeeper</button><button type=\"button\" class=\"au-btn au-btn-ghost au-btn-sm\" onclick=\"AdminUsersJs.preset('web')\">Website editor</button><button type=\"button\" class=\"au-btn au-btn-ghost au-btn-sm\" onclick=\"AdminUsersJs.preset('')\">Clear</button></div>");
            sb.Append("<div id=\"au-gridwrap\"" + (u.Admin ? " hidden" : string.Empty) + ">" + Grid(u) + "</div><em class=\"au-err\" id=\"au-e-perms\"></em>");
            sb.Append("<div class=\"au-acts\"><button type=\"submit\" class=\"au-btn\">" + (u.Id.Length == 0 ? "Add user" : "Save") + "</button><button type=\"button\" class=\"au-btn au-btn-ghost\" onclick=\"AdminUsersJs.closeModal()\">Cancel</button>" + (u.Id.Length > 0 && !self ? "<button type=\"button\" class=\"au-btn au-btn-ghost au-push\" onclick=\"AdminUsersJs.reset('" + Ui.Js(u.Id) + "','" + Ui.Js(u.Name) + "')\">Reset password</button>" : string.Empty) + "</div></form>");
            response.SetElementContents("au-mbody", sb.ToString());
            response.ExecuteScript("AdminUsersJs.openModal('" + Ui.Js(u.Id.Length == 0 ? "Add user" : u.Name) + "');");
            return response;
        }

        private static string Shown(string login, string pass, string what)
        {
            return "<div class=\"au-stack\"><div class=\"au-note au-note-ok\">" + what + "</div><dl class=\"au-kv au-pass\"><dt>User name</dt><dd>" + Ui.Esc(login) + "</dd><dt>Temporary password</dt><dd><code>" + Ui.Esc(pass) + "</code></dd></dl><p class=\"au-hint\">Give it to the person in private. It is shown only now. They choose their own password at their first sign-in.</p><div class=\"au-acts\"><button type=\"button\" class=\"au-btn\" onclick=\"AdminUsersJs.shut()\">Done</button></div></div>";
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
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "save user", id, "Only managers can manage users.");
            }
            SiteData site = await Store.LoadSite();
            string login = Ui.Clip(GetDataValue("login"), 60).ToLowerInvariant();
            string name = Ui.Clip(GetDataValue("name"), 120);
            string title = Ui.Clip(GetDataValue("title"), 80);
            bool admin = GetDataValue("admin") == "1";
            bool active = GetDataValue("active") == "1";
            string tech = Ui.Clip(GetDataValue("tech"), 40);
            bool own = GetDataValue("own") == "1";
            Dictionary<string, string> raw = new Dictionary<string, string>();
            foreach (string pair in (GetDataValue("perms") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = pair.Split(':');
                if (kv.Length == 2)
                {
                    raw[kv[0].Trim()] = kv[1].Trim();
                }
            }
            Dictionary<string, string> perms = Access.CleanPerms(raw);
            bool self = id == c.U.Id;
            if (self)
            {
                admin = c.U.Admin;
                active = true;
            }
            List<Staff> check = await Store.LoadStaff();
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                { "login", !Regex.IsMatch(login, "^[a-z0-9][a-z0-9._-]{2,29}$") ? "Use 3 to 30 lower-case letters, digits, dot or dash." : login == "demo" ? "That user name is reserved." : check.Any(s => s.Id != id && s.Login == login) ? "Another user has this user name." : string.Empty },
                { "name", name.Length < 2 || name.Length > 60 ? "Enter the full name." : string.Empty },
                { "title", title.Length > 40 ? "Keep it under 40 characters." : string.Empty },
                { "tech", tech.Length > 0 && !site.Techs.Any(t => t.Id == tech) ? "Pick a technician from the list." : own && tech.Length == 0 && !admin ? "Pick the technician whose jobs this person sees." : string.Empty },
                { "perms", !admin && perms.Count == 0 ? "Give at least one area, or tick Manager." : string.Empty }
            };
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("au-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0))
            {
                return response;
            }
            string pass = string.Empty;
            bool closeAll = false;
            List<Staff> staff;
            using (await Store.Hold())
            {
                staff = await Store.LoadStaff();
                Staff? u = id.Length == 0 ? null : staff.FirstOrDefault(x => x.Id == id && x.Id != "u-demo");
                if (u == null)
                {
                    pass = Access.NewPassword();
                    u = new Staff { Id = "u-" + Access.NewToken(5), Hash = Access.HashPassword(pass), MustChange = true };
                    staff.Add(u);
                }
                closeAll = u.Active && !active;
                u.Login = login;
                u.Name = name;
                u.Title = title;
                u.Admin = admin;
                u.Active = active;
                u.TechId = tech;
                u.Own = !admin && own && tech.Length > 0;
                u.Perms = admin ? new Dictionary<string, string>() : perms;
                id = u.Id;
                await Store.SaveStaff(staff);
            }
            if (closeAll)
            {
                await Auth.CloseAll(id, string.Empty);
            }
            await Store.Log(c.U.Id, pass.Length > 0 ? "add user" : "edit user", login + " · " + (admin ? "manager" : string.Join(" ", perms.Select(p => p.Key + ":" + p.Value))) + (active ? string.Empty : " · switched off"), true);
            response.SetElementContents("au-list", ListHtml(site, staff));
            if (pass.Length > 0)
            {
                response.SetElementContents("au-mbody", Shown(login, pass, "Account created for " + Ui.Esc(name) + "."));
                response.ExecuteScript("AdminUsersJs.clean();AdminUsersJs.toast('User added');");
            }
            else
            {
                response.ExecuteScript("AdminUsersJs.clean();AdminUsersJs.shut();AdminUsersJs.toast('Saved');");
            }
            return response;
        }

        public async Task<ApiResponse> Reset()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 40);
            if (!Access.Can(c.U, AreaKey, 'E') || id == c.U.Id)
            {
                return await Refuse(response, c, "reset password", id, id == c.U.Id ? "Change your own password under My account." : "Only managers can reset passwords.");
            }
            string pass = Access.NewPassword();
            string login = string.Empty;
            using (await Store.Hold())
            {
                List<Staff> staff = await Store.LoadStaff();
                Staff? u = staff.FirstOrDefault(x => x.Id == id && x.Id != "u-demo");
                if (u != null)
                {
                    u.Hash = Access.HashPassword(pass);
                    u.MustChange = true;
                    login = u.Login;
                    await Store.SaveStaff(staff);
                }
            }
            if (login.Length == 0)
            {
                response.ExecuteScript("AdminUsersJs.toast('That user was not found.');");
                return response;
            }
            await Auth.CloseAll(id, string.Empty);
            await Store.Log(c.U.Id, "reset password", login, true);
            response.SetElementContents("au-list", ListHtml(await Store.LoadSite(), await Store.LoadStaff()));
            response.SetElementContents("au-mbody", Shown(login, pass, "Password reset. Their open sessions are closed."));
            return response;
        }
    }
}
