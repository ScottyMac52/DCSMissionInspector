using DcsMissionReader.Models;

namespace DcsMissionReader.Services
{
    /// <summary>
    /// DCS coordinate converter service that provides methods to convert DCS mission coordinates (X, Z/Y) to geographic coordinates (latitude and longitude) based on the terrain's map projection parameters. This service supports different map projections and axis mappings for various DCS theatres, allowing for accurate coordinate conversion regardless of the specific projection used by the theatre. The conversion methods take into account the terrain's projection parameters, including false easting, false northing, scale factor, and axis mapping, to ensure that the resulting latitude and longitude values are accurate and consistent with the theatre's coordinate system. The service is designed to be extensible, allowing for additional projection types and theatres to be added in the future as needed.
    /// </summary>
    public static class DcsCoordinateConverter
    {
        /// <summary>
        /// Converts DCS mission coordinates (X, Z) to geographic coordinates (latitude and longitude) based on the terrain name. The method retrieves the corresponding DcsTerrainProjection for the specified terrain name from the TerrainProjectionRegistry and then calls the overloaded ConvertDcsToLatLon method that accepts a DcsTerrainProjection object to perform the actual coordinate conversion. This approach allows for a convenient way to convert coordinates using just the terrain name, while still leveraging the detailed projection parameters defined in the DcsTerrainProjection objects for accurate conversion.
        /// </summary>
        /// <param name="terrainName">Name of the terrain</param>
        /// <param name="dcsX">X</param>
        /// <param name="dcsZ">Z</param>
        /// <returns>Tuple of lat and lon</returns>
        public static (double lat, double lon) ConvertDcsToLatLon(string terrainName, double dcsX, double dcsZ)
        {
            var projection = TerrainProjectionRegistry.GetProjection(terrainName);

            return ConvertDcsToLatLon(projection, dcsX, dcsZ);
        }

        /// <summary>
        /// Converts DCS mission coordinates (X, Z) to geographic coordinates (latitude and longitude) based on the provided terrain projection parameters. The method identifies the projection type specified in the DcsTerrainProjection object and applies the corresponding mathematical formulas to perform the coordinate conversion. The resulting latitude and longitude values are returned as a tuple, allowing for easy integration with other components of the DCS mission reader application that require geographic coordinates for various functionalities such as mapping, navigation, or data visualization. The method is designed to handle different projection types and axis mappings, ensuring accurate coordinate conversion regardless of the specific projection used by the theatre.
        /// </summary>
        /// <param name="projection">Projection to use</param>
        /// <param name="dcsX">X</param>
        /// <param name="dcsZ">Z</param>
        /// <returns>Tuple of lat and lon</returns>
        /// <exception cref="NotSupportedException"></exception>
        public static (double lat, double lon) ConvertDcsToLatLon(
            DcsTerrainProjection projection,
            double dcsX,
            double dcsZ)
        {
            ArgumentNullException.ThrowIfNull(projection);

            return projection.Projection switch
            {
                ProjectionKind.TransverseMercator =>
                    ConvertTransverseMercatorToLatLon(projection, dcsX, dcsZ),

                _ =>
                    throw new NotSupportedException(
                        $"Projection '{projection.Projection}' is not supported for terrain '{projection.Name}'.")
            };
        }

        /// <summary>
        /// Converts DCS mission coordinates (X, Y) to geographic coordinates (latitude and longitude) based on the terrain name. This method is a convenience wrapper around the ConvertDcsToLatLon method that accepts DCS X and Z coordinates, allowing for a more intuitive interface when working with DCS mission files where the ground-plane Y coordinate is often used as the northing component. The method retrieves the corresponding DcsTerrainProjection for the specified terrain name from the TerrainProjectionRegistry and then calls the overloaded ConvertDcsToLatLon method that accepts a DcsTerrainProjection object to perform the actual coordinate conversion. This approach ensures that users can easily convert mission coordinates to geographic coordinates without needing to worry about the specific axis mapping used by the theatre, as the underlying conversion logic will handle it based on the projection parameters defined in the DcsTerrainProjection objects.
        /// </summary>
        /// <param name="terrainName">Name of the terrain</param>
        /// <param name="missionX">X</param>
        /// <param name="missionY">Z</param>
        /// <returns>Tuple of lat and lon</returns>
        public static (double lat, double lon) ConvertMissionXYToLatLon(
            string terrainName,
            double missionX,
            double missionY)
        {
            // In DCS mission files, ground-plane Y is equivalent to world-space Z.
            return ConvertDcsToLatLon(
                terrainName,
                dcsX: missionX,
                dcsZ: missionY);
        }

        /// <summary>
        /// Converts DCS mission coordinates (X, Z) to geographic coordinates (latitude and longitude
        /// </summary>
        /// <param name="terrain">Projection to use</param>
        /// <param name="dcsX">X</param>
        /// <param name="dcsZ">Z</param>
        /// <returns></returns>
        private static (double lat, double lon) ConvertTransverseMercatorToLatLon(
            DcsTerrainProjection terrain,
            double dcsX,
            double dcsZ)
        {
            var (projectedEasting, projectedNorthing) =
                MapDcsAxesToProjectedAxes(terrain, dcsX, dcsZ);

            double x = (projectedEasting - terrain.FalseEasting) / terrain.ScaleFactor;
            double y = (projectedNorthing - terrain.FalseNorthing) / terrain.ScaleFactor;

            double a = terrain.SemiMajorAxis;
            double f = terrain.Flattening;

            double e2 = f * (2.0 - f);
            double ePrime2 = e2 / (1.0 - e2);

            double e1 = (1.0 - Math.Sqrt(1.0 - e2)) /
                        (1.0 + Math.Sqrt(1.0 - e2));

            double meridionalArcDenominator =
                a *
                (
                    1.0
                    - e2 / 4.0
                    - 3.0 * e2 * e2 / 64.0
                    - 5.0 * e2 * e2 * e2 / 256.0
                );

            double mu = y / meridionalArcDenominator;

            double phi1 =
                mu
                + (3.0 * e1 / 2.0 - 27.0 * Math.Pow(e1, 3.0) / 32.0) * Math.Sin(2.0 * mu)
                + (21.0 * e1 * e1 / 16.0 - 55.0 * Math.Pow(e1, 4.0) / 32.0) * Math.Sin(4.0 * mu)
                + (151.0 * Math.Pow(e1, 3.0) / 96.0) * Math.Sin(6.0 * mu)
                + (1097.0 * Math.Pow(e1, 4.0) / 512.0) * Math.Sin(8.0 * mu);

            double sinPhi1 = Math.Sin(phi1);
            double cosPhi1 = Math.Cos(phi1);
            double tanPhi1 = Math.Tan(phi1);

            double n1 = a / Math.Sqrt(1.0 - e2 * sinPhi1 * sinPhi1);
            double t1 = tanPhi1 * tanPhi1;
            double c1 = ePrime2 * cosPhi1 * cosPhi1;
            double r1 =
                a *
                (1.0 - e2) /
                Math.Pow(1.0 - e2 * sinPhi1 * sinPhi1, 1.5);

            double d = x / n1;

            double latRad =
                phi1
                - (n1 * tanPhi1 / r1)
                *
                (
                    d * d / 2.0
                    - (5.0 + 3.0 * t1 + 10.0 * c1 - 4.0 * c1 * c1 - 9.0 * ePrime2)
                        * Math.Pow(d, 4.0) / 24.0
                    + (61.0 + 90.0 * t1 + 298.0 * c1 + 45.0 * t1 * t1 - 252.0 * ePrime2 - 3.0 * c1 * c1)
                        * Math.Pow(d, 6.0) / 720.0
                );

            double lonRad =
                DegreesToRadians(terrain.CentralMeridianDegrees)
                +
                (
                    d
                    - (1.0 + 2.0 * t1 + c1) * Math.Pow(d, 3.0) / 6.0
                    + (5.0 - 2.0 * c1 + 28.0 * t1 - 3.0 * c1 * c1 + 8.0 * c1 + 24.0 * t1 * t1)
                        * Math.Pow(d, 5.0) / 120.0
                ) / cosPhi1;

            return (RadiansToDegrees(latRad), RadiansToDegrees(lonRad));
        }

        /// <summary>
        /// Maps DCS mission coordinates (X, Z) to projected coordinates (easting, northing) based on the axis mapping defined in the DcsTerrainProjection. The method checks the AxisMapping property of the DcsTerrainProjection to determine how to interpret the DCS X and Z coordinates as easting and northing values for the map projection. This allows for flexibility in handling different theatres that may use different axis conventions, ensuring that the coordinate conversion logic can correctly map the DCS mission coordinates to the appropriate projected coordinates for accurate geographic conversion. If an unsupported axis mapping is encountered, the method throws a NotSupportedException to indicate that the conversion cannot be performed with the given terrain's axis mapping configuration.
        /// </summary>
        /// <param name="terrain">Terrain name</param>
        /// <param name="dcsX">X</param>
        /// <param name="dcsZ">Z</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        private static (double projectedEasting, double projectedNorthing) MapDcsAxesToProjectedAxes(
            DcsTerrainProjection terrain,
            double dcsX,
            double dcsZ)
        {
            return terrain.AxisMapping switch
            {
                DcsAxisMapping.XIsEasting_ZIsNorthing =>
                    (projectedEasting: dcsX, projectedNorthing: dcsZ),

                DcsAxisMapping.XIsNorthing_ZIsEasting =>
                    (projectedEasting: dcsZ, projectedNorthing: dcsX),

                _ =>
                    throw new NotSupportedException(
                        $"Unsupported DCS axis mapping '{terrain.AxisMapping}' for terrain '{terrain.Name}'.")
            };
        }

        /// <summary>
        /// Converts degrees to radians. This is a utility method used in the coordinate conversion process, as many of the mathematical formulas for map projections and geographic calculations require angles to be expressed in radians rather than degrees. The method takes an angle in degrees as input and returns the corresponding angle in radians by multiplying the degree value by π and dividing by 180. This conversion is essential for ensuring that the trigonometric functions used in the coordinate conversion formulas operate correctly, as they typically expect input angles to be in radians.
        /// </summary>
        /// <param name="degrees"></param>
        /// <returns></returns>
        private static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        /// <summary>
        /// Converts radians to degrees. This is a utility method used in the coordinate conversion process, as the final output of the conversion methods is typically expected to be in geographic coordinates (latitude and longitude) expressed in degrees. The method takes an angle in radians as input and returns the corresponding angle in degrees by multiplying the radian value by 180 and dividing by π. This conversion is essential for ensuring that the resulting latitude and longitude values are in a familiar and widely used format, making it easier for users to interpret and utilize the geographic coordinates derived from the DCS mission coordinates.
        /// </summary>
        /// <param name="radians"></param>
        /// <returns></returns>
        private static double RadiansToDegrees(double radians)
        {
            return radians * 180.0 / Math.PI;
        }
    }
}
