namespace DcsMissionReader.Models
{
    public record OrderOfBattleSide(string Coalition, int TotalAircraft, int TotalShips, int TotalGround, int TotalStatics, Dictionary<string, int> AircraftBreakdown);
}
