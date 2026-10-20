using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HomeServices.Core;
using HomeServices.Models;
using SkyNet;

namespace HomeServices.codes
{
    public class AdminZips : WebPage
    {
        private const string AreaKey = "zips";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Service area | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminZipsJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminZipsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("az-brand", Ui.Esc(site.Brand));
            response.SetElementContents("az-menu", await Auth.Menu("az", "AdminZips", c));
            response.SetElementContents("az-who", Auth.Who("az", c));
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"az-panel\"><h1>No access</h1><p class=\"az-hint\">You do not have access to " + what + ". Ask a manager if you need it.</p></div>";
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
            response.ExecuteScript("AdminZipsJs.expired();");
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
                response.SetElementContents("az-main", NoAccess("the service area"));
                return response;
            }
            bool canE = Access.Can(c.U, AreaKey, 'E');
            response.SetElementContents("az-main", "<div class=\"az-ph\"><div><h1>Service area</h1><p>The ZIP codes we cover, grouped in zones. Each zone has a trip fee that is added to quotes for that area.</p></div>" + (canE ? "<div class=\"az-acts\"><button type=\"button\" class=\"az-btn az-btn-ghost\" onclick=\"AdminZipsJs.zone('')\">Add zone</button><button type=\"button\" class=\"az-btn\" onclick=\"AdminZipsJs.zip('')\">Add ZIP code</button></div>" : string.Empty) + "</div><div id=\"az-zones\">" + ZonesHtml(site) + "</div><div class=\"az-tools az-mt\"><input type=\"search\" id=\"az-term\" placeholder=\"Search ZIP or town\" maxlength=\"40\" oninput=\"AdminZipsJs.search()\" aria-label=\"Search ZIP codes\"></div><div id=\"az-list\">" + ZipsHtml(site, string.Empty) + "</div>");
            return response;
        }

        private static string ZonesHtml(SiteData site)
        {
            StringBuilder sb = new StringBuilder("<div class=\"az-zones\">");
            foreach (Zone z in site.Zones)
            {
                int n = site.Zips.Count(x => x.Zone == z.Key);
                sb.Append("<button type=\"button\" class=\"az-zone\" onclick=\"AdminZipsJs.zone('" + Ui.Js(z.Key) + "')\"><b>" + Ui.Esc(z.Name) + "</b><span>" + (z.Fee > 0 ? Ui.Money(z.Fee) + " trip fee" : "No trip fee") + "</span><small>" + Ui.Count(n, "ZIP code", "ZIP codes") + "</small></button>");
            }
            return sb.Append("</div>").ToString();
        }

        private static string ZipsHtml(SiteData site, string term)
        {
            List<ZipArea> rows = site.Zips.Where(z => term.Length == 0 || z.Zip.Contains(term) || z.Town.Contains(term, StringComparison.OrdinalIgnoreCase)).OrderBy(z => z.Zip).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"az-panel az-empty\">" + (site.Zips.Count == 0 ? "No ZIP codes yet. Add the first one." : "No ZIP codes match.") + "</div>";
            }
            StringBuilder sb = new StringBuilder("<div class=\"az-wrapt\"><table class=\"az-table\"><thead><tr><th>ZIP code</th><th>Town</th><th>Zone</th><th class=\"az-num\">Trip fee</th></tr></thead><tbody>");
            foreach (ZipArea z in rows)
            {
                Zone? zone = site.Zones.FirstOrDefault(x => x.Key == z.Zone);
                sb.Append("<tr class=\"az-row\" tabindex=\"0\" onclick=\"AdminZipsJs.zip('" + z.Zip + "')\" onkeydown=\"if(event.key==='Enter'){AdminZipsJs.zip('" + z.Zip + "')}\"><td data-l=\"ZIP\"><b>" + z.Zip + "</b></td><td data-l=\"Town\">" + Ui.Esc(z.Town) + "</td><td data-l=\"Zone\">" + Ui.Esc(zone?.Name ?? "Missing zone") + "</td><td data-l=\"Fee\" class=\"az-num\">" + Ui.Money(zone?.Fee ?? 0) + "</td></tr>");
            }
            return sb.Append("</tbody></table></div>").ToString();
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
                return await Refuse(response, c, "list ZIP codes", string.Empty, "You do not have access to the service area.");
            }
            response.SetElementContents("az-list", ZipsHtml(await Store.LoadSite(), Ui.Clip(GetDataValue("term"), 40)));
            return response;
        }

        private static string Acts(bool ro, string drop)
        {
            return "<div class=\"az-acts\">" + (ro ? string.Empty : "<button type=\"submit\" class=\"az-btn\">Save</button>") + "<button type=\"button\" class=\"az-btn az-btn-ghost\" onclick=\"AdminZipsJs.closeModal()\">" + (ro ? "Close" : "Cancel") + "</button>" + (ro ? string.Empty : drop) + "</div>";
        }

        public async Task<ApiResponse> ZipForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string code = Ui.Clip(GetDataValue("code"), 5);
            if (!Access.Can(c.U, AreaKey, 'V') || (code.Length == 0 && !Access.Can(c.U, AreaKey, 'E')))
            {
                return await Refuse(response, c, "open ZIP code", code, "You can look at the service area but not change it.");
            }
            SiteData site = await Store.LoadSite();
            ZipArea z = site.Zips.FirstOrDefault(x => x.Zip == code) ?? new ZipArea { Zone = site.Zones.FirstOrDefault()?.Key ?? string.Empty };
            bool ro = !Access.Can(c.U, AreaKey, 'E');
            string d = ro ? " disabled" : string.Empty;
            StringBuilder sb = new StringBuilder("<form class=\"az-stack\" id=\"az-form\" onsubmit=\"return AdminZipsJs.saveZip('" + Ui.Js(z.Zip) + "')\" novalidate><div class=\"az-grid3\">");
            sb.Append("<label class=\"az-field\" for=\"az-f-zip\"><span>ZIP code</span><input type=\"text\" id=\"az-f-zip\" data-k=\"zipcode\" maxlength=\"5\" inputmode=\"numeric\" value=\"" + Ui.Esc(z.Zip) + "\"" + d + "><em class=\"az-err\" id=\"az-e-zip\"></em></label>");
            sb.Append("<label class=\"az-field\" for=\"az-f-town\"><span>Town</span><input type=\"text\" id=\"az-f-town\" data-k=\"town\" maxlength=\"40\" value=\"" + Ui.Esc(z.Town) + "\"" + d + "><em class=\"az-err\" id=\"az-e-town\"></em></label>");
            sb.Append("<label class=\"az-field\" for=\"az-f-zone\"><span>Zone</span><select id=\"az-f-zone\" data-k=\"zone\"" + d + ">" + string.Concat(site.Zones.Select(x => "<option value=\"" + Ui.Esc(x.Key) + "\"" + (x.Key == z.Zone ? " selected" : string.Empty) + ">" + Ui.Esc(x.Name) + " (" + Ui.Money(x.Fee) + ")</option>")) + "</select><em class=\"az-err\" id=\"az-e-zone\"></em></label></div>");
            sb.Append(Acts(ro, z.Zip.Length > 0 ? "<button type=\"button\" class=\"az-btn az-btn-danger az-push\" onclick=\"AdminZipsJs.dropZip('" + z.Zip + "')\">Delete</button>" : string.Empty) + "</form>");
            response.SetElementContents("az-mbody", sb.ToString());
            response.ExecuteScript("AdminZipsJs.openModal('" + (z.Zip.Length == 0 ? "Add ZIP code" : "ZIP code " + z.Zip) + "');");
            return response;
        }

        public async Task<ApiResponse> SaveZip()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string old = Ui.Clip(GetDataValue("code"), 5);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "save ZIP code", old, "You can look at the service area but not change it.");
            }
            string zip = Ui.Clip(GetDataValue("zipcode"), 10);
            string town = Ui.Clip(GetDataValue("town"), 100);
            string zone = Ui.Clip(GetDataValue("zone"), 40);
            SiteData check = await Store.LoadSite();
            string eZip = !Regex.IsMatch(zip, "^[0-9]{5}$") ? "Enter 5 digits." : zip != old && check.Zips.Any(x => x.Zip == zip) ? zip + " is already on the list." : string.Empty;
            string eTown = town.Length < 2 || town.Length > 40 ? "Enter the town, 2 to 40 characters." : string.Empty;
            string eZone = check.Zones.Any(z => z.Key == zone) ? string.Empty : "Pick a zone.";
            response.SetElementContents("az-e-zip", eZip);
            response.SetElementContents("az-e-town", eTown);
            response.SetElementContents("az-e-zone", eZone);
            if (eZip + eTown + eZone != string.Empty)
            {
                return response;
            }
            SiteData site;
            using (await Store.Hold())
            {
                site = await Store.LoadSite();
                ZipArea? z = old.Length == 0 ? null : site.Zips.FirstOrDefault(x => x.Zip == old);
                if (z == null)
                {
                    z = new ZipArea();
                    site.Zips.Add(z);
                }
                z.Zip = zip;
                z.Town = town;
                z.Zone = zone;
                await Store.SaveSite(site);
            }
            await Store.Log(c.U.Id, old.Length == 0 ? "add ZIP code" : "edit ZIP code", zip + " · " + town, true);
            response.SetElementContents("az-list", ZipsHtml(site, string.Empty));
            response.SetElementContents("az-zones", ZonesHtml(site));
            response.ExecuteScript("AdminZipsJs.saved('Saved');");
            return response;
        }

        public async Task<ApiResponse> DropZip()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string code = Ui.Clip(GetDataValue("code"), 5);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "delete ZIP code", code, "You can look at the service area but not change it.");
            }
            SiteData site;
            bool gone = false;
            using (await Store.Hold())
            {
                site = await Store.LoadSite();
                gone = site.Zips.RemoveAll(x => x.Zip == code) > 0;
                if (gone)
                {
                    await Store.SaveSite(site);
                }
            }
            if (gone)
            {
                await Store.Log(c.U.Id, "delete ZIP code", code, true);
            }
            response.SetElementContents("az-list", ZipsHtml(site, string.Empty));
            response.SetElementContents("az-zones", ZonesHtml(site));
            response.ExecuteScript("AdminZipsJs.saved('ZIP code removed');");
            return response;
        }

        public async Task<ApiResponse> ZoneForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string key = Ui.Clip(GetDataValue("zone"), 40);
            if (!Access.Can(c.U, AreaKey, 'V') || (key.Length == 0 && !Access.Can(c.U, AreaKey, 'E')))
            {
                return await Refuse(response, c, "open zone", key, "You can look at the service area but not change it.");
            }
            SiteData site = await Store.LoadSite();
            Zone z = site.Zones.FirstOrDefault(x => x.Key == key) ?? new Zone();
            bool ro = !Access.Can(c.U, AreaKey, 'E');
            string d = ro ? " disabled" : string.Empty;
            StringBuilder sb = new StringBuilder("<form class=\"az-stack\" id=\"az-form\" onsubmit=\"return AdminZipsJs.saveZone('" + Ui.Js(z.Key) + "')\" novalidate><div class=\"az-grid2\">");
            sb.Append("<label class=\"az-field\" for=\"az-f-name\"><span>Zone name</span><input type=\"text\" id=\"az-f-name\" data-k=\"name\" maxlength=\"40\" value=\"" + Ui.Esc(z.Name) + "\"" + d + "><em class=\"az-err\" id=\"az-e-name\"></em></label>");
            sb.Append("<label class=\"az-field\" for=\"az-f-fee\"><span>Trip fee per visit ($)</span><input type=\"number\" id=\"az-f-fee\" data-k=\"fee\" min=\"0\" step=\"0.01\" value=\"" + z.Fee.ToString("0.00", CultureInfo.InvariantCulture) + "\"" + d + "><em class=\"az-err\" id=\"az-e-fee\"></em></label></div><p class=\"az-hint\">A new fee applies to quotes written from now on. Quotes already sent keep their fee.</p>");
            sb.Append(Acts(ro, z.Key.Length > 0 ? "<button type=\"button\" class=\"az-btn az-btn-danger az-push\" onclick=\"AdminZipsJs.dropZone('" + Ui.Js(z.Key) + "')\">Delete zone</button>" : string.Empty) + "</form>");
            response.SetElementContents("az-mbody", sb.ToString());
            response.ExecuteScript("AdminZipsJs.openModal('" + Ui.Js(z.Key.Length == 0 ? "Add zone" : z.Name) + "');");
            return response;
        }

        public async Task<ApiResponse> SaveZone()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string key = Ui.Clip(GetDataValue("zone"), 40);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "save zone", key, "You can look at the service area but not change it.");
            }
            string name = Ui.Clip(GetDataValue("name"), 100);
            decimal? fee = Work.Amount(GetDataValue("fee"), 0, 1000);
            SiteData check = await Store.LoadSite();
            string eName = name.Length < 2 || name.Length > 40 ? "Enter a name of 2 to 40 characters." : check.Zones.Any(z => z.Key != key && z.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ? "Another zone has this name." : string.Empty;
            string eFee = fee == null ? "Enter a fee from 0 to 1,000." : string.Empty;
            response.SetElementContents("az-e-name", eName);
            response.SetElementContents("az-e-fee", eFee);
            if (eName + eFee != string.Empty || fee == null)
            {
                return response;
            }
            SiteData site;
            using (await Store.Hold())
            {
                site = await Store.LoadSite();
                Zone? z = key.Length == 0 ? null : site.Zones.FirstOrDefault(x => x.Key == key);
                if (z == null)
                {
                    z = new Zone { Key = "z-" + Access.NewToken(3) };
                    site.Zones.Add(z);
                }
                z.Name = name;
                z.Fee = fee.Value;
                await Store.SaveSite(site);
            }
            await Store.Log(c.U.Id, key.Length == 0 ? "add zone" : "edit zone", name + " · " + Ui.Money(fee.Value), true);
            response.SetElementContents("az-list", ZipsHtml(site, string.Empty));
            response.SetElementContents("az-zones", ZonesHtml(site));
            response.ExecuteScript("AdminZipsJs.saved('Zone saved');");
            return response;
        }

        public async Task<ApiResponse> DropZone()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string key = Ui.Clip(GetDataValue("zone"), 40);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "delete zone", key, "You can look at the service area but not change it.");
            }
            SiteData site;
            string error = string.Empty;
            string name = string.Empty;
            using (await Store.Hold())
            {
                site = await Store.LoadSite();
                Zone? z = site.Zones.FirstOrDefault(x => x.Key == key);
                if (z != null && site.Zips.Any(x => x.Zone == key))
                {
                    error = "Move or delete the ZIP codes in this zone first.";
                }
                else if (z != null)
                {
                    name = z.Name;
                    site.Zones.Remove(z);
                    await Store.SaveSite(site);
                }
            }
            if (error.Length > 0)
            {
                response.ExecuteScript("AdminZipsJs.toast('" + Ui.Js(error) + "');");
                return response;
            }
            if (name.Length > 0)
            {
                await Store.Log(c.U.Id, "delete zone", name, true);
            }
            response.SetElementContents("az-zones", ZonesHtml(site));
            response.ExecuteScript("AdminZipsJs.saved('Zone deleted');");
            return response;
        }
    }
}
