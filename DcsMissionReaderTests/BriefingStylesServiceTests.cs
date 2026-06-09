using DcsMissionReader.Services;
using System.Text;
using System.Text.Json;

namespace DcsMissionReaderTests
{
    public sealed class BriefingStylesServiceTests
    {
        [Fact]
        public void BuildStylesKml_WithValidJson_WritesIconLineLabelAndPolyStyles()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string jsonPath = Path.Combine(tempDirectory, "briefing-styles.json");

                File.WriteAllText(
                    jsonPath,
                    """
                    {
                      "styles": [
                        {
                          "id": "mixedStyle",
                          "iconStyle": {
                            "scale": 1.25,
                            "color": "ff00ffff",
                            "href": "icons/explode.png"
                          },
                          "lineStyle": {
                            "color": "ffff0000",
                            "width": 4
                          },
                          "labelStyle": {
                            "scale": 0.75,
                            "color": "ffffffff"
                          },
                          "polyStyle": {
                            "color": "00000000"
                          }
                        }
                      ]
                    }
                    """);

                var service = new BriefingStylesService(jsonPath);

                string kml = service.BuildStylesKml();

                Assert.Contains("<Style id=\"mixedStyle\">", kml);
                Assert.Contains("<IconStyle>", kml);
                Assert.Contains("<scale>1.25</scale>", kml);
                Assert.Contains("<color>ff00ffff</color>", kml);
                Assert.Contains("<href>icons/explode.png</href>", kml);
                Assert.Contains("<LineStyle>", kml);
                Assert.Contains("<width>4</width>", kml);
                Assert.Contains("<LabelStyle>", kml);
                Assert.Contains("<PolyStyle>", kml);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void AppendStyles_WithValidJson_AppendsStylesToExistingBuilder()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string jsonPath = Path.Combine(tempDirectory, "briefing-styles.json");

                File.WriteAllText(
                    jsonPath,
                    """
                    {
                      "styles": [
                        {
                          "id": "blueTrackStyle",
                          "lineStyle": {
                            "color": "ffff0000",
                            "width": 4
                          }
                        }
                      ]
                    }
                    """);

                var service = new BriefingStylesService(jsonPath);
                StringBuilder builder = new();

                builder.AppendLine("<Document>");
                service.AppendStyles(builder);

                string kml = builder.ToString();

                Assert.StartsWith("<Document>", kml, StringComparison.Ordinal);
                Assert.Contains("<Style id=\"blueTrackStyle\">", kml);
                Assert.Contains("<LineStyle>", kml);
                Assert.Contains("<color>ffff0000</color>", kml);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void AppendStyles_WithNullBuilder_ThrowsArgumentNullException()
        {
            var service = new BriefingStylesService("unused.json");

            Assert.Throws<ArgumentNullException>(() => service.AppendStyles(null!));
        }

        [Fact]
        public void BuildStylesKml_WithMissingJson_ThrowsFileNotFoundException()
        {
            string missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.json");
            var service = new BriefingStylesService(missingPath);

            Assert.Throws<FileNotFoundException>(() => service.BuildStylesKml());
        }

        [Fact]
        public void BuildStylesKml_WithEmptyStyles_ThrowsInvalidDataException()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string jsonPath = Path.Combine(tempDirectory, "briefing-styles.json");

                File.WriteAllText(jsonPath, "{ \"styles\": [] }");

                var service = new BriefingStylesService(jsonPath);

                InvalidDataException exception = Assert.Throws<InvalidDataException>(() => service.BuildStylesKml());

                Assert.Contains("does not contain any styles", exception.Message);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void BuildStylesKml_WithMissingStyleId_ThrowsInvalidDataException()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string jsonPath = Path.Combine(tempDirectory, "briefing-styles.json");

                File.WriteAllText(
                    jsonPath,
                    """
                    {
                      "styles": [
                        {
                          "lineStyle": {
                            "color": "ffff0000",
                            "width": 4
                          }
                        }
                      ]
                    }
                    """);

                var service = new BriefingStylesService(jsonPath);

                InvalidDataException exception = Assert.Throws<InvalidDataException>(() => service.BuildStylesKml());

                Assert.Contains("missing its required id", exception.Message);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void BuildStylesKml_WithXmlSensitiveValues_EscapesValues()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string jsonPath = Path.Combine(tempDirectory, "briefing-styles.json");

                File.WriteAllText(
                    jsonPath,
                    """
                    {
                      "styles": [
                        {
                          "id": "style<&\"id",
                          "iconStyle": {
                            "href": "icons/explode<&.png"
                          }
                        }
                      ]
                    }
                    """);

                var service = new BriefingStylesService(jsonPath);

                string kml = service.BuildStylesKml();

                Assert.Contains("<Style id=\"style&lt;&amp;&quot;id\">", kml);
                Assert.Contains("<href>icons/explode&lt;&amp;.png</href>", kml);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void BuildStylesKml_WithMalformedJson_ThrowsJsonException()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string jsonPath = Path.Combine(tempDirectory, "briefing-styles.json");

                File.WriteAllText(jsonPath, "{ not valid json }");

                var service = new BriefingStylesService(jsonPath);

                Assert.Throws<JsonException>(() => service.BuildStylesKml());
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void BuildStylesKml_WithCurrentPostBriefingVisualStyles_WritesConfiguredIconsAndNoCautionIcon()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                string jsonPath = Path.Combine(tempDirectory, "briefing-styles.json");

                File.WriteAllText(
                    jsonPath,
                    """
                    {
                      "styles": [
                        {
                          "id": "weaponResultStyle",
                          "iconStyle": {
                            "scale": 0.55,
                            "color": "ff00ffff",
                            "href": "https://maps.google.com/mapfiles/kml/shapes/placemark_circle.png"
                          },
                          "labelStyle": {
                            "scale": 0
                          }
                        },
                        {
                          "id": "destroyedObjectStyle",
                          "iconStyle": {
                            "scale": 0.75,
                            "href": "icons/explode.png"
                          },
                          "labelStyle": {
                            "scale": 0
                          }
                        }
                      ]
                    }
                    """);

                var service = new BriefingStylesService(jsonPath);

                string kml = service.BuildStylesKml();

                Assert.Contains("<Style id=\"weaponResultStyle\">", kml);
                Assert.Contains("<scale>0.55</scale>", kml);
                Assert.Contains("<href>https://maps.google.com/mapfiles/kml/shapes/placemark_circle.png</href>", kml);
                Assert.Contains("<Style id=\"destroyedObjectStyle\">", kml);
                Assert.Contains("<href>icons/explode.png</href>", kml);
                Assert.Contains("<LabelStyle>", kml);
                Assert.Contains("<scale>0</scale>", kml);
                Assert.DoesNotContain("https://maps.google.com/mapfiles/kml/shapes/caution.png", kml);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        private static string CreateTempDirectory()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }
    }
}
