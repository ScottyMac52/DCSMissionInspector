namespace DcsMissionReader.Models
{
    public class Unit
    {
        // The unique ID from the mission file
        public int UnitId { get; set; }

        // The name identifier, e.g., "S-300V 9A82 ln"
        public string Name { get; set; }

        // The internal type name, e.g., "S-300V 9A82 ln"
        public string Type { get; set; }

        // The spatial coordinates from the mission file
        public double X { get; set; }
        public double Y { get; set; }

        // Additional metadata used for filtering/mapping
        public string Category { get; set; }
        public string Country { get; set; }
    }
}