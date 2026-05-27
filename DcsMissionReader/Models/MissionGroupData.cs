using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DcsMissionReader.Models
{
    public record MissionGroupData(
        string Name,
        string Category,
        string Side,
        double X,
        double Y,
        Table Units,
        Table RoutePoints
    );
}
