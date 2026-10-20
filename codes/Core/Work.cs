using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using HomeServices.Models;

namespace HomeServices.Core
{
    public static class Work
    {
        public static readonly (string Key, string Name, int Hour)[] Windows =
        {
            ("w08", "8–10 am", 8),
            ("w10", "10 am–12 pm", 10),
            ("w12", "12–2 pm", 12),
            ("w14", "2–4 pm", 14),
            ("w16", "4–6 pm", 16)
        };

        public static readonly (string Key, string Name)[] Times =
        {
            ("morning", "Morning, 8 am–12 pm"),
            ("afternoon", "Afternoon, 12–4 pm"),
            ("late", "Late afternoon, 4–6 pm")
        };

        public static readonly string[] DayNames = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

        public static readonly string[] LongDayNames = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

        public static readonly (string Key, string Name)[] Methods = { ("card", "Card"), ("cash", "Cash"), ("check", "Check") };

        public static readonly (string Key, string Name)[] Sources = { ("phone", "Phone call"), ("email", "Email"), ("person", "In person"), ("card", "Feedback card"), ("text", "Text message") };

        private const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public static string Today()
        {
            return DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        public static string Iso(DateTime d)
        {
            return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        public static DateTime? ParseDay(string? v)
        {
            return DateTime.TryParseExact((v ?? string.Empty).Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d) ? d.Date : null;
        }

        public static int DayIndex(DateTime d)
        {
            return ((int)d.DayOfWeek + 6) % 7 + 1;
        }

        public static string WindowName(string key)
        {
            return Windows.FirstOrDefault(w => w.Key == key).Name ?? key;
        }

        public static string TimeName(string key)
        {
            return Times.FirstOrDefault(t => t.Key == key).Name ?? key;
        }

        public static string MethodName(string key)
        {
            return Methods.FirstOrDefault(t => t.Key == key).Name ?? key;
        }

        public static string SourceName(string key)
        {
            return Sources.FirstOrDefault(t => t.Key == key).Name ?? key;
        }

        public static string Code(string prefix)
        {
            char[] c = new char[6];
            for (int i = 0; i < c.Length; i++)
            {
                c[i] = Letters[RandomNumberGenerator.GetInt32(Letters.Length)];
            }
            return prefix + "-" + new string(c);
        }

        public static string Digits(string? phone)
        {
            return Regex.Replace(phone ?? string.Empty, "[^0-9]", string.Empty);
        }

        public static string Tel(string phone)
        {
            string d = Digits(phone);
            return d.Length == 10 ? "tel:+1" + d : "tel:+" + d;
        }

        public static bool IsPhone(string v)
        {
            string d = Digits(v);
            return d.Length >= 10 && d.Length <= 15 && Regex.IsMatch(v, "^[0-9+() .-]{10,24}$");
        }

        public static bool IsEmail(string v)
        {
            if (v.Length < 5 || v.Length > 80 || v.Contains(' '))
            {
                return false;
            }
            int at = v.IndexOf('@');
            int dot = v.LastIndexOf('.');
            return at > 0 && at == v.LastIndexOf('@') && dot > at + 1 && dot < v.Length - 1;
        }

        public static string CustomerKey(string email, string phone)
        {
            string e = (email ?? string.Empty).Trim().ToLowerInvariant();
            return e.Length > 0 ? e : "tel:" + Digits(phone);
        }

        public static (decimal Sub, decimal Tax, decimal Total) Totals(List<LineItem> lines, decimal trip, decimal rate)
        {
            decimal sub = lines.Sum(l => Math.Round(l.Qty * l.Price, 2, MidpointRounding.AwayFromZero)) + trip;
            decimal tax = Math.Round(sub * rate / 100m, 2, MidpointRounding.AwayFromZero);
            return (sub, tax, sub + tax);
        }

        public static decimal? Amount(string? v, decimal min, decimal max)
        {
            string s = (v ?? string.Empty).Trim().Replace("$", string.Empty).Replace(",", string.Empty);
            if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal d) && d >= min && d <= max)
            {
                return Math.Round(d, 2, MidpointRounding.AwayFromZero);
            }
            return null;
        }

        public static bool Open(string jobStatus)
        {
            return jobStatus is "scheduled" or "onway" or "inprogress";
        }

        public static string QuoteStatusName(string s)
        {
            return s switch
            {
                "new" => "New request",
                "draft" => "Draft",
                "sent" => "Sent",
                "accepted" => "Accepted",
                "declined" => "Declined",
                "expired" => "Expired",
                "closed" => "Closed",
                _ => s
            };
        }

        public static string JobStatusName(string s)
        {
            return s switch
            {
                "scheduled" => "Scheduled",
                "onway" => "On the way",
                "inprogress" => "In progress",
                "done" => "Done",
                "cancelled" => "Cancelled",
                _ => s
            };
        }

        public static string InvoiceStatusName(InvoiceRec i)
        {
            if (i.Status == "issued" && Overdue(i))
            {
                return "Overdue";
            }
            return i.Status switch
            {
                "draft" => "Draft",
                "issued" => "Unpaid",
                "paid" => "Paid",
                "void" => "Void",
                _ => i.Status
            };
        }

        public static bool Overdue(InvoiceRec i)
        {
            return i.Status == "issued" && i.Due.Length > 0 && string.CompareOrdinal(i.Due, Today()) < 0;
        }

        public static string Pill(string px, string tone, string text)
        {
            return "<span class=\"" + px + "-pill " + px + "-pill-" + tone + "\">" + Ui.Esc(text) + "</span>";
        }

        public static string QuotePill(string px, string s)
        {
            string tone = s switch { "new" => "new", "draft" => "wait", "sent" => "info", "accepted" => "go", "declined" => "bad", _ => "off" };
            return Pill(px, tone, QuoteStatusName(s));
        }

        public static string JobPill(string px, string s)
        {
            string tone = s switch { "scheduled" => "info", "onway" => "wait", "inprogress" => "new", "done" => "go", _ => "off" };
            return Pill(px, tone, JobStatusName(s));
        }

        public static string InvoicePill(string px, InvoiceRec i)
        {
            string tone = i.Status switch { "draft" => "wait", "issued" => Overdue(i) ? "bad" : "info", "paid" => "go", _ => "off" };
            return Pill(px, tone, InvoiceStatusName(i));
        }

        public static bool ExpireQuotes(List<QuoteRec> list)
        {
            bool changed = false;
            foreach (QuoteRec q in list.Where(x => x.Status == "sent" && x.Expires.HasValue && x.Expires.Value < DateTime.UtcNow))
            {
                q.Status = "expired";
                changed = true;
            }
            return changed;
        }

        public static string TradeName(SiteData site, string key)
        {
            return site.Trades.FirstOrDefault(t => t.Key == key)?.Name ?? key;
        }

        public static string TradeColor(SiteData site, string key)
        {
            string c = site.Trades.FirstOrDefault(t => t.Key == key)?.Color ?? string.Empty;
            return Regex.IsMatch(c, "^#[0-9A-Fa-f]{6}$") ? c : "#5B6670";
        }

        public static string ServiceNames(SiteData site, List<string> ids)
        {
            return string.Join(", ", ids.Select(id => site.Services.FirstOrDefault(s => s.Id == id)?.Name ?? id));
        }

        public static string MainTrade(SiteData site, QuoteRec q)
        {
            return q.Services.Select(id => site.Services.FirstOrDefault(s => s.Id == id)?.Trade ?? string.Empty).FirstOrDefault(t => t.Length > 0) ?? string.Empty;
        }

        public static decimal TripFee(SiteData site, string zip)
        {
            ZipArea? z = site.Zips.FirstOrDefault(x => x.Zip == zip);
            return z == null ? 0 : site.Zones.FirstOrDefault(x => x.Key == z.Zone)?.Fee ?? 0;
        }

        public static string TechName(SiteData site, string id)
        {
            return id.Length == 0 ? "Not assigned" : site.Techs.FirstOrDefault(t => t.Id == id)?.Name ?? "Removed technician";
        }

        public static string Initials(string name)
        {
            string[] parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? "?" : string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));
        }

        public static string DaysText(List<int> days)
        {
            return days.Count == 0 ? "No working days" : string.Join(", ", days.OrderBy(d => d).Where(d => d >= 1 && d <= 7).Select(d => DayNames[d - 1]));
        }
    }
}
