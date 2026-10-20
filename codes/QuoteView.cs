using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HomeServices.Core;
using HomeServices.Models;
using SkyNet;

namespace HomeServices.codes
{
    public class QuoteView : WebPage
    {
        private static readonly Dictionary<string, List<DateTime>> Tries = new Dictionary<string, List<DateTime>>();
        private static readonly object TryLock = new object();

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle("Your quote | " + site.Brand);
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string key = Ui.Clip(QueryValue("k"), 64);
            QuoteRec? q = await Find(key);
            HtmlDoc.HtmlBodyText = Chrome(HtmlDoc.HtmlBodyText.Replace("{plhd_view}", q == null ? Missing(site) : ViewHtml(site, q)), site);
        }

        private static async Task<QuoteRec?> Find(string key)
        {
            if (!Regex.IsMatch(key, "^[a-f0-9]{32}$"))
            {
                return null;
            }
            using (await Store.Hold())
            {
                List<QuoteRec> list = await Store.Quotes();
                if (Work.ExpireQuotes(list))
                {
                    await Store.SaveQuotes(list);
                }
                QuoteRec? q = list.FirstOrDefault(x => x.Key == key);
                return q == null || q.Status == "closed" ? null : q;
            }
        }

        private static string Missing(SiteData site)
        {
            return "<div class=\"qv-miss\"><div class=\"qv-eyebrow\">Your quote</div><h1>We could not find this quote</h1><p>The link may be incomplete, or the quote was withdrawn. Copy the whole link from our email, or call us on <a href=\"" + Ui.Esc(Work.Tel(site.Phone)) + "\">" + Ui.Esc(site.Phone) + "</a> and we will help.</p><div class=\"qv-bbtns\"><a class=\"qv-btn\" href=\"Contact\">Contact us</a><a class=\"qv-btn qv-btn-ghost\" href=\"Home\">Home page</a></div></div>";
        }

        private static string StatusLine(QuoteRec q)
        {
            return q.Status switch
            {
                "sent" => Work.Pill("qv", "info", "Waiting for your answer"),
                "accepted" => Work.Pill("qv", "go", "Accepted"),
                "declined" => Work.Pill("qv", "bad", "Declined"),
                "expired" => Work.Pill("qv", "off", "Expired"),
                _ => Work.Pill("qv", "wait", "Being updated")
            };
        }

        private static string DocHtml(SiteData site, QuoteRec q)
        {
            CultureInfo us = CultureInfo.GetCultureInfo("en-US");
            StringBuilder sb = new StringBuilder("<article class=\"qv-doc\"><header class=\"qv-doc-h\"><div><div class=\"qv-eyebrow\">Written quote</div><h1>Quote " + Ui.Esc(q.Id) + "</h1><p>For " + Ui.Esc(q.Name) + ", " + Ui.Esc(q.Address) + (q.Town.Length > 0 ? ", " + Ui.Esc(q.Town) : string.Empty) + " " + Ui.Esc(q.Zip) + "</p></div><div class=\"qv-doc-st\" id=\"qv-status\">" + StatusLine(q) + "</div></header>");
            sb.Append("<dl class=\"qv-doc-kv\">");
            if (q.SentAt.HasValue)
            {
                sb.Append("<div><dt>Sent</dt><dd>" + q.SentAt.Value.ToLocalTime().ToString("MMMM d, yyyy", us) + "</dd></div>");
            }
            if (q.Expires.HasValue)
            {
                sb.Append("<div><dt>" + (q.Status == "expired" ? "Expired" : "Valid until") + "</dt><dd>" + q.Expires.Value.ToLocalTime().ToString("MMMM d, yyyy", us) + "</dd></div>");
            }
            sb.Append("<div><dt>Services</dt><dd>" + Ui.Esc(Work.ServiceNames(site, q.Services)) + "</dd></div></dl>");
            sb.Append("<div class=\"qv-lines\"><table><thead><tr><th>Description</th><th class=\"qv-n\">Qty</th><th class=\"qv-n\">Price</th><th class=\"qv-n\">Amount</th></tr></thead><tbody>");
            foreach (LineItem l in q.Lines)
            {
                sb.Append("<tr><td data-l=\"\">" + Ui.Esc(l.Text) + "</td><td class=\"qv-n\" data-l=\"Qty\">" + Ui.Num(l.Qty) + "</td><td class=\"qv-n\" data-l=\"Price\">" + Ui.Money(l.Price) + "</td><td class=\"qv-n\" data-l=\"Amount\">" + Ui.Money(Math.Round(l.Qty * l.Price, 2, MidpointRounding.AwayFromZero)) + "</td></tr>");
            }
            if (q.TripFee > 0)
            {
                sb.Append("<tr><td data-l=\"\">Trip fee" + (q.Town.Length > 0 ? ", " + Ui.Esc(q.Town) : string.Empty) + "</td><td class=\"qv-n\" data-l=\"Qty\">1</td><td class=\"qv-n\" data-l=\"Price\">" + Ui.Money(q.TripFee) + "</td><td class=\"qv-n\" data-l=\"Amount\">" + Ui.Money(q.TripFee) + "</td></tr>");
            }
            sb.Append("</tbody></table></div><div class=\"qv-tot\"><div><span>Subtotal</span><b>" + Ui.Money(q.Subtotal) + "</b></div><div><span>Tax " + Ui.Num(q.TaxRate) + "%</span><b>" + Ui.Money(q.Tax) + "</b></div><div class=\"qv-tot-g\"><span>Total</span><b>" + Ui.Money(q.Total) + "</b></div></div>");
            if (q.Note.Length > 0)
            {
                sb.Append("<div class=\"qv-note\"><b>Note from " + Ui.Esc(site.Brand) + "</b><p>" + Ui.Esc(q.Note).Replace("\n", "<br>") + "</p></div>");
            }
            sb.Append("<p class=\"qv-small\">Payment is due after the job, by card, cash or check. Nothing is charged online. Questions? Call <a href=\"" + Ui.Esc(Work.Tel(site.Phone)) + "\">" + Ui.Esc(site.Phone) + "</a> and give the quote number.</p></article>");
            return sb.ToString();
        }

        private static string ActionHtml(SiteData site, QuoteRec q)
        {
            CultureInfo us = CultureInfo.GetCultureInfo("en-US");
            switch (q.Status)
            {
                case "sent":
                    return "<div class=\"qv-act\" id=\"qv-act\"><h2>Accept this quote</h2><p>Type your name and the last 4 digits of the phone number you gave us. We then call you to book a day and a two-hour arrival window.</p><form class=\"qv-actf\" onsubmit=\"return QuoteViewJs.accept()\" novalidate>"
                        + "<label class=\"qv-field\" for=\"qv-sig\"><span>Your full name</span><input type=\"text\" id=\"qv-sig\" maxlength=\"80\" autocomplete=\"name\"><em class=\"qv-err\" id=\"qv-e-sig\"></em></label>"
                        + "<label class=\"qv-field\" for=\"qv-last4\"><span>Last 4 digits of your phone number</span><input type=\"text\" id=\"qv-last4\" maxlength=\"4\" inputmode=\"numeric\" autocomplete=\"off\"><em class=\"qv-err\" id=\"qv-e-last4\"></em></label>"
                        + "<label class=\"qv-agree\"><input type=\"checkbox\" id=\"qv-agree\"><span>I accept the work and prices in this quote, " + Ui.Money(q.Total) + " including tax.</span></label><em class=\"qv-err\" id=\"qv-e-agree\"></em>"
                        + "<button type=\"submit\" class=\"qv-btn qv-btn-wide\">Accept quote</button></form>"
                        + "<div class=\"qv-decl\"><button type=\"button\" class=\"qv-linkbtn\" onclick=\"QuoteViewJs.declineOpen()\" id=\"qv-declbtn\">I do not want to go ahead</button><div id=\"qv-declbox\" hidden><label class=\"qv-field\" for=\"qv-why\"><span>Reason (optional, it helps us)</span><textarea id=\"qv-why\" rows=\"3\" maxlength=\"500\"></textarea></label><p class=\"qv-small\">Uses the last 4 digits you typed above.</p><em class=\"qv-err\" id=\"qv-e-decl\"></em><button type=\"button\" class=\"qv-btn qv-btn-ghost\" onclick=\"QuoteViewJs.decline()\">Decline quote</button></div></div>"
                        + "<button type=\"button\" class=\"qv-linkbtn qv-print\" onclick=\"window.print()\">Print or save as PDF</button></div>";
                case "accepted":
                    return "<div class=\"qv-act\" id=\"qv-act\">" + Accepted(site, q) + "</div>";
                case "declined":
                    return "<div class=\"qv-act\" id=\"qv-act\"><h2>You declined this quote</h2><p>" + (q.AnsweredAt.HasValue ? "On " + q.AnsweredAt.Value.ToLocalTime().ToString("MMMM d, yyyy", us) + ". " : string.Empty) + "Changed your mind? Call us and we will reopen it.</p><a class=\"qv-btn qv-btn-ghost\" href=\"Contact\">Contact us</a></div>";
                case "expired":
                    return "<div class=\"qv-act\" id=\"qv-act\"><h2>This quote has expired</h2><p>Prices are held for " + site.QuoteDays + " days. Ask us for a fresh quote; it is free.</p><a class=\"qv-btn\" href=\"Quote\">Request a new quote</a></div>";
                default:
                    return "<div class=\"qv-act\" id=\"qv-act\"><h2>We are updating this quote</h2><p>You will get an email when the new version is ready. The link stays the same.</p></div>";
            }
        }

        private static string Accepted(SiteData site, QuoteRec q)
        {
            CultureInfo us = CultureInfo.GetCultureInfo("en-US");
            return "<span class=\"qv-done-i\"><svg viewBox=\"0 0 24 24\" width=\"28\" height=\"28\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.4\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M5 12.5l4.5 4.5L19 7.5\"/></svg></span><h2>Quote accepted. Thank you!</h2><p>Accepted" + (q.AnsweredAt.HasValue ? " on " + q.AnsweredAt.Value.ToLocalTime().ToString("MMMM d, yyyy", us) : string.Empty) + (q.AcceptName.Length > 0 ? " by " + Ui.Esc(q.AcceptName) : string.Empty) + ".</p><p>We will call you on the number ending " + Last4(q.Phone) + " to book a day and a two-hour arrival window. You pay after the job, by card, cash or check.</p><button type=\"button\" class=\"qv-linkbtn qv-print\" onclick=\"window.print()\">Print or save as PDF</button>";
        }

        private static string Last4(string phone)
        {
            string d = Work.Digits(phone);
            return d.Length >= 4 ? d.Substring(d.Length - 4) : d;
        }

        private static string ViewHtml(SiteData site, QuoteRec q)
        {
            return "<div class=\"qv-qv\">" + DocHtml(site, q) + "<aside class=\"qv-qvs\">" + ActionHtml(site, q) + "</aside></div>";
        }

        private static bool Paused(string key)
        {
            lock (TryLock)
            {
                if (!Tries.TryGetValue(key, out List<DateTime>? list))
                {
                    return false;
                }
                list.RemoveAll(x => x < DateTime.UtcNow.AddMinutes(-15));
                return list.Count >= 5;
            }
        }

        private static void Missed(string key)
        {
            lock (TryLock)
            {
                if (!Tries.TryGetValue(key, out List<DateTime>? list))
                {
                    list = new List<DateTime>();
                    Tries[key] = list;
                }
                list.Add(DateTime.UtcNow);
            }
        }

        public async Task<ApiResponse> Accept()
        {
            ApiResponse response = new ApiResponse();
            string key = Ui.Clip(GetDataValue("qk"), 64);
            string sig = Ui.Clip(GetDataValue("sig"), 200);
            string last4 = Ui.Clip(GetDataValue("last4"), 10);
            bool agree = GetDataValue("agree") == "1";
            string eSig = sig.Length < 2 || sig.Length > 80 ? "Please type your full name." : string.Empty;
            string eLast = !Regex.IsMatch(last4, "^[0-9]{4}$") ? "Please enter 4 digits." : string.Empty;
            string eAgree = !agree ? "Please tick the box to accept." : string.Empty;
            response.SetElementContents("qv-e-sig", eSig);
            response.SetElementContents("qv-e-last4", eLast);
            response.SetElementContents("qv-e-agree", eAgree);
            if (eSig + eLast + eAgree != string.Empty)
            {
                return response;
            }
            if (!Regex.IsMatch(key, "^[a-f0-9]{32}$"))
            {
                response.SetElementContents("qv-e-agree", "This link is not valid.");
                return response;
            }
            if (Paused(key))
            {
                response.SetElementContents("qv-e-last4", "Too many wrong tries. Please wait 15 minutes or call us.");
                return response;
            }
            SiteData site = await Store.LoadSite();
            string error = string.Empty;
            QuoteRec? done = null;
            using (await Store.Hold())
            {
                List<QuoteRec> list = await Store.Quotes();
                bool changed = Work.ExpireQuotes(list);
                QuoteRec? q = list.FirstOrDefault(x => x.Key == key);
                if (q == null || q.Status == "closed")
                {
                    error = "This quote is no longer available.";
                }
                else if (q.Status != "sent")
                {
                    error = q.Status == "expired" ? "This quote has expired. Ask us for a fresh one." : q.Status == "accepted" ? "This quote is already accepted." : "This quote cannot be accepted right now.";
                }
                else if (Last4(q.Phone) != last4)
                {
                    error = "last4";
                }
                else
                {
                    q.Status = "accepted";
                    q.AnsweredAt = DateTime.UtcNow;
                    q.AcceptName = sig;
                    changed = true;
                    done = q;
                }
                if (changed)
                {
                    await Store.SaveQuotes(list);
                }
            }
            if (error == "last4")
            {
                Missed(key);
                response.SetElementContents("qv-e-last4", "Those digits do not match the phone number on this quote.");
                return response;
            }
            if (done == null)
            {
                response.SetElementContents("qv-e-agree", Ui.Esc(error));
                return response;
            }
            await Store.Log("customer", "accept quote", done.Id + " · " + done.Name, true);
            response.SetElementContents("qv-act", Accepted(site, done));
            response.SetElementContents("qv-status", StatusLine(done));
            response.ExecuteScript("QuoteViewJs.answered('Quote accepted');");
            return response;
        }

        public async Task<ApiResponse> Decline()
        {
            ApiResponse response = new ApiResponse();
            string key = Ui.Clip(GetDataValue("qk"), 64);
            string last4 = Ui.Clip(GetDataValue("last4"), 10);
            string why = Ui.Clip(GetDataValue("why"), 500);
            if (!Regex.IsMatch(last4, "^[0-9]{4}$"))
            {
                response.SetElementContents("qv-e-decl", "Type the last 4 digits of your phone number above first.");
                return response;
            }
            if (!Regex.IsMatch(key, "^[a-f0-9]{32}$") || Paused(key))
            {
                response.SetElementContents("qv-e-decl", "Please call us to decline this quote.");
                return response;
            }
            string error = string.Empty;
            QuoteRec? done = null;
            using (await Store.Hold())
            {
                List<QuoteRec> list = await Store.Quotes();
                QuoteRec? q = list.FirstOrDefault(x => x.Key == key);
                if (q == null || q.Status != "sent")
                {
                    error = "This quote cannot be declined right now.";
                }
                else if (Last4(q.Phone) != last4)
                {
                    error = "last4";
                }
                else
                {
                    q.Status = "declined";
                    q.AnsweredAt = DateTime.UtcNow;
                    q.DeclineWhy = why;
                    done = q;
                    await Store.SaveQuotes(list);
                }
            }
            if (error == "last4")
            {
                Missed(key);
                response.SetElementContents("qv-e-decl", "Those digits do not match the phone number on this quote.");
                return response;
            }
            if (done == null)
            {
                response.SetElementContents("qv-e-decl", Ui.Esc(error));
                return response;
            }
            await Store.Log("customer", "decline quote", done.Id + " · " + done.Name, true);
            SiteData site = await Store.LoadSite();
            response.SetElementContents("qv-act", "<h2>You declined this quote</h2><p>Thank you for letting us know. Changed your mind? Call us on " + Ui.Esc(site.Phone) + " and we will reopen it.</p>");
            response.SetElementContents("qv-status", StatusLine(done));
            response.ExecuteScript("QuoteViewJs.answered('Quote declined');");
            return response;
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

    }
}
