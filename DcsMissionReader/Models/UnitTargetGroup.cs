namespace DcsMissionReader.Models
{
    public class UnitTargetGroup
    {
        public string Coalition { get; set; }
        public string Category { get; set; }
        public string GroupName { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public int TotalUnits { get; set; }
        public string UnitInfo { get; set; }

        // Added these properties to support threat data
        public double MaxDet { get; set; }
        public double MaxThr { get; set; }

        public UnitTargetGroup(string coalition, string category, string groupName, double x, double y, int totalUnits, string unitInfo, double maxDet, double maxThr)
        {
            Coalition = coalition;
            Category = category;
            GroupName = groupName;
            X = x;
            Y = y;
            TotalUnits = totalUnits;
            UnitInfo = unitInfo;
            MaxDet = maxDet;
            MaxThr = maxThr;
        }
    }
}