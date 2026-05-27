using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DcsMissionReader.Models
{
    public record SvgMapData(List<SvgPoint> RoutePoints, List<SvgTarget> Targets, List<SvgWaypointMarker> Markers);
}
