using System;
using System.Collections.Generic;
using System.Text;

namespace DcsMissionReader.Models
{
    /// <summary>
    /// Contains the necessary parameters to define a map projection for a DCS theatre, including the type of projection,
    /// </summary>
    public sealed class DcsTerrainProjection
    {
        /// <summary>
        /// Name of the DCS theatre or terrain, used for identification and debugging purposes. This is not necessarily a standardized geodetic name, but should be descriptive enough to understand which theatre it refers to (e.g. "Caucasus", "Syria", "Persian Gulf"). The name is primarily for internal use and may not correspond to official geodetic naming conventions.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Kind of map projection used for the DCS theatre. This determines how the DCS mission coordinates are converted to geographic coordinates (latitude and longitude). The projection type is crucial for accurate coordinate conversion, as different theatres may use different projections based on their geographic location and size. For example, most DCS theatres use a Transverse Mercator projection, but this could be extended in the future to support other types of projections if needed.
        /// </summary>
        public required ProjectionKind Projection { get; init; }

        /// <summary>
        /// Central meridian of the projection in degrees. This is a key
        /// </summary>
        public required double CentralMeridianDegrees { get; init; }

        /// <summary>
        /// Is the false easting and northing applied to the DCS coordinates to shift the origin of the coordinate system. This is often used in map projections to ensure that all coordinates within a certain area are positive, which can simplify calculations and reduce errors. The false easting and northing values are added to the DCS coordinates before applying the projection formulas, effectively shifting the origin of the coordinate system to a more convenient location for the theatre. The specific values for false easting and northing depend on the theatre and its projection parameters, and they are essential for accurate coordinate conversion.
        /// </summary>
        public required double FalseEasting { get; init; }

        /// <summary>
        /// Is the false northing applied to the DCS coordinates to shift the origin of the coordinate system. Similar to false easting, this value is added to the DCS coordinates before applying the projection formulas, effectively shifting the origin of the coordinate system in the north-south direction. The false northing value is crucial for accurate coordinate conversion, as it ensures that all coordinates within the theatre are positive and properly aligned with the projection's coordinate system. The specific value for false northing depends on the theatre and its projection parameters.
        /// </summary>
        public required double FalseNorthing { get; init; }

        /// <summary>
        /// Scale factor applied to the DCS coordinates in the projection. This is a multiplicative factor that scales the DCS coordinates before applying the projection formulas. The scale factor is used to adjust the size of the projected area and can help reduce distortion in certain regions of the map. For example, a scale factor less than 1 can be used to reduce distortion near the central meridian, while a scale factor greater than 1 can be used to increase the size of the projected area. The specific value for the scale factor depends on the theatre and its projection parameters, and it is essential for accurate coordinate conversion.
        /// </summary>
        public required double ScaleFactor { get; init; }

        /// <summary>
        /// DCS axis mapping for the theatre, which specifies how DCS X and Z/Y coordinates should be interpreted in terms of easting and northing. This is important because different theatres may use different conventions for how the DCS coordinates are mapped to the projection's coordinate system. For example, some theatres may treat DCS X as easting and DCS Z/Y as northing, while others may treat DCS X as northing and DCS Z/Y as easting. The axis mapping is crucial for accurate coordinate conversion, as it determines how the DCS coordinates are transformed into geographic coordinates (latitude and longitude) based on the projection parameters.
        /// </summary>
        public required DcsAxisMapping AxisMapping { get; init; }

        /// <summary>
        /// Latitude of origin for the projection in degrees. This is the latitude at which the projection's coordinate system is defined to have zero northing. The latitude of origin is a key parameter for map projections, as it helps determine how the DCS coordinates are transformed into geographic coordinates (latitude and longitude). The specific value for the latitude of origin depends on the theatre and its projection parameters, and it is essential for accurate coordinate conversion.
        /// </summary>
        public double LatitudeOfOriginDegrees { get; init; } = 0.0;

        /// <summary>
        /// Constants for the ellipsoid used in the projection, which defines the shape of the Earth for the purposes of the projection. The semi-major axis is the radius of the Earth at the equator, and the flattening is a measure of how much the Earth is flattened at the poles compared to a perfect sphere. These parameters are crucial for accurate coordinate conversion, as they affect how distances and angles are calculated in the projection. The specific values for the semi-major axis and flattening depend on the ellipsoid model used for the theatre, and they are essential for accurate coordinate conversion.
        /// </summary>
        public double SemiMajorAxis { get; init; } = 6378137.0;

        /// <summary>
        /// Constants for the ellipsoid used in the projection, which defines the shape of the Earth for the purposes of the projection. The semi-major axis is the radius of the Earth at the equator, and the flattening is a measure of how much the Earth is flattened at the poles compared to a perfect sphere. These parameters are crucial for accurate coordinate conversion, as they affect how distances and angles are calculated in the projection. The specific values for the semi-major axis and flattening depend on the ellipsoid model used for the theatre, and they are essential for accurate coordinate conversion.
        /// </summary>
        public double Flattening { get; init; } = 1.0 / 298.257223563;

        /// <summary>
        /// The projection parameters have been validated against known reference points or test cases to ensure that the coordinate conversion is accurate. This is an important flag to indicate whether the projection parameters have been thoroughly tested and verified, as incorrect projection parameters can lead to significant errors in coordinate conversion. If the projection has not been validated, it may be necessary to perform additional testing and verification before relying on the coordinate conversion results for critical applications.
        /// </summary>
        public bool IsValidated { get; init; }

        /// <summary>
        /// Note about the source of the projection parameters, such as the reference points used for validation or the method used to derive the parameters. This information can be useful for debugging and future reference, as it provides context about how the projection parameters were determined and validated. For example, if the parameters were derived from a specific DCS mission file or from a known geodetic reference point, this information can help others understand the basis for the projection parameters and potentially reproduce or verify the results in the future.
        /// </summary>
        public string? SourceNote { get; init; }
    }
}
