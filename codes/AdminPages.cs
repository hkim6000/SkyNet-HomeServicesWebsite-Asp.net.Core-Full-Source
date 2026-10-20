using System.Globalization;
using System.Text;
using System.Text.Json;
using HomeServices.Core;
using HomeServices.Models;
using SkyNet;

namespace HomeServices.codes
{
    public class AdminPages : WebPage
    {
        private const string AreaKey = "pages";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Pages | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminPagesJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminPagesJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("pg-brand", Ui.Esc(site.Brand));
            response.SetElementContents("pg-menu", await Auth.Menu("pg", "AdminPages", c));
            response.SetElementContents("pg-who", Auth.Who("pg", c));
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"pg-panel\"><h1>No access</h1><p class=\"pg-hint\">You do not have access to " + what + ". Ask a manager if you need it.</p></div>";
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
            response.ExecuteScript("AdminPagesJs.expired();");
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
                response.SetElementContents("pg-main", "<div class=\"pg-panel\"><h1>No access</h1><p class=\"pg-hint\">You do not have access to pages. Ask a manager if you need it.</p></div>");
                return response;
            }
            response.SetElementContents("pg-main", MainHtml(c));
            response.ExecuteScript("AdminPagesJs.loaded();");
            return response;
        }

        private static string Opts(IEnumerable<(string Key, string Name)> list, string cur)
        {
            return string.Concat(list.Select(x => "<option value=\"" + Ui.Esc(x.Key) + "\"" + (x.Key == cur ? " selected" : string.Empty) + ">" + Ui.Esc(x.Name) + "</option>"));
        }

        private static string CE(string f, string value, string ph, string cls, bool ro, bool multi = false)
        {
            return "<div class=\"" + cls + (multi ? " pg-ml" : string.Empty) + "\" data-f=\"" + f + "\" contenteditable=\"" + (ro ? "false" : "plaintext-only") + "\" data-ph=\"" + Ui.Esc(ph) + "\"" + (multi ? " data-ml=\"1\"" : string.Empty) + ">" + Ui.Esc(value) + "</div>";
        }

        private static string CI(string f, string value, string ph, bool ro, bool multi)
        {
            return "<div data-i=\"" + f + "\" contenteditable=\"" + (ro ? "false" : "plaintext-only") + "\" data-ph=\"" + Ui.Esc(ph) + "\"" + (multi ? " data-ml=\"1\"" : string.Empty) + ">" + Ui.Esc(value) + "</div>";
        }

        private static string RichEd(string html, string ph, bool ro)
        {
            return "<div class=\"pg-er\" data-f=\"text\" contenteditable=\"" + (ro ? "false" : "true") + "\" data-ph=\"" + Ui.Esc(ph) + "\">" + html + "</div>";
        }

        private static string MediaEd(List<MediaItem> media, string src, string allow, string ph, bool ro)
        {
            MediaItem? m = media.FirstOrDefault(x => x.Path == src);
            bool vid = Blocks.IsVideo(src);
            string prev = src == string.Empty ? string.Empty : vid ? "<video src=\"" + Ui.Esc(src) + "#t=0.1\" muted preload=\"metadata\" playsinline></video>" : "<img src=\"" + Ui.Esc(m != null && m.Thumb.Length > 0 ? m.Thumb : src) + "\" alt=\"\">";
            return "<div class=\"pg-em\" data-f=\"media\" data-src=\"" + Ui.Esc(src) + "\" data-allow=\"" + allow + "\" data-ph=\"" + Ui.Esc(ph) + "\"><button type=\"button\" class=\"pg-emb\"" + (ro ? " disabled" : " onclick=\"AdminPagesJs.media(this)\"") + " aria-label=\"" + Ui.Esc(ph) + "\">" + (prev != string.Empty ? prev + (vid ? "<span class=\"pg-emv\">Video</span>" : string.Empty) + (ro ? string.Empty : "<span class=\"pg-emc\">Change</span>") : "<span>" + Ui.Esc(ph) + "</span>") + "</button>" + (ro ? string.Empty : "<button type=\"button\" class=\"pg-emx\" onclick=\"AdminPagesJs.noMedia(this)\"" + (prev == string.Empty ? " hidden" : string.Empty) + ">Remove</button>") + "</div>";
        }

        private static string BtnEd(string n, string label, string link, string title, bool ro)
        {
            string r = ro ? " readonly" : string.Empty;
            return "<div class=\"pg-ebtn\"><span>" + title + "</span><input type=\"text\" data-f=\"label" + n + "\" maxlength=\"40\" value=\"" + Ui.Esc(label) + "\" placeholder=\"Button text\"" + r + "><input type=\"text\" data-f=\"link" + n + "\" maxlength=\"300\" value=\"" + Ui.Esc(link) + "\" placeholder=\"Link: Quote, Contact or https://\"" + r + "></div>";
        }

        private static string ItemEd(List<MediaItem> media, Block b, PartSpec spec, BlockItem it, bool ro)
        {
            bool icon = spec.ItemFields.Contains("icon");
            bool link = spec.ItemFields.Contains("link");
            bool pic = spec.ItemFields.Contains("media");
            (string tp, string xp) = b.Type switch
            {
                "steps" => ("Step", "What happens in this step"),
                "stats" => ("Number, like 14", "What it counts"),
                "people" => ("Name", "Role or title"),
                "boxes" => ("Box title", "One item per line"),
                "faq" => ("Question", "Answer"),
                "cards" => ("Card title", "Short text"),
                _ => ("Title", "Text")
            };
            StringBuilder sb = new StringBuilder("<div class=\"pg-eit\">");
            if (icon)
            {
                sb.Append("<select data-i=\"icon\" aria-label=\"Icon\"" + (ro ? " disabled" : string.Empty) + "><option value=\"\">No icon</option>" + Opts(Blocks.Icons, it.Icon) + "</select>");
            }
            if (pic)
            {
                sb.Append(MediaEd(media, it.Media, "photo", "Add a photo", ro).Replace("data-f=\"media\"", "data-i=\"media\"").Replace("class=\"pg-em\"", "class=\"pg-em pg-emi\""));
            }
            sb.Append(CI("title", it.Title, tp, ro, false) + CI("text", it.Text, xp, ro, true));
            if (link)
            {
                sb.Append("<div class=\"pg-elink\"><input type=\"text\" data-i=\"label\" maxlength=\"40\" value=\"" + Ui.Esc(it.Label) + "\" placeholder=\"Link text\"" + (ro ? " readonly" : string.Empty) + "><input type=\"text\" data-i=\"link\" maxlength=\"300\" value=\"" + Ui.Esc(it.Link) + "\" placeholder=\"Link: Quote or https://\"" + (ro ? " readonly" : string.Empty) + "></div>");
            }
            if (!ro)
            {
                sb.Append("<div class=\"pg-ictl\"><button type=\"button\" aria-label=\"Move earlier\" onclick=\"AdminPagesJs.itemMove(this,-1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M6 15l6-6 6 6\"/></svg></button><button type=\"button\" aria-label=\"Move later\" onclick=\"AdminPagesJs.itemMove(this,1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M6 9l6 6 6-6\"/></svg></button><button type=\"button\" aria-label=\"Remove\" onclick=\"AdminPagesJs.itemDrop(this)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3\"/></svg></button></div>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string Ed(List<MediaItem> lib, Block b, bool ro)
        {
            PartSpec? spec = Blocks.Spec(b);
            if (spec == null)
            {
                return string.Empty;
            }
            bool data = b.Type == "data";
            string x = data ? "data" : b.Type;
            StringBuilder sb = new StringBuilder("<div class=\"pg-blk pg-x-" + x + (b.Hidden ? " pg-hid" : string.Empty) + "\" data-id=\"" + Ui.Esc(b.Id) + "\" data-type=\"" + b.Type + "\" data-src=\"" + Ui.Esc(b.Source) + "\" data-hidden=\"" + (b.Hidden ? "1" : "0") + "\"><header class=\"pg-bh\">");
            if (!ro)
            {
                sb.Append("<button type=\"button\" class=\"pg-grip\" aria-label=\"Drag to move\" title=\"Drag to move\" onpointerdown=\"AdminPagesJs.grab(event,this)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"currentColor\" aria-hidden=\"true\"><circle cx=\"9\" cy=\"6\" r=\"1.6\"/><circle cx=\"15\" cy=\"6\" r=\"1.6\"/><circle cx=\"9\" cy=\"12\" r=\"1.6\"/><circle cx=\"15\" cy=\"12\" r=\"1.6\"/><circle cx=\"9\" cy=\"18\" r=\"1.6\"/><circle cx=\"15\" cy=\"18\" r=\"1.6\"/></svg></button>");
            }
            sb.Append("<b>" + Ui.Esc(spec.Name) + "</b>" + (data ? "<span class=\"pg-src\">From your data</span>" : string.Empty) + "<span class=\"pg-bsp\"></span>");
            if (!ro)
            {
                sb.Append("<button type=\"button\" class=\"pg-hb\" aria-pressed=\"" + (b.Hidden ? "true" : "false") + "\" onclick=\"AdminPagesJs.hide(this)\" title=\"Hide on the website without deleting\"><span>" + (b.Hidden ? "Show" : "Hide") + "</span></button><button type=\"button\" class=\"pg-hb\" aria-label=\"Move up\" title=\"Move up\" onclick=\"AdminPagesJs.move(this,-1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M6 15l6-6 6 6\"/></svg></button><button type=\"button\" class=\"pg-hb\" aria-label=\"Move down\" title=\"Move down\" onclick=\"AdminPagesJs.move(this,1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M6 9l6 6 6-6\"/></svg></button>" + (data ? string.Empty : "<button type=\"button\" class=\"pg-hb pg-del\" aria-label=\"Remove block\" title=\"Remove block\" onclick=\"AdminPagesJs.drop(this)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3\"/></svg></button>"));
            }
            sb.Append("</header><div class=\"pg-bb\">");
            if (data)
            {
                sb.Append("<div class=\"pg-dinfo\">" + Ui.Esc(spec.Desc) + "</div>");
            }
            string kick = Blocks.Has(spec, "kicker") ? CE("kicker", b.Kicker, "Small label above the title", "pg-ek", ro) : string.Empty;
            string title = Blocks.Has(spec, "title") ? CE("title", b.Title, "Title", "pg-et", ro) : string.Empty;
            string text = Blocks.Has(spec, "text") ? RichEd(b.Text, "Write here", ro) : string.Empty;
            string media = Blocks.Has(spec, "media") ? MediaEd(lib, b.Media, b.Type == "quote" ? "photo" : string.Empty, b.Type == "quote" ? "Add a photo" : "Add a picture or video", ro) : string.Empty;
            string btns = Blocks.Has(spec, "buttons") ? "<div class=\"pg-ebtns\">" + BtnEd(string.Empty, b.Label, b.Link, "Main button", ro) + BtnEd("2", b.Label2, b.Link2, "Second button", ro) + "</div>" : Blocks.Has(spec, "button") ? "<div class=\"pg-ebtns\">" + BtnEd(string.Empty, b.Label, b.Link, "Button", ro) + "</div>" : string.Empty;
            string extra = string.Empty;
            if (Blocks.Has(spec, "flip"))
            {
                extra = "<label class=\"pg-chk\"><input type=\"checkbox\" data-f=\"flip\"" + (b.Flip ? " checked" : string.Empty) + (ro ? " disabled" : string.Empty) + " onchange=\"this.closest('.pg-bb').querySelector('.pg-two').classList.toggle('pg-flip',this.checked)\"> Picture on the right</label>";
            }
            switch (b.Type)
            {
                case "hero":
                    sb.Append("<div class=\"pg-two\"><div class=\"pg-col\">" + kick + title + text + btns + "</div>" + media + "</div>");
                    break;
                case "split":
                    sb.Append("<div class=\"pg-two" + (b.Flip ? " pg-flip" : string.Empty) + "\">" + media + "<div class=\"pg-col\">" + kick + title + text + btns + "</div></div>" + extra);
                    break;
                case "quote":
                    sb.Append(media + CE("text", b.Text, "The quote", "pg-eq", ro, true) + CE("title", b.Title, "Who said it, for example A happy customer", "pg-et", ro));
                    break;
                case "media":
                    sb.Append(media + CE("text", b.Text, "Caption (optional)", "pg-hint2", ro));
                    break;
                default:
                    sb.Append(kick + title + text + btns);
                    break;
            }
            if (Blocks.Has(spec, "items"))
            {
                sb.Append("<div><div class=\"pg-eitems\" data-f=\"items\">");
                foreach (BlockItem it in b.Items)
                {
                    sb.Append(ItemEd(lib, b, spec, it, ro));
                }
                sb.Append("</div>" + (ro ? string.Empty : "<button type=\"button\" class=\"pg-eadd\" onclick=\"AdminPagesJs.addItem(this)\">+ Add " + (b.Type switch { "steps" => "a step", "faq" => "a question", "boxes" => "a box", "cards" => "a card", "stats" => "a number", "people" => "a person", _ => "a line" }) + "</button><template>" + ItemEd(lib, b, spec, new BlockItem(), false) + "</template>") + "</div>");
            }
            sb.Append("</div></div>");
            return sb.ToString();
        }

        private static string Tabs(string cur)
        {
            return string.Concat(Blocks.Pages.Select(p => "<button type=\"button\" class=\"pg-chip" + (p.Key == cur ? " pg-act" : string.Empty) + "\" aria-pressed=\"" + (p.Key == cur ? "true" : "false") + "\" onclick=\"AdminPagesJs.tab('" + p.Key + "')\">" + Ui.Esc(p.Name) + "</button>"));
        }

        private static string MainHtml(Ctx c)
        {
            bool canE = Access.Can(c.U, AreaKey, 'E');
            return "<div class=\"pg-ph\"><div><h1>Pages</h1><p>" + (canE ? "Click any text to change it. Drag a block by its handle, or use the arrows, to move it. Use {name} to show the business name from Settings. Changes go live when you save." : "You can look at the pages but not change them.") + "</p></div></div><div class=\"pg-ptools\"><div class=\"pg-ptabs\" id=\"pg-ptabs\">" + Tabs("home") + "</div><div class=\"pg-pacts\"><span class=\"pg-dirty\" id=\"pg-dirty\"></span><button type=\"button\" class=\"pg-btn pg-btn-sm pg-btn-ghost\" onclick=\"AdminPagesJs.versions()\">Versions</button><button type=\"button\" class=\"pg-btn pg-btn-sm pg-btn-ghost\" id=\"pg-viewlink\" data-href=\"Home\" onclick=\"AdminPagesJs.view()\">View page</button>" + (canE ? "<button type=\"button\" class=\"pg-btn pg-btn-sm\" id=\"pg-savebtn\" onclick=\"AdminPagesJs.save()\">Save page</button>" : string.Empty) + "</div></div><div class=\"pg-canvas\" id=\"pg-canvas\"></div><div id=\"pg-holder\" hidden></div><div class=\"pg-rt\" id=\"pg-rt\" role=\"toolbar\" aria-label=\"Text style\"><button type=\"button\" onmousedown=\"event.preventDefault()\" onclick=\"AdminPagesJs.fmt('bold')\" title=\"Bold\"><b>B</b></button><button type=\"button\" onmousedown=\"event.preventDefault()\" onclick=\"AdminPagesJs.fmt('italic')\" title=\"Italic\"><i>I</i></button><button type=\"button\" onmousedown=\"event.preventDefault()\" onclick=\"AdminPagesJs.fmt('link')\" title=\"Link\">Link</button><button type=\"button\" onmousedown=\"event.preventDefault()\" onclick=\"AdminPagesJs.fmt('insertUnorderedList')\" title=\"List\">&bull; List</button><button type=\"button\" onmousedown=\"event.preventDefault()\" onclick=\"AdminPagesJs.fmt('clear')\" title=\"Clear formatting\">Clear</button></div>";
        }

        private static string Canvas(SiteData site, List<MediaItem> media, PageSpec p, bool canE)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Block b in site.Pages[p.Key].Blocks)
            {
                sb.Append(Ed(media, b, !canE));
            }
            if (canE)
            {
                sb.Append("<div class=\"pg-addp\" id=\"pg-addp\"><b>Add a block</b><div class=\"pg-addl\">" + string.Concat(Blocks.Types.Select(t => "<button type=\"button\" onclick=\"AdminPagesJs.addBlock('" + t.Key + "')\"><b>" + Ui.Esc(t.Name) + "</b><small>" + Ui.Esc(t.Desc) + "</small></button>")) + "</div><p class=\"pg-hint2\">Blocks marked From your data (services, ZIP check, reviews, forms) cannot be removed, but you can move them, change their titles or hide them.</p></div>");
            }
            return sb.ToString();
        }

        public async Task<ApiResponse> Show()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            List<MediaItem> media = await Media.Load();
            PageSpec p = Blocks.PageOf(GetDataValue("page")) ?? Blocks.Pages[0];
            bool canE = Access.Can(c.U, AreaKey, 'E');
            response.SetElementContents("pg-canvas", Canvas(site, media, p, canE));
            response.SetElementContents("pg-ptabs", Tabs(p.Key));
            response.ExecuteScript("AdminPagesJs.shown('" + p.Key + "','" + p.Href + "');");
            return response;
        }

        public async Task<ApiResponse> AddBlock()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "add block", GetDataValue("btype"), "You can look but not change pages.");
            }
            PartSpec? t = Blocks.Types.FirstOrDefault(x => x.Key == GetDataValue("btype"));
            if (t == null)
            {
                return response;
            }
            List<MediaItem> media = await Media.Load();
            Block b = new Block { Id = Blocks.NewId(), Type = t.Key };
            if (t.Key is "cards" or "steps" or "boxes" or "faq" or "stats" or "people")
            {
                b.Items.Add(new BlockItem());
                b.Items.Add(new BlockItem());
                b.Items.Add(new BlockItem());
            }
            response.SetElementContents("pg-holder", Ed(media, b, false));
            response.ExecuteScript("AdminPagesJs.added();");
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
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "save page", GetDataValue("page"), "You can look but not change pages.");
            }
            PageSpec? p = Blocks.PageOf(GetDataValue("page"));
            if (p == null)
            {
                return response;
            }
            List<MediaItem> media = await Media.Load();
            string err = string.Empty;
            SiteData? done = null;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                (List<Block>? list, string e) = Blocks.Clean(media, p, GetDataValue("blocks"));
                if (list == null)
                {
                    err = e;
                }
                else
                {
                    PageDoc doc = site.Pages[p.Key];
                    doc.Versions.Insert(0, new PageVersion { When = doc.Saved == default ? DateTime.UtcNow.AddMinutes(-1) : doc.Saved, By = doc.By, Blocks = doc.Blocks });
                    if (doc.Versions.Count > 5)
                    {
                        doc.Versions.RemoveRange(5, doc.Versions.Count - 5);
                    }
                    doc.Blocks = list;
                    doc.Saved = DateTime.UtcNow;
                    doc.By = c.U.Id;
                    await Store.SaveSite(site);
                    done = site;
                }
            }
            if (done == null)
            {
                response.ExecuteScript("AdminPagesJs.fail('" + Ui.Js(err) + "');");
                return response;
            }
            await Store.Log(c.U.Id, "save page", p.Name, true);
            response.SetElementContents("pg-canvas", Canvas(done, media, p, true));
            response.ExecuteScript("AdminPagesJs.shown('" + p.Key + "','" + p.Href + "');AdminPagesJs.saved('" + Ui.Js(p.Name + " saved. It is live on the website.") + "');");
            return response;
        }

        public async Task<ApiResponse> Versions()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "page versions", GetDataValue("page"), "You do not have access to pages.");
            }
            PageSpec? p = Blocks.PageOf(GetDataValue("page"));
            if (p == null)
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            PageDoc doc = site.Pages[p.Key];
            List<Staff> staff = await Store.LoadStaff();
            string Who(string id) => staff.FirstOrDefault(s => s.Id == id)?.Name ?? id;
            bool canE = Access.Can(c.U, AreaKey, 'E');
            StringBuilder sb = new StringBuilder("<p class=\"pg-hint\">The last five saved versions are kept. Restoring one makes it live right away.</p><div class=\"pg-vlist\"><div class=\"pg-vrow\"><div><b>Live now</b><small>" + (doc.Saved != default ? "Saved " + Ui.Esc(Ui.When(doc.Saved)) + (doc.By != string.Empty ? " by " + Ui.Esc(Who(doc.By)) : string.Empty) : "Sample content") + " &middot; " + Ui.Count(doc.Blocks.Count, "block", "blocks") + "</small></div></div>");
            if (doc.Versions.Count == 0)
            {
                sb.Append("<p class=\"pg-hint\">No earlier versions yet. They appear here after you save.</p>");
            }
            for (int i = 0; i < doc.Versions.Count; i++)
            {
                PageVersion v = doc.Versions[i];
                string when = Ui.When(v.When);
                sb.Append("<div class=\"pg-vrow\"><div><b>" + Ui.Esc(when) + "</b><small>" + (v.By != string.Empty ? Ui.Esc(Who(v.By)) + " &middot; " : string.Empty) + Ui.Count(v.Blocks.Count, "block", "blocks") + "</small></div>" + (canE ? "<button type=\"button\" class=\"pg-btn pg-btn-sm pg-btn-ghost\" onclick=\"AdminPagesJs.restore(" + i + ",'" + Ui.Js(when) + "')\">Restore</button>" : string.Empty) + "</div>");
            }
            sb.Append("</div><div class=\"pg-acts\"><button type=\"button\" class=\"pg-btn pg-btn-ghost\" onclick=\"AdminPagesJs.closeModal()\">Close</button></div>");
            response.SetElementContents("pg-mbody", sb.ToString());
            response.ExecuteScript("AdminPagesJs.opened('" + Ui.Js(p.Name + ": versions") + "');");
            return response;
        }

        public async Task<ApiResponse> Restore()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "restore page", GetDataValue("page"), "You can look but not change pages.");
            }
            PageSpec? p = Blocks.PageOf(GetDataValue("page"));
            int i = int.TryParse(GetDataValue("vi"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int iv) ? iv : -1;
            SiteData? done = null;
            if (p != null)
            {
                using (await Store.Hold())
                {
                    SiteData site = await Store.LoadSite();
                    PageDoc doc = site.Pages[p.Key];
                    if (i >= 0 && i < doc.Versions.Count)
                    {
                        PageVersion v = doc.Versions[i];
                        doc.Versions.RemoveAt(i);
                        doc.Versions.Insert(0, new PageVersion { When = doc.Saved == default ? DateTime.UtcNow.AddMinutes(-1) : doc.Saved, By = doc.By, Blocks = doc.Blocks });
                        if (doc.Versions.Count > 5)
                        {
                            doc.Versions.RemoveRange(5, doc.Versions.Count - 5);
                        }
                        doc.Blocks = v.Blocks;
                        doc.Saved = DateTime.UtcNow;
                        doc.By = c.U.Id;
                        Blocks.Ensure(site);
                        await Store.SaveSite(site);
                        done = site;
                    }
                }
            }
            if (p == null || done == null)
            {
                return response;
            }
            await Store.Log(c.U.Id, "restore page", p.Name, true);
            List<MediaItem> media = await Media.Load();
            response.SetElementContents("pg-canvas", Canvas(done, media, p, true));
            response.ExecuteScript("AdminPagesJs.shown('" + p.Key + "','" + p.Href + "');AdminPagesJs.saved('Version restored. It is live on the website.');");
            return response;
        }

        private static string PickItem(MediaItem m)
        {
            bool vid = m.Kind == "video";
            string thumb = m.Thumb.Length > 0 ? m.Thumb : m.Path;
            return "<button type=\"button\" class=\"pg-pki\" data-src=\"" + Ui.Esc(m.Path) + "\" data-thumb=\"" + Ui.Esc(thumb) + "\" data-kind=\"" + m.Kind + "\" data-title=\"" + Ui.Esc(m.Name) + "\" onclick=\"AdminPagesJs.pkPick(this)\" title=\"" + Ui.Esc(m.Name) + "\">" + (vid ? "<video src=\"" + Ui.Esc(m.Path) + "#t=0.1\" muted preload=\"metadata\" playsinline></video><b class=\"pg-vbadge\">Video</b>" : "<img src=\"" + Ui.Esc(thumb) + "\" alt=\"\" loading=\"lazy\">") + "<span>" + Ui.Esc(m.Name) + "</span></button>";
        }

        private static string PickGrid(List<MediaItem> media, string allow, string kind, string term)
        {
            string k = allow != string.Empty ? allow : kind;
            List<MediaItem> rows = media.Where(m => (k == string.Empty || m.Kind == k) && (term == string.Empty || Ui.Has(m.Name, term) || Ui.Has(m.Alt, term) || Ui.Has(m.Caption, term))).OrderByDescending(m => m.Added).Take(400).ToList();
            if (rows.Count == 0)
            {
                return "<p class=\"pg-none\">Nothing matches. Upload a photo or video above, or add them in the Media library.</p>";
            }
            return string.Concat(rows.Select(PickItem));
        }

        public async Task<ApiResponse> MediaPick()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return response;
            }
            List<MediaItem> media = await Media.Load();
            string allow = GetDataValue("pallow") == "photo" ? "photo" : string.Empty;
            string kind = GetDataValue("pkind") is "photo" or "video" ? GetDataValue("pkind") : string.Empty;
            response.SetElementContents("pg-pkgrid", PickGrid(media, allow, kind, Ui.Clip(GetDataValue("pterm"), 40)));
            return response;
        }

        public async Task<ApiResponse> MediaUp()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                response.ExecuteScript("AdminPagesJs.pkUploaded(false,'You can look but not upload.');");
                return response;
            }
            SiteData site = await Store.LoadSite();
            List<Incoming> files = await Media.Received();
            if (files.Count == 0)
            {
                response.ExecuteScript("AdminPagesJs.pkUploaded(false,'Choose a photo or video first.');");
                return response;
            }
            (int added, List<string> errors) = await Media.AddAll(files, site.MaxVideoMb, c.U.Name);
            if (added > 0)
            {
                await Store.Log(c.U.Id, "upload media", Ui.Count(added, "file", "files"), true);
            }
            List<MediaItem> media = await Media.Load();
            string allow = GetDataValue("pallow") == "photo" ? "photo" : string.Empty;
            response.SetElementContents("pg-pkgrid", PickGrid(media, allow, string.Empty, string.Empty));
            response.ExecuteScript("AdminPagesJs.pkUploaded(" + (added > 0 ? "true" : "false") + ",'" + Ui.Js(((added > 0 ? Ui.Count(added, "file", "files") + " added to the library. " : string.Empty) + string.Join(" ", errors)).Trim()) + "');");
            return response;
        }
    }
}
