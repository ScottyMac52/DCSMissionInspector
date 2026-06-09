namespace DcsMissionReader.Models
{
    public sealed class KmlStyleDocument
    {
        public List<KmlStyleDefinition> Styles { get; set; } = [];
    }

    public sealed class KmlStyleDefinition
    {
        public string Id { get; set; } = string.Empty;

        public KmlIconStyleDefinition? IconStyle { get; set; }

        public KmlLineStyleDefinition? LineStyle { get; set; }

        public KmlLabelStyleDefinition? LabelStyle { get; set; }

        public KmlPolyStyleDefinition? PolyStyle { get; set; }
    }

    public sealed class KmlIconStyleDefinition
    {
        public double? Scale { get; set; }

        public string? Color { get; set; }

        public string? Href { get; set; }
    }

    public sealed class KmlLineStyleDefinition
    {
        public string? Color { get; set; }

        public double? Width { get; set; }
    }

    public sealed class KmlLabelStyleDefinition
    {
        public double? Scale { get; set; }

        public string? Color { get; set; }
    }

    public sealed class KmlPolyStyleDefinition
    {
        public string? Color { get; set; }
    }
}
