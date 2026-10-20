using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HomeServices.Core;
using HomeServices.Models;
using SkyNet;

namespace HomeServices.codes
{
    public class ServiceArea : WebPage
    {
        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle("Service area | " + site.Brand);
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("description", "Check your ZIP code and see the towns we cover and their trip fees.");
            List<MediaItem> media = await Media.Load();
            HtmlDoc.HtmlBodyText = Chrome(HtmlDoc.HtmlBodyText.Replace("{plhd_blocks}", PageHtml(site, media, "area")), site);
        }

        public async Task<ApiResponse> CheckZip()
        {
            ApiResponse response = new ApiResponse();
            string zip = Ui.Clip(GetDataValue("zipcode"), 10);
            if (!Regex.IsMatch(zip, "^[0-9]{5}$"))
            {
                response.SetElementContents("sa-zipr", "<div class=\"sa-zipno\"><span>Please enter a 5-digit ZIP code.</span></div>");
                return response;
            }
            SiteData site = await Store.LoadSite();
            ZipArea? z = site.Zips.FirstOrDefault(x => x.Zip == zip);
            if (z == null)
            {
                response.SetElementContents("sa-zipr", "<div class=\"sa-zipno\"><span><b>We do not cover " + zip + " yet.</b> You can still <a href=\"Quote?zip=" + zip + "\">send a quote request</a>, and we will tell you if we can make the trip.</span></div>");
                return response;
            }
            decimal fee = site.Zones.FirstOrDefault(x => x.Key == z.Zone)?.Fee ?? 0;
            response.SetElementContents("sa-zipr", "<div class=\"sa-zipyes\"><span><b>Yes, we come to " + Ui.Esc(z.Town) + " (" + zip + ").</b> " + (fee > 0 ? "The trip fee is " + Ui.Money(fee) + " per visit." : "There is no trip fee.") + " <a href=\"Quote?zip=" + zip + "\">Request a quote</a></span></div>");
            return response;
        }

        private static string ZipBox(SiteData site, Block b, bool first)
        {
            string tag = first ? "h1" : "h2";
            string head = Kick(site, b) + (b.Title == string.Empty ? string.Empty : "<" + tag + " class=\"sa-bh2\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">") + Rich(site, b.Text, "sa-blead");
            return "<section class=\"sa-wrap sa-sec\" id=\"" + Ui.Esc(b.Id) + "\"><div class=\"sa-zip\"><div>" + head + "</div><div><form class=\"sa-zipf\" onsubmit=\"return ServiceAreaJs.zip()\" novalidate><label class=\"sa-sr\" for=\"sa-zipin\">ZIP code</label><input type=\"text\" id=\"sa-zipin\" inputmode=\"numeric\" maxlength=\"5\" autocomplete=\"postal-code\" placeholder=\"ZIP code\"><button type=\"submit\" class=\"sa-btn\">Check my ZIP</button></form><div class=\"sa-zipr\" id=\"sa-zipr\" aria-live=\"polite\"></div></div></div></section>";
        }

        private string DataPart(SiteData site, Block b, bool first)
        {
            switch (b.Source)
            {
                case "zipcheck":
                    return ZipBox(site, b, first);
                case "towns":
                    {
                        StringBuilder sb = new StringBuilder(Head(site, b, true, first ? "h1" : "h2") + "<div class=\"sa-zones\">");
                        foreach (Zone z in site.Zones)
                        {
                            List<ZipArea> zips = site.Zips.Where(x => x.Zone == z.Key).OrderBy(x => x.Town).ThenBy(x => x.Zip).ToList();
                            if (zips.Count == 0)
                            {
                                continue;
                            }
                            sb.Append("<div class=\"sa-zone\"><div class=\"sa-zone-h\"><h3>" + Ui.Esc(z.Name) + "</h3><b>" + (z.Fee > 0 ? Ui.Price(z.Fee) + " trip fee" : "No trip fee") + "</b></div><ul>");
                            foreach (ZipArea a in zips)
                            {
                                sb.Append("<li><span>" + Ui.Esc(a.Town) + "</span><small>" + Ui.Esc(a.Zip) + "</small></li>");
                            }
                            sb.Append("</ul></div>");
                        }
                        sb.Append("</div>");
                        return Sec(b, sb.ToString());
                    }
                default:
                    return string.Empty;
            }
        }

        private static string Chrome(string html, SiteData site)
        {
            StringBuilder trades = new StringBuilder();
            foreach (Trade t in site.Trades)
            {
                trades.Append("<li><a href=\"Services#trade-" + Ui.Esc(t.Key) + "\">" + Ui.Esc(t.Name) + "</a></li>");
            }
            return html
                .Replace("{plhd_brand}", Ui.Esc(site.Brand))
                .Replace("{plhd_tagline}", Ui.Esc(site.Tagline))
                .Replace("{plhd_phone}", Ui.Esc(site.Phone))
                .Replace("{plhd_tel}", Ui.Esc(Work.Tel(site.Phone)))
                .Replace("{plhd_email}", Ui.Esc(site.Email))
                .Replace("{plhd_address}", Ui.Esc(site.Address))
                .Replace("{plhd_hours}", string.Concat(site.Hours.Select(h => "<li>" + Ui.Esc(h) + "</li>")))
                .Replace("{plhd_licence}", Ui.Esc(site.Licence))
                .Replace("{plhd_year}", DateTime.Now.Year.ToString(CultureInfo.InvariantCulture))
                .Replace("{plhd_tradelinks}", trades.ToString());
        }

        private static string Tok(SiteData site, string v)
        {
            return v.Replace("{name}", site.Brand);
        }

        private static string TokH(SiteData site, string v)
        {
            return v.Replace("{name}", Ui.Esc(site.Brand));
        }

        private static string Lines(string v)
        {
            return Ui.Esc(v).Replace("\n", "<br>");
        }

        private static string Icon(string k)
        {
            string a = "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\">";
            return k switch
            {
                "wrench" => a + "<path d=\"M14.5 4.5a4.5 4.5 0 0 0-4.3 5.8L4 16.5 7.5 20l6.2-6.2a4.5 4.5 0 0 0 5.8-4.3l-2.6 2.6-3-1-1-3z\"/></svg>",
                "drop" => a + "<path d=\"M12 3.5s-6 6.6-6 10.5a6 6 0 0 0 12 0c0-3.9-6-10.5-6-10.5z\"/></svg>",
                "flame" => a + "<path d=\"M12 21c-3.9 0-6.5-2.7-6.5-6.2 0-4.3 4.3-6.2 4.3-10.3 2.9 1.6 5 4.4 5 7.2.9-.6 1.5-1.6 1.7-2.8 1.3 1.4 2 3.5 2 5.9 0 3.5-2.6 6.2-6.5 6.2z\"/></svg>",
                "snow" => a + "<path d=\"M12 3v18M4.2 7.5l15.6 9M4.2 16.5l15.6-9M9.5 4.5 12 7l2.5-2.5M9.5 19.5 12 17l2.5 2.5\"/></svg>",
                "bolt" => a + "<path d=\"M13 3 5 13.5h6L10 21l8-10.5h-6z\"/></svg>",
                "spark" => a + "<path d=\"M12 3.5l1.8 5.2 5.2 1.8-5.2 1.8L12 17.5l-1.8-5.2L5 10.5l5.2-1.8zM18.5 15.5l.8 2.2 2.2.8-2.2.8-.8 2.2-.8-2.2-2.2-.8 2.2-.8z\"/></svg>",
                "home" => a + "<path d=\"M3.5 11 12 4l8.5 7M6 9.5V20h12V9.5M10 20v-5.5h4V20\"/></svg>",
                "clock" => a + "<circle cx=\"12\" cy=\"12\" r=\"8.5\"/><path d=\"M12 7.5V12l3 2\"/></svg>",
                "pin" => a + "<path d=\"M12 21s-6.5-6-6.5-11a6.5 6.5 0 0 1 13 0c0 5-6.5 11-6.5 11z\"/><circle cx=\"12\" cy=\"10\" r=\"2.4\"/></svg>",
                "cal" => a + "<rect x=\"3.5\" y=\"5\" width=\"17\" height=\"15\" rx=\"2\"/><path d=\"M3.5 10h17M8 3v4M16 3v4\"/></svg>",
                "shield" => a + "<path d=\"M12 3.5 5 6v5.5c0 4.5 3 7.8 7 9 4-1.2 7-4.5 7-9V6z\"/><path d=\"M9 12l2.2 2.2L15.5 10\"/></svg>",
                "star" => a + "<path d=\"M12 3.5l2.6 5.4 5.9.8-4.3 4.1 1 5.8L12 16.9l-5.2 2.7 1-5.8-4.3-4.1 5.9-.8z\"/></svg>",
                "users" => a + "<circle cx=\"9\" cy=\"8\" r=\"3.2\"/><path d=\"M3 19c0-3.3 2.7-5.5 6-5.5s6 2.2 6 5.5\"/><circle cx=\"17\" cy=\"9\" r=\"2.4\"/><path d=\"M16 13.6c2.8 0 5 1.8 5 4.9\"/></svg>",
                "check" => "<svg viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.4\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M5 12.5l4.5 4.5L19 7.5\"/></svg>",
                "phone" => a + "<path d=\"M6.5 3.5h3l1.5 4.5-2 1.5a11 11 0 0 0 5.5 5.5l1.5-2 4.5 1.5v3a2 2 0 0 1-2 2A16 16 0 0 1 4.5 5.5a2 2 0 0 1 2-2z\"/></svg>",
                "mail" => a + "<rect x=\"3.5\" y=\"5.5\" width=\"17\" height=\"13\" rx=\"2\"/><path d=\"M4 7l8 6 8-6\"/></svg>",
                "card" => a + "<rect x=\"3\" y=\"6\" width=\"18\" height=\"12\" rx=\"2\"/><path d=\"M3 10h18M7 15h4\"/></svg>",
                _ => string.Empty
            };
        }

        private static string Mv(List<MediaItem> media, string src, string alt, bool bg, bool eager)
        {
            if (src == string.Empty)
            {
                return string.Empty;
            }
            MediaItem? m = media.FirstOrDefault(x => x.Path == src);
            if (Blocks.IsVideo(src))
            {
                return "<video src=\"" + Ui.Esc(src) + "\" playsinline " + (bg ? "autoplay muted loop aria-hidden=\"true\"" : "controls preload=\"metadata\" aria-label=\"" + Ui.Esc(alt) + "\"") + "></video>";
            }
            return "<img src=\"" + Ui.Esc(src) + "\" alt=\"" + Ui.Esc(m != null && m.Alt != string.Empty ? m.Alt : alt) + "\"" + (eager ? string.Empty : " loading=\"lazy\"") + ">";
        }

        private static string Btn(SiteData site, string label, string link, bool main)
        {
            return label == string.Empty || link == string.Empty ? string.Empty : "<a class=\"sa-btn" + (main ? string.Empty : " sa-btn-ghost") + "\" href=\"" + Ui.Esc(link) + "\"" + (link.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? " target=\"_blank\" rel=\"noopener\"" : string.Empty) + ">" + Ui.Esc(Tok(site, label)) + "</a>";
        }

        private static string Btns(SiteData site, Block b)
        {
            string x = Btn(site, b.Label, b.Link, true) + Btn(site, b.Label2, b.Link2, false);
            return x == string.Empty ? string.Empty : "<div class=\"sa-bbtns\">" + x + "</div>";
        }

        private static string Kick(SiteData site, Block b)
        {
            return b.Kicker == string.Empty ? string.Empty : "<div class=\"sa-eyebrow\">" + Ui.Esc(Tok(site, b.Kicker)) + "</div>";
        }

        private static string H2(SiteData site, Block b, string tag = "h2")
        {
            return b.Title == string.Empty ? string.Empty : "<" + tag + " class=\"sa-bh2\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">";
        }

        private static string Rich(SiteData site, string html, string cls)
        {
            return html == string.Empty ? string.Empty : "<div class=\"sa-rich" + (cls == string.Empty ? string.Empty : " " + cls) + "\">" + TokH(site, html) + "</div>";
        }

        private static string Head(SiteData site, Block b, bool lead, string tag = "h2")
        {
            string x = Kick(site, b) + H2(site, b, tag) + (lead ? Rich(site, b.Text, "sa-blead") : string.Empty);
            return x == string.Empty ? string.Empty : "<div class=\"sa-bhead\">" + x + "</div>";
        }

        private static string Sec(Block b, string inner)
        {
            return "<section class=\"sa-wrap sa-sec\" id=\"" + Ui.Esc(b.Id) + "\">" + inner + "</section>";
        }

        private static string Hero(SiteData site, List<MediaItem> media, Block b, bool h1)
        {
            string tag = h1 ? "h1" : "h2";
            bool vid = Blocks.IsVideo(b.Media);
            string mv = Mv(media, b.Media, Tok(site, b.Title), vid, h1);
            return "<section class=\"sa-bhero" + (mv == string.Empty ? " sa-bplain" : string.Empty) + "\" id=\"" + Ui.Esc(b.Id) + "\">" + (mv != string.Empty ? "<div class=\"sa-bhero-p\">" + mv + "</div>" : string.Empty) + "<div class=\"sa-wrap sa-bhero-in\"><div class=\"sa-bhero-t\">" + Kick(site, b) + "<" + tag + " class=\"sa-btitle\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">" + Rich(site, b.Text, "sa-blead") + Btns(site, b) + "</div></div></section>";
        }

        private static string Split(SiteData site, List<MediaItem> media, Block b)
        {
            string btn = Btn(site, b.Label, b.Link, true);
            return Sec(b, "<div class=\"sa-bsplit" + (b.Flip ? " sa-bflip" : string.Empty) + (b.Media == string.Empty ? " sa-bnomedia" : string.Empty) + "\">" + (b.Media != string.Empty ? "<div class=\"sa-bsm\">" + Mv(media, b.Media, Tok(site, b.Title), false, false) + "</div>" : string.Empty) + "<div class=\"sa-bst\">" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, string.Empty) + (btn != string.Empty ? "<div class=\"sa-bbtns\">" + btn + "</div>" : string.Empty) + "</div></div>");
        }

        private static string Cards(SiteData site, Block b)
        {
            int n = Math.Clamp(b.Items.Count, 1, 4);
            if (b.Items.Count == 6)
            {
                n = 3;
            }
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"sa-bcards sa-n" + n + "\">");
            foreach (BlockItem it in b.Items)
            {
                string ico = Icon(it.Icon);
                string inner = (ico != string.Empty ? "<span class=\"sa-wico\">" + ico + "</span>" : string.Empty) + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b>" + (it.Text != string.Empty ? "<p>" + Lines(Tok(site, it.Text)) + "</p>" : string.Empty);
                if (it.Link != string.Empty)
                {
                    sb.Append("<a class=\"sa-bcard\" href=\"" + Ui.Esc(it.Link) + "\">" + inner + "<span class=\"sa-bmore\">" + Ui.Esc(it.Label != string.Empty ? it.Label : "Learn more") + " &rarr;</span></a>");
                }
                else
                {
                    sb.Append("<div class=\"sa-bcard\">" + inner + "</div>");
                }
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Steps(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<ol class=\"sa-bsteps sa-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
            int i = 1;
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<li><i>" + i++ + "</i><b>" + Ui.Esc(Tok(site, it.Title)) + "</b><p>" + Lines(Tok(site, it.Text)) + "</p></li>");
            }
            sb.Append("</ol>");
            return Sec(b, sb.ToString());
        }

        private static string StatRow(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, false) + "<div class=\"sa-bstats sa-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div><b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

                private static string People(SiteData site, List<MediaItem> media, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"sa-bpeople sa-n" + Math.Clamp(b.Items.Count, 1, 4) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div class=\"sa-bperson\">" + (it.Media != string.Empty && !Blocks.IsVideo(it.Media) ? Mv(media, it.Media, it.Title, false, false) : "<span class=\"sa-bnoface\">" + Ui.Esc(Work.Initials(it.Title)) + "</span>") + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Boxes(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, false) + "<div class=\"sa-bboxes sa-n" + Math.Clamp(b.Items.Count, 1, 3) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div class=\"sa-bbox\">" + (it.Title != string.Empty ? "<h3>" + Ui.Esc(Tok(site, it.Title)) + "</h3>" : string.Empty) + "<ul class=\"sa-bticks\">");
                foreach (string line in it.Text.Split('\n').Select(x => x.Trim()).Where(x => x != string.Empty))
                {
                    sb.Append("<li>" + Icon("check") + "<span>" + Ui.Esc(Tok(site, line)) + "</span></li>");
                }
                sb.Append("</ul></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Faq(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder("<div class=\"sa-bfaq\">" + Head(site, b, false) + "<div class=\"sa-bfaqs\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<details class=\"sa-bq\"><summary>" + Ui.Esc(Tok(site, it.Title)) + "</summary><p>" + Lines(Tok(site, it.Text)) + "</p></details>");
            }
            sb.Append("</div></div>");
            return Sec(b, sb.ToString());
        }

        private static string QuoteBlock(SiteData site, List<MediaItem> media, Block b)
        {
            return Sec(b, "<figure class=\"sa-bquote\">" + (b.Media != string.Empty && !Blocks.IsVideo(b.Media) ? Mv(media, b.Media, b.Title, false, false) : string.Empty) + "<blockquote>" + Lines(Tok(site, b.Text)) + "</blockquote>" + (b.Title != string.Empty ? "<figcaption>" + Ui.Esc(Tok(site, b.Title)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        private static string Wide(SiteData site, List<MediaItem> media, Block b)
        {
            return b.Media == string.Empty ? string.Empty : Sec(b, "<figure class=\"sa-bmedia\">" + Mv(media, b.Media, b.Text, false, false) + (b.Text != string.Empty ? "<figcaption>" + Ui.Esc(Tok(site, b.Text)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        private static string Cta(SiteData site, Block b)
        {
            return Sec(b, "<div class=\"sa-bcta\"><div>" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, "sa-blead") + "</div>" + Btns(site, b) + "</div>");
        }

        private string PageHtml(SiteData site, List<MediaItem> media, string key)
        {
            StringBuilder sb = new StringBuilder();
            bool first = true;
            if (!site.Pages.TryGetValue(key, out PageDoc? doc))
            {
                return string.Empty;
            }
            foreach (Block b in doc.Blocks.Where(x => !x.Hidden))
            {
                string html = b.Type switch
                {
                    "hero" => Hero(site, media, b, first),
                    "text" => Sec(b, "<div class=\"sa-btext\">" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, string.Empty) + "</div>"),
                    "split" => Split(site, media, b),
                    "cards" => Cards(site, b),
                    "steps" => Steps(site, b),
                    "stats" => StatRow(site, b),
                    "people" => People(site, media, b),
                    "boxes" => Boxes(site, b),
                    "faq" => Faq(site, b),
                    "quote" => QuoteBlock(site, media, b),
                    "media" => Wide(site, media, b),
                    "cta" => Cta(site, b),
                    "data" => DataPart(site, b, first),
                    _ => string.Empty
                };
                if (html.Length > 0 && (b.Type == "hero" || b.Type == "data"))
                {
                    first = false;
                }
                sb.Append(html);
            }
            string all = sb.ToString();
            string open = "<h2 class=\"sa-bh2\">";
            int at = all.IndexOf(open, StringComparison.Ordinal);
            if (!all.Contains("<h1", StringComparison.Ordinal) && at >= 0)
            {
                int end = all.IndexOf("</h2>", at, StringComparison.Ordinal);
                all = all.Substring(0, at) + "<h1 class=\"sa-bh2\">" + all.Substring(at + open.Length, end - at - open.Length) + "</h1>" + all.Substring(end + 5);
            }
            return all;
        }

    }
}
