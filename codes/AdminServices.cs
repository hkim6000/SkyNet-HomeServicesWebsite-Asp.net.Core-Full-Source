using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HomeServices.Core;
using HomeServices.Models;
using SkyNet;

namespace HomeServices.codes
{
    public class AdminServices : WebPage
    {
        private const string AreaKey = "services";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Services and prices | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminServicesJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminServicesJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("as-brand", Ui.Esc(site.Brand));
            response.SetElementContents("as-menu", await Auth.Menu("as", "AdminServices", c));
            response.SetElementContents("as-who", Auth.Who("as", c));
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"as-panel\"><h1>No access</h1><p class=\"as-hint\">You do not have access to " + what + ". Ask a manager if you need it.</p></div>";
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
            response.ExecuteScript("AdminServicesJs.expired();");
            return response;
        }

        private static string PickGrid(List<MediaItem> media, string term)
        {
            List<MediaItem> rows = media.Where(m => m.Kind == "photo" && (term.Length == 0 || Ui.Has(m.Name, term) || Ui.Has(m.Alt, term) || Ui.Has(m.Caption, term))).OrderByDescending(m => m.Added).Take(300).ToList();
            if (rows.Count == 0)
            {
                return "<p class=\"as-hint\">No photos match. Upload one above, or add it in the Media library.</p>";
            }
            return string.Concat(rows.Select(m => "<button type=\"button\" class=\"as-pki\" data-src=\"" + Ui.Esc(m.Path) + "\" data-thumb=\"" + Ui.Esc(m.Thumb.Length > 0 ? m.Thumb : m.Path) + "\" onclick=\"AdminServicesJs.pkPick(this)\" title=\"" + Ui.Esc(m.Name) + "\"><img src=\"" + Ui.Esc(m.Thumb.Length > 0 ? m.Thumb : m.Path) + "\" alt=\"\" loading=\"lazy\"><span>" + Ui.Esc(m.Name) + "</span></button>"));
        }

        private static string PicField(List<MediaItem> media, string path, string label, bool ro)
        {
            MediaItem? m = media.FirstOrDefault(x => x.Path == path);
            string thumb = m != null && m.Thumb.Length > 0 ? m.Thumb : path;
            return "<div class=\"as-field\"><span>" + label + "</span><div class=\"as-pic\"><img src=\"" + Ui.Esc(thumb) + "\" alt=\"\"" + (path.Length == 0 ? " hidden" : string.Empty) + "><input type=\"hidden\" data-k=\"photo\" value=\"" + Ui.Esc(path) + "\">" + (ro ? string.Empty : "<button type=\"button\" class=\"as-btn as-btn-ghost as-btn-sm\" onclick=\"AdminServicesJs.pkOpen(this)\">Choose photo</button><button type=\"button\" class=\"as-btn as-btn-ghost as-btn-sm\" onclick=\"AdminServicesJs.pkNone(this)\">Remove</button>") + "</div><em class=\"as-err\" id=\"as-e-photo\"></em></div>";
        }

        public async Task<ApiResponse> Pick()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null || !Access.Can(c.U, AreaKey, 'E'))
            {
                return response;
            }
            response.SetElementContents("as-pkgrid", PickGrid(await Media.Load(), Ui.Clip(GetDataValue("pterm"), 40)));
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
                response.ExecuteScript("AdminServicesJs.pkUploaded('You are not allowed to upload to the media library.');");
                return await Refuse(response, c, "upload media", string.Empty, "You are not allowed to upload to the media library.");
            }
            SiteData site = await Store.LoadSite();
            List<Incoming> files = (await Media.Received()).Where(f => Path.GetExtension(f.Name).ToLowerInvariant() != ".mp4").ToList();
            if (files.Count == 0)
            {
                response.ExecuteScript("AdminServicesJs.pkUploaded('Choose a photo first.');");
                return response;
            }
            (int added, List<string> errors) = await Media.AddAll(files, site.MaxVideoMb, c.U.Name);
            if (added > 0)
            {
                await Store.Log(c.U.Id, "upload media", Ui.Count(added, "file", "files"), true);
            }
            response.SetElementContents("as-pkgrid", PickGrid(await Media.Load(), string.Empty));
            response.ExecuteScript("AdminServicesJs.pkUploaded('" + Ui.Js(((added > 0 ? Ui.Count(added, "photo", "photos") + " added to the library. " : string.Empty) + string.Join(" ", errors)).Trim()) + "');");
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
                response.SetElementContents("as-main", NoAccess("services and prices"));
                return response;
            }
            response.SetElementContents("as-main", "<div class=\"as-ph\"><div><h1>Services and prices</h1><p>The trades and the services shown on the Services page, with their starting prices. Staff can add them to quotes from the price list.</p></div>" + (Access.Can(c.U, AreaKey, 'E') ? "<button type=\"button\" class=\"as-btn\" onclick=\"AdminServicesJs.edit('')\">Add service</button>" : string.Empty) + "</div><div id=\"as-list\">" + ListHtml(site, c) + "</div>");
            return response;
        }

        private static string ListHtml(SiteData site, Ctx c)
        {
            bool canE = Access.Can(c.U, AreaKey, 'E');
            StringBuilder sb = new StringBuilder();
            foreach (Trade t in site.Trades)
            {
                List<ServiceItem> list = site.Services.Where(s => s.Trade == t.Key).OrderBy(s => s.From).ToList();
                sb.Append("<section class=\"as-panel as-trade\" style=\"--as-tc:" + Work.TradeColor(site, t.Key) + "\"><div class=\"as-trade-h\">" + (t.Image.Length > 0 ? "<img src=\"" + Ui.Esc(Media.Thumb(t.Image)) + "\" alt=\"\">" : string.Empty) + "<div><h2>" + Ui.Esc(t.Name) + "</h2><p class=\"as-hint\">" + Ui.Esc(t.Blurb) + "</p></div><button type=\"button\" class=\"as-btn as-btn-ghost as-btn-sm\" onclick=\"AdminServicesJs.trade('" + Ui.Js(t.Key) + "')\">" + (canE ? "Edit trade" : "View") + "</button></div>");
                if (list.Count == 0)
                {
                    sb.Append("<p class=\"as-hint\">No services in this trade yet.</p></section>");
                    continue;
                }
                sb.Append("<div class=\"as-wrapt\"><table class=\"as-table\"><thead><tr><th>Service</th><th>Typical time</th><th class=\"as-num\">From</th><th>Shown</th></tr></thead><tbody>");
                foreach (ServiceItem s in list)
                {
                    sb.Append("<tr class=\"as-row\" tabindex=\"0\" onclick=\"AdminServicesJs.edit('" + Ui.Js(s.Id) + "')\" onkeydown=\"if(event.key==='Enter'){AdminServicesJs.edit('" + Ui.Js(s.Id) + "')}\"><td data-l=\"Service\"><b>" + Ui.Esc(s.Name) + "</b><small>" + Ui.Esc(s.Text) + "</small></td><td data-l=\"Time\">" + Ui.Esc(s.Time) + "</td><td data-l=\"From\" class=\"as-num\">" + Ui.Money(s.From) + "<small>" + Ui.Esc(s.Unit) + "</small></td><td data-l=\"Shown\">" + (s.Active ? Work.Pill("as", "go", "On the website") : Work.Pill("as", "off", "Hidden")) + "</td></tr>");
                }
                sb.Append("</tbody></table></div></section>");
            }
            return sb.ToString();
        }

        private static string Input(string key, string label, string value, int max, bool ro, string type = "text", string extra = "")
        {
            return "<label class=\"as-field\" for=\"as-f-" + key + "\"><span>" + label + "</span><input type=\"" + type + "\" id=\"as-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + (ro ? " disabled" : string.Empty) + "><em class=\"as-err\" id=\"as-e-" + key + "\"></em></label>";
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
                return await Refuse(response, c, "open service", id, "You can look at services but not add them.");
            }
            SiteData site = await Store.LoadSite();
            ServiceItem? s = id.Length == 0 ? new ServiceItem { Trade = site.Trades.FirstOrDefault()?.Key ?? string.Empty, Unit = "per visit" } : site.Services.FirstOrDefault(x => x.Id == id);
            if (s == null)
            {
                response.ExecuteScript("AdminServicesJs.toast('That service was not found.');");
                return response;
            }
            bool ro = !Access.Can(c.U, AreaKey, 'E');
            StringBuilder sb = new StringBuilder("<form class=\"as-stack\" id=\"as-form\" onsubmit=\"return AdminServicesJs.save('" + Ui.Js(s.Id) + "')\" novalidate><div class=\"as-grid2\">");
            sb.Append(Input("name", "Name", s.Name, 60, ro));
            sb.Append("<label class=\"as-field\" for=\"as-f-trade\"><span>Trade</span><select id=\"as-f-trade\" data-k=\"trade\"" + (ro ? " disabled" : string.Empty) + ">" + string.Concat(site.Trades.Select(t => "<option value=\"" + Ui.Esc(t.Key) + "\"" + (t.Key == s.Trade ? " selected" : string.Empty) + ">" + Ui.Esc(t.Name) + "</option>")) + "</select><em class=\"as-err\" id=\"as-e-trade\"></em></label>");
            sb.Append(Input("from", "Starting price ($)", s.From.ToString("0.00", CultureInfo.InvariantCulture), 10, ro, "number", " min=\"0\" step=\"0.01\""));
            sb.Append(Input("unit", "Price unit, like per visit", s.Unit, 30, ro));
            sb.Append(Input("time", "Typical time", s.Time, 30, ro) + "</div>");
            sb.Append("<label class=\"as-field\" for=\"as-f-text\"><span>What is included</span><textarea id=\"as-f-text\" data-k=\"text\" rows=\"3\" maxlength=\"300\"" + (ro ? " disabled" : string.Empty) + ">" + Ui.Esc(s.Text) + "</textarea><em class=\"as-err\" id=\"as-e-text\"></em></label>");
            sb.Append("<label class=\"as-check\"><input type=\"checkbox\" data-k=\"active\"" + (s.Active ? " checked" : string.Empty) + (ro ? " disabled" : string.Empty) + "> Show on the website and in the quote form</label>");
            sb.Append("<div class=\"as-acts\">" + (ro ? string.Empty : "<button type=\"submit\" class=\"as-btn\">Save</button>") + "<button type=\"button\" class=\"as-btn as-btn-ghost\" onclick=\"AdminServicesJs.closeModal()\">" + (ro ? "Close" : "Cancel") + "</button>" + (!ro && s.Id.Length > 0 ? "<button type=\"button\" class=\"as-btn as-btn-danger as-push\" onclick=\"AdminServicesJs.drop('" + Ui.Js(s.Id) + "','" + Ui.Js(s.Name) + "')\">Delete</button>" : string.Empty) + "</div></form>");
            response.SetElementContents("as-mbody", sb.ToString());
            response.ExecuteScript("AdminServicesJs.openModal('" + Ui.Js(s.Id.Length == 0 ? "Add service" : s.Name) + "');");
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
                return await Refuse(response, c, "save service", id, "You can look at services but not change them.");
            }
            string name = Ui.Clip(GetDataValue("name"), 200);
            string trade = Ui.Clip(GetDataValue("trade"), 40);
            decimal? from = Work.Amount(GetDataValue("from"), 0, 100000);
            string unit = Ui.Clip(GetDataValue("unit"), 100);
            string time = Ui.Clip(GetDataValue("time"), 100);
            string text = Ui.Clip(GetDataValue("text"), 1000);
            bool active = GetDataValue("active") == "1";
            SiteData check = await Store.LoadSite();
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                { "name", name.Length < 2 || name.Length > 60 ? "Enter a name of 2 to 60 characters." : check.Services.Any(s => s.Id != id && s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ? "Another service has this name." : string.Empty },
                { "trade", check.Trades.Any(t => t.Key == trade) ? string.Empty : "Pick a trade." },
                { "from", from == null ? "Enter a price from 0 to 100,000." : string.Empty },
                { "unit", unit.Length > 30 ? "Keep it under 30 characters." : string.Empty },
                { "time", time.Length > 30 ? "Keep it under 30 characters." : string.Empty },
                { "text", text.Length > 300 ? "Keep it under 300 characters." : string.Empty }
            };
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("as-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0) || from == null)
            {
                return response;
            }
            SiteData site;
            using (await Store.Hold())
            {
                site = await Store.LoadSite();
                ServiceItem? s = id.Length == 0 ? null : site.Services.FirstOrDefault(x => x.Id == id);
                if (s == null)
                {
                    s = new ServiceItem { Id = "s-" + Access.NewToken(4) };
                    site.Services.Add(s);
                }
                s.Name = name;
                s.Trade = trade;
                s.From = from.Value;
                s.Unit = unit;
                s.Time = time;
                s.Text = text;
                s.Active = active;
                await Store.SaveSite(site);
            }
            await Store.Log(c.U.Id, id.Length == 0 ? "add service" : "edit service", name + " · from " + Ui.Money(from.Value), true);
            response.SetElementContents("as-list", ListHtml(site, c));
            response.ExecuteScript("AdminServicesJs.saved('Saved');");
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
                return await Refuse(response, c, "delete service", id, "You can look at services but not change them.");
            }
            if ((await Store.Quotes()).Any(q => q.Services.Contains(id)))
            {
                response.ExecuteScript("AdminServicesJs.toast('Quotes refer to this service. Untick Show on the website instead, so their history stays.');");
                return response;
            }
            string name = string.Empty;
            SiteData site;
            using (await Store.Hold())
            {
                site = await Store.LoadSite();
                ServiceItem? s = site.Services.FirstOrDefault(x => x.Id == id);
                if (s != null)
                {
                    name = s.Name;
                    site.Services.Remove(s);
                    await Store.SaveSite(site);
                }
            }
            if (name.Length > 0)
            {
                await Store.Log(c.U.Id, "delete service", name, true);
            }
            response.SetElementContents("as-list", ListHtml(site, c));
            response.ExecuteScript("AdminServicesJs.saved('Service deleted');");
            return response;
        }

        public async Task<ApiResponse> TradeForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string key = Ui.Clip(GetDataValue("trade"), 40);
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "open trade", key, "You do not have access to services.");
            }
            SiteData site = await Store.LoadSite();
            Trade? t = site.Trades.FirstOrDefault(x => x.Key == key);
            if (t == null)
            {
                return response;
            }
            bool ro = !Access.Can(c.U, AreaKey, 'E');
            List<MediaItem> media = await Media.Load();
            StringBuilder sb = new StringBuilder("<form class=\"as-stack\" id=\"as-form\" onsubmit=\"return AdminServicesJs.saveTrade('" + Ui.Js(t.Key) + "')\" novalidate><div class=\"as-grid2\">");
            sb.Append(Input("name", "Name", t.Name, 40, ro));
            sb.Append(Input("color", "Colour on cards and the dispatch board", t.Color, 7, ro, "text", " placeholder=\"#2F6F8F\"") + "</div>");
            sb.Append("<label class=\"as-field\" for=\"as-f-blurb\"><span>Short description</span><textarea id=\"as-f-blurb\" data-k=\"blurb\" rows=\"2\" maxlength=\"160\"" + (ro ? " disabled" : string.Empty) + ">" + Ui.Esc(t.Blurb) + "</textarea><em class=\"as-err\" id=\"as-e-blurb\"></em></label>");
            sb.Append(PicField(media, t.Image, "Picture on the Home and Services pages", ro));
            sb.Append("<div class=\"as-acts\">" + (ro ? string.Empty : "<button type=\"submit\" class=\"as-btn\">Save</button>") + "<button type=\"button\" class=\"as-btn as-btn-ghost\" onclick=\"AdminServicesJs.closeModal()\">" + (ro ? "Close" : "Cancel") + "</button></div></form>");
            response.SetElementContents("as-mbody", sb.ToString());
            response.ExecuteScript("AdminServicesJs.openModal('" + Ui.Js("Trade: " + t.Name) + "');");
            return response;
        }

        public async Task<ApiResponse> SaveTrade()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string key = Ui.Clip(GetDataValue("trade"), 40);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "edit trade", key, "You can look at services but not change them.");
            }
            string name = Ui.Clip(GetDataValue("name"), 100);
            string color = Ui.Clip(GetDataValue("color"), 20);
            string blurb = Ui.Clip(GetDataValue("blurb"), 400);
            string photo = Ui.Clip(GetDataValue("photo"), 300);
            List<MediaItem> media = await Media.Load();
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                { "name", name.Length < 2 || name.Length > 40 ? "Enter a name of 2 to 40 characters." : string.Empty },
                { "color", !Regex.IsMatch(color, "^#[0-9A-Fa-f]{6}$") ? "Use a colour code like #2F6F8F." : string.Empty },
                { "blurb", blurb.Length > 160 ? "Keep it under 160 characters." : string.Empty },
                { "photo", photo.Length > 0 && Media.Find(media, photo, "photo") == null ? "That photo is no longer in the media library." : string.Empty }
            };
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("as-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0))
            {
                return response;
            }
            SiteData site;
            using (await Store.Hold())
            {
                site = await Store.LoadSite();
                Trade? t = site.Trades.FirstOrDefault(x => x.Key == key);
                if (t != null)
                {
                    t.Name = name;
                    t.Color = color.ToUpperInvariant();
                    t.Blurb = blurb;
                    t.Image = photo;
                    await Store.SaveSite(site);
                }
            }
            await Store.Log(c.U.Id, "edit trade", name, true);
            response.SetElementContents("as-list", ListHtml(site, c));
            response.ExecuteScript("AdminServicesJs.saved('Trade saved');");
            return response;
        }
    }
}
