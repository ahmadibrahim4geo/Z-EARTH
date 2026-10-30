using System;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.App.Services;

namespace ZEarth.Tests;

[TestClass]
public sealed class GlbModelImportServiceTests
{
    private static byte[] MinimalGlb()
    {
        var json = Encoding.UTF8.GetBytes("{\"asset\":{\"version\":\"2.0\"}} ");
        var bytes = new byte[20 + json.Length];
        bytes[0] = 0x67; bytes[1] = 0x6C; bytes[2] = 0x54; bytes[3] = 0x46;
        BitConverter.GetBytes(2u).CopyTo(bytes, 4);
        BitConverter.GetBytes((uint)bytes.Length).CopyTo(bytes, 8);
        BitConverter.GetBytes((uint)json.Length).CopyTo(bytes, 12);
        BitConverter.GetBytes(0x4E4F534Au).CopyTo(bytes, 16);
        json.CopyTo(bytes, 20);
        return bytes;
    }

    [TestMethod]
    public void ValidGlb_IsValidatedChecksummedAndRestorable()
    {
        var root = Environment.GetEnvironmentVariable("ZEARTH_SETTINGS_DIRECTORY")!;
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "model.glb");
        File.WriteAllBytes(path, MinimalGlb());

        Assert.IsTrue(GlbModelImportService.TryLoad(path, out var imported, out var error), error);
        Assert.IsNotNull(imported);
        Assert.AreEqual(MinimalGlb().Length, imported.ByteLength);
        StringAssert.StartsWith(imported.DataUri, "data:model/gltf-binary;base64,");
        Assert.IsTrue(GlbModelImportService.TryRestore(imported.FileName, imported.Base64,
            imported.Sha256, imported.ByteLength, out var restored, out error), error);
        Assert.AreEqual(imported.Sha256, restored!.Sha256);
    }

    [TestMethod]
    public void InvalidOrTamperedGlb_IsRejected()
    {
        var root = Environment.GetEnvironmentVariable("ZEARTH_SETTINGS_DIRECTORY")!;
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "bad.glb");
        File.WriteAllBytes(path, new byte[24]);

        Assert.IsFalse(GlbModelImportService.TryLoad(path, out _, out _));

        var valid = MinimalGlb();
        File.WriteAllBytes(path, valid);
        Assert.IsTrue(GlbModelImportService.TryLoad(path, out var imported, out _));
        Assert.IsFalse(GlbModelImportService.TryRestore(imported!.FileName, imported.Base64,
            new string('0', 64), imported.ByteLength, out _, out var error));
        StringAssert.Contains(error, "checksum");
    }
}
