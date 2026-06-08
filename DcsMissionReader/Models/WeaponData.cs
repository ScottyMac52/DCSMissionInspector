namespace DcsMissionReader.Models
{
    public class WeaponData
    {
        public string? CLSID { get; set; }
        public string? DisplayName { get; set; }
        public double? Weight { get; set; }
        public List<string> Aliases { get; set; } = [];
    }
}