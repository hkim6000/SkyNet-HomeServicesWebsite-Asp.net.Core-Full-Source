using System.Security.Cryptography;
using HomeServices.Models;
using Microsoft.AspNetCore.Http;

namespace HomeServices.Core
{
    public sealed class Incoming
    {
        public string Name { get; init; } = string.Empty;
        public long Length { get; init; }
        public Func<Stream> Open { get; init; } = () => Stream.Null;
    }

    public static class Media
    {
        public const int PhotoMb = 8;
        public const int CeilingMb = 100;
        public const int MaxItems = 3000;
        public const string Accept = "image/jpeg,image/png,image/webp,image/gif,video/mp4";

        private const string Letters = "abcdefghijkmnpqrstuvwxyz23456789";
        private static readonly string[] Seeded = { ".webp", ".jpg", ".jpeg", ".png", ".gif", ".mp4" };

        public static string ListFile => Path.Combine(Store.DataDir, "media.json");

        public static string UploadDir => Path.Combine(Store.Root, "images", "uploads");

        public static async Task<List<MediaItem>> Load()
        {
            if (File.Exists(ListFile))
            {
                return await Store.Read(ListFile, () => new List<MediaItem>());
            }
            List<MediaItem> list = Seed();
            await Store.Write(ListFile, list);
            return list;
        }

        public static Task Save(List<MediaItem> list)
        {
            return Store.Write(ListFile, list);
        }

        private static List<MediaItem> Seed()
        {
            List<MediaItem> list = new List<MediaItem>();
            string root = Path.Combine(Store.Root, "images");
            if (!Directory.Exists(root))
            {
                return list;
            }
            foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                string name = Path.GetFileNameWithoutExtension(file);
                if (!Seeded.Contains(ext) || name.EndsWith("-thumb", StringComparison.Ordinal))
                {
                    continue;
                }
                string rel = Path.GetRelativePath(Store.Root, file).Replace('\\', '/');
                if (rel.StartsWith("images/requests/", StringComparison.Ordinal))
                {
                    continue;
                }
                string thumb = Path.Combine(Path.GetDirectoryName(file) ?? root, name + "-thumb.webp");
                list.Add(new MediaItem
                {
                    Id = NewId(),
                    Path = rel,
                    Thumb = File.Exists(thumb) ? Path.GetRelativePath(Store.Root, thumb).Replace('\\', '/') : string.Empty,
                    Kind = ext == ".mp4" ? "video" : "photo",
                    Name = Path.GetFileName(file),
                    Alt = Words(name),
                    Bytes = new FileInfo(file).Length,
                    Added = File.GetLastWriteTimeUtc(file),
                    By = "system"
                });
            }
            return list;
        }

        private static string Words(string name)
        {
            string w = name.Replace('-', ' ').Replace('_', ' ').Trim();
            return w.Length == 0 ? string.Empty : char.ToUpperInvariant(w[0]) + w.Substring(1);
        }

        public static string NewId()
        {
            char[] c = new char[12];
            for (int i = 0; i < c.Length; i++)
            {
                c[i] = Letters[RandomNumberGenerator.GetInt32(Letters.Length)];
            }
            return new string(c);
        }

        public static string Thumb(string path)
        {
            if (path.Length == 0)
            {
                return path;
            }
            string ext = Path.GetExtension(path);
            if (ext.Length == 0)
            {
                return path;
            }
            string candidate = path.Substring(0, path.Length - ext.Length) + "-thumb.webp";
            return File.Exists(Path.Combine(Store.Root, candidate)) ? candidate : path;
        }

        public static MediaItem? Find(List<MediaItem> list, string path, string kind)
        {
            return list.FirstOrDefault(m => m.Path == path && (kind == string.Empty || m.Kind == kind));
        }

        public static string Size(long bytes)
        {
            if (bytes >= 1024 * 1024)
            {
                return (bytes / 1024d / 1024d).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " MB";
            }
            return Math.Max(1, bytes / 1024) + " KB";
        }

        public static async Task<List<Incoming>> Received()
        {
            List<Incoming> files = new List<Incoming>();
            HttpRequest? r = Local.Accessor?.HttpContext?.Request;
            if (r == null || !r.HasFormContentType)
            {
                return files;
            }
            IFormCollection form = await r.ReadFormAsync();
            foreach (IFormFile f in form.Files.Where(x => x.Length > 0).Take(20))
            {
                IFormFile file = f;
                files.Add(new Incoming { Name = file.FileName, Length = file.Length, Open = file.OpenReadStream });
            }
            return files;
        }

        public static string Detect(byte[] head, int n)
        {
            if (n >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF)
            {
                return ".jpg";
            }
            if (n >= 8 && head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47 && head[4] == 0x0D && head[5] == 0x0A && head[6] == 0x1A && head[7] == 0x0A)
            {
                return ".png";
            }
            if (n >= 6 && head[0] == 'G' && head[1] == 'I' && head[2] == 'F' && head[3] == '8' && (head[4] == '7' || head[4] == '9') && head[5] == 'a')
            {
                return ".gif";
            }
            if (n >= 12 && head[0] == 'R' && head[1] == 'I' && head[2] == 'F' && head[3] == 'F' && head[8] == 'W' && head[9] == 'E' && head[10] == 'B' && head[11] == 'P')
            {
                return ".webp";
            }
            if (n >= 12 && head[4] == 'f' && head[5] == 't' && head[6] == 'y' && head[7] == 'p' && !(head[8] == 'q' && head[9] == 't'))
            {
                return ".mp4";
            }
            return string.Empty;
        }

        private static bool Matches(string ext, string detected)
        {
            return ext == detected || ext == ".jpeg" && detected == ".jpg";
        }

        public static string CleanName(string name)
        {
            string n = Path.GetFileName(name ?? string.Empty).Trim();
            char[] bad = Path.GetInvalidFileNameChars();
            n = new string(n.Select(ch => bad.Contains(ch) || ch < 32 || ch == '<' || ch == '>' ? '-' : ch).ToArray());
            return n.Length > 80 ? n.Substring(n.Length - 80) : n;
        }

        public static async Task<(MediaItem? item, string error)> Add(Incoming file, int videoMb, string by)
        {
            string name = CleanName(file.Name);
            string ext = Path.GetExtension(name).ToLowerInvariant();
            if (name.Length == 0 || ext.Length == 0)
            {
                return (null, "The file has no name or type.");
            }
            byte[] head = new byte[16];
            int read;
            using (Stream s = file.Open())
            {
                read = await s.ReadAtLeastAsync(head, head.Length, false);
            }
            string detected = Detect(head, read);
            if (detected == string.Empty || !Matches(ext, detected))
            {
                return (null, name + " is not a JPG, PNG, WebP, GIF or MP4 file.");
            }
            bool video = detected == ".mp4";
            long limit = (video ? Math.Max(1, Math.Min(CeilingMb, videoMb)) : PhotoMb) * 1024L * 1024L;
            if (file.Length > limit)
            {
                return (null, name + " is larger than " + (limit / 1024 / 1024) + " MB.");
            }
            Directory.CreateDirectory(UploadDir);
            string id = NewId();
            string target = Path.Combine(UploadDir, id + detected);
            string tmp = target + ".part";
            long total = 0;
            try
            {
                using (Stream src = file.Open())
                using (FileStream dst = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write))
                {
                    byte[] buffer = new byte[81920];
                    int got;
                    while ((got = await src.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        total += got;
                        if (total > limit)
                        {
                            break;
                        }
                        await dst.WriteAsync(buffer, 0, got);
                    }
                }
                if (total > limit)
                {
                    File.Delete(tmp);
                    return (null, name + " is larger than " + (limit / 1024 / 1024) + " MB.");
                }
                File.Move(tmp, target);
            }
            catch (IOException)
            {
                if (File.Exists(tmp))
                {
                    File.Delete(tmp);
                }
                return (null, name + " could not be saved. Check that the server may write to images/uploads.");
            }
            catch (UnauthorizedAccessException)
            {
                return (null, name + " could not be saved. Check that the server may write to images/uploads.");
            }
            MediaItem item = new MediaItem
            {
                Id = id,
                Path = "images/uploads/" + id + detected,
                Kind = video ? "video" : "photo",
                Name = name,
                Alt = video ? string.Empty : Words(Path.GetFileNameWithoutExtension(name)),
                Bytes = total,
                Added = DateTime.UtcNow,
                By = by
            };
            return (item, string.Empty);
        }

        public static async Task<(int added, List<string> errors)> AddAll(List<Incoming> files, int videoMb, string by)
        {
            List<string> errors = new List<string>();
            List<MediaItem> fresh = new List<MediaItem>();
            foreach (Incoming f in files)
            {
                (MediaItem? item, string error) = await Add(f, videoMb, by);
                if (item == null)
                {
                    errors.Add(error);
                }
                else
                {
                    fresh.Add(item);
                }
            }
            if (fresh.Count == 0)
            {
                return (0, errors);
            }
            using (await Store.Hold())
            {
                List<MediaItem> list = await Load();
                if (list.Count + fresh.Count > MaxItems)
                {
                    foreach (MediaItem m in fresh)
                    {
                        Erase(m);
                    }
                    errors.Add("The library is full. Delete files you no longer use first.");
                    return (0, errors);
                }
                list.AddRange(fresh);
                await Save(list);
            }
            return (fresh.Count, errors);
        }

        public static List<string> UsedBy(SiteData site, string path)
        {
            List<string> used = new List<string>();
            used.AddRange(Blocks.Uses(site, path));
            foreach (Trade t in site.Trades.Where(x => x.Image == path))
            {
                used.Add("Trade: " + t.Name);
            }
            foreach (Technician t in site.Techs.Where(x => x.Photo == path))
            {
                used.Add("Technician: " + t.Name);
            }
            return used;
        }

        public static void Erase(MediaItem m)
        {
            foreach (string rel in new[] { m.Path, m.Thumb })
            {
                if (rel.Length == 0 || !rel.StartsWith("images/", StringComparison.Ordinal) || rel.Contains(".."))
                {
                    continue;
                }
                string full = Path.GetFullPath(Path.Combine(Store.Root, rel));
                string images = Path.GetFullPath(Path.Combine(Store.Root, "images")) + Path.DirectorySeparatorChar;
                if (full.StartsWith(images, StringComparison.Ordinal) && File.Exists(full))
                {
                    try
                    {
                        File.Delete(full);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
        }
    }
}
