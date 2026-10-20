using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HomeServices.Models;

namespace HomeServices.Core
{
    public sealed class PageSpec
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Href { get; init; } = string.Empty;
        public string[] Data { get; init; } = Array.Empty<string>();
    }

    public sealed class PartSpec
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Desc { get; init; } = string.Empty;
        public string Fields { get; init; } = string.Empty;
        public string ItemFields { get; init; } = string.Empty;
    }

    public static class Blocks
    {
        public static readonly PageSpec[] Pages =
        {
            new PageSpec { Key = "home", Name = "Home", Href = "Home", Data = new[] { "trades", "zipcheck", "reviews" } },
            new PageSpec { Key = "services", Name = "Services", Href = "Services", Data = new[] { "servicelist" } },
            new PageSpec { Key = "area", Name = "Service area", Href = "ServiceArea", Data = new[] { "zipcheck", "towns" } },
            new PageSpec { Key = "quote", Name = "Request a quote", Href = "Quote", Data = new[] { "quoteform" } },
            new PageSpec { Key = "reviews", Name = "Reviews", Href = "Reviews", Data = new[] { "reviewlist" } },
            new PageSpec { Key = "about", Name = "About", Href = "About", Data = Array.Empty<string>() },
            new PageSpec { Key = "contact", Name = "Contact", Href = "Contact", Data = new[] { "contactform" } }
        };

        public static readonly PartSpec[] Types =
        {
            new PartSpec { Key = "hero", Name = "Page top", Desc = "Big title, text, buttons and a picture or video.", Fields = "kicker,title,text,media,buttons" },
            new PartSpec { Key = "text", Name = "Text", Desc = "A title and paragraphs.", Fields = "kicker,title,text" },
            new PartSpec { Key = "split", Name = "Picture and text", Desc = "A picture or video beside the text.", Fields = "kicker,title,text,media,button,flip" },
            new PartSpec { Key = "cards", Name = "Cards", Desc = "A row of cards with an icon, title, text and link.", Fields = "kicker,title,text,items", ItemFields = "icon,title,text,link" },
            new PartSpec { Key = "steps", Name = "Steps", Desc = "Numbered steps, for how a job works.", Fields = "kicker,title,text,items", ItemFields = "title,text" },
            new PartSpec { Key = "stats", Name = "Numbers", Desc = "A row of big numbers with a label under each.", Fields = "kicker,title,items", ItemFields = "title,text" },
            new PartSpec { Key = "boxes", Name = "Checklists", Desc = "Boxes with a title and one tick per line.", Fields = "kicker,title,items", ItemFields = "title,lines" },
            new PartSpec { Key = "people", Name = "People", Desc = "Portraits with a name and a role, for the team.", Fields = "kicker,title,text,items", ItemFields = "media,title,text" },
            new PartSpec { Key = "faq", Name = "Questions", Desc = "Questions that open to show the answer.", Fields = "kicker,title,items", ItemFields = "title,text" },
            new PartSpec { Key = "quote", Name = "Quote", Desc = "A quote with a name and a small photo.", Fields = "quote,name,media" },
            new PartSpec { Key = "media", Name = "Picture or video", Desc = "One wide picture or video with a caption.", Fields = "media,caption" },
            new PartSpec { Key = "cta", Name = "Call to action", Desc = "A colored band with a title, text and buttons.", Fields = "kicker,title,text,buttons" }
        };

        public static readonly PartSpec[] Parts =
        {
            new PartSpec { Key = "trades", Name = "Our trades", Desc = "The four trades with their picture and lowest starting price. Change them on Services and prices.", Fields = "kicker,title,text" },
            new PartSpec { Key = "zipcheck", Name = "ZIP code check", Desc = "A box where visitors check their ZIP code against the service area and see the trip fee.", Fields = "kicker,title,text" },
            new PartSpec { Key = "reviews", Name = "Latest reviews", Desc = "Up to three published reviews. Shows nothing until a review is published.", Fields = "kicker,title" },
            new PartSpec { Key = "servicelist", Name = "Services and prices", Desc = "Every active service, grouped by trade, with its starting price and typical time.", Fields = "kicker,title,text" },
            new PartSpec { Key = "towns", Name = "Towns and trip fees", Desc = "Every zone with its trip fee and the towns and ZIP codes in it.", Fields = "kicker,title,text" },
            new PartSpec { Key = "quoteform", Name = "Quote request form", Desc = "Services, job description, photos, preferred days and times, and contact details.", Fields = "kicker,title,text" },
            new PartSpec { Key = "reviewlist", Name = "Published reviews", Desc = "Every published review, newest first, with a filter by trade.", Fields = "kicker,title,text" },
            new PartSpec { Key = "contactform", Name = "Contact details and form", Desc = "Phone, email, address and hours from Settings, beside the message form.", Fields = "kicker,title,text" }
        };

        public static readonly (string Key, string Name)[] Icons = { ("wrench", "Wrench"), ("drop", "Water drop"), ("flame", "Flame"), ("snow", "Snowflake"), ("bolt", "Lightning bolt"), ("spark", "Sparkle"), ("home", "House"), ("clock", "Clock"), ("pin", "Map pin"), ("cal", "Calendar"), ("shield", "Shield"), ("star", "Star"), ("users", "People"), ("check", "Tick"), ("phone", "Phone"), ("mail", "Email"), ("card", "Card") };

        private static readonly string[] Allowed = { "p", "br", "b", "i", "ul", "ol", "li", "a" };

        public static PageSpec? PageOf(string? key)
        {
            return Pages.FirstOrDefault(p => p.Key == (key ?? string.Empty).Trim().ToLowerInvariant());
        }

        public static PartSpec? Spec(Block b)
        {
            return b.Type == "data" ? Parts.FirstOrDefault(p => p.Key == b.Source) : Types.FirstOrDefault(t => t.Key == b.Type);
        }

        public static bool Has(PartSpec s, string field)
        {
            return ("," + s.Fields + ",").Contains("," + field + ",", StringComparison.Ordinal);
        }

        public static bool IsVideo(string src)
        {
            return src.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);
        }

        public static string SafeLink(string? value)
        {
            string v = (value ?? string.Empty).Trim();
            if (v.Length == 0 || v.Length > 300)
            {
                return string.Empty;
            }
            if (Regex.IsMatch(v, "^https?://[^\\s<>\"']+$", RegexOptions.IgnoreCase) || Regex.IsMatch(v, "^mailto:[^\\s<>\"']+$", RegexOptions.IgnoreCase) || Regex.IsMatch(v, "^tel:[0-9+() .-]+$", RegexOptions.IgnoreCase) || Regex.IsMatch(v, "^#[A-Za-z0-9_-]+$") || Regex.IsMatch(v, "^[A-Za-z][A-Za-z0-9_-]*(\\?[A-Za-z0-9=&%._+-]*)?(#[A-Za-z0-9_-]+)?$"))
            {
                return v;
            }
            return string.Empty;
        }

        public static string Sanitize(string? html, int max)
        {
            string src = Regex.Replace(html ?? string.Empty, "<(script|style|template|iframe|object)[\\s\\S]*?</\\1\\s*>", string.Empty, RegexOptions.IgnoreCase);
            src = Regex.Replace(src, "<!--[\\s\\S]*?-->", string.Empty);
            StringBuilder sb = new StringBuilder();
            List<string> open = new List<string>();
            foreach (Match m in Regex.Matches(src, "<[^>]*>|[^<]+|<"))
            {
                string t = m.Value;
                if (!t.StartsWith('<') || t == "<")
                {
                    sb.Append(Ui.Esc(WebUtility.HtmlDecode(t)));
                    continue;
                }
                Match tag = Regex.Match(t, "^<\\s*(/?)\\s*([a-zA-Z][a-zA-Z0-9]*)([^>]*)>$");
                if (!tag.Success)
                {
                    continue;
                }
                bool close = tag.Groups[1].Value == "/";
                string name = tag.Groups[2].Value.ToLowerInvariant() switch
                {
                    "strong" => "b",
                    "em" => "i",
                    "div" => "p",
                    "h1" or "h2" or "h3" or "h4" or "h5" or "h6" => "p",
                    string n => n
                };
                if (!Allowed.Contains(name))
                {
                    continue;
                }
                if (name == "br")
                {
                    if (!close)
                    {
                        sb.Append("<br>");
                    }
                    continue;
                }
                if (close)
                {
                    int at = open.LastIndexOf(name);
                    if (at < 0)
                    {
                        continue;
                    }
                    for (int i = open.Count - 1; i >= at; i--)
                    {
                        sb.Append("</" + open[i] + ">");
                    }
                    open.RemoveRange(at, open.Count - at);
                    continue;
                }
                if (name == "p" && open.Contains("p"))
                {
                    int at = open.LastIndexOf("p");
                    for (int i = open.Count - 1; i >= at; i--)
                    {
                        sb.Append("</" + open[i] + ">");
                    }
                    open.RemoveRange(at, open.Count - at);
                }
                if (name == "a")
                {
                    Match h = Regex.Match(tag.Groups[3].Value, "href\\s*=\\s*(\"([^\"]*)\"|'([^']*)'|([^\\s>]+))", RegexOptions.IgnoreCase);
                    string href = h.Success ? SafeLink(WebUtility.HtmlDecode(h.Groups[2].Value + h.Groups[3].Value + h.Groups[4].Value)) : string.Empty;
                    if (href == string.Empty || open.Contains("a"))
                    {
                        continue;
                    }
                    bool outside = href.StartsWith("http", StringComparison.OrdinalIgnoreCase);
                    sb.Append("<a href=\"" + Ui.Esc(href) + "\"" + (outside ? " target=\"_blank\" rel=\"noopener\"" : string.Empty) + ">");
                    open.Add("a");
                    continue;
                }
                sb.Append("<" + name + ">");
                open.Add(name);
            }
            for (int i = open.Count - 1; i >= 0; i--)
            {
                sb.Append("</" + open[i] + ">");
            }
            string outHtml = Regex.Replace(sb.ToString(), "<(p|b|i|li|ul|ol)>(\\s|<br>)*</\\1>", string.Empty);
            outHtml = Regex.Replace(outHtml, "(<br>\\s*){3,}", "<br><br>").Trim();
            return outHtml.Length > max ? string.Empty : outHtml;
        }

        public static string Plain(string? value, int max)
        {
            string v = Regex.Replace((value ?? string.Empty).Replace("\r", string.Empty), "[ \\t]+", " ");
            v = Regex.Replace(v, "\\n{3,}", "\n\n").Trim();
            return v.Length > max ? v.Substring(0, max).Trim() : v;
        }

        public static string NewId()
        {
            return "b" + Guid.NewGuid().ToString("N").Substring(0, 9);
        }

        public static (List<Block>? Blocks, string Error) Clean(List<MediaItem> library, PageSpec page, string? json)
        {
            List<Block>? raw;
            try
            {
                raw = JsonSerializer.Deserialize<List<Block>>(json ?? "[]", new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                return (null, "The page could not be read. Reload and try again.");
            }
            if (raw == null || raw.Count == 0 || raw.Count > 40)
            {
                return (null, "A page needs between 1 and 40 blocks.");
            }
            HashSet<string> media = library.Select(m => m.Path).ToHashSet(StringComparer.Ordinal);
            HashSet<string> photos = library.Where(m => m.Kind == "photo").Select(m => m.Path).ToHashSet(StringComparer.Ordinal);
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            List<Block> list = new List<Block>();
            foreach (Block r in raw)
            {
                string type = r.Type ?? string.Empty;
                PartSpec? spec = type == "data" ? Parts.FirstOrDefault(p => p.Key == r.Source && page.Data.Contains(p.Key)) : Types.FirstOrDefault(t => t.Key == type);
                if (spec == null)
                {
                    return (null, "That page has a block it cannot use. Reload and try again.");
                }
                if (type == "data" && list.Any(x => x.Type == "data" && x.Source == spec.Key))
                {
                    return (null, spec.Name + " is on the page twice.");
                }
                string id = Regex.IsMatch(r.Id ?? string.Empty, "^b[a-z0-9]{3,12}$") && !ids.Contains(r.Id!) ? r.Id! : NewId();
                ids.Add(id);
                Block b = new Block { Id = id, Type = type, Source = type == "data" ? spec.Key : string.Empty, Hidden = r.Hidden };
                b.Kicker = Has(spec, "kicker") ? Plain(r.Kicker, 80) : string.Empty;
                b.Title = Has(spec, "title") || Has(spec, "name") ? Plain(r.Title, 140).Replace("\n", " ") : string.Empty;
                if (Has(spec, "text"))
                {
                    b.Text = Sanitize(r.Text, 6000);
                    if (b.Text == string.Empty && Regex.Replace(r.Text ?? string.Empty, "<[^>]*>|\\s|&nbsp;", string.Empty).Length > 0)
                    {
                        return (null, "The text in " + spec.Name + " is too long.");
                    }
                }
                else if (Has(spec, "quote") || Has(spec, "caption"))
                {
                    b.Text = Plain(r.Text, Has(spec, "quote") ? 600 : 240);
                }
                if (Has(spec, "media"))
                {
                    string m = (r.Media ?? string.Empty).Trim();
                    bool photoOnly = type == "quote";
                    if (m != string.Empty && !(photoOnly ? photos.Contains(m) : media.Contains(m)))
                    {
                        return (null, "A picture in " + spec.Name + " is no longer in the media library. Choose it again.");
                    }
                    b.Media = m;
                }
                if (Has(spec, "buttons") || Has(spec, "button"))
                {
                    b.Label = Plain(r.Label, 40).Replace("\n", " ");
                    b.Link = SafeLink(r.Link);
                    if (b.Label != string.Empty && b.Link == string.Empty)
                    {
                        return (null, "Check the link of the button “" + b.Label + "”. Use a page name like Shop, Contact or a full https:// address.");
                    }
                    if (Has(spec, "buttons"))
                    {
                        b.Label2 = Plain(r.Label2, 40).Replace("\n", " ");
                        b.Link2 = SafeLink(r.Link2);
                        if (b.Label2 != string.Empty && b.Link2 == string.Empty)
                        {
                            return (null, "Check the link of the button “" + b.Label2 + "”.");
                        }
                    }
                }
                b.Flip = Has(spec, "flip") && r.Flip;
                if (Has(spec, "items"))
                {
                    foreach (BlockItem it in (r.Items ?? new List<BlockItem>()).Take(12))
                    {
                        BlockItem c = new BlockItem { Title = Plain(it.Title, 120).Replace("\n", " "), Text = Plain(it.Text, 600) };
                        if (spec.ItemFields.Contains("icon"))
                        {
                            c.Icon = Icons.Any(x => x.Key == it.Icon) ? it.Icon : string.Empty;
                        }
                        if (spec.ItemFields.Contains("media"))
                        {
                            string m = (it.Media ?? string.Empty).Trim();
                            if (m != string.Empty && !photos.Contains(m))
                            {
                                return (null, "A photo in " + spec.Name + " is no longer in the media library. Choose it again.");
                            }
                            c.Media = m;
                        }
                        if (spec.ItemFields.Contains("link"))
                        {
                            c.Link = SafeLink(it.Link);
                            c.Label = c.Link != string.Empty ? Plain(it.Label, 40).Replace("\n", " ") : string.Empty;
                            if ((it.Link ?? string.Empty).Trim() != string.Empty && c.Link == string.Empty)
                            {
                                return (null, "Check the link on the card “" + c.Title + "”.");
                            }
                        }
                        if (c.Title != string.Empty || c.Text != string.Empty || c.Media != string.Empty)
                        {
                            b.Items.Add(c);
                        }
                    }
                }
                list.Add(b);
            }
            string? missing = page.Data.FirstOrDefault(k => !list.Any(x => x.Type == "data" && x.Source == k));
            if (missing != null)
            {
                return (null, (Parts.FirstOrDefault(p => p.Key == missing)?.Name ?? missing) + " must stay on the page. Hide it instead.");
            }
            return (list, string.Empty);
        }

        private static Block Data(string key, string kicker = "", string title = "", string text = "")
        {
            return new Block { Id = NewId(), Type = "data", Source = key, Kicker = kicker, Title = title, Text = text };
        }

        private static BlockItem It(string title, string text, string icon = "", string media = "", string link = "", string label = "")
        {
            return new BlockItem { Title = title, Text = text, Icon = icon, Media = media, Link = link, Label = label };
        }

        public static void Ensure(SiteData site)
        {
            foreach (PageSpec p in Pages)
            {
                if (!site.Pages.TryGetValue(p.Key, out PageDoc? doc) || doc == null)
                {
                    doc = new PageDoc { Blocks = Seed(site, p.Key) };
                    site.Pages[p.Key] = doc;
                }
                foreach (string k in p.Data)
                {
                    if (!doc.Blocks.Any(b => b.Type == "data" && b.Source == k))
                    {
                        doc.Blocks.Add(Data(k, string.Empty, Parts.First(x => x.Key == k).Name));
                    }
                }
            }
        }

        private static List<Block> Seed(SiteData site, string key)
        {
            string tel = Work.Tel(site.Phone);
            switch (key)
            {
                case "home":
                    return new List<Block>
                    {
                        new Block { Id = NewId(), Type = "hero", Kicker = "Plumbing · Heating and cooling · Electrical · Cleaning", Title = "One call for the whole house", Text = "<p>Licensed technicians for the pipes, the furnace, the wiring and the deep clean. You get a written quote before any work starts.</p>", Media = "images/hero.webp", Label = "Request a quote", Link = "Quote", Label2 = "See starting prices", Link2 = "Services" },
                        Data("trades", "What we do", "Four trades, one team", "<p>Every technician is trained, background-checked and carries the right licence for the work.</p>"),
                        new Block { Id = NewId(), Type = "steps", Kicker = "How it works", Title = "From first call to final invoice", Items = new List<BlockItem> { It("Tell us about the job", "Pick the services, describe the problem and add a few photos."), It("Get a written quote", "We send a quote with every line priced. No surprises later."), It("Accept online", "Open the link, check the quote and accept it with one click."), It("We do the work", "A technician arrives in your time window. You pay the invoice after the job.") } },
                        Data("zipcheck", "Service area", "Do we come to you?", "<p>Enter your ZIP code to see if you are in our area and what the trip fee is.</p>"),
                        new Block { Id = NewId(), Type = "cards", Kicker = "Why {name}", Title = "The way we like to work", Items = new List<BlockItem> { It("Prices in writing", "Every quote lists each part and each hour before we start.", "card"), It("Two-hour windows", "We give you a two-hour arrival window and text when we are on the way.", "clock"), It("Licensed and insured", "Plumbing, gas and electrical work is done by licensed technicians.", "shield"), It("Clean when we leave", "Drop cloths, shoe covers and a tidy work area, every time.", "spark") } },
                        Data("reviews", "Reviews", "What customers told us"),
                        new Block { Id = NewId(), Type = "cta", Title = "A drip, a draft or a dead outlet?", Text = "<p>Tell us about it today. Most quotes go out within one working day.</p>", Label = "Request a quote", Link = "Quote", Label2 = "Call " + site.Phone, Link2 = tel }
                    };
                case "services":
                    return new List<Block>
                    {
                        Data("servicelist", "Services and prices", "Starting prices, written down", "<p>Every job gets a written quote before we start. The prices below are where most jobs begin; your quote lists exactly what is included.</p>"),
                        new Block { Id = NewId(), Type = "faq", Kicker = "About our prices", Title = "Good to know", Items = new List<BlockItem> { It("What does starting price mean?", "It is the lowest price for a simple job of that kind. Parts, extra hours and harder access are listed on your quote."), It("Is there a trip fee?", "Some towns farther from our shop have a small trip fee. Check your ZIP code on the Service area page."), It("Do you charge for quotes?", "No. Quotes are free and stay valid for 30 days."), It("How do I pay?", "After the job we send an invoice. You can pay by card, cash or check.") } },
                        new Block { Id = NewId(), Type = "cta", Title = "Not sure which service you need?", Text = "<p>Describe the problem in your own words and add a photo. We will work out the rest.</p>", Label = "Request a quote", Link = "Quote" }
                    };
                case "area":
                    return new List<Block>
                    {
                        Data("zipcheck", "Service area", "Check your ZIP code", "<p>We work in the towns around our shop. Enter your ZIP code to see if we cover it and what the trip fee is.</p>"),
                        Data("towns", "Where we work", "Towns and trip fees", "<p>The trip fee is added once per visit and is shown on your quote.</p>"),
                        new Block { Id = NewId(), Type = "split", Kicker = "Just outside?", Title = "Ask us anyway", Text = "<p>If your ZIP code is not on the list, send a quote request all the same. For larger jobs we often travel a little farther.</p>", Media = "images/area.webp", Label = "Request a quote", Link = "Quote" }
                    };
                case "quote":
                    return new List<Block>
                    {
                        Data("quoteform", "Request a quote", "Tell us about the job", "<p>The more we know, the more exact your quote. Photos help a lot. Quotes are free.</p>"),
                        new Block { Id = NewId(), Type = "steps", Kicker = "What happens next", Title = "After you send the request", Items = new List<BlockItem> { It("We read it", "A person reads every request, usually the same working day."), It("We write the quote", "You get an email with a link to your written quote."), It("You accept online", "Check the lines and accept with the last four digits of your phone number."), It("We book a time", "We call to set the day and a two-hour arrival window.") } }
                    };
                case "reviews":
                    return new List<Block>
                    {
                        Data("reviewlist", "Reviews", "What customers told us", "<p>Every review here was written by our staff from feedback a customer gave us after a job, with the customer's permission. We never invent reviews.</p>"),
                        new Block { Id = NewId(), Type = "cta", Title = "Had a job done by us?", Text = "<p>Tell us how it went. Good or bad, we read every word.</p>", Label = "Send feedback", Link = "Contact" }
                    };
                case "about":
                    return new List<Block>
                    {
                        new Block { Id = NewId(), Type = "split", Kicker = "About us", Title = "A small team that answers the phone", Text = "<p><b>{name} started with one van and one promise: write the price down before the work starts.</b></p><p>Today we are a small team of plumbers, heating and cooling technicians, electricians and cleaners. We still quote every job in writing, we still give two-hour arrival windows, and we still clean up before we leave.</p>", Media = "images/about/workshop.webp", Label = "Request a quote", Link = "Quote" },
                        new Block { Id = NewId(), Type = "stats", Title = "In numbers", Items = new List<BlockItem> { It("4", "trades under one roof"), It("2 hours", "arrival windows"), It("30 days", "every quote stays valid"), It("1", "invoice, after the job") } },
                        new Block { Id = NewId(), Type = "people", Kicker = "The team", Title = "Who you will meet", Items = site.Techs.Where(t => t.Active).Select(t => It(t.Name, t.Role, string.Empty, t.Photo)).ToList() },
                        new Block { Id = NewId(), Type = "boxes", Title = "What we promise", Items = new List<BlockItem> { It("Before the job", "A written quote with every line priced\nA two-hour arrival window\nA text when we are on the way"), It("During the job", "Shoe covers and drop cloths\nWe explain what we find\nNo extra work without your yes"), It("After the job", "A tidy work area\nOne clear invoice\nPay by card, cash or check") } },
                        new Block { Id = NewId(), Type = "cta", Title = "Let us take a look", Label = "Request a quote", Link = "Quote", Label2 = "Contact us", Link2 = "Contact" }
                    };
                case "contact":
                    return new List<Block>
                    {
                        Data("contactform", "Contact", "Talk to a person", "<p>Questions about a quote, a job or an invoice? Send us a note and we will reply the same working day. For a new job, the quote form is quicker.</p>"),
                        new Block { Id = NewId(), Type = "faq", Title = "Good to know", Items = new List<BlockItem> { It("Do you do emergency call-outs?", "For a burst pipe, no heat in winter or a burning smell from an outlet, call us. If we cannot come quickly, we will say so."), It("Can I change my time window?", "Yes. Call or write to us at least one working day before the visit."), It("Do I need to be home?", "Someone over 18 needs to be home at the start and end of the job.") } }
                    };
                default:
                    return new List<Block>();
            }
        }

        public static List<string> Uses(SiteData site, string src)
        {
            List<string> u = new List<string>();
            foreach (PageSpec p in Pages)
            {
                if (site.Pages.TryGetValue(p.Key, out PageDoc? doc) && doc.Blocks.Any(b => b.Media == src || b.Items.Any(i => i.Media == src)))
                {
                    u.Add("Page: " + p.Name);
                }
            }
            return u;
        }
    }
}
