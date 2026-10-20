using System.Globalization;
using System.Text;
using HomeServices.Core;
using HomeServices.Models;
using SkyNet;

namespace HomeServices.codes
{
    public class AdminTechs : WebPage
    {
        private const string AreaKey = "techs";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Technicians | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminTechsJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminTechsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("at-brand", Ui.Esc(site.Brand));
            response.SetElementContents("at-menu", await Auth.Menu("at", "AdminTechs", c));
            response.SetElementContents("at-who", Auth.Who("at", c));
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"at-panel\"><h1>No access</h1><p class=\"at-hint\">You do not have access to " + what + ". Ask a manager if you need it.</p></div>";
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
            response.ExecuteScript("AdminTechsJs.expired();");
            return response;
        }

        private static string PickGrid(List<MediaItem> media, string term)
        {
            List<MediaItem> rows = media.Where(m => m.Kind == "photo" && (term.Length == 0 || Ui.Has(m.Name, term) || Ui.Has(m.Alt, term) || Ui.Has(m.Caption, term))).OrderByDescending(m => m.Added).Take(300).ToList();
            if (rows.Count == 0)
            {
                return "<p class=\"at-hint\">No photos match. Upload one above, or add it in the Media library.</p>";
            }
            return string.Concat(rows.Select(m => "<button type=\"button\" class=\"at-pki\" data-src=\"" + Ui.Esc(m.Path) + "\" data-thumb=\"" + Ui.Esc(m.Thumb.Length > 0 ? m.Thumb : m.Path) + "\" onclick=\"AdminTechsJs.pkPick(this)\" title=\"" + Ui.Esc(m.Name) + "\"><img src=\"" + Ui.Esc(m.Thumb.Length > 0 ? m.Thumb : m.Path) + "\" alt=\"\" loading=\"lazy\"><span>" + Ui.Esc(m.Name) + "</span></button>"));
        }

        private static string PicField(List<MediaItem> media, string path, string label, bool ro)
        {
            MediaItem? m = media.FirstOrDefault(x => x.Path == path);
            string thumb = m != null && m.Thumb.Length > 0 ? m.Thumb : path;
            return "<div class=\"at-field\"><span>" + label + "</span><div class=\"at-pic\"><img src=\"" + Ui.Esc(thumb) + "\" alt=\"\"" + (path.Length == 0 ? " hidden" : string.Empty) + "><input type=\"hidden\" data-k=\"photo\" value=\"" + Ui.Esc(path) + "\">" + (ro ? string.Empty : "<button type=\"button\" class=\"at-btn at-btn-ghost at-btn-sm\" onclick=\"AdminTechsJs.pkOpen(this)\">Choose photo</button><button type=\"button\" class=\"at-btn at-btn-ghost at-btn-sm\" onclick=\"AdminTechsJs.pkNone(this)\">Remove</button>") + "</div><em class=\"at-err\" id=\"at-e-photo\"></em></div>";
        }

        public async Task<ApiResponse> Pick()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null || !Access.Can(c.U, AreaKey, 'E'))
            {
                return response;
            }
            response.SetElementContents("at-pkgrid", PickGrid(await Media.Load(), Ui.Clip(GetDataValue("pterm"), 40)));
            return response;
        }

        public async Task<ApiResponse> PickUp()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E') || !Access.Can(c.U, "media", 'E'))
            {
                response.ExecuteScript("AdminTechsJs.pkUploaded('You are not allowed to upload to the media library.');");
                return await Refuse(response, c, "upload media", string.Empty, "You are not allowed to upload to the media library.");
            }
            SiteData site = await Store.LoadSite();
            List<Incoming> files = (await Media.Received()).Where(f => Path.GetExtension(f.Name).ToLowerInvariant() != ".mp4").ToList();
            if (files.Count == 0)
            {
                response.ExecuteScript("AdminTechsJs.pkUploaded('Choose a photo first.');");
                return response;
            }
            (int added, List<string> errors) = await Media.AddAll(files, site.MaxVideoMb, c.U.Name);
            if (added > 0)
            {
                await Store.Log(c.U.Id, "upload media", Ui.Count(added, "file", "files"), true);
            }
            response.SetElementContents("at-pkgrid", PickGrid(await Media.Load(), string.Empty));
            response.ExecuteScript("AdminTechsJs.pkUploaded('" + Ui.Js(((added > 0 ? Ui.Count(added, "photo", "photos") + " added to the library. " : string.Empty) + string.Join(" ", errors)).Trim()) + "');");
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
                response.SetElementContents("at-main", NoAccess("technicians"));
                return response;
            }
            response.SetElementContents("at-main", "<div class=\"at-ph\"><div><h1>Technicians</h1><p>Who does which trade, and on which days. Jobs can only be booked on a technician&rsquo;s working days.</p></div>" + (Access.Can(c.U, AreaKey, 'E') ? "<button type=\"button\" class=\"at-btn\" onclick=\"AdminTechsJs.edit('')\">Add technician</button>" : string.Empty) + "</div><div id=\"at-list\">" + ListHtml(site, await Store.Jobs(), c) + "</div>");
            return response;
        }

        private static string ListHtml(SiteData site, List<JobRec> jobs, Ctx c)
        {
            if (site.Techs.Count == 0)
            {
                return "<div class=\"at-panel at-empty\">No technicians yet.</div>";
            }
            string today = Work.Today();
            StringBuilder sb = new StringBuilder("<div class=\"at-tcards\">");
            foreach (Technician t in site.Techs.OrderBy(t => !t.Active).ThenBy(t => t.Name))
            {
                int upcoming = jobs.Count(j => j.TechId == t.Id && Work.Open(j.Status) && string.CompareOrdinal(j.Date, today) >= 0);
                sb.Append("<article class=\"at-tcard" + (t.Active ? string.Empty : " at-off") + "\">" + (t.Photo.Length > 0 ? "<img src=\"" + Ui.Esc(t.Photo) + "\" alt=\"\">" : "<span class=\"at-tav\">" + Ui.Esc(Work.Initials(t.Name)) + "</span>") + "<div class=\"at-tbody\"><b>" + Ui.Esc(t.Name) + "</b><small>" + Ui.Esc(t.Role) + "</small><div class=\"at-ttr\">" + string.Concat(t.Trades.Select(k => "<span class=\"at-pill\"><span class=\"at-dot\" style=\"background:" + Work.TradeColor(site, k) + "\"></span>" + Ui.Esc(Work.TradeName(site, k)) + "</span>")) + "</div><div class=\"at-tdays\">");
                for (int d = 1; d <= 7; d++)
                {
                    sb.Append("<i class=\"" + (t.Days.Contains(d) ? "at-don" : string.Empty) + "\" title=\"" + Work.LongDayNames[d - 1] + (t.Days.Contains(d) ? ": working" : ": off") + "\">" + Work.DayNames[d - 1].Substring(0, 2) + "</i>");
                }
                sb.Append("</div><small>" + (t.Active ? Ui.Count(upcoming, "upcoming job", "upcoming jobs") : "Not active") + (t.Phone.Length > 0 ? " &middot; " + Ui.Esc(t.Phone) : string.Empty) + "</small></div><button type=\"button\" class=\"at-btn at-btn-ghost at-btn-sm\" onclick=\"AdminTechsJs.edit('" + Ui.Js(t.Id) + "')\">" + (Access.Can(c.U, AreaKey, 'E') ? "Edit" : "View") + "</button></article>");
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
            string id = Ui.Clip(GetDataValue("id"), 40);
            if (!Access.Can(c.U, AreaKey, 'V') || (id.Length == 0 && !Access.Can(c.U, AreaKey, 'E')))
            {
                return await Refuse(response, c, "open technician", id, "You can look at technicians but not add them.");
            }
            SiteData site = await Store.LoadSite();
            Technician? t = id.Length == 0 ? new Technician { Days = new List<int> { 1, 2, 3, 4, 5 } } : site.Techs.FirstOrDefault(x => x.Id == id);
            if (t == null)
            {
                response.ExecuteScript("AdminTechsJs.toast('That technician was not found.');");
                return response;
            }
            bool ro = !Access.Can(c.U, AreaKey, 'E');
            string d = ro ? " disabled" : string.Empty;
            List<MediaItem> media = await Media.Load();
            StringBuilder sb = new StringBuilder("<form class=\"at-stack\" id=\"at-form\" onsubmit=\"return AdminTechsJs.save('" + Ui.Js(t.Id) + "')\" novalidate><div class=\"at-grid3\">");
            sb.Append("<label class=\"at-field\" for=\"at-f-name\"><span>Name</span><input type=\"text\" id=\"at-f-name\" data-k=\"name\" maxlength=\"60\" value=\"" + Ui.Esc(t.Name) + "\"" + d + "><em class=\"at-err\" id=\"at-e-name\"></em></label>");
            sb.Append("<label class=\"at-field\" for=\"at-f-role\"><span>Role shown on the website</span><input type=\"text\" id=\"at-f-role\" data-k=\"role\" maxlength=\"60\" value=\"" + Ui.Esc(t.Role) + "\"" + d + "><em class=\"at-err\" id=\"at-e-role\"></em></label>");
            sb.Append("<label class=\"at-field\" for=\"at-f-phone\"><span>Work phone</span><input type=\"tel\" id=\"at-f-phone\" data-k=\"phone\" maxlength=\"24\" value=\"" + Ui.Esc(t.Phone) + "\"" + d + "><em class=\"at-err\" id=\"at-e-phone\"></em></label></div>");
            sb.Append("<div class=\"at-field\"><span>Trades</span><div class=\"at-checks\" id=\"at-trades\">" + string.Concat(site.Trades.Select(x => "<label class=\"at-check\"><input type=\"checkbox\" value=\"" + Ui.Esc(x.Key) + "\"" + (t.Trades.Contains(x.Key) ? " checked" : string.Empty) + d + ">" + Ui.Esc(x.Name) + "</label>")) + "</div><em class=\"at-err\" id=\"at-e-trades\"></em></div>");
            sb.Append("<div class=\"at-field\"><span>Working days</span><div class=\"at-checks\" id=\"at-days\">");
            for (int i = 1; i <= 7; i++)
            {
                sb.Append("<label class=\"at-check\"><input type=\"checkbox\" value=\"" + i + "\"" + (t.Days.Contains(i) ? " checked" : string.Empty) + d + ">" + Work.LongDayNames[i - 1] + "</label>");
            }
            sb.Append("</div><em class=\"at-err\" id=\"at-e-days\"></em></div>");
            sb.Append(PicField(media, t.Photo, "Photo (shown on the dispatch board; add it to the About page under Pages)", ro));
            sb.Append("<label class=\"at-check\"><input type=\"checkbox\" data-k=\"active\"" + (t.Active ? " checked" : string.Empty) + d + "> Active: can be booked for jobs</label>");
            sb.Append("<div class=\"at-acts\">" + (ro ? string.Empty : "<button type=\"submit\" class=\"at-btn\">Save</button>") + "<button type=\"button\" class=\"at-btn at-btn-ghost\" onclick=\"AdminTechsJs.closeModal()\">" + (ro ? "Close" : "Cancel") + "</button>" + (!ro && t.Id.Length > 0 ? "<button type=\"button\" class=\"at-btn at-btn-danger at-push\" onclick=\"AdminTechsJs.drop('" + Ui.Js(t.Id) + "','" + Ui.Js(t.Name) + "')\">Delete</button>" : string.Empty) + "</div></form>");
            response.SetElementContents("at-mbody", sb.ToString());
            response.ExecuteScript("AdminTechsJs.openModal('" + Ui.Js(t.Id.Length == 0 ? "Add technician" : t.Name) + "');");
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
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "save technician", id, "You can look at technicians but not change them.");
            }
            string name = Ui.Clip(GetDataValue("name"), 200);
            string role = Ui.Clip(GetDataValue("role"), 200);
            string phone = Ui.Clip(GetDataValue("phone"), 40);
            string photo = Ui.Clip(GetDataValue("photo"), 300);
            bool active = GetDataValue("active") == "1";
            SiteData check = await Store.LoadSite();
            List<string> trades = (GetDataValue("trades") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().Where(k => check.Trades.Any(t => t.Key == k)).ToList();
            List<int> days = (GetDataValue("days") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0).Where(v => v >= 1 && v <= 7).Distinct().OrderBy(v => v).ToList();
            List<MediaItem> media = await Media.Load();
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                { "name", name.Length < 2 || name.Length > 60 ? "Enter a name of 2 to 60 characters." : check.Techs.Any(t => t.Id != id && t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ? "Another technician has this name." : string.Empty },
                { "role", role.Length > 60 ? "Keep it under 60 characters." : string.Empty },
                { "phone", phone.Length > 0 && !Work.IsPhone(phone) ? "Enter a phone number with area code, or leave it empty." : string.Empty },
                { "trades", trades.Count == 0 ? "Pick at least one trade." : string.Empty },
                { "days", days.Count == 0 ? "Pick at least one working day." : string.Empty },
                { "photo", photo.Length > 0 && Media.Find(media, photo, "photo") == null ? "That photo is no longer in the media library. Choose it again." : string.Empty }
            };
            string error = string.Empty;
            if (e.Values.All(v => v.Length == 0) && id.Length > 0)
            {
                List<JobRec> jobs = await Store.Jobs();
                string today = Work.Today();
                List<JobRec> future = jobs.Where(j => j.TechId == id && Work.Open(j.Status) && string.CompareOrdinal(j.Date, today) >= 0).ToList();
                if (!active && future.Count > 0)
                {
                    error = "This technician still has " + Ui.Count(future.Count, "booked job", "booked jobs") + ". Move them to someone else first.";
                }
                else
                {
                    int clash = future.Count(j => Work.ParseDay(j.Date) is DateTime d && !days.Contains(Work.DayIndex(d)));
                    if (clash > 0)
                    {
                        e["days"] = Ui.Count(clash, "booked job falls", "booked jobs fall") + " on a day you removed. Move them first.";
                    }
                    else
                    {
                        int other = future.Count(j => j.Trade.Length > 0 && !trades.Contains(j.Trade));
                        if (other > 0)
                        {
                            e["trades"] = Ui.Count(other, "booked job needs", "booked jobs need") + " a trade you removed. Move them first.";
                        }
                    }
                }
            }
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("at-e-" + kv.Key, Ui.Esc(kv.Value));
            }
            if (error.Length > 0)
            {
                response.ExecuteScript("AdminTechsJs.toast('" + Ui.Js(error) + "');");
                return response;
            }
            if (e.Values.Any(v => v.Length > 0))
            {
                return response;
            }
            SiteData site;
            using (await Store.Hold())
            {
                site = await Store.LoadSite();
                Technician? t = id.Length == 0 ? null : site.Techs.FirstOrDefault(x => x.Id == id);
                if (t == null)
                {
                    t = new Technician { Id = "t-" + Access.NewToken(4) };
                    site.Techs.Add(t);
                }
                t.Name = name;
                t.Role = role;
                t.Phone = phone;
                t.Photo = photo;
                t.Trades = trades;
                t.Days = days;
                t.Active = active;
                await Store.SaveSite(site);
            }
            await Store.Log(c.U.Id, id.Length == 0 ? "add technician" : "edit technician", name + " · " + Work.DaysText(days), true);
            response.SetElementContents("at-list", ListHtml(site, await Store.Jobs(), c));
            response.ExecuteScript("AdminTechsJs.saved('Saved');");
            return response;
        }

        public async Task<ApiResponse> Drop()
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
                return await Refuse(response, c, "delete technician", id, "You can look at technicians but not change them.");
            }
            List<JobRec> jobs = await Store.Jobs();
            List<Staff> staff = await Store.LoadStaff();
            if (jobs.Any(j => j.TechId == id))
            {
                response.ExecuteScript("AdminTechsJs.toast('This technician has jobs on record. Untick Active instead, so the history stays.');");
                return response;
            }
            if (staff.Any(s => s.TechId == id))
            {
                response.ExecuteScript("AdminTechsJs.toast('A staff login is linked to this technician. Change it under Users and rights first.');");
                return response;
            }
            string name = string.Empty;
            SiteData site;
            using (await Store.Hold())
            {
                site = await Store.LoadSite();
                Technician? t = site.Techs.FirstOrDefault(x => x.Id == id);
                if (t != null)
                {
                    name = t.Name;
                    site.Techs.Remove(t);
                    await Store.SaveSite(site);
                }
            }
            if (name.Length > 0)
            {
                await Store.Log(c.U.Id, "delete technician", name, true);
            }
            response.SetElementContents("at-list", ListHtml(site, jobs, c));
            response.ExecuteScript("AdminTechsJs.saved('Technician deleted');");
            return response;
        }
    }
}
