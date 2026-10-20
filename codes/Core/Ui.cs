using System.Globalization;
using System.Net;
using System.Text;

namespace HomeServices.Core
{
    public static class Ui
    {
        private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

        public static string Esc(string? v)
        {
            return WebUtility.HtmlEncode(v ?? string.Empty);
        }

        public static string Js(string? v)
        {
            return (v ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"").Replace("<", "\\u003C").Replace("\r", string.Empty).Replace("\n", "\\n");
        }

        public static string Clip(string? v, int max)
        {
            string s = (v ?? string.Empty).Trim();
            return s.Length > max ? s.Substring(0, max) : s;
        }

        public static string Count(int n, string one, string many)
        {
            return n.ToString("#,0", Us) + " " + (n == 1 ? one : many);
        }

        public static bool Has(string? text, string q)
        {
            return q.Length > 0 && (text ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase);
        }

        public static string When(DateTime utc)
        {
            DateTime t = utc.Kind == DateTimeKind.Local ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
            return t.ToString("MMM d, yyyy, h:mm tt", Us);
        }

        public static string Day(string iso)
        {
            return DateTime.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d) ? d.ToString("ddd, MMM d", Us) : iso;
        }

        public static string LongDay(string iso)
        {
            return DateTime.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d) ? d.ToString("dddd, MMMM d, yyyy", Us) : iso;
        }

        public static string Money(decimal v)
        {
            return (v < 0 ? "-$" : "$") + Math.Abs(v).ToString("#,0.00", Us);
        }

        public static string Price(decimal v)
        {
            return v == Math.Floor(v) ? "$" + v.ToString("#,0", Us) : Money(v);
        }

        public static string Num(decimal v)
        {
            return v.ToString("0.##", CultureInfo.InvariantCulture);
        }

        public static string Pager(string px, int page, int size, int total, string one, string many, string fn)
        {
            int pages = Math.Max(1, (total + size - 1) / size);
            StringBuilder sb = new StringBuilder("<div class=\"" + px + "-pager\"><span>Page " + page + " of " + pages + " &middot; " + Count(total, one, many) + "</span><div>");
            sb.Append("<button type=\"button\" class=\"" + px + "-btn " + px + "-btn-ghost " + px + "-btn-sm\"" + (page <= 1 ? " disabled" : " onclick=\"" + fn + "(" + (page - 1) + ")\"") + ">Previous</button>");
            sb.Append("<button type=\"button\" class=\"" + px + "-btn " + px + "-btn-ghost " + px + "-btn-sm\"" + (page >= pages ? " disabled" : " onclick=\"" + fn + "(" + (page + 1) + ")\"") + ">Next</button>");
            sb.Append("</div></div>");
            return sb.ToString();
        }

        public static int PageNo(string? v)
        {
            return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int p) && p > 0 && p < 100000 ? p : 1;
        }
    }
}
