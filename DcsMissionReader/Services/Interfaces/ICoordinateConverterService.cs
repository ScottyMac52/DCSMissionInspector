using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DcsMissionReader.Services.Interfaces
{
    public interface ICoordinateConverterService
    {
        (double lat, double lon) Convert(double dcsX, double dcsY, string theatre);
    }
}
