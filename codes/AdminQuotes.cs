using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HomeServices.Core;
using HomeServices.Models;
using SkyNet;

namespace HomeServices.codes
{
    public class AdminQuotes : WebPage
    {
        private const string AreaKey = "quotes";
        private const int Size = 15;

        private static readonly (string Key, string Name)[] Views =
        {
            ("open", "To do"), ("new", "New"), ("draft", "Draft"), ("sent", "Sent"), ("accepted", "Accepted"), ("declined", "Declined"), ("expired", "Expired"), ("closed", "Closed"), ("all", "All")
        };

        private static readonly (string Key, string Name)[] Channels = { ("phone", "Phone call"), ("email", "Email"), ("person", "In person") };

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Quotes | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminQuotesJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminQuotesJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("aq-brand", Ui.Esc(site.Brand));
            response.SetElementContents("aq-menu", await Auth.Menu("aq", "AdminQuotes", c));
            response.SetElementContents("aq-who", Auth.Who("aq", c));
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"aq-panel\"><h1>No access</h1><p class=\"aq-hint\">You do not have access to " + what + ". Ask a manager if you need it.</p></div>";
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
            response.ExecuteScript("AdminQuotesJs.expired();");
            return response;
        }

        private static async Task<List<QuoteRec>> Fresh()
        {
            using (await Store.Hold())
            {
                List<QuoteRec> list = await Store.Quotes();
                if (Work.ExpireQuotes(list))
                {
                    await Store.SaveQuotes(list);
                }
                return list;
            }
        }

        private static string ViewKey(string? v)
        {
            string k = (v ?? string.Empty).Trim();
            return Views.Any(x => x.Key == k) ? k : "open";
        }

        private static bool InView(QuoteRec q, string view)
        {
            return view switch
            {
                "open" => q.Status is "new" or "draft",
                "all" => true,
                _ => q.Status == view
            };
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
                response.SetElementContents("aq-main", NoAccess("quotes"));
                return response;
            }
            List<QuoteRec> list = await Fresh();
            string view = ViewKey(GetDataValue("view"));
            StringBuilder sb = new StringBuilder("<div class=\"aq-ph\"><div><h1>Quotes</h1><p>Requests from the website and the phone. Write the quote, send the link, and the customer accepts online.</p></div>" + (Access.Can(c.U, AreaKey, 'E') ? "<button type=\"button\" class=\"aq-btn\" onclick=\"AdminQuotesJs.add()\">New request</button>" : string.Empty) + "</div>");
            sb.Append("<div class=\"aq-tools\"><div class=\"aq-chips\" id=\"aq-chips\">" + Chips(list, view) + "</div><input type=\"search\" id=\"aq-term\" placeholder=\"Search name, phone, email, ZIP or Q- number\" maxlength=\"60\" oninput=\"AdminQuotesJs.search()\" aria-label=\"Search quotes\"></div>");
            sb.Append("<div id=\"aq-list\">" + ListHtml(site, list, view, string.Empty, 1) + "</div>");
            response.SetElementContents("aq-main", sb.ToString());
            response.ExecuteScript("AdminQuotesJs.loaded('" + view + "');");
            string open = Ui.Clip(GetDataValue("show"), 20);
            if (open.Length > 0 && list.Any(q => q.Id == open))
            {
                response.ExecuteScript("AdminQuotesJs.edit('" + Ui.Js(open) + "');");
            }
            return response;
        }

        private static string Chips(List<QuoteRec> list, string view)
        {
            StringBuilder sb = new StringBuilder();
            foreach ((string key, string name) in Views)
            {
                int n = list.Count(q => InView(q, key));
                sb.Append("<button type=\"button\" class=\"aq-chip" + (key == view ? " aq-act" : string.Empty) + "\" data-v=\"" + key + "\" onclick=\"AdminQuotesJs.view('" + key + "')\">" + name + (key != "all" && n > 0 ? "<i>" + n + "</i>" : string.Empty) + "</button>");
            }
            return sb.ToString();
        }

        private static string ListHtml(SiteData site, List<QuoteRec> all, string view, string term, int page)
        {
            string t = term.ToLowerInvariant();
            string digits = Work.Digits(term);
            List<QuoteRec> rows = all.Where(q => InView(q, view) && (t.Length == 0 || q.Id.ToLowerInvariant().Contains(t) || q.Name.ToLowerInvariant().Contains(t) || q.Email.ToLowerInvariant().Contains(t) || q.Zip.Contains(t) || q.Town.ToLowerInvariant().Contains(t) || (digits.Length >= 3 && Work.Digits(q.Phone).Contains(digits)))).OrderByDescending(q => q.Created).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"aq-panel aq-empty\">" + (all.Count == 0 ? "No quote requests yet. They appear here when customers use the quote form, or when you add one with New request." : "No quotes match.") + "</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"aq-wrapt\"><table class=\"aq-table\"><thead><tr><th>Quote</th><th>Customer</th><th>Services</th><th>Status</th><th class=\"aq-num\">Total</th></tr></thead><tbody>");
            foreach (QuoteRec q in rows.Skip((page - 1) * Size).Take(Size))
            {
                sb.Append("<tr class=\"aq-row\" tabindex=\"0\" onclick=\"AdminQuotesJs.edit('" + q.Id + "')\" onkeydown=\"if(event.key==='Enter'){AdminQuotesJs.edit('" + q.Id + "')}\">");
                sb.Append("<td data-l=\"Quote\"><b>" + q.Id + "</b><small>" + Ui.Esc(Ui.When(q.Created)) + "</small></td>");
                sb.Append("<td data-l=\"Customer\"><b>" + Ui.Esc(q.Name) + "</b><small>" + Ui.Esc(q.Town.Length > 0 ? q.Town + " " + q.Zip : q.Zip) + (q.InArea ? string.Empty : " &middot; outside area") + "</small></td>");
                sb.Append("<td data-l=\"Services\">" + Ui.Esc(Work.ServiceNames(site, q.Services)) + (q.Photos.Count > 0 ? "<small>" + Ui.Count(q.Photos.Count, "photo", "photos") + "</small>" : string.Empty) + "</td>");
                sb.Append("<td data-l=\"Status\">" + Work.QuotePill("aq", q.Status) + (q.Status == "sent" && q.Expires.HasValue ? "<small>until " + q.Expires.Value.ToLocalTime().ToString("MMM d", CultureInfo.GetCultureInfo("en-US")) + "</small>" : string.Empty) + "</td>");
                sb.Append("<td data-l=\"Total\" class=\"aq-num\">" + (q.Lines.Count > 0 ? Ui.Money(q.Total) : "&ndash;") + "</td></tr>");
            }
            sb.Append("</tbody></table></div>" + Ui.Pager("aq", page, Size, rows.Count, "quote", "quotes", "AdminQuotesJs.page"));
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
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "list quotes", string.Empty, "You do not have access to quotes.");
            }
            await Refresh(response);
            return response;
        }

        private async Task Refresh(ApiResponse response)
        {
            SiteData site = await Store.LoadSite();
            List<QuoteRec> list = await Fresh();
            string view = ViewKey(GetDataValue("st"));
            response.SetElementContents("aq-list", ListHtml(site, list, view, Ui.Clip(GetDataValue("term"), 60), Ui.PageNo(GetDataValue("pg"))));
            response.SetElementContents("aq-chips", Chips(list, view));
        }

        private static string Kv(string label, string value)
        {
            return "<dt>" + label + "</dt><dd>" + value + "</dd>";
        }

        private static string LineRow(LineItem l, bool ro)
        {
            string d = ro ? " disabled" : string.Empty;
            return "<div class=\"aq-ln\"><input type=\"text\" class=\"aq-lt\" data-f=\"t\" maxlength=\"200\" value=\"" + Ui.Esc(l.Text) + "\" placeholder=\"Work or part\" aria-label=\"Description\"" + d + "><input type=\"number\" class=\"aq-lq\" data-f=\"q\" min=\"0\" step=\"0.25\" value=\"" + Ui.Num(l.Qty) + "\" aria-label=\"Quantity\"" + d + "><input type=\"number\" class=\"aq-lp\" data-f=\"p\" min=\"0\" step=\"0.01\" value=\"" + l.Price.ToString("0.00", CultureInfo.InvariantCulture) + "\" aria-label=\"Unit price\"" + d + ">" + (ro ? string.Empty : "<button type=\"button\" class=\"aq-ibtn aq-lx\" aria-label=\"Remove line\" onclick=\"AdminQuotesJs.dropLine(this)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M6 6l12 12M18 6L6 18\"/></svg></button>") + "</div>";
        }

        private static string TotalsHtml(decimal sub, decimal rate, decimal tax, decimal total)
        {
            return "<div><span>Subtotal (with trip fee)</span><b>" + Ui.Money(sub) + "</b></div><div><span>Tax " + Ui.Num(rate) + "%</span><b>" + Ui.Money(tax) + "</b></div><div class=\"aq-qtot-g\"><span>Total</span><b>" + Ui.Money(total) + "</b></div>";
        }

        private static string FormHtml(SiteData site, QuoteRec q, Ctx c)
        {
            CultureInfo us = CultureInfo.GetCultureInfo("en-US");
            bool canE = Access.Can(c.U, AreaKey, 'E');
            bool canX = Access.Can(c.U, AreaKey, 'X');
            bool editable = canE && q.Status is "new" or "draft";
            bool ro = !editable;
            StringBuilder sb = new StringBuilder("<div class=\"aq-stack\" id=\"aq-form\" data-id=\"" + q.Id + "\">");
            sb.Append("<div class=\"aq-qtop\">" + Work.QuotePill("aq", q.Status) + "<span>Received " + Ui.Esc(Ui.When(q.Created)) + " &middot; " + (q.Source == "web" ? "website" : Ui.Esc(Channels.FirstOrDefault(x => x.Key == q.Source).Name ?? q.Source)) + "</span></div>");
            if (q.Status == "sent")
            {
                sb.Append("<div class=\"aq-note\">Sent " + (q.SentAt.HasValue ? Ui.Esc(Ui.When(q.SentAt.Value)) : string.Empty) + ". Valid until " + (q.Expires.HasValue ? q.Expires.Value.ToLocalTime().ToString("MMMM d, yyyy", us) : "-") + ". The customer accepts with the link and the last 4 digits of " + Ui.Esc(q.Phone) + ".</div>");
                sb.Append(LinkBox(site, q));
            }
            else if (q.Status == "accepted")
            {
                sb.Append("<div class=\"aq-note aq-note-ok\">Accepted " + (q.AnsweredAt.HasValue ? Ui.Esc(Ui.When(q.AnsweredAt.Value)) : string.Empty) + (q.AcceptName.Length > 0 ? " by " + Ui.Esc(q.AcceptName) : string.Empty) + "." + (q.JobId.Length > 0 ? " Job " + Ui.Esc(q.JobId) + " is booked." : " Ready to schedule.") + "</div>");
            }
            else if (q.Status == "declined")
            {
                sb.Append("<div class=\"aq-note\">Declined " + (q.AnsweredAt.HasValue ? Ui.Esc(Ui.When(q.AnsweredAt.Value)) : string.Empty) + (q.DeclineWhy.Length > 0 ? ": &ldquo;" + Ui.Esc(q.DeclineWhy) + "&rdquo;" : ".") + "</div>");
            }
            else if (q.Status == "expired")
            {
                sb.Append("<div class=\"aq-note\">The customer did not answer in time. Reopen it as a draft to send a fresh quote.</div>");
            }
            sb.Append("<div class=\"aq-sect\">Customer</div><div class=\"aq-grid2\">");
            sb.Append(Field("name", "Name", q.Name, 80, "text", ro));
            sb.Append(Field("phone", "Phone", q.Phone, 24, "tel", ro));
            sb.Append(Field("email", "Email", q.Email, 80, "email", ro));
            sb.Append(Field("zip", "ZIP code", q.Zip, 5, "text", ro));
            sb.Append("</div>" + Field("address", "Street address", q.Address, 160, "text", ro));
            sb.Append("<p class=\"aq-hint\">" + (q.InArea ? "In the service area: " + Ui.Esc(q.Town) + "." : "<b>Outside the service area.</b> Check before quoting.") + "</p>");
            sb.Append("<div class=\"aq-sect\">The job</div><dl class=\"aq-kv\">");
            sb.Append(Kv("Services", Ui.Esc(Work.ServiceNames(site, q.Services))));
            sb.Append(Kv("Description", Ui.Esc(q.Description).Replace("\n", "<br>")));
            if (q.Days.Count > 0)
            {
                sb.Append(Kv("Preferred days", string.Join(", ", q.Days.Select(d => Ui.Esc(Ui.Day(d))))));
            }
            if (q.Times.Count > 0)
            {
                sb.Append(Kv("Preferred times", string.Join(", ", q.Times.Select(t => Ui.Esc(Work.TimeName(t))))));
            }
            sb.Append("</dl>");
            if (q.Photos.Count > 0)
            {
                sb.Append("<div class=\"aq-qph\">" + string.Concat(q.Photos.Select((p, i) => "<a href=\"" + Ui.Esc(p) + "\" target=\"_blank\" rel=\"noopener\"><img src=\"" + Ui.Esc(p) + "\" alt=\"Customer photo " + (i + 1) + "\" loading=\"lazy\"></a>")) + "</div>");
            }
            sb.Append("<div class=\"aq-sect\">Quote</div>");
            sb.Append("<div class=\"aq-lnh\"><span>Description</span><span>Qty</span><span>Unit price</span><span></span></div><div class=\"aq-lines\" id=\"aq-lines\">");
            List<LineItem> shown = editable && q.Lines.Count == 0 ? q.Services.Select(id => site.Services.FirstOrDefault(x => x.Id == id)).Where(x => x != null).Select(x => new LineItem { Text = x!.Name, Qty = 1, Price = x.From }).ToList() : q.Lines;
            foreach (LineItem l in shown)
            {
                sb.Append(LineRow(l, ro));
            }
            sb.Append("</div>");
            if (!ro)
            {
                sb.Append("<template id=\"aq-lntpl\">" + LineRow(new LineItem { Qty = 1 }, false) + "</template><div class=\"aq-lnadd\"><button type=\"button\" class=\"aq-btn aq-btn-ghost aq-btn-sm\" onclick=\"AdminQuotesJs.addLine()\">+ Add a line</button><select id=\"aq-svcadd\" aria-label=\"Add a line from the price list\" onchange=\"AdminQuotesJs.addService(this)\"><option value=\"\">+ Add from the price list</option>");
                foreach (Trade t in site.Trades)
                {
                    sb.Append("<optgroup label=\"" + Ui.Esc(t.Name) + "\">");
                    foreach (ServiceItem s in site.Services.Where(s => s.Trade == t.Key && s.Active))
                    {
                        sb.Append("<option value=\"" + Ui.Esc(s.Id) + "\" data-n=\"" + Ui.Esc(s.Name) + "\" data-p=\"" + s.From.ToString("0.00", CultureInfo.InvariantCulture) + "\">" + Ui.Esc(s.Name) + " (" + Ui.Money(s.From) + ")</option>");
                    }
                    sb.Append("</optgroup>");
                }
                sb.Append("</select></div>");
            }
            else if (q.Lines.Count == 0)
            {
                sb.Append("<p class=\"aq-hint\">No lines yet.</p>");
            }
            decimal rate = editable ? site.TaxRate : q.TaxRate;
            (decimal sub, decimal tax, decimal total) = Work.Totals(shown, q.TripFee, rate);
            sb.Append("<div class=\"aq-qfoot\"><label class=\"aq-field aq-trip\" for=\"aq-trip\"><span>Trip fee</span><input type=\"number\" id=\"aq-trip\" min=\"0\" step=\"0.01\" value=\"" + q.TripFee.ToString("0.00", CultureInfo.InvariantCulture) + "\"" + (ro ? " disabled" : string.Empty) + "><small>From the ZIP code zone: " + Ui.Money(Work.TripFee(site, q.Zip)) + "</small></label><div class=\"aq-qtot\" id=\"aq-qtot\">" + TotalsHtml(sub, rate, tax, total) + "</div></div>");
            sb.Append("<em class=\"aq-err\" id=\"aq-e-lines\"></em>");
            sb.Append("<label class=\"aq-field\" for=\"aq-note\"><span>Note to the customer (shown on the quote)</span><textarea id=\"aq-note\" rows=\"3\" maxlength=\"1000\"" + (ro ? " disabled" : string.Empty) + ">" + Ui.Esc(q.Note) + "</textarea></label>");
            sb.Append("<label class=\"aq-field\" for=\"aq-snote\"><span>Staff note (not shown to the customer)</span><textarea id=\"aq-snote\" rows=\"2\" maxlength=\"1000\"" + (canE ? string.Empty : " disabled") + ">" + Ui.Esc(q.StaffNote) + "</textarea><small>Job details only. Do not store card numbers or other sensitive data.</small></label>");
            sb.Append("<div class=\"aq-acts\">");
            if (editable)
            {
                sb.Append("<button type=\"button\" class=\"aq-btn aq-btn-ghost\" onclick=\"AdminQuotesJs.save(0)\">Save draft</button>");
                if (canX)
                {
                    sb.Append("<button type=\"button\" class=\"aq-btn\" onclick=\"AdminQuotesJs.save(1)\">Send to customer</button>");
                }
                sb.Append("<button type=\"button\" class=\"aq-btn aq-btn-danger aq-push\" onclick=\"AdminQuotesJs.act('close','" + q.Id + "')\">Close request</button>");
            }
            else if (canE)
            {
                sb.Append("<button type=\"button\" class=\"aq-btn aq-btn-ghost\" onclick=\"AdminQuotesJs.saveNote()\">Save staff note</button>");
            }
            if (q.Status == "sent" && canX)
            {
                sb.Append("<button type=\"button\" class=\"aq-btn aq-btn-ghost\" onclick=\"AdminQuotesJs.act('phone','" + q.Id + "')\">Customer accepted by phone</button><button type=\"button\" class=\"aq-btn aq-btn-danger aq-push\" onclick=\"AdminQuotesJs.act('withdraw','" + q.Id + "')\">Withdraw and edit</button>");
            }
            if (q.Status == "accepted" && q.JobId.Length == 0 && Access.Can(c.U, "jobs", 'E') && c.Own.Length == 0)
            {
                sb.Append("<a class=\"aq-btn\" href=\"AdminJobs?from=" + q.Id + "\">Schedule the job</a>");
            }
            if (q.Status == "accepted" && q.JobId.Length > 0 && Access.Can(c.U, "jobs", 'V'))
            {
                sb.Append("<a class=\"aq-btn aq-btn-ghost\" href=\"AdminJobs?show=" + Ui.Esc(q.JobId) + "\">Open job " + Ui.Esc(q.JobId) + "</a>");
            }
            if (canE && q.Status is "declined" or "expired" or "closed")
            {
                sb.Append("<button type=\"button\" class=\"aq-btn aq-btn-ghost\" onclick=\"AdminQuotesJs.act('reopen','" + q.Id + "')\">Reopen as draft</button>");
            }
            sb.Append("<button type=\"button\" class=\"aq-btn aq-btn-ghost" + (editable || q.Status == "sent" ? string.Empty : " aq-push") + "\" onclick=\"AdminQuotesJs.closeModal()\">Close</button></div></div>");
            return sb.ToString();
        }

        private static string LinkBox(SiteData site, QuoteRec q)
        {
            string first = q.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? q.Name;
            string mail = "Hello " + first + ",\n\nThank you for asking " + site.Brand + " for a quote. Your written quote " + q.Id + " is ready:\n\n{link}\n\nThe total is " + Ui.Money(q.Total) + " including tax. To accept it, open the link and type the last 4 digits of your phone number. The quote is valid until " + (q.Expires.HasValue ? q.Expires.Value.ToLocalTime().ToString("MMMM d, yyyy", CultureInfo.GetCultureInfo("en-US")) : "the date shown") + ".\n\nQuestions? Call us on " + site.Phone + ".\n\n" + site.Brand;
            return "<div class=\"aq-linkbox\"><label class=\"aq-field\" for=\"aq-link\"><span>Link for the customer</span><span class=\"aq-copyrow\"><input type=\"text\" id=\"aq-link\" readonly data-key=\"" + q.Key + "\" value=\"QuoteView?k=" + q.Key + "\"><button type=\"button\" class=\"aq-btn aq-btn-ghost aq-btn-sm\" onclick=\"AdminQuotesJs.copy('aq-link')\">Copy</button></span></label><label class=\"aq-field\" for=\"aq-mail\"><span>Email to send (copy into your mail program)</span><textarea id=\"aq-mail\" rows=\"7\" readonly>" + Ui.Esc(mail) + "</textarea></label><div class=\"aq-acts\"><button type=\"button\" class=\"aq-btn aq-btn-ghost aq-btn-sm\" onclick=\"AdminQuotesJs.copy('aq-mail')\">Copy email text</button>" + (q.Email.Length > 0 ? "<a class=\"aq-btn aq-btn-ghost aq-btn-sm\" id=\"aq-mailto\" data-to=\"" + Ui.Esc(q.Email) + "\" data-sub=\"" + Ui.Esc("Your quote " + q.Id + " from " + site.Brand) + "\" href=\"mailto:" + Ui.Esc(q.Email) + "\">Open in mail program</a>" : string.Empty) + "<a class=\"aq-btn aq-btn-ghost aq-btn-sm\" href=\"QuoteView?k=" + q.Key + "\" target=\"_blank\" rel=\"noopener\">Preview as customer</a></div></div>";
        }

        private static string Field(string key, string label, string value, int max, string type, bool ro)
        {
            return "<label class=\"aq-field\" for=\"aq-f-" + key + "\"><span>" + label + "</span><input type=\"" + type + "\" id=\"aq-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + (ro ? " disabled" : string.Empty) + "><em class=\"aq-err\" id=\"aq-e-" + key + "\"></em></label>";
        }

        public async Task<ApiResponse> Edit()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 20);
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "open quote", id, "You do not have access to quotes.");
            }
            QuoteRec? q = (await Fresh()).FirstOrDefault(x => x.Id == id);
            if (q == null)
            {
                response.ExecuteScript("AdminQuotesJs.toast('That quote was not found.');");
                return response;
            }
            SiteData site = await Store.LoadSite();
            response.SetElementContents("aq-mbody", FormHtml(site, q, c));
            response.ExecuteScript("AdminQuotesJs.opened('" + Ui.Js("Quote " + q.Id + " · " + q.Name) + "');");
            return response;
        }

        private static List<LineItem>? ParseLines(string? json, out string error)
        {
            error = string.Empty;
            List<LineItem> list = new List<LineItem>();
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json))
                {
                    if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    {
                        error = "The lines could not be read.";
                        return null;
                    }
                    foreach (JsonElement e in doc.RootElement.EnumerateArray())
                    {
                        string t = e.TryGetProperty("t", out JsonElement te) && te.ValueKind == JsonValueKind.String ? (te.GetString() ?? string.Empty).Trim() : string.Empty;
                        string qs = e.TryGetProperty("q", out JsonElement qe) && qe.ValueKind == JsonValueKind.String ? qe.GetString() ?? string.Empty : string.Empty;
                        string ps = e.TryGetProperty("p", out JsonElement pe) && pe.ValueKind == JsonValueKind.String ? pe.GetString() ?? string.Empty : string.Empty;
                        if (t.Length == 0 && qs.Trim().Length == 0 && ps.Trim().Length == 0)
                        {
                            continue;
                        }
                        decimal? qty = Work.Amount(qs, 0.01m, 9999m);
                        decimal? price = Work.Amount(ps, 0m, 100000m);
                        if (t.Length == 0 || t.Length > 200)
                        {
                            error = "Every line needs a description of up to 200 characters.";
                            return null;
                        }
                        if (qty == null)
                        {
                            error = "Check the quantity on “" + t + "”. Use a number from 0.01 to 9,999.";
                            return null;
                        }
                        if (price == null)
                        {
                            error = "Check the price on “" + t + "”. Use a number from 0 to 100,000.";
                            return null;
                        }
                        list.Add(new LineItem { Text = t, Qty = qty.Value, Price = price.Value });
                    }
                }
            }
            catch (JsonException)
            {
                error = "The lines could not be read.";
                return null;
            }
            if (list.Count > 40)
            {
                error = "A quote can have up to 40 lines.";
                return null;
            }
            return list;
        }

        public async Task<ApiResponse> Calc()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null || !Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            List<LineItem>? lines = ParseLines(GetDataValue("lines"), out string error);
            decimal trip = Work.Amount(GetDataValue("trip"), 0, 10000) ?? 0;
            response.SetElementContents("aq-e-lines", Ui.Esc(error));
            if (lines != null)
            {
                SiteData site = await Store.LoadSite();
                (decimal sub, decimal tax, decimal total) = Work.Totals(lines, trip, site.TaxRate);
                response.SetElementContents("aq-qtot", TotalsHtml(sub, site.TaxRate, tax, total));
            }
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
            string id = Ui.Clip(GetDataValue("id"), 20);
            bool send = GetDataValue("send") == "1";
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "save quote", id, "You can look at quotes but not change them.");
            }
            if (send && !Access.Can(c.U, AreaKey, 'X'))
            {
                return await Refuse(response, c, "send quote", id, "You are not allowed to send quotes.");
            }
            string name = Ui.Clip(GetDataValue("name"), 200);
            string phone = Ui.Clip(GetDataValue("phone"), 40);
            string email = Ui.Clip(GetDataValue("email"), 200);
            string zip = Ui.Clip(GetDataValue("zip"), 10);
            string address = Ui.Clip(GetDataValue("address"), 300);
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                { "name", name.Length < 2 || name.Length > 80 ? "Enter the customer's name." : string.Empty },
                { "phone", !Work.IsPhone(phone) ? "Enter a phone number with area code. The customer needs its last 4 digits." : string.Empty },
                { "email", email.Length > 0 && !Work.IsEmail(email) ? "Enter a valid email address or leave it empty." : string.Empty },
                { "zip", !Regex.IsMatch(zip, "^[0-9]{5}$") ? "Enter a 5-digit ZIP code." : string.Empty },
                { "address", address.Length < 5 || address.Length > 160 ? "Enter the street address." : string.Empty }
            };
            List<LineItem>? lines = ParseLines(GetDataValue("lines"), out string lineErr);
            decimal? trip = Work.Amount(GetDataValue("trip"), 0, 10000);
            if (lineErr.Length == 0 && trip == null)
            {
                lineErr = "Enter a trip fee from 0 to 10,000.";
            }
            if (lineErr.Length == 0 && send && (lines == null || lines.Count == 0))
            {
                lineErr = "Add at least one line before you send the quote.";
            }
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("aq-e-" + kv.Key, kv.Value);
            }
            response.SetElementContents("aq-e-lines", Ui.Esc(lineErr));
            if (e.Values.Any(v => v.Length > 0) || lineErr.Length > 0 || lines == null || trip == null)
            {
                response.ExecuteScript("AdminQuotesJs.toast('Please check the marked fields.');");
                return response;
            }
            SiteData site = await Store.LoadSite();
            string error = string.Empty;
            QuoteRec? done = null;
            using (await Store.Hold())
            {
                List<QuoteRec> list = await Store.Quotes();
                QuoteRec? q = list.FirstOrDefault(x => x.Id == id);
                if (q == null)
                {
                    error = "That quote was not found.";
                }
                else if (q.Status is not ("new" or "draft"))
                {
                    error = "This quote was already sent or answered. Reload it.";
                }
                else
                {
                    ZipArea? area = site.Zips.FirstOrDefault(x => x.Zip == zip);
                    q.Name = name;
                    q.Phone = phone;
                    q.Email = email;
                    q.Zip = zip;
                    q.Address = address;
                    q.Town = area?.Town ?? string.Empty;
                    q.InArea = area != null;
                    q.Lines = lines;
                    q.TripFee = trip.Value;
                    q.TaxRate = site.TaxRate;
                    (q.Subtotal, q.Tax, q.Total) = Work.Totals(lines, trip.Value, site.TaxRate);
                    q.Note = Ui.Clip(GetDataValue("note"), 1000);
                    q.StaffNote = Ui.Clip(GetDataValue("snote"), 1000);
                    q.By = c.U.Id;
                    q.Status = "draft";
                    if (send)
                    {
                        if (q.Key.Length == 0)
                        {
                            q.Key = Access.NewToken(16);
                        }
                        q.Status = "sent";
                        q.SentAt = DateTime.UtcNow;
                        q.Expires = DateTime.Today.AddDays(site.QuoteDays + 1).ToUniversalTime().AddSeconds(-1);
                        q.AnsweredAt = null;
                    }
                    await Store.SaveQuotes(list);
                    done = q;
                }
            }
            if (done == null)
            {
                response.ExecuteScript("AdminQuotesJs.toast('" + Ui.Js(error) + "');");
                return response;
            }
            await Store.Log(c.U.Id, send ? "send quote" : "save quote", done.Id + " · " + done.Name + " · " + Ui.Money(done.Total), true);
            await Refresh(response);
            response.SetElementContents("aq-mbody", FormHtml(site, done, c));
            response.ExecuteScript("AdminQuotesJs.saved('" + (send ? "Quote sent. Copy the link or email text to the customer." : "Draft saved") + "');");
            return response;
        }

        public async Task<ApiResponse> SaveNote()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 20);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "save quote note", id, "You can look at quotes but not change them.");
            }
            bool found = false;
            using (await Store.Hold())
            {
                List<QuoteRec> list = await Store.Quotes();
                QuoteRec? q = list.FirstOrDefault(x => x.Id == id);
                if (q != null)
                {
                    q.StaffNote = Ui.Clip(GetDataValue("snote"), 1000);
                    await Store.SaveQuotes(list);
                    found = true;
                }
            }
            if (found)
            {
                await Store.Log(c.U.Id, "save quote note", id, true);
            }
            response.ExecuteScript("AdminQuotesJs.clean();AdminQuotesJs.toast('" + (found ? "Staff note saved" : "That quote was not found.") + "');");
            return response;
        }

        public async Task<ApiResponse> Act()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 20);
            string act = (GetDataValue("act") ?? string.Empty).Trim();
            string why = Ui.Clip(GetDataValue("why"), 300);
            char need = act is "withdraw" or "phone" ? 'X' : 'E';
            if (!(act is "close" or "reopen" or "withdraw" or "phone"))
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, need))
            {
                return await Refuse(response, c, act + " quote", id, need == 'X' ? "You are not allowed to do that with sent quotes." : "You can look at quotes but not change them.");
            }
            string error = string.Empty;
            QuoteRec? done = null;
            using (await Store.Hold())
            {
                List<QuoteRec> list = await Store.Quotes();
                Work.ExpireQuotes(list);
                QuoteRec? q = list.FirstOrDefault(x => x.Id == id);
                if (q == null)
                {
                    error = "That quote was not found.";
                }
                else if (act == "close" && q.Status is "new" or "draft")
                {
                    q.Status = "closed";
                    q.StaffNote = (q.StaffNote + (why.Length > 0 ? "\nClosed: " + why : string.Empty)).Trim();
                    done = q;
                }
                else if (act == "reopen" && q.Status is "declined" or "expired" or "closed")
                {
                    q.Status = "draft";
                    q.Expires = null;
                    q.AnsweredAt = null;
                    q.DeclineWhy = string.Empty;
                    done = q;
                }
                else if (act == "withdraw" && q.Status == "sent")
                {
                    q.Status = "draft";
                    q.Expires = null;
                    done = q;
                }
                else if (act == "phone" && q.Status == "sent")
                {
                    if (why.Length < 2)
                    {
                        error = "Type the name of the person who accepted.";
                    }
                    else
                    {
                        q.Status = "accepted";
                        q.AnsweredAt = DateTime.UtcNow;
                        q.AcceptName = why + " (by phone, noted by " + c.U.Name + ")";
                        done = q;
                    }
                }
                else
                {
                    error = "That is not possible for a quote in this state.";
                }
                if (done != null)
                {
                    await Store.SaveQuotes(list);
                }
            }
            if (done == null)
            {
                response.ExecuteScript("AdminQuotesJs.toast('" + Ui.Js(error) + "');");
                return response;
            }
            string label = act switch { "close" => "close quote", "reopen" => "reopen quote", "withdraw" => "withdraw quote", _ => "accept quote by phone" };
            await Store.Log(c.U.Id, label, done.Id + " · " + done.Name, true);
            SiteData site = await Store.LoadSite();
            await Refresh(response);
            response.SetElementContents("aq-mbody", FormHtml(site, done, c));
            response.ExecuteScript("AdminQuotesJs.saved('" + Ui.Js(act switch { "close" => "Request closed", "reopen" => "Reopened as a draft", "withdraw" => "Quote withdrawn. The customer link shows it is being updated.", _ => "Marked as accepted" }) + "');");
            return response;
        }

        public async Task<ApiResponse> AddForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "new request", string.Empty, "You can look at quotes but not add them.");
            }
            SiteData site = await Store.LoadSite();
            StringBuilder sb = new StringBuilder("<form class=\"aq-stack\" id=\"aq-nform\" onsubmit=\"return AdminQuotesJs.create()\" novalidate><p class=\"aq-hint\">For requests that come in by phone, email or in person.</p><div class=\"aq-grid2\">");
            sb.Append(Field("name", "Name", string.Empty, 80, "text", false) + Field("phone", "Phone", string.Empty, 24, "tel", false) + Field("email", "Email (optional)", string.Empty, 80, "email", false) + Field("zip", "ZIP code", string.Empty, 5, "text", false));
            sb.Append("</div>" + Field("address", "Street address", string.Empty, 160, "text", false));
            sb.Append("<label class=\"aq-field\" for=\"aq-f-source\"><span>How did it come in?</span><select id=\"aq-f-source\" data-k=\"source\">" + string.Concat(Channels.Select(x => "<option value=\"" + x.Key + "\">" + x.Name + "</option>")) + "</select></label>");
            sb.Append("<fieldset class=\"aq-fs\"><legend>Services</legend>");
            foreach (Trade t in site.Trades)
            {
                sb.Append("<div class=\"aq-svgrp\"><b>" + Ui.Esc(t.Name) + "</b><div class=\"aq-checks\">" + string.Concat(site.Services.Where(s => s.Trade == t.Key && s.Active).Select(s => "<label class=\"aq-check\"><input type=\"checkbox\" name=\"aq-nsvc\" value=\"" + Ui.Esc(s.Id) + "\">" + Ui.Esc(s.Name) + "</label>")) + "</div></div>");
            }
            sb.Append("<em class=\"aq-err\" id=\"aq-e-svc\"></em></fieldset>");
            sb.Append("<label class=\"aq-field\" for=\"aq-f-desc\"><span>What does the customer need?</span><textarea id=\"aq-f-desc\" data-k=\"desc\" rows=\"4\" maxlength=\"2000\"></textarea><em class=\"aq-err\" id=\"aq-e-desc\"></em></label>");
            sb.Append("<div class=\"aq-acts\"><button type=\"submit\" class=\"aq-btn\">Add request</button><button type=\"button\" class=\"aq-btn aq-btn-ghost\" onclick=\"AdminQuotesJs.closeModal()\">Cancel</button></div></form>");
            response.SetElementContents("aq-mbody", sb.ToString());
            response.ExecuteScript("AdminQuotesJs.opened('New request');");
            return response;
        }

        public async Task<ApiResponse> Create()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "new request", string.Empty, "You can look at quotes but not add them.");
            }
            SiteData site = await Store.LoadSite();
            string name = Ui.Clip(GetDataValue("name"), 200);
            string phone = Ui.Clip(GetDataValue("phone"), 40);
            string email = Ui.Clip(GetDataValue("email"), 200);
            string zip = Ui.Clip(GetDataValue("zip"), 10);
            string address = Ui.Clip(GetDataValue("address"), 300);
            string desc = (GetDataValue("desc") ?? string.Empty).Trim();
            string source = Channels.Any(x => x.Key == GetDataValue("source")) ? GetDataValue("source") : "phone";
            List<string> services = (GetDataValue("services") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().Where(x => site.Services.Any(s => s.Id == x && s.Active)).ToList();
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                { "name", name.Length < 2 || name.Length > 80 ? "Enter the customer's name." : string.Empty },
                { "phone", !Work.IsPhone(phone) ? "Enter a phone number with area code." : string.Empty },
                { "email", email.Length > 0 && !Work.IsEmail(email) ? "Enter a valid email address or leave it empty." : string.Empty },
                { "zip", !Regex.IsMatch(zip, "^[0-9]{5}$") ? "Enter a 5-digit ZIP code." : string.Empty },
                { "address", address.Length < 5 || address.Length > 160 ? "Enter the street address." : string.Empty },
                { "svc", services.Count == 0 ? "Pick at least one service." : string.Empty },
                { "desc", desc.Length < 5 || desc.Length > 2000 ? "Describe the job in 5 to 2,000 characters." : string.Empty }
            };
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("aq-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0))
            {
                return response;
            }
            ZipArea? area = site.Zips.FirstOrDefault(x => x.Zip == zip);
            QuoteRec q = new QuoteRec { Status = "new", Created = DateTime.UtcNow, Source = source, Name = name, Phone = phone, Email = email, Zip = zip, Address = address, Town = area?.Town ?? string.Empty, InArea = area != null, Services = services, Description = desc, TripFee = Work.TripFee(site, zip), TaxRate = site.TaxRate, By = c.U.Id };
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
                list.Insert(0, q);
                await Store.SaveQuotes(list);
            }
            await Store.Log(c.U.Id, "new request", q.Id + " · " + q.Name, true);
            await Refresh(response);
            response.SetElementContents("aq-mbody", FormHtml(site, q, c));
            response.ExecuteScript("AdminQuotesJs.saved('Request added. Write the quote below.');AdminQuotesJs.opened('" + Ui.Js("Quote " + q.Id + " · " + q.Name) + "');");
            return response;
        }
    }
}
