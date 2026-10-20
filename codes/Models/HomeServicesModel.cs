namespace HomeServices.Models
{
    public class SiteData
    {
        public string Brand { get; set; } = string.Empty;
        public string Tagline { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public List<string> Hours { get; set; } = new List<string>();
        public string Licence { get; set; } = string.Empty;
        public decimal TaxRate { get; set; }
        public int QuoteDays { get; set; } = 30;
        public int PayDays { get; set; } = 14;
        public string InvoicePrefix { get; set; } = "HL";
        public int NextInvoice { get; set; } = 1;
        public int MaxVideoMb { get; set; } = 30;
        public List<Trade> Trades { get; set; } = new List<Trade>();
        public List<ServiceItem> Services { get; set; } = new List<ServiceItem>();
        public List<Zone> Zones { get; set; } = new List<Zone>();
        public List<ZipArea> Zips { get; set; } = new List<ZipArea>();
        public List<Technician> Techs { get; set; } = new List<Technician>();
        public Dictionary<string, PageDoc> Pages { get; set; } = new Dictionary<string, PageDoc>();
    }

    public class Trade
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public string Blurb { get; set; } = string.Empty;
    }

    public class ServiceItem
    {
        public string Id { get; set; } = string.Empty;
        public string Trade { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal From { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
    }

    public class Zone
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Fee { get; set; }
    }

    public class ZipArea
    {
        public string Zip { get; set; } = string.Empty;
        public string Town { get; set; } = string.Empty;
        public string Zone { get; set; } = string.Empty;
    }

    public class Technician
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Photo { get; set; } = string.Empty;
        public List<string> Trades { get; set; } = new List<string>();
        public List<int> Days { get; set; } = new List<int>();
        public bool Active { get; set; } = true;
    }

    public class LineItem
    {
        public string Text { get; set; } = string.Empty;
        public decimal Qty { get; set; } = 1;
        public decimal Price { get; set; }
    }

    public class QuoteRec
    {
        public string Id { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Status { get; set; } = "new";
        public DateTime Created { get; set; }
        public string Source { get; set; } = "web";
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Zip { get; set; } = string.Empty;
        public string Town { get; set; } = string.Empty;
        public bool InArea { get; set; }
        public List<string> Services { get; set; } = new List<string>();
        public string Description { get; set; } = string.Empty;
        public List<string> Photos { get; set; } = new List<string>();
        public List<string> Days { get; set; } = new List<string>();
        public List<string> Times { get; set; } = new List<string>();
        public List<LineItem> Lines { get; set; } = new List<LineItem>();
        public decimal TripFee { get; set; }
        public decimal TaxRate { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Total { get; set; }
        public string Note { get; set; } = string.Empty;
        public string StaffNote { get; set; } = string.Empty;
        public DateTime? SentAt { get; set; }
        public DateTime? Expires { get; set; }
        public DateTime? AnsweredAt { get; set; }
        public string AcceptName { get; set; } = string.Empty;
        public string DeclineWhy { get; set; } = string.Empty;
        public string JobId { get; set; } = string.Empty;
        public string By { get; set; } = string.Empty;
    }

    public class StepRec
    {
        public DateTime When { get; set; }
        public string Status { get; set; } = string.Empty;
        public string By { get; set; } = string.Empty;
    }

    public class JobRec
    {
        public string Id { get; set; } = string.Empty;
        public string QuoteId { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string Window { get; set; } = string.Empty;
        public string TechId { get; set; } = string.Empty;
        public string Status { get; set; } = "scheduled";
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Zip { get; set; } = string.Empty;
        public string Town { get; set; } = string.Empty;
        public string Trade { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string CancelWhy { get; set; } = string.Empty;
        public List<StepRec> Steps { get; set; } = new List<StepRec>();
        public string InvoiceId { get; set; } = string.Empty;
        public DateTime Created { get; set; }
    }

    public class InvoiceRec
    {
        public string Id { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string JobId { get; set; } = string.Empty;
        public string QuoteId { get; set; } = string.Empty;
        public string Status { get; set; } = "draft";
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public List<LineItem> Lines { get; set; } = new List<LineItem>();
        public decimal TripFee { get; set; }
        public decimal TaxRate { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Total { get; set; }
        public DateTime Created { get; set; }
        public string Issued { get; set; } = string.Empty;
        public string Due { get; set; } = string.Empty;
        public string PaidOn { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public string Ref { get; set; } = string.Empty;
        public string VoidWhy { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
    }

    public class ReviewRec
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Town { get; set; } = string.Empty;
        public string Trade { get; set; } = string.Empty;
        public int Rating { get; set; } = 5;
        public string Text { get; set; } = string.Empty;
        public string Heard { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string JobId { get; set; } = string.Empty;
        public bool Published { get; set; }
        public string By { get; set; } = string.Empty;
        public DateTime Created { get; set; }
    }

    public class MessageRec
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime When { get; set; }
        public bool Read { get; set; }
        public bool Done { get; set; }
    }

    public class MediaItem
    {
        public string Id { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Thumb { get; set; } = string.Empty;
        public string Kind { get; set; } = "photo";
        public string Name { get; set; } = string.Empty;
        public string Alt { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;
        public long Bytes { get; set; }
        public DateTime Added { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class PageDoc
    {
        public List<Block> Blocks { get; set; } = new List<Block>();
        public List<PageVersion> Versions { get; set; } = new List<PageVersion>();
        public DateTime Saved { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class PageVersion
    {
        public DateTime When { get; set; }
        public string By { get; set; } = string.Empty;
        public List<Block> Blocks { get; set; } = new List<Block>();
    }

    public class Block
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public bool Hidden { get; set; }
        public string Kicker { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Media { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string Label2 { get; set; } = string.Empty;
        public string Link2 { get; set; } = string.Empty;
        public bool Flip { get; set; }
        public List<BlockItem> Items { get; set; } = new List<BlockItem>();
    }

    public class BlockItem
    {
        public string Icon { get; set; } = string.Empty;
        public string Media { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public class Staff
    {
        public string Id { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public bool Admin { get; set; }
        public bool Active { get; set; } = true;
        public bool MustChange { get; set; }
        public string TechId { get; set; } = string.Empty;
        public bool Own { get; set; }
        public Dictionary<string, string> Perms { get; set; } = new Dictionary<string, string>();
    }

    public class Session
    {
        public string Tok { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTime Until { get; set; }
    }

    public class LogEntry
    {
        public DateTime When { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Who { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public bool Ok { get; set; }
    }
}
