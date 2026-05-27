namespace DcsMissionReader.Models
{
    // The new data structure for easy testing
    public record WeatherData(
        string Clouds,
        string WindSurface,
        string Wind2000,
        string Wind8000,
        string Visibility,
        double Qnh,
        double Temp,
        string CloudLine
    );
}
