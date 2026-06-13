using DcsMissionReader.Models;
using System.Collections.ObjectModel;

namespace DcsMissionReader.Services
{
    /// <summary>
    /// Defines a registry of map projections for DCS theatres, allowing for lookup of projection parameters based on the terrain name. This registry provides a centralized location for storing and retrieving the necessary parameters to define a map projection for each DCS theatre, including the type of projection, central meridian, false easting and northing, scale factor, and axis mapping. The registry is implemented as a read-only dictionary that maps normalized terrain names to their corresponding DcsTerrainProjection instances, enabling efficient retrieval of projection parameters for coordinate conversion during mission processing.
    /// </summary>
    public static class TerrainProjectionRegistry
    {
        /// <summary>
        /// Dictionary mapping normalized terrain names to their corresponding DcsTerrainProjection instances. The keys are normalized by trimming whitespace and removing spaces, underscores, and hyphens, allowing for flexible lookup based on various input formats of terrain names. The values are instances of DcsTerrainProjection that contain the necessary parameters to define the map projection for each DCS theatre. This registry is initialized with known projections for various DCS theatres and can be used to retrieve projection parameters for coordinate conversion during mission processing.
        /// </summary>
        public static IReadOnlyDictionary<string, DcsTerrainProjection> Projections { get; } =
            CreateRegistry();

        /// <summary>
        /// Attempts to retrieve the <see cref="DcsTerrainProjection"/> registered for the specified
        /// DCS terrain name.
        /// 
        /// The provided <paramref name="terrainName"/> is normalized (whitespace trimmed and common
        /// separators such as spaces, underscores and hyphens removed) before performing the lookup,
        /// allowing flexible input formats (for example, "Persian Gulf", "PersianGulf" or "PG").
        /// </summary>
        /// <param name="terrainName">
        /// The terrain name to look up (examples: "Caucasus", "Nevada", "Persian Gulf", "NTTR").
        /// </param>
        /// <param name="projection">
        /// When this method returns, contains the matching <see cref="DcsTerrainProjection"/> if found;
        /// otherwise <c>null</c>.
        /// </param>
        /// <returns>
        /// <c>true</c> if a projection was found for the specified terrain name; otherwise <c>false</c>.
        /// </returns>
        /// <remarks>
        /// Use <see cref="GetProjection(string)"/> when a missing projection should be treated as an error
        /// (it throws <see cref="NotSupportedException"/>). This method performs a case-insensitive lookup.
        /// </remarks>        
        public static bool TryGetProjection(
        string terrainName,
            out DcsTerrainProjection projection)
        {
            return Projections.TryGetValue(NormalizeTerrainName(terrainName), out projection!);
        }

        /// <summary>
        /// Retrieves the <see cref="DcsTerrainProjection"/> registered for the specified DCS terrain name.
        /// </summary>
        /// <param name="terrainName"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public static DcsTerrainProjection GetProjection(string terrainName)
        {
            if (TryGetProjection(terrainName, out var projection))
            {
                return projection;
            }

            throw new NotSupportedException(
                $"No registered DCS terrain projection exists for terrain '{terrainName}'.");
        }

        /// <summary>
        /// Creates the registry of DCS terrain projections with known parameters for each theatre. This method initializes a dictionary with normalized terrain names as keys and their corresponding DcsTerrainProjection instances as values. The registry includes projections for various DCS theatres such as Caucasus, Persian Gulf, Nevada, Normandy, Syria, Sinai Map, Mariana Islands, Falklands, and The Channel. Each projection is registered with multiple aliases to allow for flexible lookup based on different input formats of terrain names. The resulting dictionary is wrapped in a ReadOnlyDictionary to ensure that the registry cannot be modified after initialization.
        /// </summary>
        /// <returns></returns>
        private static IReadOnlyDictionary<string, DcsTerrainProjection> CreateRegistry()
        {
            var dictionary = new Dictionary<string, DcsTerrainProjection>(
                StringComparer.OrdinalIgnoreCase);

            Register(
                dictionary,
                new DcsTerrainProjection
                {
                    Name = "Caucasus",
                    Projection = ProjectionKind.TransverseMercator,
                    CentralMeridianDegrees = 33.0,
                    FalseEasting = -99516.99999997323,
                    FalseNorthing = -4998114.999999984,
                    ScaleFactor = 0.9996,
                    AxisMapping = DcsAxisMapping.XIsNorthing_ZIsEasting,
                    IsValidated = true,
                    SourceNote = "Public PyDCS-derived Transverse Mercator projection."
                },
                "Caucasus");

            Register(
                dictionary,
                new DcsTerrainProjection
                {
                    Name = "PersianGulf",
                    Projection = ProjectionKind.TransverseMercator,
                    CentralMeridianDegrees = 57.0,
                    FalseEasting = 75755.99999999645,
                    FalseNorthing = -2894933.0000000377,
                    ScaleFactor = 0.9996,
                    AxisMapping = DcsAxisMapping.XIsNorthing_ZIsEasting,
                    IsValidated = true,
                    SourceNote = "Public PyDCS-derived Transverse Mercator projection."
                },
                "PersianGulf",
                "Persian Gulf",
                "PG");

            Register(
                dictionary,
                new DcsTerrainProjection
                {
                    Name = "Nevada",
                    Projection = ProjectionKind.TransverseMercator,
                    CentralMeridianDegrees = -117.0,
                    FalseEasting = -193996.80999964548,
                    FalseNorthing = -4410028.063999966,
                    ScaleFactor = 0.9996,
                    AxisMapping = DcsAxisMapping.XIsNorthing_ZIsEasting,
                    IsValidated = true,
                    SourceNote = "Public PyDCS-derived Transverse Mercator projection."
                },
                "Nevada",
                "NTTR",
                "Nevada Test and Training Range");

            Register(
                dictionary,
                new DcsTerrainProjection
                {
                    Name = "Normandy",
                    Projection = ProjectionKind.TransverseMercator,
                    CentralMeridianDegrees = -3.0,
                    FalseEasting = -195526.00000000204,
                    FalseNorthing = -5484812.999999951,
                    ScaleFactor = 0.9996,
                    AxisMapping = DcsAxisMapping.XIsNorthing_ZIsEasting,
                    IsValidated = true,
                    SourceNote = "Public PyDCS-derived Transverse Mercator projection."
                },
                "Normandy",
                "Normandy2",
                "Normandy 2",
                "Normandy 2.0");

            Register(
                dictionary,
                new DcsTerrainProjection
                {
                    Name = "Syria",
                    Projection = ProjectionKind.TransverseMercator,
                    CentralMeridianDegrees = 39.0,
                    FalseEasting = 282801.00000003993,
                    FalseNorthing = -3879865.9999999935,
                    ScaleFactor = 0.9996,
                    AxisMapping = DcsAxisMapping.XIsNorthing_ZIsEasting,
                    IsValidated = true,
                    SourceNote = "Public PyDCS-derived Transverse Mercator projection."
                },
                "Syria");

            Register(
                dictionary,
                new DcsTerrainProjection
                {
                    Name = "SinaiMap",
                    Projection = ProjectionKind.TransverseMercator,
                    CentralMeridianDegrees = 33.0,
                    FalseEasting = 169221.9999999585,
                    FalseNorthing = -3325312.9999999693,
                    ScaleFactor = 0.9996,
                    AxisMapping = DcsAxisMapping.XIsNorthing_ZIsEasting,
                    IsValidated = true,
                    SourceNote = "Public PyDCS-derived Transverse Mercator projection."
                },
                "SinaiMap",
                "Sinai");

            Register(
                dictionary,
                new DcsTerrainProjection
                {
                    Name = "MarianaIslands",
                    Projection = ProjectionKind.TransverseMercator,
                    CentralMeridianDegrees = 147.0,
                    FalseEasting = 238417.99999989968,
                    FalseNorthing = -1491840.000000048,
                    ScaleFactor = 0.9996,
                    AxisMapping = DcsAxisMapping.XIsNorthing_ZIsEasting,
                    IsValidated = true,
                    SourceNote = "Public PyDCS-derived Transverse Mercator projection."
                },
                "MarianaIslands",
                "Marianas",
                "Mariana Islands");

            Register(
                dictionary,
                new DcsTerrainProjection
                {
                    Name = "Falklands",
                    Projection = ProjectionKind.TransverseMercator,
                    CentralMeridianDegrees = -57.0,
                    FalseEasting = 147639.99999997593,
                    FalseNorthing = 5815417.000000032,
                    ScaleFactor = 0.9996,
                    AxisMapping = DcsAxisMapping.XIsNorthing_ZIsEasting,
                    IsValidated = true,
                    SourceNote = "Public PyDCS-derived Transverse Mercator projection."
                },
                "Falklands",
                "SouthAtlantic",
                "South Atlantic",
                "SA");

            Register(
                dictionary,
                new DcsTerrainProjection
                {
                    Name = "GermanyCW",
                    Projection = ProjectionKind.TransverseMercator,
                    CentralMeridianDegrees = 21.0,
                    FalseEasting = 35427.619999985734,
                    FalseNorthing = -6061633.128000011,
                    ScaleFactor = 0.9996,
                    AxisMapping = DcsAxisMapping.XIsNorthing_ZIsEasting,
                    IsValidated = true,
                    SourceNote = "Public PyDCS-derived generated Transverse Mercator projection."
                },
                "GermanyCW",
                "Germany CW",
                "ColdWarGermany",
                "Cold War Germany",
                "Germany Cold War",
                "German Cold War",
                "Germany");

            Register(
                dictionary,
                new DcsTerrainProjection
                {
                    Name = "TheChannel",
                    Projection = ProjectionKind.TransverseMercator,
                    CentralMeridianDegrees = 3.0,
                    FalseEasting = 99376.00000000288,
                    FalseNorthing = -5636889.00000001,
                    ScaleFactor = 0.9996,
                    AxisMapping = DcsAxisMapping.XIsNorthing_ZIsEasting,
                    IsValidated = true,
                    SourceNote = "Public dcs-projections Transverse Mercator projection."
                },
                "TheChannel",
                "Channel",
                "The Channel");

            return new ReadOnlyDictionary<string, DcsTerrainProjection>(dictionary);
        }

        /// <summary>
        /// Registers a DCS terrain projection in the provided dictionary with multiple aliases for flexible lookup. This method takes a dictionary, a DcsTerrainProjection instance, and an array of aliases (terrain names) to register the projection under. For each alias, the method normalizes the terrain name by trimming whitespace and removing common separators such as spaces, underscores, and hyphens before adding it to the dictionary with the corresponding projection. This allows for flexible lookup of projections based on various input formats of terrain names when retrieving projections from the registry.
        /// </summary>
        /// <param name="dictionary"></param>
        /// <param name="projection"></param>
        /// <param name="aliases"></param>
        private static void Register(
            Dictionary<string, DcsTerrainProjection> dictionary,
            DcsTerrainProjection projection,
            params string[] aliases)
        {
            foreach (var alias in aliases)
            {
                dictionary[NormalizeTerrainName(alias)] = projection;
            }
        }

        /// <summary>
        /// Returns a normalized version of the terrain name by trimming whitespace and removing common separators such as spaces, underscores, and hyphens. This normalization allows for flexible lookup of terrain names in the registry, enabling users to input terrain names in various formats (e.g., "Persian Gulf", "PersianGulf", "PG") while still successfully retrieving the corresponding projection from the registry. The normalization process ensures that different variations of terrain names can be treated as equivalent when performing lookups in the registry.
        /// </summary>
        /// <param name="terrainName"></param>
        /// <returns></returns>
        private static string NormalizeTerrainName(string terrainName)
        {
            return terrainName
                .Trim()
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty);
        }
    }
}
