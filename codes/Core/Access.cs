using System.Security.Cryptography;
using HomeServices.Models;

namespace HomeServices.Core
{
    public sealed class Area
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Page { get; init; } = string.Empty;
        public string Extra { get; init; } = string.Empty;
        public string Hint { get; init; } = string.Empty;
        public bool AdminOnly { get; init; }
        public bool NoEdit { get; init; }
        public bool Own { get; init; }
    }

    public static class Access
    {
        public static readonly Area[] Areas =
        {
            new Area { Key = "quotes", Name = "Quotes", Page = "AdminQuotes", Extra = "Send", Hint = "Requests and written quotes. Extra: send quotes to customers and withdraw them." },
            new Area { Key = "jobs", Name = "Jobs", Page = "AdminJobs", Extra = "Step back", Own = true, Hint = "Schedule and move jobs along. Extra: step a job back a status." },
            new Area { Key = "dispatch", Name = "Dispatch board", Page = "AdminDispatch", Own = true, Hint = "Day view by technician. Edit: assign and move jobs on the board." },
            new Area { Key = "invoices", Name = "Invoices", Page = "AdminInvoices", Extra = "Mark paid", Hint = "Invoices from finished jobs. Extra: record payments and void invoices." },
            new Area { Key = "customers", Name = "Customers", Page = "AdminCustomers", NoEdit = true, Hint = "History of quotes, jobs and invoices per customer." },
            new Area { Key = "techs", Name = "Technicians", Page = "AdminTechs", Hint = "Technicians, their trades and working days." },
            new Area { Key = "services", Name = "Services and prices", Page = "AdminServices", Hint = "Trades, services and starting prices." },
            new Area { Key = "zips", Name = "Service area", Page = "AdminZips", Hint = "Zones, trip fees and ZIP codes." },
            new Area { Key = "reviews", Name = "Reviews", Page = "AdminReviews", Extra = "Publish", Hint = "Reviews written from real customer feedback. Extra: publish and delete." },
            new Area { Key = "pages", Name = "Pages", Page = "AdminPages", Hint = "Words and pictures of the public pages." },
            new Area { Key = "media", Name = "Media library", Page = "AdminMedia", Extra = "Delete files", Hint = "Photos and videos. Extra: delete files." },
            new Area { Key = "messages", Name = "Messages", Page = "AdminMessages", Extra = "Delete", Hint = "Contact form inbox. Extra: delete messages." },
            new Area { Key = "activity", Name = "Activity log", Page = "AdminActivity", NoEdit = true, Hint = "Everything staff did, including refused requests." },
            new Area { Key = "settings", Name = "Settings", Page = "AdminSettings", AdminOnly = true, Hint = "Business details, tax rate, quote and invoice rules." },
            new Area { Key = "users", Name = "Users and rights", Page = "AdminUsers", AdminOnly = true, Hint = "Staff accounts and what each can do." }
        };

        public static Area? AreaOf(string key)
        {
            return Areas.FirstOrDefault(a => a.Key == key);
        }

        public static string Level(Staff u, string area)
        {
            Area? a = AreaOf(area);
            if (a == null || !u.Active)
            {
                return string.Empty;
            }
            if (u.Admin)
            {
                return a.NoEdit ? "V" : a.Extra.Length > 0 ? "VEX" : "VE";
            }
            if (a.AdminOnly)
            {
                return string.Empty;
            }
            return u.Perms.TryGetValue(area, out string? p) ? CleanLevel(a, p) : string.Empty;
        }

        public static bool Can(Staff u, string area, char right)
        {
            return Level(u, area).Contains(right);
        }

        public static string CleanLevel(Area a, string? raw)
        {
            string p = (raw ?? string.Empty).ToUpperInvariant();
            if (a.AdminOnly || !p.Contains('V'))
            {
                return string.Empty;
            }
            if (a.NoEdit || !p.Contains('E'))
            {
                return "V";
            }
            return p.Contains('X') && a.Extra.Length > 0 ? "VEX" : "VE";
        }

        public static Dictionary<string, string> CleanPerms(Dictionary<string, string> raw)
        {
            Dictionary<string, string> clean = new Dictionary<string, string>();
            foreach (Area a in Areas)
            {
                string lvl = CleanLevel(a, raw.TryGetValue(a.Key, out string? v) ? v : string.Empty);
                if (lvl.Length > 0)
                {
                    clean[a.Key] = lvl;
                }
            }
            return clean;
        }

        public static string OwnTech(Staff u)
        {
            if (u.Admin || !u.Own)
            {
                return string.Empty;
            }
            return u.TechId.Length > 0 ? u.TechId : "-none-";
        }

        public static string HashPassword(string pass)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(pass, salt, 100000, HashAlgorithmName.SHA256, 32);
            return "pbkdf2$100000$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);
        }

        public static bool Verify(string pass, string stored)
        {
            string[] p = (stored ?? string.Empty).Split('$');
            if (p.Length != 4 || p[0] != "pbkdf2" || !int.TryParse(p[1], out int rounds))
            {
                return false;
            }
            try
            {
                byte[] salt = Convert.FromBase64String(p[2]);
                byte[] want = Convert.FromBase64String(p[3]);
                byte[] got = Rfc2898DeriveBytes.Pbkdf2(pass ?? string.Empty, salt, rounds, HashAlgorithmName.SHA256, want.Length);
                return CryptographicOperations.FixedTimeEquals(got, want);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public static string NewPassword()
        {
            const string letters = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
            char[] c = new char[14];
            for (int i = 0; i < c.Length; i++)
            {
                c[i] = i == 4 || i == 9 ? '-' : letters[RandomNumberGenerator.GetInt32(letters.Length)];
            }
            return new string(c);
        }

        public static string NewToken(int bytes)
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(bytes)).ToLowerInvariant();
        }
    }
}
