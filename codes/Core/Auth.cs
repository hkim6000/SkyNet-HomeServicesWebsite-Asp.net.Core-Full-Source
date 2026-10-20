using System.Text;
using System.Text.RegularExpressions;
using HomeServices.Models;

namespace HomeServices.Core
{
    public sealed class Ctx
    {
        public Staff U { get; init; } = new Staff();
        public string Tok { get; init; } = string.Empty;
        public string Own => Access.OwnTech(U);
    }

    public static class Auth
    {
        private static readonly Dictionary<string, List<DateTime>> Fails = new Dictionary<string, List<DateTime>>();
        private static readonly object FailLock = new object();

        private static readonly (string Group, string[] Keys)[] Groups =
        {
            ("Work", new[] { "quotes", "jobs", "dispatch", "invoices", "customers" }),
            ("Business", new[] { "techs", "services", "zips", "reviews" }),
            ("Website", new[] { "pages", "media", "messages" }),
            ("Admin", new[] { "activity", "settings", "users" })
        };

        public static async Task<Ctx?> Check(string? tok)
        {
            string t = (tok ?? string.Empty).Trim();
            if (!Regex.IsMatch(t, "^[a-f0-9]{32}$"))
            {
                return null;
            }
            List<Session> sessions = await Store.Sessions();
            Session? s = sessions.FirstOrDefault(x => x.Tok == t);
            if (s == null || s.Until < DateTime.UtcNow)
            {
                return null;
            }
            List<Staff> staff = await Store.LoadStaff();
            Staff? u = staff.FirstOrDefault(x => x.Id == s.UserId);
            if (u == null || !u.Active || u.MustChange || (u.Id == "u-demo" && !Store.Demo()))
            {
                return null;
            }
            if (s.Until < DateTime.UtcNow.AddHours(8).AddMinutes(-10))
            {
                using (await Store.Hold())
                {
                    List<Session> all = await Store.Sessions();
                    Session? mine = all.FirstOrDefault(x => x.Tok == t);
                    if (mine != null)
                    {
                        mine.Until = DateTime.UtcNow.AddHours(8);
                        all.RemoveAll(x => x.Until < DateTime.UtcNow);
                        await Store.SaveSessions(all);
                    }
                }
            }
            return new Ctx { U = u, Tok = t };
        }

        public static async Task<string> Open(string userId)
        {
            string tok = Access.NewToken(16);
            using (await Store.Hold())
            {
                List<Session> all = await Store.Sessions();
                all.RemoveAll(x => x.Until < DateTime.UtcNow);
                all.Add(new Session { Tok = tok, UserId = userId, Until = DateTime.UtcNow.AddHours(8) });
                await Store.SaveSessions(all);
            }
            return tok;
        }

        public static async Task Close(string tok)
        {
            using (await Store.Hold())
            {
                List<Session> all = await Store.Sessions();
                if (all.RemoveAll(x => x.Tok == tok) > 0)
                {
                    await Store.SaveSessions(all);
                }
            }
        }

        public static async Task CloseAll(string userId, string keep)
        {
            using (await Store.Hold())
            {
                List<Session> all = await Store.Sessions();
                if (all.RemoveAll(x => x.UserId == userId && x.Tok != keep) > 0)
                {
                    await Store.SaveSessions(all);
                }
            }
        }

        public static bool Paused(string login)
        {
            lock (FailLock)
            {
                if (!Fails.TryGetValue(login.ToLowerInvariant(), out List<DateTime>? list))
                {
                    return false;
                }
                list.RemoveAll(x => x < DateTime.UtcNow.AddMinutes(-5));
                return list.Count >= 6;
            }
        }

        public static void Failed(string login)
        {
            lock (FailLock)
            {
                string k = login.ToLowerInvariant();
                if (!Fails.TryGetValue(k, out List<DateTime>? list))
                {
                    list = new List<DateTime>();
                    Fails[k] = list;
                }
                list.Add(DateTime.UtcNow);
            }
        }

        public static void Cleared(string login)
        {
            lock (FailLock)
            {
                Fails.Remove(login.ToLowerInvariant());
            }
        }

        public static async Task<string> Menu(string px, string page, Ctx c)
        {
            List<QuoteRec> quotes = await Store.Quotes();
            List<MessageRec> msgs = await Store.Messages();
            List<JobRec> jobs = await Store.Jobs();
            string today = Work.Today();
            string own = c.Own;
            Dictionary<string, int> badge = new Dictionary<string, int>
            {
                { "quotes", quotes.Count(q => q.Status == "new") },
                { "messages", msgs.Count(m => !m.Read) },
                { "jobs", jobs.Count(j => j.Date == today && Work.Open(j.Status) && (own == string.Empty || j.TechId == own)) },
                { "invoices", jobs.Count(j => j.Status == "done" && j.InvoiceId == string.Empty) }
            };
            StringBuilder sb = new StringBuilder();
            sb.Append(Item(px, page, "Admin", "Dashboard", 0));
            foreach ((string group, string[] keys) in Groups)
            {
                List<Area> list = keys.Select(k => Access.AreaOf(k)!).Where(a => Access.Can(c.U, a.Key, 'V')).ToList();
                if (list.Count == 0)
                {
                    continue;
                }
                sb.Append("<div class=\"" + px + "-mg\">" + group + "</div>");
                foreach (Area a in list)
                {
                    sb.Append(Item(px, page, a.Page, a.Name, badge.TryGetValue(a.Key, out int n) && Access.Can(c.U, a.Key, 'V') ? n : 0));
                }
            }
            sb.Append("<div class=\"" + px + "-mg\">You</div>");
            if (!Access.Can(c.U, "activity", 'V'))
            {
                sb.Append(Item(px, page, "AdminActivity", "My activity", 0));
            }
            sb.Append(Item(px, page, "AdminAccount", "My account", 0));
            return sb.ToString();
        }

        private static string Item(string px, string page, string target, string name, int n)
        {
            return "<a class=\"" + px + "-mi" + (page == target ? " " + px + "-on" : string.Empty) + "\" href=\"" + target + "\"" + (page == target ? " aria-current=\"page\"" : string.Empty) + "><span>" + Ui.Esc(name) + "</span>" + (n > 0 ? "<i class=\"" + px + "-badge\">" + n + "</i>" : string.Empty) + "</a>";
        }

        public static string Who(string px, Ctx c)
        {
            return "<b>" + Ui.Esc(c.U.Name) + "</b><small>" + Ui.Esc(c.U.Admin ? "Manager" : c.U.Title.Length > 0 ? c.U.Title : "Staff") + "</small>";
        }
    }
}
