using System.Text.Json;
using HomeServices.Models;

namespace HomeServices.Core
{
    public static class Store
    {
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim FileGate = new SemaphoreSlim(1, 1);
        private static DateTime configStamp = DateTime.MinValue;
        private static bool demoMode;

        public static readonly JsonSerializerOptions Opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

        public static string Root => Directory.GetCurrentDirectory();

        public static string DataDir => Path.Combine(Root, "data");

        public static string ConfigFile => Path.Combine(Root, "appConfig", "application.cfg");

        public static string InitialFile => Path.Combine(Root, "appConfig", "initial-admin.txt");

        public static string SessionFile => Path.Combine(Root, "temp", "admin", "sessions.json");

        private sealed class Releaser : IDisposable
        {
            private readonly SemaphoreSlim gate;
            private bool done;

            public Releaser(SemaphoreSlim g)
            {
                gate = g;
            }

            public void Dispose()
            {
                if (!done)
                {
                    done = true;
                    gate.Release();
                }
            }
        }

        public static async Task<IDisposable> Hold()
        {
            await Gate.WaitAsync();
            return new Releaser(Gate);
        }

        public static async Task<T> Read<T>(string file, Func<T> empty)
        {
            if (!File.Exists(file))
            {
                return empty();
            }
            try
            {
                string json = await File.ReadAllTextAsync(file);
                return JsonSerializer.Deserialize<T>(json, Opts) ?? empty();
            }
            catch (JsonException)
            {
                return empty();
            }
            catch (IOException)
            {
                return empty();
            }
        }

        public static async Task Write<T>(string file, T value)
        {
            await FileGate.WaitAsync();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file) ?? Root);
                string tmp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
                await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(value, Opts));
                File.Move(tmp, file, true);
            }
            finally
            {
                FileGate.Release();
            }
        }

        public static async Task<SiteData> LoadSite()
        {
            string file = Path.Combine(DataDir, "site.json");
            SiteData site = await Read(file, () => new SiteData());
            int before = site.Pages.Count;
            int blocks = site.Pages.Values.Sum(p => p.Blocks.Count);
            Blocks.Ensure(site);
            if (File.Exists(file) && (site.Pages.Count != before || site.Pages.Values.Sum(p => p.Blocks.Count) != blocks))
            {
                await Write(file, site);
            }
            return site;
        }

        public static Task SaveSite(SiteData site)
        {
            return Write(Path.Combine(DataDir, "site.json"), site);
        }

        private static string DataFile(string name)
        {
            return Path.Combine(DataDir, name + ".json");
        }

        public static Task<List<QuoteRec>> Quotes()
        {
            return Read(DataFile("quotes"), () => new List<QuoteRec>());
        }

        public static Task SaveQuotes(List<QuoteRec> list)
        {
            return Write(DataFile("quotes"), list);
        }

        public static Task<List<JobRec>> Jobs()
        {
            return Read(DataFile("jobs"), () => new List<JobRec>());
        }

        public static Task SaveJobs(List<JobRec> list)
        {
            return Write(DataFile("jobs"), list);
        }

        public static Task<List<InvoiceRec>> Invoices()
        {
            return Read(DataFile("invoices"), () => new List<InvoiceRec>());
        }

        public static Task SaveInvoices(List<InvoiceRec> list)
        {
            return Write(DataFile("invoices"), list);
        }

        public static Task<List<ReviewRec>> Reviews()
        {
            return Read(DataFile("reviews"), () => new List<ReviewRec>());
        }

        public static Task SaveReviews(List<ReviewRec> list)
        {
            return Write(DataFile("reviews"), list);
        }

        public static Task<List<MessageRec>> Messages()
        {
            return Read(DataFile("messages"), () => new List<MessageRec>());
        }

        public static Task SaveMessages(List<MessageRec> list)
        {
            return Write(DataFile("messages"), list);
        }

        public static Task<List<Staff>> LoadStaff()
        {
            return Read(DataFile("staff"), () => new List<Staff>());
        }

        public static Task SaveStaff(List<Staff> list)
        {
            return Write(DataFile("staff"), list);
        }

        public static Task<List<Session>> Sessions()
        {
            return Read(SessionFile, () => new List<Session>());
        }

        public static Task SaveSessions(List<Session> list)
        {
            return Write(SessionFile, list);
        }

        public static Task<List<LogEntry>> LoadLog()
        {
            return Read(DataFile("activity"), () => new List<LogEntry>());
        }

        public static async Task Log(string userId, string action, string target, bool ok)
        {
            List<Staff> staff = await LoadStaff();
            string who = userId == "u-demo" ? "Demo manager" : userId == "customer" ? "Customer" : staff.FirstOrDefault(s => s.Id == userId)?.Name ?? userId;
            using (await Hold())
            {
                List<LogEntry> list = await LoadLog();
                list.Insert(0, new LogEntry { When = DateTime.UtcNow, UserId = userId, Who = who, Action = action, Target = target.Length > 160 ? target.Substring(0, 160) : target, Ok = ok });
                if (list.Count > 5000)
                {
                    list.RemoveRange(5000, list.Count - 5000);
                }
                await Write(DataFile("activity"), list);
            }
        }

        public static bool Demo()
        {
            if (!File.Exists(ConfigFile))
            {
                return false;
            }
            DateTime stamp = File.GetLastWriteTimeUtc(ConfigFile);
            if (stamp != configStamp)
            {
                string line = File.ReadLines(ConfigFile).Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("app.app.runmode", StringComparison.Ordinal)) ?? string.Empty;
                string[] part = line.Split('|', 2);
                demoMode = part.Length == 2 && part[1].Trim() == "0";
                configStamp = stamp;
            }
            return demoMode;
        }

        public static async Task<Staff> DemoManager()
        {
            using (await Hold())
            {
                List<Staff> staff = await LoadStaff();
                Staff? u = staff.FirstOrDefault(x => x.Id == "u-demo");
                if (u == null)
                {
                    u = new Staff { Id = "u-demo", Login = "demo", Name = "Demo manager", Title = "Manager", Admin = true, Active = true };
                    staff.Add(u);
                    await SaveStaff(staff);
                }
                return u;
            }
        }

        public static async Task EnsureFirstManager()
        {
            if (Demo())
            {
                return;
            }
            using (await Hold())
            {
                List<Staff> staff = await LoadStaff();
                if (staff.Any(u => u.Admin && u.Active && !u.MustChange && u.Hash != string.Empty && u.Id != "u-demo"))
                {
                    return;
                }
                if (File.Exists(InitialFile) && staff.Any(u => u.Admin && u.Active && u.MustChange))
                {
                    return;
                }
                string pass = Access.NewPassword();
                Staff? u = staff.FirstOrDefault(x => x.Login == "admin");
                if (u == null)
                {
                    u = new Staff { Id = "u-admin", Login = "admin", Name = "Administrator", Title = "Manager" };
                    staff.Insert(0, u);
                }
                u.Admin = true;
                u.Active = true;
                u.MustChange = true;
                u.Hash = Access.HashPassword(pass);
                await SaveStaff(staff);
                Directory.CreateDirectory(Path.GetDirectoryName(InitialFile) ?? Root);
                await File.WriteAllTextAsync(InitialFile, "user: admin" + Environment.NewLine + "password: " + pass + Environment.NewLine);
            }
        }

        public static string InitialPassword()
        {
            if (!File.Exists(InitialFile))
            {
                return string.Empty;
            }
            string line = File.ReadAllLines(InitialFile).FirstOrDefault(l => l.StartsWith("password: ", StringComparison.Ordinal)) ?? string.Empty;
            return line.Length > 10 ? line.Substring(10).Trim() : string.Empty;
        }
    }
}
