using System.Globalization;
using System.Text;
using HomeServices.Core;
using HomeServices.Models;
using SkyNet;

namespace HomeServices.codes
{
    public class AdminMessages : WebPage
    {
        private const string AreaKey = "messages";
        private const int Size = 20;

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Messages | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMessagesJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminMessagesJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("ms-brand", Ui.Esc(site.Brand));
            response.SetElementContents("ms-menu", await Auth.Menu("ms", "AdminMessages", c));
            response.SetElementContents("ms-who", Auth.Who("ms", c));
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"ms-panel\"><h1>No access</h1><p class=\"ms-hint\">You do not have access to " + what + ". Ask a manager if you need it.</p></div>";
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
            response.ExecuteScript("AdminMessagesJs.expired();");
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
                response.SetElementContents("ms-main", NoAccess("messages"));
                return response;
            }
            List<MessageRec> list = await Store.Messages();
            response.SetElementContents("ms-main", "<div class=\"ms-ph\"><div><h1>Messages</h1><p>Notes sent with the contact form. Reply by email or phone, then mark them done.</p></div></div><div class=\"ms-tools\"><div class=\"ms-chips\" id=\"ms-chips\">" + Chips(list, "open") + "</div><input type=\"search\" id=\"ms-term\" placeholder=\"Search name, email or text\" maxlength=\"60\" oninput=\"AdminMessagesJs.search()\" aria-label=\"Search messages\"></div><div id=\"ms-list\">" + ListHtml(list, "open", string.Empty, 1) + "</div>");
            return response;
        }

        private static string Chips(List<MessageRec> list, string view)
        {
            (string Key, string Name, int N)[] chips = { ("open", "To answer", list.Count(m => !m.Done)), ("unread", "Unread", list.Count(m => !m.Read)), ("done", "Done", 0), ("all", "All", 0) };
            return string.Concat(chips.Select(x => "<button type=\"button\" class=\"ms-chip" + (x.Key == view ? " ms-act" : string.Empty) + "\" onclick=\"AdminMessagesJs.view('" + x.Key + "')\">" + x.Name + (x.N > 0 ? "<i>" + x.N + "</i>" : string.Empty) + "</button>"));
        }

        private static string ListHtml(List<MessageRec> all, string view, string term, int page)
        {
            List<MessageRec> rows = all.Where(m => (view == "open" ? !m.Done : view == "unread" ? !m.Read : view == "done" ? m.Done : true) && (term.Length == 0 || Ui.Has(m.Name, term) || Ui.Has(m.Email, term) || Ui.Has(m.Text, term) || Ui.Has(m.Topic, term))).OrderByDescending(m => m.When).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"ms-panel ms-empty\">" + (all.Count == 0 ? "No messages yet. They arrive here from the contact form." : "No messages match.") + "</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"ms-msgs\">");
            foreach (MessageRec m in rows.Skip((page - 1) * Size).Take(Size))
            {
                string snippet = m.Text.Length > 140 ? m.Text.Substring(0, 140) + "…" : m.Text;
                sb.Append("<button type=\"button\" class=\"ms-msg" + (m.Read ? string.Empty : " ms-unread") + "\" onclick=\"AdminMessagesJs.open('" + m.Id + "')\"><span class=\"ms-msg-h\"><b>" + Ui.Esc(m.Name) + "</b><small>" + Ui.Esc(Ui.When(m.When)) + "</small></span><span class=\"ms-msg-t\">" + Ui.Esc(m.Topic) + (m.Done ? " &middot; done" : string.Empty) + "</span><span class=\"ms-msg-s\">" + Ui.Esc(snippet) + "</span></button>");
            }
            sb.Append("</div>" + Ui.Pager("ms", page, Size, rows.Count, "message", "messages", "AdminMessagesJs.page"));
            return sb.ToString();
        }

        private async Task Refresh(ApiResponse response)
        {
            List<MessageRec> list = await Store.Messages();
            string view = GetDataValue("st") is "unread" or "done" or "all" ? GetDataValue("st") : "open";
            response.SetElementContents("ms-list", ListHtml(list, view, Ui.Clip(GetDataValue("term"), 60), Ui.PageNo(GetDataValue("pg"))));
            response.SetElementContents("ms-chips", Chips(list, view));
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
                return await Refuse(response, c, "list messages", string.Empty, "You do not have access to messages.");
            }
            await Refresh(response);
            return response;
        }

        public async Task<ApiResponse> Open()
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
                return await Refuse(response, c, "open message", id, "You do not have access to messages.");
            }
            MessageRec? m;
            using (await Store.Hold())
            {
                List<MessageRec> list = await Store.Messages();
                m = list.FirstOrDefault(x => x.Id == id);
                if (m != null && !m.Read)
                {
                    m.Read = true;
                    await Store.SaveMessages(list);
                }
            }
            if (m == null)
            {
                response.ExecuteScript("AdminMessagesJs.toast('That message was not found.');");
                return response;
            }
            SiteData site = await Store.LoadSite();
            bool canE = Access.Can(c.U, AreaKey, 'E');
            string mail = "mailto:" + m.Email + "?subject=" + Uri.EscapeDataString("Re: your message to " + site.Brand);
            StringBuilder sb = new StringBuilder("<div class=\"ms-stack\"><dl class=\"ms-kv\"><dt>From</dt><dd><b>" + Ui.Esc(m.Name) + "</b></dd><dt>Email</dt><dd><a class=\"ms-link\" href=\"" + Ui.Esc(mail) + "\">" + Ui.Esc(m.Email) + "</a></dd>" + (m.Phone.Length > 0 ? "<dt>Phone</dt><dd><a class=\"ms-link\" href=\"" + Ui.Esc(Work.Tel(m.Phone)) + "\">" + Ui.Esc(m.Phone) + "</a></dd>" : string.Empty) + "<dt>Topic</dt><dd>" + Ui.Esc(m.Topic) + "</dd><dt>Received</dt><dd>" + Ui.Esc(Ui.When(m.When)) + "</dd></dl><div class=\"ms-msgbody\">" + Ui.Esc(m.Text).Replace("\n", "<br>") + "</div><div class=\"ms-acts\"><a class=\"ms-btn\" href=\"" + Ui.Esc(mail) + "\">Reply by email</a>");
            if (canE)
            {
                sb.Append("<button type=\"button\" class=\"ms-btn ms-btn-ghost\" onclick=\"AdminMessagesJs.mark('" + m.Id + "','" + (m.Done ? "open" : "done") + "')\">" + (m.Done ? "Mark as not done" : "Mark done") + "</button><button type=\"button\" class=\"ms-btn ms-btn-ghost\" onclick=\"AdminMessagesJs.mark('" + m.Id + "','unread')\">Mark unread</button>");
            }
            if (Access.Can(c.U, AreaKey, 'X'))
            {
                sb.Append("<button type=\"button\" class=\"ms-btn ms-btn-danger ms-push\" onclick=\"AdminMessagesJs.drop('" + m.Id + "')\">Delete</button>");
            }
            sb.Append("</div></div>");
            response.SetElementContents("ms-mbody", sb.ToString());
            response.ExecuteScript("AdminMessagesJs.openModal('" + Ui.Js(m.Topic + " · " + m.Name) + "');");
            await Refresh(response);
            response.SetElementContents("ms-menu", await Auth.Menu("ms", "AdminMessages", c));
            return response;
        }

        public async Task<ApiResponse> Mark()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 20);
            string how = GetDataValue("how") is "done" or "open" or "unread" ? GetDataValue("how") : string.Empty;
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "mark message", id, "You can read messages but not change them.");
            }
            bool ok = false;
            using (await Store.Hold())
            {
                List<MessageRec> list = await Store.Messages();
                MessageRec? m = list.FirstOrDefault(x => x.Id == id);
                if (m != null && how.Length > 0)
                {
                    if (how == "unread")
                    {
                        m.Read = false;
                    }
                    else
                    {
                        m.Done = how == "done";
                    }
                    await Store.SaveMessages(list);
                    ok = true;
                }
            }
            if (ok)
            {
                await Store.Log(c.U.Id, "mark message " + how, id, true);
            }
            await Refresh(response);
            response.SetElementContents("ms-menu", await Auth.Menu("ms", "AdminMessages", c));
            response.ExecuteScript("AdminMessagesJs.shut();AdminMessagesJs.toast('" + (how == "done" ? "Marked done" : how == "unread" ? "Marked unread" : "Moved back to To answer") + "');");
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
                return await Refuse(response, c, "delete message", id, "You are not allowed to delete messages.");
            }
            bool gone;
            using (await Store.Hold())
            {
                List<MessageRec> list = await Store.Messages();
                gone = list.RemoveAll(x => x.Id == id) > 0;
                if (gone)
                {
                    await Store.SaveMessages(list);
                }
            }
            if (gone)
            {
                await Store.Log(c.U.Id, "delete message", id, true);
            }
            await Refresh(response);
            response.SetElementContents("ms-menu", await Auth.Menu("ms", "AdminMessages", c));
            response.ExecuteScript("AdminMessagesJs.shut();AdminMessagesJs.toast('Message deleted');");
            return response;
        }
    }
}
