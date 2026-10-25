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
                }
            }

            if (string.IsNullOrEmpty(primaryKmlPath) || !File.Exists(primaryKmlPath))
            {
                throw new FileNotFoundException("Primary KML document could not be extracted from KMZ archive.");
            }

            return KmlParser.ParseFile(primaryKmlPath);
        }

        /// <summary>
        /// Packages a PlaceItem tree into a KMZ archive containing doc.kml and any referenced icon assets.
        /// </summary>
        public static void CreatePackage(string kmzFilePath, PlaceItem root, IEnumerable<string>? assetFiles = null)
        {
            var targetDir = Path.GetDirectoryName(kmzFilePath);
            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            if (File.Exists(kmzFilePath))
            {
                File.Delete(kmzFilePath);
            }

            string tempKml = Path.Combine(Path.GetTempPath(), $"doc_{Guid.NewGuid():N}.kml");
            try
            {
                KmlSerializer.SerializeToFile(root, tempKml);

                using (var zip = ZipFile.Open(kmzFilePath, ZipArchiveMode.Create))
                {
                    zip.CreateEntryFromFile(tempKml, "doc.kml", CompressionLevel.Optimal);

                    if (assetFiles != null)
                    {
                        foreach (var asset in assetFiles)
                        {
                            if (File.Exists(asset))
                            {
                                string entryName = $"files/{Path.GetFileName(asset)}";
                                zip.CreateEntryFromFile(asset, entryName, CompressionLevel.Optimal);
                            }
                        }
                    }
                }
            }
            finally
            {
                if (File.Exists(tempKml))
                {
                    try { File.Delete(tempKml); } catch { }
                }
            }
        }
    }
}
