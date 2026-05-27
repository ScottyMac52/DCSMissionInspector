using MoonSharp.Interpreter;

namespace DcsMissionReader.Models
{
    public record FlightWaypointGroup(string Coalition, string GroupName, string Aircraft, int UnitCount, string Task, Table RoutePoints);
}
