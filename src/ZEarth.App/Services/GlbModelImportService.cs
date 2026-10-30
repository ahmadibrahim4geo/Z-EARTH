using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace ZEarth.App.Services;

public sealed record GlbModelImport(
    string FileName,
    string Base64,
    string Sha256,
    int ByteLength)
{
    public string DataUri => $"data:model/gltf-binary;base64,{Base64}";
}

/// <summary>
/// Validates a self-contained glTF 2.0 binary before it crosses the WebView2
/// bridge. External-resource .gltf, Collada and OBJ files are intentionally not
/// advertised because Z EARTH does not ship a converter for those formats.
/// </summary>
public static class GlbModelImportService
{
    public const int MaxBytes = 8 * 1024 * 1024;

    public static bool TryLoad(string path, out GlbModelImport? model, out string error)
    {
        model = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(path) ||
            !string.Equals(Path.GetExtension(path), ".glb", StringComparison.OrdinalIgnoreCase))
        {
            error = "Only self-contained glTF 2.0 Binary (.glb) models are supported.";
            return false;
        }

        FileInfo info;
        try
        {
            info = new FileInfo(path);
            if (!info.Exists)
            {
                error = "The selected GLB file no longer exists.";
                return false;
            }
            if (info.Length < 20 || info.Length > MaxBytes)
            {
                error = $"GLB size must be between 20 bytes and {MaxBytes / (1024 * 1024)} MB.";
                return false;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            error = $"The selected model cannot be inspected: {ex.Message}";
            return false;
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(info.FullName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = $"The selected model cannot be read: {ex.Message}";
            return false;
        }

        if (!TryCreate(info.Name, bytes, expectedSha256: null, out model, out error))
            return false;
        return true;
    }

    public static bool TryRestore(string fileName, string base64, string sha256,
        int expectedByteLength, out GlbModelImport? model, out string error)
    {
        model = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(base64) || expectedByteLength < 20 || expectedByteLength > MaxBytes)
        {
            error = "Stored GLB metadata is invalid.";
            return false;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            error = "Stored GLB data is not valid Base64.";
            return false;
        }

        if (bytes.Length != expectedByteLength)
        {
            error = "Stored GLB length does not match its project metadata.";
            return false;
        }
        return TryCreate(Path.GetFileName(fileName), bytes, sha256, out model, out error);
    }

    private static bool TryCreate(string fileName, byte[] bytes, string? expectedSha256,
        out GlbModelImport? model, out string error)
    {
        model = null;
        error = string.Empty;
        var magicIsGlb = bytes.Length >= 12 &&
            bytes[0] == 0x67 && bytes[1] == 0x6C && bytes[2] == 0x54 && bytes[3] == 0x46;
        var version = bytes.Length >= 12 ? BitConverter.ToUInt32(bytes, 4) : 0;
        var declaredLength = bytes.Length >= 12 ? BitConverter.ToUInt32(bytes, 8) : 0;
        var jsonChunkLength = bytes.Length >= 20 ? BitConverter.ToUInt32(bytes, 12) : 0;
        var jsonChunkType = bytes.Length >= 20 ? BitConverter.ToUInt32(bytes, 16) : 0;
        if (!magicIsGlb || version != 2 || declaredLength != bytes.Length ||
            jsonChunkLength == 0 || jsonChunkLength % 4 != 0 ||
            jsonChunkType != 0x4E4F534A || 20L + jsonChunkLength > bytes.Length)
        {
            error = "The file is not a valid self-contained glTF 2.0 Binary (GLB).";
            return false;
        }

        try
        {
            using var json = JsonDocument.Parse(new ReadOnlyMemory<byte>(bytes, 20, checked((int)jsonChunkLength)));
            if (!json.RootElement.TryGetProperty("asset", out var asset) ||
                !asset.TryGetProperty("version", out var assetVersion) ||
                assetVersion.GetString() != "2.0")
            {
                error = "The GLB JSON chunk does not declare glTF asset version 2.0.";
                return false;
            }
        }
        catch (JsonException)
        {
            error = "The GLB JSON chunk is malformed.";
            return false;
        }

        var actualSha256 = Convert.ToHexString(SHA256.HashData(bytes));
        if (!string.IsNullOrWhiteSpace(expectedSha256) &&
            !string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            error = "Stored GLB content does not match its project checksum.";
            return false;
        }

        model = new GlbModelImport(
            fileName,
            Convert.ToBase64String(bytes),
            actualSha256,
            bytes.Length);
        return true;
    }
}
