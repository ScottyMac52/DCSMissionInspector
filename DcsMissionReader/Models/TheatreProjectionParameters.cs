namespace DcsMissionReader.Models
{
    // The mathematical parameters required for Transverse Mercator projections
    public class TheatreProjectionParameters
    {
        public double CentralMeridian { get; set; }
        public double FalseEasting { get; set; }
        public double FalseNorthing { get; set; }
        public double ScaleFactor { get; set; } = 0.9996;
    }
}
