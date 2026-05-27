using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DcsMissionReader.Models
{
    public record AtoGroupData(string Coalition, string GroupName, string Task, string AircraftType, int UnitsCount, double StartTimeSec);
}
