using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HomeServices.Core;
using HomeServices.Models;
using SkyNet;

namespace HomeServices.codes
{
    public class Quote : WebPage
    {
        private const int MaxPhotos = 6;
        private const int PhotoMb = 8;
        private string preZip = string.Empty;
        private string preSvc = string.Empty;

        private static string PendingDir => Path.Combine(Store.Root, "images", "requests", "pending");

        private static string FinalDir => Path.Combine(Store.Root, "images", "requests");

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle("Request a quote | " + site.Brand);
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("description", "Tell us about the job, add photos and pick the days that suit you. Quotes are free.");
            string z = Ui.Clip(QueryValue("zip"), 5);
            preZip = Regex.IsMatch(z, "^[0-9]{5}$") ? z : string.Empty;
            string s = Ui.Clip(QueryValue("svc"), 40);
            preSvc = site.Services.Any(x => x.Active && x.Id == s) ? s : string.Empty;
            List<MediaItem> media = await Media.Load();
            HtmlDoc.HtmlBodyText = Chrome(HtmlDoc.HtmlBodyText.Replace("{plhd_blocks}", PageHtml(site, media, "quote")), site);
        }

        private static List<string> Csv(string? v, int max)
        {
            return (v ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().Take(max).ToList();
        }

        private static string SummaryHtml(SiteData site, List<string> ids, string zip)
        {
            List<ServiceItem> picked = site.Services.Where(s => s.Active && ids.Contains(s.Id)).ToList();
            StringBuilder sb = new StringBuilder("<h2>Your request</h2>");
            if (picked.Count == 0)
            {
                sb.Append("<p class=\"qt-small\">Pick a service to see where prices start.</p>");
            }
            else
            {
                sb.Append("<ul class=\"qt-suml\">");
                foreach (ServiceItem s in picked)
                {
                    sb.Append("<li><span>" + Ui.Esc(s.Name) + "</span><b>from " + Ui.Price(s.From) + "</b></li>");
                }
                sb.Append("</ul>");
            }
            ZipArea? area = site.Zips.FirstOrDefault(x => x.Zip == zip);
            decimal fee = area == null ? 0 : site.Zones.FirstOrDefault(x => x.Key == area.Zone)?.Fee ?? 0;
            if (area != null)
            {
                sb.Append("<p class=\"qt-sumz\">" + Ui.Esc(area.Town) + ": " + (fee > 0 ? Ui.Price(fee) + " trip fee" : "no trip fee") + "</p>");
            }
            else if (zip.Length == 5)
            {
                sb.Append("<p class=\"qt-sumz qt-sumz-out\">" + zip + " is outside our usual area. Send the request anyway and we will tell you if we can come.</p>");
            }
            if (picked.Count > 0)
            {
                sb.Append("<p class=\"qt-sumt\"><span>Starting from</span><b>" + Ui.Price(picked.Sum(s => s.From) + fee) + "</b></p>");
            }
            sb.Append("<p class=\"qt-small\">Your written quote lists every part and hour. Quotes are free and valid for " + site.QuoteDays + " days.</p>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Pick()
        {
            ApiResponse response = new ApiResponse();
            SiteData site = await Store.LoadSite();
            string zip = Ui.Clip(GetDataValue("zipcode"), 5);
            response.SetElementContents("qt-sum", SummaryHtml(site, Csv(GetDataValue("services"), 30), Regex.IsMatch(zip, "^[0-9]{5}$") ? zip : string.Empty));
            return response;
        }

        private static bool PhotoName(string v)
        {
            return Regex.IsMatch(v, "^[a-f0-9]{20}\\.(jpg|png|webp)$");
        }

        private static string PhotosHtml(List<string> list)
        {
            StringBuilder sb = new StringBuilder();
            int i = 1;
            foreach (string p in list)
            {
                sb.Append("<div class=\"qt-pt\" data-p=\"" + p + "\"><img src=\"images/requests/pending/" + p + "\" alt=\"Your photo " + i + "\"><button type=\"button\" class=\"qt-ptx\" onclick=\"QuoteJs.removePhoto('" + p + "')\" aria-label=\"Remove photo " + i + "\">Remove</button></div>");
                i++;
            }
            return sb.ToString();
        }

        private static void Sweep()
        {
            if (!Directory.Exists(PendingDir))
            {
                return;
            }
            foreach (string f in Directory.GetFiles(PendingDir))
            {
                if (File.GetLastWriteTimeUtc(f) < DateTime.UtcNow.AddDays(-1))
                {
                    try
                    {
                        File.Delete(f);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
        }

        public async Task<ApiResponse> Upload()
        {
            ApiResponse response = new ApiResponse();
            List<string> have = Csv(GetDataValue("photos"), MaxPhotos).Where(p => PhotoName(p) && File.Exists(Path.Combine(PendingDir, p))).ToList();
            List<Incoming> files = await Media.Received();
            List<string> errors = new List<string>();
            Sweep();
            if (files.Count == 0)
            {
                errors.Add("Choose one or more photos first.");
            }
            foreach (Incoming f in files)
            {
                string name = Media.CleanName(f.Name);
                if (have.Count >= MaxPhotos)
                {
                    errors.Add("You can add up to " + MaxPhotos + " photos.");
                    break;
                }
                byte[] head = new byte[16];
                int read;
                using (Stream s = f.Open())
                {
                    read = await s.ReadAtLeastAsync(head, head.Length, false);
                }
                string ext = Media.Detect(head, read);
                if (ext != ".jpg" && ext != ".png" && ext != ".webp")
                {
                    errors.Add(name + " is not a JPG, PNG or WebP photo.");
                    continue;
                }
                if (f.Length > PhotoMb * 1024L * 1024L)
                {
                    errors.Add(name + " is larger than " + PhotoMb + " MB.");
                    continue;
                }
                string token = Access.NewToken(10) + ext;
                try
                {
                    Directory.CreateDirectory(PendingDir);
                    using (Stream src = f.Open())
                    using (FileStream dst = new FileStream(Path.Combine(PendingDir, token), FileMode.CreateNew, FileAccess.Write))
                    {
                        await src.CopyToAsync(dst);
                    }
                    have.Add(token);
                }
                catch (IOException)
                {
                    errors.Add(name + " could not be saved. Please try again.");
                }
                catch (UnauthorizedAccessException)
                {
                    errors.Add(name + " could not be saved. Please try again.");
                }
            }
            response.SetElementContents("qt-photos", PhotosHtml(have));
            response.SetElementContents("qt-e-photos", Ui.Esc(string.Join(" ", errors.Distinct())));
            response.ExecuteScript("QuoteJs.uploaded(" + have.Count + ");");
            return response;
        }

        public async Task<ApiResponse> RemovePhoto()
        {
            ApiResponse response = new ApiResponse();
            string drop = Ui.Clip(GetDataValue("drop"), 40);
            List<string> have = Csv(GetDataValue("photos"), MaxPhotos).Where(p => PhotoName(p) && File.Exists(Path.Combine(PendingDir, p))).ToList();
            if (PhotoName(drop) && have.Remove(drop))
            {
                try
                {
                    File.Delete(Path.Combine(PendingDir, drop));
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
            response.SetElementContents("qt-photos", PhotosHtml(have));
            response.SetElementContents("qt-e-photos", string.Empty);
            response.ExecuteScript("QuoteJs.uploaded(" + have.Count + ");");
            return response;
        }

        public async Task<ApiResponse> Submit()
        {
            ApiResponse response = new ApiResponse();
            SiteData site = await Store.LoadSite();
            List<string> services = Csv(GetDataValue("services"), 30).Where(id => site.Services.Any(s => s.Active && s.Id == id)).ToList();
            string desc = (GetDataValue("desc") ?? string.Empty).Trim();
            List<string> photos = Csv(GetDataValue("photos"), MaxPhotos).Where(p => PhotoName(p) && File.Exists(Path.Combine(PendingDir, p))).ToList();
            DateTime today = DateTime.Today;
            List<string> days = Csv(GetDataValue("days"), 14).Where(d => Work.ParseDay(d) is DateTime x && x > today && x <= today.AddDays(30)).OrderBy(d => d, StringComparer.Ordinal).ToList();
            List<string> times = Csv(GetDataValue("times"), 5).Where(t => Work.Times.Any(x => x.Key == t)).ToList();
            string name = Ui.Clip(GetDataValue("name"), 200);
            string phone = Ui.Clip(GetDataValue("phone"), 40);
            string email = Ui.Clip(GetDataValue("email"), 200);
            string address = Ui.Clip(GetDataValue("address"), 300);
            string zip = Ui.Clip(GetDataValue("zipcode"), 10);
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                { "svc", services.Count == 0 ? "Pick at least one service." : string.Empty },
                { "desc", desc.Length < 20 ? "Please describe the job in at least 20 characters." : desc.Length > 2000 ? "Please keep it under 2,000 characters." : string.Empty },
                { "days", days.Count == 0 ? "Pick at least one day that suits you." : days.Count > 6 ? "Pick up to 6 days." : string.Empty },
                { "times", times.Count == 0 ? "Pick at least one time of day." : string.Empty },
                { "name", name.Length < 2 || name.Length > 80 ? "Please enter your name." : string.Empty },
                { "phone", !Work.IsPhone(phone) ? "Please enter a phone number with area code. You will need its last 4 digits to accept the quote." : string.Empty },
                { "email", !Work.IsEmail(email) ? "Please enter a valid email address. We send the quote there." : string.Empty },
                { "address", address.Length < 5 || address.Length > 160 ? "Please enter the street address of the job." : string.Empty },
                { "zip", !Regex.IsMatch(zip, "^[0-9]{5}$") ? "Please enter a 5-digit ZIP code." : string.Empty }
            };
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("qt-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0))
            {
                response.ExecuteScript("QuoteJs.failed();");
                return response;
            }
            ZipArea? area = site.Zips.FirstOrDefault(x => x.Zip == zip);
            QuoteRec q = new QuoteRec
            {
                Status = "new",
                Created = DateTime.UtcNow,
                Source = "web",
                Name = name,
                Phone = phone,
                Email = email,
                Address = address,
                Zip = zip,
                Town = area?.Town ?? string.Empty,
                InArea = area != null,
                Services = services,
                Description = desc,
                Days = days,
                Times = times,
                TripFee = Work.TripFee(site, zip),
                TaxRate = site.TaxRate
            };
            using (await Store.Hold())
            {
                List<QuoteRec> list = await Store.Quotes();
                string id;
                do
                {
                    id = Work.Code("Q");
                }
                while (list.Any(x => x.Id == id));
                q.Id = id;
                int n = 1;
                foreach (string p in photos)
                {
                    string target = id.ToLowerInvariant() + "-" + n + "-" + Access.NewToken(3) + Path.GetExtension(p);
                    try
                    {
                        File.Move(Path.Combine(PendingDir, p), Path.Combine(FinalDir, target));
                        q.Photos.Add("images/requests/" + target);
                        n++;
                    }
                    catch (IOException)
                    {
                    }
                }
                list.Insert(0, q);
                await Store.SaveQuotes(list);
            }
            await Store.Log("customer", "quote request", q.Id + " · " + q.Name, true);
            StringBuilder ok = new StringBuilder("<div class=\"qt-done\" tabindex=\"-1\" id=\"qt-donebox\"><span class=\"qt-done-i\">" + Icon("check") + "</span><h2>Thank you, " + Ui.Esc(name) + ". Your request is in.</h2>");
            ok.Append("<p>Your reference is <b>" + q.Id + "</b>. We will email your written quote to <b>" + Ui.Esc(email) + "</b>, usually within one working day.</p>");
            if (!q.InArea)
            {
                ok.Append("<p>ZIP code " + zip + " is outside our usual area. We will let you know if we can make the trip.</p>");
            }
            ok.Append("<p>To accept the quote you will need the last 4 digits of the phone number you gave: <b>&middot;&middot;&middot;" + Work.Digits(phone).Substring(Work.Digits(phone).Length - 4) + "</b>.</p><div class=\"qt-bbtns\"><a class=\"qt-btn\" href=\"Home\">Back to the home page</a><a class=\"qt-btn qt-btn-ghost\" href=\"Quote\">Send another request</a></div></div>");
            response.SetElementContents("qt-qbox", ok.ToString());
            response.ExecuteScript("QuoteJs.done();");
            return response;
        }

        private string DataPart(SiteData site, Block b, bool first)
        {
            if (b.Source != "quoteform")
            {
                return string.Empty;
            }
            StringBuilder sb = new StringBuilder("<div class=\"qt-qbox\" id=\"qt-qbox\">" + Head(site, b, true, first ? "h1" : "h2") + "<div class=\"qt-qwrap\"><form class=\"qt-qform\" id=\"qt-qform\" onsubmit=\"return QuoteJs.submit()\" novalidate>");
            sb.Append("<fieldset class=\"qt-step\"><legend><i>1</i>What do you need?</legend><p class=\"qt-small\">Pick one or more services. Not sure? Pick the closest one and explain below.</p>");
            foreach (Trade t in site.Trades)
            {
                List<ServiceItem> list = site.Services.Where(s => s.Active && s.Trade == t.Key).ToList();
                if (list.Count == 0)
                {
                    continue;
                }
                sb.Append("<div class=\"qt-svg\" style=\"--qt-tc:" + Work.TradeColor(site, t.Key) + "\"><h3>" + Ui.Esc(t.Name) + "</h3><div class=\"qt-opts\">");
                foreach (ServiceItem s in list)
                {
                    sb.Append("<label class=\"qt-opt\"><input type=\"checkbox\" name=\"qt-svc\" value=\"" + Ui.Esc(s.Id) + "\" onchange=\"QuoteJs.pick()\"" + (s.Id == preSvc ? " checked" : string.Empty) + "><span><b>" + Ui.Esc(s.Name) + "</b><small>From " + Ui.Price(s.From) + " " + Ui.Esc(s.Unit) + "</small></span></label>");
                }
                sb.Append("</div></div>");
            }
            sb.Append("<em class=\"qt-err\" id=\"qt-e-svc\"></em></fieldset>");
            sb.Append("<fieldset class=\"qt-step\"><legend><i>2</i>Describe the job</legend><label class=\"qt-field\" for=\"qt-desc\"><span>What is happening, where in the house, and since when?</span><textarea id=\"qt-desc\" rows=\"6\" maxlength=\"2000\" oninput=\"QuoteJs.count()\" placeholder=\"For example: the kitchen sink drains slowly and gurgles. It started last week.\"></textarea><small id=\"qt-count\">0 of 2,000 characters</small><em class=\"qt-err\" id=\"qt-e-desc\"></em></label></fieldset>");
            sb.Append("<fieldset class=\"qt-step\"><legend><i>3</i>Photos <span class=\"qt-opt-l\">optional</span></legend><p class=\"qt-small\">A photo of the problem, the area and any model plate helps us quote exactly. Up to " + MaxPhotos + " photos, JPG, PNG or WebP, " + PhotoMb + " MB each.</p><div class=\"qt-photos\" id=\"qt-photos\"></div><label class=\"qt-btn qt-btn-ghost qt-upbtn\" id=\"qt-upl\"><input type=\"file\" id=\"qt-file\" accept=\"image/jpeg,image/png,image/webp\" multiple onchange=\"QuoteJs.upload()\">Add photos</label><em class=\"qt-err\" id=\"qt-e-photos\" role=\"status\"></em></fieldset>");
            sb.Append("<fieldset class=\"qt-step\"><legend><i>4</i>When suits you?</legend><p class=\"qt-small\">Pick the days and times you could be home. We confirm the exact day when we book.</p><div class=\"qt-days\" role=\"group\" aria-label=\"Days\">");
            DateTime d = DateTime.Today.AddDays(1);
            int shown = 0;
            CultureInfo us = CultureInfo.GetCultureInfo("en-US");
            while (shown < 12)
            {
                if (d.DayOfWeek != DayOfWeek.Sunday)
                {
                    string iso = Work.Iso(d);
                    sb.Append("<label class=\"qt-day\"><input type=\"checkbox\" name=\"qt-day\" value=\"" + iso + "\"><span><small>" + d.ToString("ddd", us) + "</small><b>" + d.Day + "</b><small>" + d.ToString("MMM", us) + "</small></span></label>");
                    shown++;
                }
                d = d.AddDays(1);
            }
            sb.Append("</div><em class=\"qt-err\" id=\"qt-e-days\"></em><div class=\"qt-times\" role=\"group\" aria-label=\"Times of day\">");
            foreach ((string key, string name) in Work.Times)
            {
                sb.Append("<label class=\"qt-opt\"><input type=\"checkbox\" name=\"qt-time\" value=\"" + key + "\"><span><b>" + Ui.Esc(name) + "</b></span></label>");
            }
            sb.Append("</div><em class=\"qt-err\" id=\"qt-e-times\"></em></fieldset>");
            sb.Append("<fieldset class=\"qt-step\"><legend><i>5</i>Your details</legend><div class=\"qt-g2\">");
            sb.Append("<label class=\"qt-field\" for=\"qt-name\"><span>Name</span><input type=\"text\" id=\"qt-name\" maxlength=\"80\" autocomplete=\"name\"><em class=\"qt-err\" id=\"qt-e-name\"></em></label>");
            sb.Append("<label class=\"qt-field\" for=\"qt-phone\"><span>Phone</span><input type=\"tel\" id=\"qt-phone\" maxlength=\"24\" autocomplete=\"tel\"><em class=\"qt-err\" id=\"qt-e-phone\"></em></label>");
            sb.Append("<label class=\"qt-field\" for=\"qt-email\"><span>Email</span><input type=\"email\" id=\"qt-email\" maxlength=\"80\" autocomplete=\"email\"><em class=\"qt-err\" id=\"qt-e-email\"></em></label>");
            sb.Append("<label class=\"qt-field\" for=\"qt-zip\"><span>ZIP code</span><input type=\"text\" id=\"qt-zip\" maxlength=\"5\" inputmode=\"numeric\" autocomplete=\"postal-code\" value=\"" + preZip + "\" onchange=\"QuoteJs.pick()\"><em class=\"qt-err\" id=\"qt-e-zip\"></em></label></div>");
            sb.Append("<label class=\"qt-field\" for=\"qt-address\"><span>Street address of the job</span><input type=\"text\" id=\"qt-address\" maxlength=\"160\" autocomplete=\"street-address\"><em class=\"qt-err\" id=\"qt-e-address\"></em></label></fieldset>");
            sb.Append("<p class=\"qt-small\">We use these details only for your quote and the job. You will need the last 4 digits of your phone number to accept the quote online.</p><button type=\"submit\" class=\"qt-btn qt-qsend\">Send my request</button></form>");
            sb.Append("<aside class=\"qt-qside\"><div class=\"qt-sum\" id=\"qt-sum\">" + SummaryHtml(site, preSvc.Length > 0 ? new List<string> { preSvc } : new List<string>(), preZip) + "</div></aside></div></div>");
            return Sec(b, sb.ToString());
        }

        private static string Chrome(string html, SiteData site)
        {
            StringBuilder trades = new StringBuilder();
            foreach (Trade t in site.Trades)
            {
                trades.Append("<li><a href=\"Services#trade-" + Ui.Esc(t.Key) + "\">" + Ui.Esc(t.Name) + "</a></li>");
            }
            return html
                .Replace("{plhd_brand}", Ui.Esc(site.Brand))
                .Replace("{plhd_tagline}", Ui.Esc(site.Tagline))
                .Replace("{plhd_phone}", Ui.Esc(site.Phone))
                .Replace("{plhd_tel}", Ui.Esc(Work.Tel(site.Phone)))
                .Replace("{plhd_email}", Ui.Esc(site.Email))
                .Replace("{plhd_address}", Ui.Esc(site.Address))
                .Replace("{plhd_hours}", string.Concat(site.Hours.Select(h => "<li>" + Ui.Esc(h) + "</li>")))
                .Replace("{plhd_licence}", Ui.Esc(site.Licence))
                .Replace("{plhd_year}", DateTime.Now.Year.ToString(CultureInfo.InvariantCulture))
                .Replace("{plhd_tradelinks}", trades.ToString());
        }

        private static string Tok(SiteData site, string v)
        {
            return v.Replace("{name}", site.Brand);
        }

        private static string TokH(SiteData site, string v)
        {
            return v.Replace("{name}", Ui.Esc(site.Brand));
        }

        private static string Lines(string v)
        {
            return Ui.Esc(v).Replace("\n", "<br>");
        }

        private static string Icon(string k)
        {
            string a = "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\">";
            return k switch
            {
                "wrench" => a + "<path d=\"M14.5 4.5a4.5 4.5 0 0 0-4.3 5.8L4 16.5 7.5 20l6.2-6.2a4.5 4.5 0 0 0 5.8-4.3l-2.6 2.6-3-1-1-3z\"/></svg>",
                "drop" => a + "<path d=\"M12 3.5s-6 6.6-6 10.5a6 6 0 0 0 12 0c0-3.9-6-10.5-6-10.5z\"/></svg>",
                "flame" => a + "<path d=\"M12 21c-3.9 0-6.5-2.7-6.5-6.2 0-4.3 4.3-6.2 4.3-10.3 2.9 1.6 5 4.4 5 7.2.9-.6 1.5-1.6 1.7-2.8 1.3 1.4 2 3.5 2 5.9 0 3.5-2.6 6.2-6.5 6.2z\"/></svg>",
                "snow" => a + "<path d=\"M12 3v18M4.2 7.5l15.6 9M4.2 16.5l15.6-9M9.5 4.5 12 7l2.5-2.5M9.5 19.5 12 17l2.5 2.5\"/></svg>",
                "bolt" => a + "<path d=\"M13 3 5 13.5h6L10 21l8-10.5h-6z\"/></svg>",
                "spark" => a + "<path d=\"M12 3.5l1.8 5.2 5.2 1.8-5.2 1.8L12 17.5l-1.8-5.2L5 10.5l5.2-1.8zM18.5 15.5l.8 2.2 2.2.8-2.2.8-.8 2.2-.8-2.2-2.2-.8 2.2-.8z\"/></svg>",
                "home" => a + "<path d=\"M3.5 11 12 4l8.5 7M6 9.5V20h12V9.5M10 20v-5.5h4V20\"/></svg>",
                "clock" => a + "<circle cx=\"12\" cy=\"12\" r=\"8.5\"/><path d=\"M12 7.5V12l3 2\"/></svg>",
                "pin" => a + "<path d=\"M12 21s-6.5-6-6.5-11a6.5 6.5 0 0 1 13 0c0 5-6.5 11-6.5 11z\"/><circle cx=\"12\" cy=\"10\" r=\"2.4\"/></svg>",
                "cal" => a + "<rect x=\"3.5\" y=\"5\" width=\"17\" height=\"15\" rx=\"2\"/><path d=\"M3.5 10h17M8 3v4M16 3v4\"/></svg>",
                "shield" => a + "<path d=\"M12 3.5 5 6v5.5c0 4.5 3 7.8 7 9 4-1.2 7-4.5 7-9V6z\"/><path d=\"M9 12l2.2 2.2L15.5 10\"/></svg>",
                "star" => a + "<path d=\"M12 3.5l2.6 5.4 5.9.8-4.3 4.1 1 5.8L12 16.9l-5.2 2.7 1-5.8-4.3-4.1 5.9-.8z\"/></svg>",
                "users" => a + "<circle cx=\"9\" cy=\"8\" r=\"3.2\"/><path d=\"M3 19c0-3.3 2.7-5.5 6-5.5s6 2.2 6 5.5\"/><circle cx=\"17\" cy=\"9\" r=\"2.4\"/><path d=\"M16 13.6c2.8 0 5 1.8 5 4.9\"/></svg>",
                "check" => "<svg viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.4\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M5 12.5l4.5 4.5L19 7.5\"/></svg>",
                "phone" => a + "<path d=\"M6.5 3.5h3l1.5 4.5-2 1.5a11 11 0 0 0 5.5 5.5l1.5-2 4.5 1.5v3a2 2 0 0 1-2 2A16 16 0 0 1 4.5 5.5a2 2 0 0 1 2-2z\"/></svg>",
                "mail" => a + "<rect x=\"3.5\" y=\"5.5\" width=\"17\" height=\"13\" rx=\"2\"/><path d=\"M4 7l8 6 8-6\"/></svg>",
                "card" => a + "<rect x=\"3\" y=\"6\" width=\"18\" height=\"12\" rx=\"2\"/><path d=\"M3 10h18M7 15h4\"/></svg>",
                _ => string.Empty
            };
        }

        private static string Mv(List<MediaItem> media, string src, string alt, bool bg, bool eager)
        {
            if (src == string.Empty)
            {
                return string.Empty;
            }
            MediaItem? m = media.FirstOrDefault(x => x.Path == src);
            if (Blocks.IsVideo(src))
            {
                return "<video src=\"" + Ui.Esc(src) + "\" playsinline " + (bg ? "autoplay muted loop aria-hidden=\"true\"" : "controls preload=\"metadata\" aria-label=\"" + Ui.Esc(alt) + "\"") + "></video>";
            }
            return "<img src=\"" + Ui.Esc(src) + "\" alt=\"" + Ui.Esc(m != null && m.Alt != string.Empty ? m.Alt : alt) + "\"" + (eager ? string.Empty : " loading=\"lazy\"") + ">";
        }

        private static string Btn(SiteData site, string label, string link, bool main)
        {
            return label == string.Empty || link == string.Empty ? string.Empty : "<a class=\"qt-btn" + (main ? string.Empty : " qt-btn-ghost") + "\" href=\"" + Ui.Esc(link) + "\"" + (link.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? " target=\"_blank\" rel=\"noopener\"" : string.Empty) + ">" + Ui.Esc(Tok(site, label)) + "</a>";
        }

        private static string Btns(SiteData site, Block b)
        {
            string x = Btn(site, b.Label, b.Link, true) + Btn(site, b.Label2, b.Link2, false);
            return x == string.Empty ? string.Empty : "<div class=\"qt-bbtns\">" + x + "</div>";
        }

        private static string Kick(SiteData site, Block b)
        {
            return b.Kicker == string.Empty ? string.Empty : "<div class=\"qt-eyebrow\">" + Ui.Esc(Tok(site, b.Kicker)) + "</div>";
        }

        private static string H2(SiteData site, Block b, string tag = "h2")
        {
            return b.Title == string.Empty ? string.Empty : "<" + tag + " class=\"qt-bh2\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">";
        }

        private static string Rich(SiteData site, string html, string cls)
        {
            return html == string.Empty ? string.Empty : "<div class=\"qt-rich" + (cls == string.Empty ? string.Empty : " " + cls) + "\">" + TokH(site, html) + "</div>";
        }

        private static string Head(SiteData site, Block b, bool lead, string tag = "h2")
        {
            string x = Kick(site, b) + H2(site, b, tag) + (lead ? Rich(site, b.Text, "qt-blead") : string.Empty);
            return x == string.Empty ? string.Empty : "<div class=\"qt-bhead\">" + x + "</div>";
        }

        private static string Sec(Block b, string inner)
        {
            return "<section class=\"qt-wrap qt-sec\" id=\"" + Ui.Esc(b.Id) + "\">" + inner + "</section>";
        }

        private static string Hero(SiteData site, List<MediaItem> media, Block b, bool h1)
        {
            string tag = h1 ? "h1" : "h2";
            bool vid = Blocks.IsVideo(b.Media);
            string mv = Mv(media, b.Media, Tok(site, b.Title), vid, h1);
            return "<section class=\"qt-bhero" + (mv == string.Empty ? " qt-bplain" : string.Empty) + "\" id=\"" + Ui.Esc(b.Id) + "\">" + (mv != string.Empty ? "<div class=\"qt-bhero-p\">" + mv + "</div>" : string.Empty) + "<div class=\"qt-wrap qt-bhero-in\"><div class=\"qt-bhero-t\">" + Kick(site, b) + "<" + tag + " class=\"qt-btitle\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">" + Rich(site, b.Text, "qt-blead") + Btns(site, b) + "</div></div></section>";
        }

        private static string Split(SiteData site, List<MediaItem> media, Block b)
        {
            string btn = Btn(site, b.Label, b.Link, true);
            return Sec(b, "<div class=\"qt-bsplit" + (b.Flip ? " qt-bflip" : string.Empty) + (b.Media == string.Empty ? " qt-bnomedia" : string.Empty) + "\">" + (b.Media != string.Empty ? "<div class=\"qt-bsm\">" + Mv(media, b.Media, Tok(site, b.Title), false, false) + "</div>" : string.Empty) + "<div class=\"qt-bst\">" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, string.Empty) + (btn != string.Empty ? "<div class=\"qt-bbtns\">" + btn + "</div>" : string.Empty) + "</div></div>");
        }

        private static string Cards(SiteData site, Block b)
        {
            int n = Math.Clamp(b.Items.Count, 1, 4);
            if (b.Items.Count == 6)
            {
                n = 3;
            }
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"qt-bcards qt-n" + n + "\">");
            foreach (BlockItem it in b.Items)
            {
                string ico = Icon(it.Icon);
                string inner = (ico != string.Empty ? "<span class=\"qt-wico\">" + ico + "</span>" : string.Empty) + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b>" + (it.Text != string.Empty ? "<p>" + Lines(Tok(site, it.Text)) + "</p>" : string.Empty);
                if (it.Link != string.Empty)
                {
                    sb.Append("<a class=\"qt-bcard\" href=\"" + Ui.Esc(it.Link) + "\">" + inner + "<span class=\"qt-bmore\">" + Ui.Esc(it.Label != string.Empty ? it.Label : "Learn more") + " &rarr;</span></a>");
                }
                else
                {
                    sb.Append("<div class=\"qt-bcard\">" + inner + "</div>");
                }
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Steps(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<ol class=\"qt-bsteps qt-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
            int i = 1;
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<li><i>" + i++ + "</i><b>" + Ui.Esc(Tok(site, it.Title)) + "</b><p>" + Lines(Tok(site, it.Text)) + "</p></li>");
            }
            sb.Append("</ol>");
            return Sec(b, sb.ToString());
        }

        private static string StatRow(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, false) + "<div class=\"qt-bstats qt-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div><b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

                private static string People(SiteData site, List<MediaItem> media, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"qt-bpeople qt-n" + Math.Clamp(b.Items.Count, 1, 4) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div class=\"qt-bperson\">" + (it.Media != string.Empty && !Blocks.IsVideo(it.Media) ? Mv(media, it.Media, it.Title, false, false) : "<span class=\"qt-bnoface\">" + Ui.Esc(Work.Initials(it.Title)) + "</span>") + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Boxes(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, false) + "<div class=\"qt-bboxes qt-n" + Math.Clamp(b.Items.Count, 1, 3) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div class=\"qt-bbox\">" + (it.Title != string.Empty ? "<h3>" + Ui.Esc(Tok(site, it.Title)) + "</h3>" : string.Empty) + "<ul class=\"qt-bticks\">");
                foreach (string line in it.Text.Split('\n').Select(x => x.Trim()).Where(x => x != string.Empty))
                {
                    sb.Append("<li>" + Icon("check") + "<span>" + Ui.Esc(Tok(site, line)) + "</span></li>");
                }
                sb.Append("</ul></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Faq(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder("<div class=\"qt-bfaq\">" + Head(site, b, false) + "<div class=\"qt-bfaqs\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<details class=\"qt-bq\"><summary>" + Ui.Esc(Tok(site, it.Title)) + "</summary><p>" + Lines(Tok(site, it.Text)) + "</p></details>");
            }
            sb.Append("</div></div>");
            return Sec(b, sb.ToString());
        }

        private static string QuoteBlock(SiteData site, List<MediaItem> media, Block b)
        {
            return Sec(b, "<figure class=\"qt-bquote\">" + (b.Media != string.Empty && !Blocks.IsVideo(b.Media) ? Mv(media, b.Media, b.Title, false, false) : string.Empty) + "<blockquote>" + Lines(Tok(site, b.Text)) + "</blockquote>" + (b.Title != string.Empty ? "<figcaption>" + Ui.Esc(Tok(site, b.Title)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        private static string Wide(SiteData site, List<MediaItem> media, Block b)
        {
            return b.Media == string.Empty ? string.Empty : Sec(b, "<figure class=\"qt-bmedia\">" + Mv(media, b.Media, b.Text, false, false) + (b.Text != string.Empty ? "<figcaption>" + Ui.Esc(Tok(site, b.Text)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        private static string Cta(SiteData site, Block b)
        {
            return Sec(b, "<div class=\"qt-bcta\"><div>" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, "qt-blead") + "</div>" + Btns(site, b) + "</div>");
        }

        private string PageHtml(SiteData site, List<MediaItem> media, string key)
        {
            StringBuilder sb = new StringBuilder();
            bool first = true;
            if (!site.Pages.TryGetValue(key, out PageDoc? doc))
            {
                return string.Empty;
            }
            foreach (Block b in doc.Blocks.Where(x => !x.Hidden))
            {
                string html = b.Type switch
                {
                    "hero" => Hero(site, media, b, first),
                    "text" => Sec(b, "<div class=\"qt-btext\">" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, string.Empty) + "</div>"),
                    "split" => Split(site, media, b),
                    "cards" => Cards(site, b),
                    "steps" => Steps(site, b),
                    "stats" => StatRow(site, b),
                    "people" => People(site, media, b),
                    "boxes" => Boxes(site, b),
                    "faq" => Faq(site, b),
                    "quote" => QuoteBlock(site, media, b),
                    "media" => Wide(site, media, b),
                    "cta" => Cta(site, b),
                    "data" => DataPart(site, b, first),
                    _ => string.Empty
                };
                if (html.Length > 0 && (b.Type == "hero" || b.Type == "data"))
                {
                    first = false;
                }
                sb.Append(html);
            }
            string all = sb.ToString();
            string open = "<h2 class=\"qt-bh2\">";
            int at = all.IndexOf(open, StringComparison.Ordinal);
            if (!all.Contains("<h1", StringComparison.Ordinal) && at >= 0)
            {
                int end = all.IndexOf("</h2>", at, StringComparison.Ordinal);
                all = all.Substring(0, at) + "<h1 class=\"qt-bh2\">" + all.Substring(at + open.Length, end - at - open.Length) + "</h1>" + all.Substring(end + 5);
            }
            return all;
        }

    }
}
