using DcsMissionReader.Models;
using DcsMissionReader.Services.Interfaces;
using System.Globalization;
using System.Security;
using System.Text;
using System.Text.Json;

namespace DcsMissionReader.Services
{
    public sealed class BriefingStylesService : IBriefingStylesService
    {
        private const string DefaultStylesJsonFileName = "briefing-styles.json";

        private readonly string? _stylesJsonPath;

        public BriefingStylesService(string? stylesJsonPath = null)
        {
            _stylesJsonPath = stylesJsonPath;
        }

        public void AppendStyles(StringBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Append(BuildStylesKml());
        }

        public string BuildStylesKml()
        {
            KmlStyleDocument document = LoadStyleDocument();

            if (document.Styles.Count == 0)
            {
                throw new InvalidDataException("The briefing styles JSON file does not contain any styles.");
            }

            StringBuilder builder = new();

            foreach (KmlStyleDefinition style in document.Styles)
            {
                AppendStyle(builder, style);
            }

            return builder.ToString();
        }

        private KmlStyleDocument LoadStyleDocument()
        {
            string? stylePath = ResolveStylesJsonPath();

            if (string.IsNullOrWhiteSpace(stylePath) || !File.Exists(stylePath))
            {
                throw new FileNotFoundException("Briefing styles JSON file was not found.", stylePath);
            }

            string json = File.ReadAllText(stylePath, Encoding.UTF8);

            KmlStyleDocument? document = JsonSerializer.Deserialize<KmlStyleDocument>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });

            return document ?? throw new InvalidDataException("Briefing styles JSON file could not be deserialized.");
        }

        private string? ResolveStylesJsonPath()
        {
            if (!string.IsNullOrWhiteSpace(_stylesJsonPath))
            {
                return _stylesJsonPath;
            }

            string[] candidatePaths =
            [
                Path.Combine(AppContext.BaseDirectory, "Data", "KmlStyles", DefaultStylesJsonFileName),
                Path.Combine(Environment.CurrentDirectory, "Data", "KmlStyles", DefaultStylesJsonFileName),
                Path.Combine(AppContext.BaseDirectory, DefaultStylesJsonFileName),
                Path.Combine(Environment.CurrentDirectory, DefaultStylesJsonFileName)
            ];

            return candidatePaths.FirstOrDefault(File.Exists)
                ?? candidatePaths[0];
        }

        private static void AppendStyle(StringBuilder builder, KmlStyleDefinition style)
        {
            if (string.IsNullOrWhiteSpace(style.Id))
            {
                throw new InvalidDataException("A KML style definition is missing its required id.");
            }

            builder.AppendLine($"<Style id=\"{Escape(style.Id)}\">");

            AppendIconStyle(builder, style.IconStyle);
            AppendLineStyle(builder, style.LineStyle);
            AppendLabelStyle(builder, style.LabelStyle);
            AppendPolyStyle(builder, style.PolyStyle);

            builder.AppendLine("</Style>");
        }

        private static void AppendIconStyle(StringBuilder builder, KmlIconStyleDefinition? iconStyle)
        {
            if (iconStyle is null)
            {
                return;
            }

            builder.AppendLine("    <IconStyle>");

            AppendNumberElement(builder, "scale", iconStyle.Scale, indent: 8);
            AppendStringElement(builder, "color", iconStyle.Color, indent: 8);

            if (!string.IsNullOrWhiteSpace(iconStyle.Href))
            {
                builder.AppendLine("        <Icon>");
                AppendStringElement(builder, "href", iconStyle.Href, indent: 12);
                builder.AppendLine("        </Icon>");
            }

            builder.AppendLine("    </IconStyle>");
        }

        private static void AppendLineStyle(StringBuilder builder, KmlLineStyleDefinition? lineStyle)
        {
            if (lineStyle is null)
            {
                return;
            }

            builder.AppendLine("    <LineStyle>");
            AppendStringElement(builder, "color", lineStyle.Color, indent: 8);
            AppendNumberElement(builder, "width", lineStyle.Width, indent: 8);
            builder.AppendLine("    </LineStyle>");
        }

        private static void AppendLabelStyle(StringBuilder builder, KmlLabelStyleDefinition? labelStyle)
        {
            if (labelStyle is null)
            {
                return;
            }

            builder.AppendLine("    <LabelStyle>");
            AppendNumberElement(builder, "scale", labelStyle.Scale, indent: 8);
            AppendStringElement(builder, "color", labelStyle.Color, indent: 8);
            builder.AppendLine("    </LabelStyle>");
        }

        private static void AppendPolyStyle(StringBuilder builder, KmlPolyStyleDefinition? polyStyle)
        {
            if (polyStyle is null)
            {
                return;
            }

            builder.AppendLine("    <PolyStyle>");
            AppendStringElement(builder, "color", polyStyle.Color, indent: 8);
            builder.AppendLine("    </PolyStyle>");
        }

        private static void AppendStringElement(
            StringBuilder builder,
            string name,
            string? value,
            int indent)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            builder.Append(' ', indent);
            builder.Append('<');
            builder.Append(name);
            builder.Append('>');
            builder.Append(Escape(value));
            builder.Append("</");
            builder.Append(name);
            builder.AppendLine(">");
        }

        private static void AppendNumberElement(
            StringBuilder builder,
            string name,
            double? value,
            int indent)
        {
            if (value is null)
            {
                return;
            }

            AppendStringElement(
                builder,
                name,
                value.Value.ToString("0.########", CultureInfo.InvariantCulture),
                indent);
        }

        private static string Escape(string value)
        {
            return SecurityElement.Escape(value) ?? string.Empty;
        }
    }
}
