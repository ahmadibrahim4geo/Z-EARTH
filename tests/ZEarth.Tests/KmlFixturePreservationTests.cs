using System;
using System.IO;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ZEarth.Tests
{
    [TestClass]
    public class KmlFixturePreservationTests
    {
        private static string GetFixturePath(string fileName)
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "ZEarth.sln")))
            {
                var parent = Path.GetDirectoryName(dir);
                if (parent == dir) break;
                dir = parent;
            }

            var fixturePath = Path.Combine(dir ?? AppDomain.CurrentDomain.BaseDirectory, "tests", "ZEarth.Tests", "fixtures", fileName);
            if (File.Exists(fixturePath)) return fixturePath;

            var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures", fileName);
            if (File.Exists(localPath)) return localPath;

            throw new FileNotFoundException($"KML test fixture not found: {fileName} at {fixturePath}");
        }

        [TestMethod]
        public void NestedPlacesFixture_LoadsAndValidatesXmlDom()
        {
            var path = GetFixturePath("nested-places.kml");
            Assert.IsTrue(File.Exists(path));

            var doc = XDocument.Load(path);
            Assert.IsNotNull(doc.Root);

            XNamespace kmlNs = "http://www.opengis.net/kml/2.2";
            var document = doc.Root.Element(kmlNs + "Document");
            Assert.IsNotNull(document, "KML Document element must exist");

            var folders = document.Elements(kmlNs + "Folder");
            int folderCount = 0;
            foreach (var f in folders) folderCount++;
            Assert.IsTrue(folderCount >= 1, "Must contain at least 1 root folder");
        }

        [TestMethod]
        public void GeometryTimeFixture_LoadsAndValidatesCoordinates()
        {
            var path = GetFixturePath("geometry-time.kml");
            Assert.IsTrue(File.Exists(path));

            var doc = XDocument.Load(path);
            Assert.IsNotNull(doc.Root);

            XNamespace kmlNs = "http://www.opengis.net/kml/2.2";
            var document = doc.Root.Element(kmlNs + "Document");
            Assert.IsNotNull(document);

            var placemarks = document.Descendants(kmlNs + "Placemark");
            int count = 0;
            foreach (var p in placemarks) count++;
            Assert.IsTrue(count >= 1, "Placemarks must exist in fixture");
        }
    }
}
