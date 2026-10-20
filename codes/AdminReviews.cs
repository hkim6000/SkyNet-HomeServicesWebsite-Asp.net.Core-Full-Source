using System.Globalization;
using System.Text;
using HomeServices.Core;
using HomeServices.Models;
using SkyNet;

namespace HomeServices.codes
{
    public class AdminReviews : WebPage
    {
        private const string AreaKey = "reviews";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Reviews | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminReviewsJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminReviewsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("ar-brand", Ui.Esc(site.Brand));
            response.SetElementContents("ar-menu", await Auth.Menu("ar", "AdminReviews", c));
            response.SetElementContents("ar-who", Auth.Who("ar", c));
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"ar-panel\"><h1>No access</h1><p class=\"ar-hint\">You do not have access to " + what + ". Ask a manager if you need it.</p></div>";
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
            response.ExecuteScript("AdminReviewsJs.expired();");
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
                response.SetElementContents("ar-main", NoAccess("reviews"));
                return response;
            }
            response.SetElementContents("ar-main", "<div class=\"ar-ph\"><div><h1>Reviews</h1><p>Write a review only from feedback a real customer gave you after a job, and only with their permission. Never invent one. Only published reviews show on the website.</p></div>" + (Access.Can(c.U, AreaKey, 'E') ? "<button type=\"button\" class=\"ar-btn\" onclick=\"AdminReviewsJs.edit('')\">Write up feedback</button>" : string.Empty) + "</div><div id=\"ar-list\">" + ListHtml(site, await Store.Reviews(), c) + "</div>");
            return response;
        }

        private static string Stars(int n)
        {
            return "<span class=\"ar-stars\" aria-label=\"" + n + " out of 5\">" + new string('★', n) + "<i>" + new string('★', 5 - n) + "</i></span>";
        }

        private static string ListHtml(SiteData site, List<ReviewRec> list, Ctx c)
        {
            if (list.Count == 0)
            {
                return "<div class=\"ar-panel ar-empty\">No reviews yet. When a customer tells you how a job went, write it up here, ask if you may publish it, then publish.</div>";
            }
            bool canE = Access.Can(c.U, AreaKey, 'E');
            bool canX = Access.Can(c.U, AreaKey, 'X');
            StringBuilder sb = new StringBuilder("<div class=\"ar-revs\">");
            foreach (ReviewRec r in list.OrderBy(r => r.Published).ThenByDescending(r => r.Heard))
            {
                sb.Append("<article class=\"ar-rev\"><div class=\"ar-rev-t\">" + Stars(r.Rating) + (r.Published ? Work.Pill("ar", "go", "Published") : Work.Pill("ar", "wait", "Not published")) + "</div><p>" + Ui.Esc(r.Text) + "</p><small><b>" + Ui.Esc(r.Name) + "</b>" + (r.Town.Length > 0 ? ", " + Ui.Esc(r.Town) : string.Empty) + " &middot; " + Ui.Esc(Work.TradeName(site, r.Trade)) + "<br>Heard " + Ui.Esc(Ui.Day(r.Heard)) + " by " + Ui.Esc(Work.SourceName(r.Source).ToLowerInvariant()) + (r.JobId.Length > 0 ? " &middot; job " + Ui.Esc(r.JobId) : string.Empty) + " &middot; written up by " + Ui.Esc(r.By) + "</small><div class=\"ar-acts\">");
                if (canE && !r.Published)
                {
                    sb.Append("<button type=\"button\" class=\"ar-btn ar-btn-ghost ar-btn-sm\" onclick=\"AdminReviewsJs.edit('" + r.Id + "')\">Edit</button>");
                }
                if (canX)
                {
                    sb.Append(r.Published ? "<button type=\"button\" class=\"ar-btn ar-btn-ghost ar-btn-sm\" onclick=\"AdminReviewsJs.pub('" + r.Id + "',0)\">Unpublish</button>" : "<button type=\"button\" class=\"ar-btn ar-btn-sm\" onclick=\"AdminReviewsJs.pub('" + r.Id + "',1)\">Publish</button>");
                    sb.Append("<button type=\"button\" class=\"ar-btn ar-btn-danger ar-btn-sm ar-push\" onclick=\"AdminReviewsJs.drop('" + r.Id + "')\">Delete</button>");
                }
                sb.Append("</div></article>");
            }
            return sb.Append("</div>").ToString();
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
                return await Refuse(response, c, "write review", id, "You can look at reviews but not write them.");
            }
            SiteData site = await Store.LoadSite();
            ReviewRec? r = id.Length == 0 ? new ReviewRec { Heard = Work.Today(), Source = "phone", Trade = site.Trades.FirstOrDefault()?.Key ?? string.Empty } : (await Store.Reviews()).FirstOrDefault(x => x.Id == id && !x.Published);
            if (r == null)
            {
                response.ExecuteScript("AdminReviewsJs.toast('Unpublish the review before you change it.');");
                return response;
            }
            List<JobRec> done = (await Store.Jobs()).Where(j => j.Status == "done").OrderByDescending(j => j.Date).Take(200).ToList();
            StringBuilder sb = new StringBuilder("<form class=\"ar-stack\" id=\"ar-form\" onsubmit=\"return AdminReviewsJs.save('" + r.Id + "')\" novalidate>");
            sb.Append("<label class=\"ar-field\" for=\"ar-f-job\"><span>Job (optional)</span><select id=\"ar-f-job\" data-k=\"job\" onchange=\"AdminReviewsJs.fromJob(this)\"><option value=\"\">No job linked</option>" + string.Concat(done.Select(j => "<option value=\"" + j.Id + "\" data-n=\"" + Ui.Esc(ShortName(j.Name)) + "\" data-t=\"" + Ui.Esc(j.Town) + "\" data-tr=\"" + Ui.Esc(j.Trade) + "\"" + (j.Id == r.JobId ? " selected" : string.Empty) + ">" + Ui.Esc(Ui.Day(j.Date) + " · " + j.Name + " · " + j.Summary) + "</option>")) + "</select><small>Linking a finished job fills in the name, town and trade.</small></label><div class=\"ar-grid3\">");
            sb.Append("<label class=\"ar-field\" for=\"ar-f-name\"><span>Name to show</span><input type=\"text\" id=\"ar-f-name\" data-k=\"name\" maxlength=\"40\" value=\"" + Ui.Esc(r.Name) + "\" placeholder=\"First name and initial\"><em class=\"ar-err\" id=\"ar-e-name\"></em></label>");
            sb.Append("<label class=\"ar-field\" for=\"ar-f-town\"><span>Town</span><input type=\"text\" id=\"ar-f-town\" data-k=\"town\" maxlength=\"40\" value=\"" + Ui.Esc(r.Town) + "\"><em class=\"ar-err\" id=\"ar-e-town\"></em></label>");
            sb.Append("<label class=\"ar-field\" for=\"ar-f-trade\"><span>Trade</span><select id=\"ar-f-trade\" data-k=\"trade\">" + string.Concat(site.Trades.Select(t => "<option value=\"" + Ui.Esc(t.Key) + "\"" + (t.Key == r.Trade ? " selected" : string.Empty) + ">" + Ui.Esc(t.Name) + "</option>")) + "</select></label>");
            sb.Append("<label class=\"ar-field\" for=\"ar-f-rating\"><span>Rating the customer gave</span><select id=\"ar-f-rating\" data-k=\"rating\">" + string.Concat(Enumerable.Range(1, 5).Reverse().Select(n => "<option value=\"" + n + "\"" + (n == r.Rating ? " selected" : string.Empty) + ">" + n + " out of 5</option>")) + "</select></label>");
            sb.Append("<label class=\"ar-field\" for=\"ar-f-heard\"><span>Date of the feedback</span><input type=\"date\" id=\"ar-f-heard\" data-k=\"heard\" max=\"" + Work.Today() + "\" value=\"" + Ui.Esc(r.Heard) + "\"><em class=\"ar-err\" id=\"ar-e-heard\"></em></label>");
            sb.Append("<label class=\"ar-field\" for=\"ar-f-source\"><span>How it reached us</span><select id=\"ar-f-source\" data-k=\"source\">" + string.Concat(Work.Sources.Select(s => "<option value=\"" + s.Key + "\"" + (s.Key == r.Source ? " selected" : string.Empty) + ">" + s.Name + "</option>")) + "</select></label></div>");
            sb.Append("<label class=\"ar-field\" for=\"ar-f-text\"><span>The review, in the customer&rsquo;s own words as far as possible</span><textarea id=\"ar-f-text\" data-k=\"text\" rows=\"5\" maxlength=\"800\">" + Ui.Esc(r.Text) + "</textarea><em class=\"ar-err\" id=\"ar-e-text\"></em></label>");
            sb.Append("<label class=\"ar-check ar-real\"><input type=\"checkbox\" data-k=\"real\"> This is real feedback from this customer, and they agreed that we may publish it.</label><em class=\"ar-err\" id=\"ar-e-real\"></em>");
            sb.Append("<div class=\"ar-acts\"><button type=\"submit\" class=\"ar-btn\">Save (not published yet)</button><button type=\"button\" class=\"ar-btn ar-btn-ghost\" onclick=\"AdminReviewsJs.closeModal()\">Cancel</button></div></form>");
            response.SetElementContents("ar-mbody", sb.ToString());
            response.ExecuteScript("AdminReviewsJs.openModal('" + (r.Id.Length == 0 ? "Write up feedback" : "Edit review") + "');");
            return response;
        }

        private static string ShortName(string name)
        {
            string[] p = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return p.Length == 0 ? string.Empty : p.Length == 1 ? p[0] : p[0] + " " + char.ToUpperInvariant(p[^1][0]) + ".";
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
                return await Refuse(response, c, "save review", id, "You can look at reviews but not write them.");
            }
            SiteData site = await Store.LoadSite();
            string name = Ui.Clip(GetDataValue("name"), 100);
            string town = Ui.Clip(GetDataValue("town"), 100);
            string trade = site.Trades.Any(t => t.Key == GetDataValue("trade")) ? GetDataValue("trade") : string.Empty;
            int rating = int.TryParse(GetDataValue("rating"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rv) && rv >= 1 && rv <= 5 ? rv : 0;
            DateTime? heard = Work.ParseDay(GetDataValue("heard"));
            string source = Work.Sources.Any(s => s.Key == GetDataValue("source")) ? GetDataValue("source") : string.Empty;
            string text = (GetDataValue("text") ?? string.Empty).Trim();
            string job = Ui.Clip(GetDataValue("job"), 20);
            bool real = GetDataValue("real") == "1";
            List<JobRec> jobs = await Store.Jobs();
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                { "name", name.Length < 2 || name.Length > 40 ? "Enter the name to show, like Dana R." : string.Empty },
                { "town", town.Length > 40 ? "Keep it under 40 characters." : string.Empty },
                { "heard", heard == null ? "Enter the date of the feedback." : heard.Value > DateTime.Today ? "The date cannot be in the future." : string.Empty },
                { "text", text.Length < 20 ? "Write at least 20 characters." : text.Length > 800 ? "Keep it under 800 characters." : string.Empty },
                { "real", !real ? "Confirm that this is real feedback the customer agreed to publish." : string.Empty }
            };
            if (job.Length > 0 && !jobs.Any(j => j.Id == job && j.Status == "done"))
            {
                e["name"] = "The linked job was not found.";
            }
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("ar-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0) || rating == 0 || trade.Length == 0 || source.Length == 0 || heard == null)
            {
                return response;
            }
            List<ReviewRec> list;
            using (await Store.Hold())
            {
                list = await Store.Reviews();
                ReviewRec? r = id.Length == 0 ? null : list.FirstOrDefault(x => x.Id == id);
                if (r != null && r.Published)
                {
                    response.ExecuteScript("AdminReviewsJs.toast('Unpublish the review before you change it.');");
                    return response;
                }
                if (r == null)
                {
                    r = new ReviewRec { Id = "r-" + Access.NewToken(5), Created = DateTime.UtcNow };
                    list.Add(r);
                }
                r.Name = name;
                r.Town = town;
                r.Trade = trade;
                r.Rating = rating;
                r.Heard = Work.Iso(heard.Value);
                r.Source = source;
                r.Text = text;
                r.JobId = job;
                r.By = c.U.Name;
                await Store.SaveReviews(list);
            }
            await Store.Log(c.U.Id, id.Length == 0 ? "write review" : "edit review", name + " · " + rating + "/5", true);
            response.SetElementContents("ar-list", ListHtml(site, list, c));
            response.ExecuteScript("AdminReviewsJs.saved('Saved. Publish it when the customer has agreed.');");
            return response;
        }

        public async Task<ApiResponse> Pub()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 40);
            bool on = GetDataValue("on") == "1";
            if (!Access.Can(c.U, AreaKey, 'X'))
            {
                return await Refuse(response, c, on ? "publish review" : "unpublish review", id, "You are not allowed to publish reviews.");
            }
            List<ReviewRec> list;
            string name = string.Empty;
            using (await Store.Hold())
            {
                list = await Store.Reviews();
                ReviewRec? r = list.FirstOrDefault(x => x.Id == id);
                if (r != null)
                {
                    r.Published = on;
                    name = r.Name;
                    await Store.SaveReviews(list);
                }
            }
            if (name.Length > 0)
            {
                await Store.Log(c.U.Id, on ? "publish review" : "unpublish review", name, true);
            }
            response.SetElementContents("ar-list", ListHtml(await Store.LoadSite(), list, c));
            response.ExecuteScript("AdminReviewsJs.toast('" + (on ? "Published. It shows on the Reviews page now." : "Unpublished") + "');");
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
            if (!Access.Can(c.U, AreaKey, 'X'))
            {
                return await Refuse(response, c, "delete review", id, "You are not allowed to delete reviews.");
            }
            List<ReviewRec> list;
            string name = string.Empty;
            using (await Store.Hold())
            {
                list = await Store.Reviews();
                ReviewRec? r = list.FirstOrDefault(x => x.Id == id);
                if (r != null)
                {
                    name = r.Name;
                    list.Remove(r);
                    await Store.SaveReviews(list);
                }
            }
            if (name.Length > 0)
            {
                await Store.Log(c.U.Id, "delete review", name, true);
            }
            response.SetElementContents("ar-list", ListHtml(await Store.LoadSite(), list, c));
            response.ExecuteScript("AdminReviewsJs.toast('Review deleted');");
            return response;
        }
    }
}
