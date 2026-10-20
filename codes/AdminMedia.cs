using System.Text;
using HomeServices.Core;
using HomeServices.Models;
using SkyNet;

namespace HomeServices.codes
{
    public class AdminMedia : WebPage
    {
        private const string AreaKey = "media";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Media library | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMediaJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminMediaJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("am-brand", Ui.Esc(site.Brand));
            response.SetElementContents("am-menu", await Auth.Menu("am", "AdminMedia", c));
            response.SetElementContents("am-who", Auth.Who("am", c));
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"am-panel\"><h1>No access</h1><p class=\"am-hint\">You do not have access to " + what + ". Ask a manager if you need it.</p></div>";
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
            response.ExecuteScript("AdminMediaJs.expired();");
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
                response.SetElementContents("am-main", NoAccess("the media library"));
                return response;
            }
            List<MediaItem> media = await Media.Load();
            response.SetElementContents("am-main", PageHtml(site, media, c));
            return response;
        }

        public async Task<ApiResponse> Lib()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "open media", string.Empty, "You do not have access to the media library.");
            }
            SiteData site = await Store.LoadSite();
            List<MediaItem> media = await Media.Load();
            response.SetElementContents("am-lib", LibHtml(site, media, c, Kind(GetDataValue("mk")), Ui.Clip(GetDataValue("mq"), 40)));
            return response;
        }

        public async Task<ApiResponse> Upload()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                response.ExecuteScript("AdminMediaJs.uploaded();");
                return await Refuse(response, c, "upload media", string.Empty, "You can look at the media library but not upload.");
            }
            SiteData site = await Store.LoadSite();
            List<Incoming> files = await Media.Received();
            if (files.Count == 0)
            {
                response.SetElementContents("am-upmsg", "Choose one or more photos or videos first.");
                response.ExecuteScript("AdminMediaJs.uploaded();");
                return response;
            }
            (int added, List<string> errors) = await Media.AddAll(files, site.MaxVideoMb, c.U.Name);
            if (added > 0)
            {
                await Store.Log(c.U.Id, "upload media", Ui.Count(added, "file", "files"), true);
            }
            List<MediaItem> media = await Media.Load();
            string msg = (added > 0 ? Ui.Count(added, "file", "files") + " added to the library. " : string.Empty) + string.Join(" ", errors);
            response.SetElementContents("am-lib", LibHtml(site, media, c, Kind(GetDataValue("mk")), Ui.Clip(GetDataValue("mq"), 40)));
            response.SetElementContents("am-upmsg", Ui.Esc(msg.Trim()));
            response.ExecuteScript("AdminMediaJs.uploaded();");
            return response;
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
                return await Refuse(response, c, "open media", id, "You do not have access to the media library.");
            }
            List<MediaItem> media = await Media.Load();
            MediaItem? m = media.FirstOrDefault(x => x.Id == id);
            if (m == null)
            {
                response.ExecuteScript("AdminMediaJs.toast('That file was not found.');");
                return response;
            }
            SiteData site = await Store.LoadSite();
            response.SetElementContents("am-mbody", FormHtml(site, m, Access.Can(c.U, AreaKey, 'E'), Access.Can(c.U, AreaKey, 'X')));
            response.ExecuteScript("AdminMediaJs.opened('" + Ui.Js(m.Name) + "');");
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
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "edit media", id, "You can look at the media library but not change it.");
            }
            Dictionary<string, string> errors = new Dictionary<string, string>();
            string alt = Need("alt", 160, errors);
            string caption = Need("caption", 200, errors);
            response.SetElementContents("am-e-alt", errors.TryGetValue("alt", out string? e1) ? e1 : string.Empty);
            response.SetElementContents("am-e-caption", errors.TryGetValue("caption", out string? e2) ? e2 : string.Empty);
            if (errors.Count > 0)
            {
                return response;
            }
            string name;
            using (await Store.Hold())
            {
                List<MediaItem> list = await Media.Load();
                MediaItem? m = list.FirstOrDefault(x => x.Id == id);
                if (m == null)
                {
                    response.ExecuteScript("AdminMediaJs.toast('That file was not found.');");
                    return response;
                }
                m.Alt = alt;
                m.Caption = caption;
                name = m.Name;
                await Media.Save(list);
            }
            await Store.Log(c.U.Id, "edit media", name, true);
            SiteData site = await Store.LoadSite();
            List<MediaItem> media = await Media.Load();
            response.SetElementContents("am-lib", LibHtml(site, media, c, Kind(GetDataValue("mk")), Ui.Clip(GetDataValue("mq"), 40)));
            response.ExecuteScript("AdminMediaJs.saved();");
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
            string id = Ui.Clip(GetDataValue("id"), 20);
            if (!Access.Can(c.U, AreaKey, 'X'))
            {
                return await Refuse(response, c, "delete media", id, "You are not allowed to delete files.");
            }
            string name = string.Empty;
            List<string> used = new List<string>();
            SiteData site;
            using (await Store.Hold())
            {
                site = await Store.LoadSite();
                List<MediaItem> list = await Media.Load();
                MediaItem? m = list.FirstOrDefault(x => x.Id == id);
                if (m != null)
                {
                    name = m.Name;
                    used = Media.UsedBy(site, m.Path);
                    if (used.Count == 0)
                    {
                        list.Remove(m);
                        await Media.Save(list);
                        Media.Erase(m);
                    }
                }
            }
            if (name.Length == 0)
            {
                response.ExecuteScript("AdminMediaJs.toast('That file was not found.');");
                return response;
            }
            if (used.Count > 0)
            {
                await Store.Log(c.U.Id, "delete media", name, false);
                response.ExecuteScript("AdminMediaJs.toast('" + Ui.Js(name + " is still used: " + string.Join(", ", used.Take(3)) + (used.Count > 3 ? " and more" : string.Empty) + ". Replace it there first.") + "');");
                return response;
            }
            await Store.Log(c.U.Id, "delete media", name, true);
            List<MediaItem> media = await Media.Load();
            response.SetElementContents("am-lib", LibHtml(site, media, c, Kind(GetDataValue("mk")), Ui.Clip(GetDataValue("mq"), 40)));
            response.ExecuteScript("AdminMediaJs.dropped();");
            return response;
        }

        private string Need(string key, int max, Dictionary<string, string> errors)
        {
            string v = (GetDataValue(key) ?? string.Empty).Trim();
            if (v.Length > max)
            {
                errors[key] = "Keep it under " + max + " characters.";
            }
            return v;
        }

        private static string Kind(string? v)
        {
            string k = (v ?? string.Empty).Trim();
            return k == "photo" || k == "video" ? k : string.Empty;
        }

        private static string PageHtml(SiteData site, List<MediaItem> media, Ctx c)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"am-ph\"><div><h1>Media library</h1><p>Photos and videos for the website pages, the trades and the technicians.</p></div></div>");
            if (Access.Can(c.U, AreaKey, 'E'))
            {
                sb.Append("<section class=\"am-panel am-uppanel\"><label class=\"am-btn am-upbtn\"><input type=\"file\" id=\"am-upfile\" multiple accept=\"" + Media.Accept + "\" onchange=\"AdminMediaJs.upload()\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M12 16V4M6 10l6-6 6 6M4 20h16\"/></svg>Upload photos or videos</label>");
                sb.Append("<p class=\"am-hint\">Photos: JPG, PNG, WebP or GIF up to " + Media.PhotoMb + " MB. Videos: MP4 up to " + site.MaxVideoMb + " MB (a manager can change this in Settings). Up to 20 files at a time.</p><p class=\"am-upmsg\" id=\"am-upmsg\" role=\"status\"></p></section>");
            }
            sb.Append("<div class=\"am-tools\"><div class=\"am-chips\" id=\"am-chips\"><button type=\"button\" class=\"am-chip am-act\" data-mk=\"\" onclick=\"AdminMediaJs.libKind('')\">All</button><button type=\"button\" class=\"am-chip\" data-mk=\"photo\" onclick=\"AdminMediaJs.libKind('photo')\">Photos</button><button type=\"button\" class=\"am-chip\" data-mk=\"video\" onclick=\"AdminMediaJs.libKind('video')\">Videos</button></div>");
            sb.Append("<input type=\"search\" id=\"am-mq\" placeholder=\"Search by name or description\" maxlength=\"40\" oninput=\"AdminMediaJs.libType()\" aria-label=\"Search media\"></div>");
            sb.Append("<div id=\"am-lib\">" + LibHtml(site, media, c, string.Empty, string.Empty) + "</div>");
            return sb.ToString();
        }

        private static string LibHtml(SiteData site, List<MediaItem> media, Ctx c, string kind, string term)
        {
            bool edit = Access.Can(c.U, AreaKey, 'E');
            bool extra = Access.Can(c.U, AreaKey, 'X');
            List<MediaItem> rows = media.Where(m => (kind == string.Empty || m.Kind == kind) && (term == string.Empty || Ui.Has(m.Name, term) || Ui.Has(m.Alt, term) || Ui.Has(m.Caption, term))).OrderByDescending(m => m.Added).ToList();
            int photos = media.Count(m => m.Kind == "photo");
            int videos = media.Count(m => m.Kind == "video");
            StringBuilder sb = new StringBuilder();
            sb.Append("<p class=\"am-hint am-libcount\">" + Ui.Count(photos, "photo", "photos") + " &middot; " + Ui.Count(videos, "video", "videos") + (rows.Count != media.Count ? " &middot; showing " + rows.Count : string.Empty) + "</p>");
            if (rows.Count == 0)
            {
                return sb.Append("<div class=\"am-panel am-empty\">" + (media.Count == 0 ? "The library is empty. Upload your first photo or video." : "Nothing matches.") + "</div>").ToString();
            }
            sb.Append("<div class=\"am-grid\">");
            foreach (MediaItem m in rows.Take(240))
            {
                int used = Media.UsedBy(site, m.Path).Count;
                string id = Ui.Js(m.Id);
                sb.Append("<article class=\"am-md\"><button type=\"button\" class=\"am-md-pv\" onclick=\"AdminMediaJs.edit('" + id + "')\" aria-label=\"Open " + Ui.Esc(m.Name) + "\">");
                sb.Append(m.Kind == "video" ? "<video src=\"" + Ui.Esc(m.Path) + "#t=0.1\" muted preload=\"metadata\" playsinline></video><i class=\"am-md-play\" aria-hidden=\"true\"><svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"currentColor\"><path d=\"M8 5v14l11-7z\"/></svg></i>" : "<img src=\"" + Ui.Esc(m.Thumb.Length > 0 ? m.Thumb : m.Path) + "\" alt=\"\" loading=\"lazy\">");
                sb.Append("</button><div class=\"am-md-t\"><b title=\"" + Ui.Esc(m.Name) + "\">" + Ui.Esc(m.Name) + "</b><small>" + (m.Kind == "video" ? "Video" : "Photo") + " &middot; " + Media.Size(m.Bytes) + (used > 0 ? " &middot; used " + (used == 1 ? "once" : used + " times") : string.Empty) + "</small></div><div class=\"am-md-a\">");
                sb.Append("<button type=\"button\" class=\"am-btn am-btn-ghost am-btn-sm\" onclick=\"AdminMediaJs.edit('" + id + "')\">" + (edit ? "Edit" : "View") + "</button>");
                if (extra)
                {
                    sb.Append("<button type=\"button\" class=\"am-btn am-btn-danger am-btn-sm\" onclick=\"AdminMediaJs.drop('" + id + "','" + Ui.Js(m.Name) + "'," + used + ")\">Delete</button>");
                }
                sb.Append("</div></article>");
            }
            sb.Append("</div>");
            if (rows.Count > 240)
            {
                sb.Append("<p class=\"am-hint\">Showing the newest 240. Search to find older files.</p>");
            }
            return sb.ToString();
        }

        private static string FormHtml(SiteData site, MediaItem m, bool edit, bool extra)
        {
            List<string> used = Media.UsedBy(site, m.Path);
            string d = edit ? string.Empty : " disabled";
            StringBuilder sb = new StringBuilder();
            sb.Append("<form class=\"am-stack\" id=\"am-form\" onsubmit=\"return AdminMediaJs.save('" + Ui.Js(m.Id) + "')\" novalidate><div class=\"am-view\">");
            sb.Append(m.Kind == "video" ? "<video src=\"" + Ui.Esc(m.Path) + "\" controls preload=\"metadata\" playsinline></video>" : "<img src=\"" + Ui.Esc(m.Path) + "\" alt=\"" + Ui.Esc(m.Alt) + "\">");
            sb.Append("</div><dl class=\"am-kv\"><dt>File</dt><dd>" + Ui.Esc(m.Path) + "</dd><dt>Size</dt><dd>" + Media.Size(m.Bytes) + "</dd><dt>Added</dt><dd>" + Ui.Esc(Ui.When(m.Added)) + (m.By.Length > 0 ? " by " + Ui.Esc(m.By) : string.Empty) + "</dd><dt>Used in</dt><dd>" + (used.Count == 0 ? "Not used yet" : string.Join("<br>", used.Select(Ui.Esc))) + "</dd></dl>");
            sb.Append("<label class=\"am-field\" for=\"am-f-alt\"><span>Description for screen readers (alt text)</span><input type=\"text\" id=\"am-f-alt\" data-k=\"alt\" maxlength=\"160\" value=\"" + Ui.Esc(m.Alt) + "\"" + d + "><small>Say what the picture shows, in a few words.</small><em class=\"am-err\" id=\"am-e-alt\"></em></label>");
            sb.Append("<label class=\"am-field\" for=\"am-f-caption\"><span>Note for staff (optional)</span><input type=\"text\" id=\"am-f-caption\" data-k=\"caption\" maxlength=\"200\" value=\"" + Ui.Esc(m.Caption) + "\"" + d + "><em class=\"am-err\" id=\"am-e-caption\"></em></label>");
            sb.Append("<div class=\"am-acts\">" + (edit ? "<button type=\"submit\" class=\"am-btn\">Save</button>" : string.Empty) + "<button type=\"button\" class=\"am-btn am-btn-ghost\" onclick=\"AdminMediaJs.closeModal()\">" + (edit ? "Cancel" : "Close") + "</button>");
            if (extra)
            {
                sb.Append("<button type=\"button\" class=\"am-btn am-btn-danger am-push\" onclick=\"AdminMediaJs.drop('" + Ui.Js(m.Id) + "','" + Ui.Js(m.Name) + "'," + used.Count + ")\">Delete file</button>");
            }
            sb.Append("</div></form>");
            return sb.ToString();
        }
    }
}
