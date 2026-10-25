using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace ZEarth.Domain.Kml
{
    public static class KmzService
    {
        public const long MaxAllowedDecompressedBytes = 100 * 1024 * 1024; // 100 MB zip bomb safety threshold

        /// <summary>
        /// Safely extracts a KMZ archive with strict ZipSlip and ZipBomb defenses, returning the parsed PlaceItem tree.
        /// </summary>
        public static PlaceItem ExtractAndParse(string kmzFilePath, string extractDirectory)
        {
            if (!File.Exists(kmzFilePath))
            {
                throw new FileNotFoundException($"KMZ file not found: {kmzFilePath}");
            }

            if (!Directory.Exists(extractDirectory))
            {
                Directory.CreateDirectory(extractDirectory);
            }

            string fullDestDir = Path.GetFullPath(extractDirectory);
            if (!fullDestDir.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                fullDestDir += Path.DirectorySeparatorChar;
            }

            string? primaryKmlPath = null;
            var resources = new List<(string ArchivePath, string FilePath)>();
            long totalExtractedBytes = 0;

            using (var archive = ZipFile.OpenRead(kmzFilePath))
            {
                // Find primary KML entry (doc.kml or first *.kml)
                var kmlEntry = archive.Entries.FirstOrDefault(e => e.FullName.Equals("doc.kml", StringComparison.OrdinalIgnoreCase))
                            ?? archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".kml", StringComparison.OrdinalIgnoreCase));

                if (kmlEntry == null)
                {
                    throw new InvalidDataException("KMZ archive contains no .kml entry.");
                }

                foreach (var entry in archive.Entries)
                {
                    // 1. Defend against ZipSlip (path traversal via ../ or rooted paths)
                    string destPath = Path.GetFullPath(Path.Combine(fullDestDir, entry.FullName));
                    if (!destPath.StartsWith(fullDestDir, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new UnauthorizedAccessException($"Malicious entry detected (ZipSlip attempt): '{entry.FullName}'");
                    }

                    // 2. Defend against Zip Bombs
                    totalExtractedBytes += entry.Length;
                    if (totalExtractedBytes > MaxAllowedDecompressedBytes)
                    {
                        throw new InvalidDataException($"KMZ decompressed payload exceeds maximum safety threshold ({MaxAllowedDecompressedBytes / (1024 * 1024)} MB).");
                    }

                    // Directory entry
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(destPath);
                        continue;
                    }

                    string? parentDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                    {
                        Directory.CreateDirectory(parentDir);
                    }

                    entry.ExtractToFile(destPath, overwrite: true);

                    if (entry == kmlEntry)
                    {
                        primaryKmlPath = destPath;
                    }
                    else
                    {
                        // All non-primary entries can be referenced by KML hrefs
                        // (icons, images, linked KML, models and textures).
                        resources.Add((entry.FullName.Replace('\\', '/'), destPath));
                    }
                }
            }

            if (string.IsNullOrEmpty(primaryKmlPath) || !File.Exists(primaryKmlPath))
            {
                throw new FileNotFoundException("Primary KML document could not be extracted from KMZ archive.");
            }

            var root = KmlParser.ParseFile(primaryKmlPath);
            foreach (var (archivePath, filePath) in resources)
            {
                root.KmzResourcesBase64[archivePath] = Convert.ToBase64String(File.ReadAllBytes(filePath));
            }
            return root;
        }

        /// <summary>
        /// Packages original KMZ assets and KML without discarding existing output on failure.
        /// Every relative href within KML stays mapped to its original archive entry path.
        /// </summary>
        public static void CreatePackage(string kmzFilePath, PlaceItem root, IEnumerable<string>? assetFiles = null)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            var targetDir = Path.GetDirectoryName(Path.GetFullPath(kmzFilePath));
            if (!string.IsNullOrEmpty(targetDir)) Directory.CreateDirectory(targetDir);

            var assets = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            void Gather(PlaceItem item)
            {
                foreach (var asset in item.KmzResourcesBase64)
                {
                    var name = asset.Key.Replace('\\', '/');
                    if (string.IsNullOrWhiteSpace(name) || name.StartsWith('/') ||
                        name.Split('/').Any(part => part == ".." || part == "." || part.Length == 0) ||
                        name.Contains(':') || name.Equals("doc.kml", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Unsafe KMZ archive asset path.");
                    byte[] bytes = Convert.FromBase64String(asset.Value);
                    if (assets.TryGetValue(name, out var previous))
                    {
                        if (!previous.SequenceEqual(bytes))
                            throw new InvalidDataException("KMZ asset name collision: " + name);
                    }
                    else assets[name] = bytes;
                }
                foreach (var child in item.Children) Gather(child);
            }
            Gather(root);
            if (assetFiles != null)
            {
                foreach (var file in assetFiles)
                {
                    if (!File.Exists(file)) throw new FileNotFoundException("KMZ asset missing", file);
                    var name = "files/" + Path.GetFileName(file);
                    var bytes = File.ReadAllBytes(file);
                    if (assets.TryGetValue(name, out var previous) && !previous.SequenceEqual(bytes))
                        throw new InvalidDataException("KMZ asset name collision: " + name);
                    assets[name] = bytes;
                }
            }

            long total = assets.Values.Sum(bytes => (long)bytes.Length);
            if (total > MaxAllowedDecompressedBytes)
                throw new InvalidDataException("KMZ assets exceed the 100 MB safety threshold.");

            var tempArchive = kmzFilePath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var zip = ZipFile.Open(tempArchive, ZipArchiveMode.Create))
                {
                    var kmlEntry = zip.CreateEntry("doc.kml", CompressionLevel.Optimal);
                    using (var writer = new StreamWriter(kmlEntry.Open(), new System.Text.UTF8Encoding(false)))
                        writer.Write(KmlSerializer.SerializeToString(root));

                    foreach (var asset in assets)
                    {
                        var entry = zip.CreateEntry(asset.Key, CompressionLevel.Optimal);
                        using var stream = entry.Open();
                        stream.Write(asset.Value, 0, asset.Value.Length);
                    }
                }
                File.Move(tempArchive, kmzFilePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempArchive)) File.Delete(tempArchive);
            }
        }

    }
}
