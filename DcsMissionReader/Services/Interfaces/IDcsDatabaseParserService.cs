using System.Collections.Generic;

namespace DcsMissionReader.Services.Interfaces
{
    public interface IDcsDatabaseParserService
    {
        /// <summary>
        /// Loads real threat ranges (tracking + firing) from DCS core database + any mods in Saved Games.
        /// This is the definitive source instead of a static dictionary.
        /// </summary>
        Dictionary<string, (double trackNm, double fireNm)> LoadRealThreatRanges();
    }
}