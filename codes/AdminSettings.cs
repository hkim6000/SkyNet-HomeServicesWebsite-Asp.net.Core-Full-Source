using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HomeServices.Core;
using HomeServices.Models;
using SkyNet;

namespace HomeServices.codes
{
    public class AdminSettings : WebPage
    {
        private const string AreaKey = "settings";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Settings | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminSettingsJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminSettingsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("st-brand", Ui.Esc(site.Brand));
            response.SetElementContents("st-menu", await Auth.Menu("st", "AdminSettings", c));
            response.SetElementContents("st-who", Auth.Who("st", c));
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"st-panel\"><h1>No access</h1><p class=\"st-hint\">You do not have access to " + what + ". Ask a manager if you need it.</p></div>";
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
            response.ExecuteScript("AdminSettingsJs.expired();");
            return response;
        }

        private static string Input(string key, string label, string value, int max, string hint = "", string type = "text", string extra = "")
        {
            return "<label class=\"st-field\" for=\"st-f-" + key + "\"><span>" + label + "</span><input type=\"" + type + "\" id=\"st-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">" + (hint.Length > 0 ? "<small>" + hint + "</small>" : string.Empty) + "<em class=\"st-err\" id=\"st-e-" + key + "\"></em></label>";
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
                response.SetElementContents("st-main", NoAccess("settings"));
                return response;
            }
            response.SetElementContents("st-main", FormHtml(site));
            return response;
        }

        private static string FormHtml(SiteData site)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            StringBuilder sb = new StringBuilder("<div class=\"st-ph\"><div><h1>Settings</h1><p>Business details shown on every page, and the rules for quotes and invoices. Run mode (Demo or Production) is set in appConfig/application.cfg.</p></div></div><form class=\"st-stack\" id=\"st-form\" onsubmit=\"return AdminSettingsJs.save()\" novalidate>");
            sb.Append("<section class=\"st-panel st-stack\"><h2>Business</h2><div class=\"st-grid2\">" + Input("brand", "Business name", site.Brand, 60) + Input("phone", "Phone", site.Phone, 24, string.Empty, "tel") + Input("email", "Email", site.Email, 80, string.Empty, "email") + Input("address", "Address", site.Address, 120) + "</div>");
            sb.Append(Input("tagline", "Tagline (footer and search results)", site.Tagline, 160));
            sb.Append("<label class=\"st-field\" for=\"st-f-hours\"><span>Opening hours, one line each</span><textarea id=\"st-f-hours\" data-k=\"hours\" rows=\"3\" maxlength=\"400\">" + Ui.Esc(string.Join("\n", site.Hours)) + "</textarea><em class=\"st-err\" id=\"st-e-hours\"></em></label>");
            sb.Append(Input("licence", "Licence line (footer)", site.Licence, 160) + "</section>");
            sb.Append("<section class=\"st-panel st-stack\"><h2>Quotes and invoices</h2><div class=\"st-grid3\">" + Input("tax", "Tax rate (%)", site.TaxRate.ToString("0.###", inv), 6, "One rate for labour, parts and trip fees. Applies to quotes and invoices written from now on.", "number", " min=\"0\" max=\"30\" step=\"0.001\""));
            sb.Append(Input("quotedays", "Quotes are valid for (days)", site.QuoteDays.ToString(inv), 3, "Counted from the day the quote is sent.", "number", " min=\"1\" max=\"180\""));
            sb.Append(Input("paydays", "Invoices are due after (days)", site.PayDays.ToString(inv), 3, "0 means due on the day it is issued.", "number", " min=\"0\" max=\"90\""));
            sb.Append(Input("prefix", "Invoice number prefix", site.InvoicePrefix, 6, "Numbers look like " + Ui.Esc((site.InvoicePrefix.Length > 0 ? site.InvoicePrefix : "INV") + "-" + DateTime.Today.Year + "-" + Math.Max(1, site.NextInvoice).ToString("0000", inv)) + "."));
            sb.Append(Input("next", "Next invoice number", Math.Max(1, site.NextInvoice).ToString(inv), 6, "Can only go up, so numbers never repeat.", "number", " min=\"1\" max=\"999999\""));
            sb.Append(Input("video", "Largest video upload (MB)", site.MaxVideoMb.ToString(inv), 3, "For the media library, 1 to 100.", "number", " min=\"1\" max=\"100\"") + "</div></section>");
            sb.Append("<div class=\"st-acts\"><button type=\"submit\" class=\"st-btn\">Save settings</button></div></form>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "save settings", string.Empty, "Only managers can change settings.");
            }
            CultureInfo inv = CultureInfo.InvariantCulture;
            SiteData check = await Store.LoadSite();
            string brand = Ui.Clip(GetDataValue("brand"), 200);
            string phone = Ui.Clip(GetDataValue("phone"), 40);
            string email = Ui.Clip(GetDataValue("email"), 200);
            string address = Ui.Clip(GetDataValue("address"), 300);
            string tagline = Ui.Clip(GetDataValue("tagline"), 400);
            string licence = Ui.Clip(GetDataValue("licence"), 400);
            List<string> hours = (GetDataValue("hours") ?? string.Empty).Replace("\r", string.Empty).Split('\n').Select(h => h.Trim()).Where(h => h.Length > 0).ToList();
            decimal? tax = Work.Amount(GetDataValue("tax"), 0, 30);
            int quoteDays = int.TryParse(GetDataValue("quotedays"), NumberStyles.Integer, inv, out int q) ? q : -1;
            int payDays = int.TryParse(GetDataValue("paydays"), NumberStyles.Integer, inv, out int p) ? p : -1;
            string prefix = Ui.Clip(GetDataValue("prefix"), 20).ToUpperInvariant();
            int next = int.TryParse(GetDataValue("next"), NumberStyles.Integer, inv, out int n) ? n : -1;
            int video = int.TryParse(GetDataValue("video"), NumberStyles.Integer, inv, out int v) ? v : -1;
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                { "brand", brand.Length < 2 || brand.Length > 60 ? "Enter a name of 2 to 60 characters." : string.Empty },
                { "phone", !Work.IsPhone(phone) ? "Enter a phone number with area code." : string.Empty },
                { "email", !Work.IsEmail(email) ? "Enter a valid email address." : string.Empty },
                { "address", address.Length < 5 || address.Length > 120 ? "Enter the address." : string.Empty },
                { "tagline", tagline.Length > 160 ? "Keep it under 160 characters." : string.Empty },
                { "licence", licence.Length > 160 ? "Keep it under 160 characters." : string.Empty },
                { "hours", hours.Count == 0 || hours.Count > 6 || hours.Any(h => h.Length > 60) ? "Enter 1 to 6 lines of up to 60 characters." : string.Empty },
                { "tax", tax == null ? "Enter a rate from 0 to 30." : string.Empty },
                { "quotedays", quoteDays < 1 || quoteDays > 180 ? "Enter 1 to 180 days." : string.Empty },
                { "paydays", payDays < 0 || payDays > 90 ? "Enter 0 to 90 days." : string.Empty },
                { "prefix", !Regex.IsMatch(prefix, "^[A-Z0-9]{1,6}$") ? "Use 1 to 6 letters or digits." : string.Empty },
                { "next", next < Math.Max(1, check.NextInvoice) || next > 999999 ? "Use " + Math.Max(1, check.NextInvoice) + " or higher." : string.Empty },
                { "video", video < 1 || video > 100 ? "Enter 1 to 100." : string.Empty }
            };
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("st-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(x => x.Length > 0) || tax == null)
            {
                response.ExecuteScript("AdminSettingsJs.toast('Please check the marked fields.');");
                return response;
            }
            SiteData site;
            using (await Store.Hold())
            {
                site = await Store.LoadSite();
                site.Brand = brand;
                site.Phone = phone;
                site.Email = email;
                site.Address = address;
                site.Tagline = tagline;
                site.Licence = licence;
                site.Hours = hours;
                site.TaxRate = tax.Value;
                site.QuoteDays = quoteDays;
                site.PayDays = payDays;
                site.InvoicePrefix = prefix;
                site.NextInvoice = Math.Max(site.NextInvoice, next);
                site.MaxVideoMb = video;
                await Store.SaveSite(site);
            }
            await Store.Log(c.U.Id, "save settings", "tax " + Ui.Num(tax.Value) + "% · quotes " + quoteDays + " days", true);
            response.SetElementContents("st-main", FormHtml(site));
            response.SetElementContents("st-brand", Ui.Esc(site.Brand));
            response.ExecuteScript("AdminSettingsJs.toast('Settings saved');");
            return response;
        }
    }
}
