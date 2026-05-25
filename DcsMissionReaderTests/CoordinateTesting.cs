namespace DcsMissionReaderTests
{
    public class CoordinateTesting
    {
        [Theory]
        [InlineData(-40718, 279264, 44.669722, 37.778611)]
        [InlineData(-50379, 298406, 44.5829, 38.0219)]
        public void FindTheCorrectLatLongCoordinatesBasedOnDCSCoordinates(int dcsX, int dcsY, double expectedLat, double expectedLon)
        {
            BruteForceFindCaucasusReference(dcsX, dcsY, expectedLat, expectedLon);
        }

        private static void BruteForceFindCaucasusReference(int dcsX, int dcsY, double expectedLat, double expectedLon)
        {

            double bestError = double.MaxValue;
            double bestRefLat = 0;
            double bestRefLon = 0;

            Console.WriteLine("=== BRUTE FORCE SEARCH STARTED (90° CCW) ===");

            // Fine grid search around expected area
            for (double refLat = 44.0; refLat <= 46.0; refLat += 0.0001)
            {
                for (double refLon = 33.0; refLon <= 42.5; refLon += 0.0001)
                {
                    double newX = dcsY;
                    double newY = dcsX;

                    var (calcLat, calcLon) = DcsMissionReader.Services.MissionProcessor.ConvertGeneric(newX, newY, refLat, refLon);

                    double error = Math.Abs(calcLat - expectedLat) + Math.Abs(calcLon - expectedLon);

                    if (error < bestError)
                    {
                        bestError = error;
                        bestRefLat = refLat;
                        bestRefLon = refLon;

                        if (bestError < 0.000001)  // found exact match
                        {
                            Console.WriteLine($"EXACT MATCH FOUND!");
                            Console.WriteLine($"refLat = {bestRefLat:F8}, refLon = {bestRefLon:F8}");
                            Console.WriteLine($"Error = {bestError:F8}");
                            return;
                        }
                    }
                }
            }

            Console.WriteLine($"Best match:");
            Console.WriteLine($"refLat = {bestRefLat:F8}, refLon = {bestRefLon:F8}");
            Console.WriteLine($"Error = {bestError:F8}");
        }

        /*
        [Theory]
        [InlineData(YOUR_DCS_X, YOUR_DCS_Y, YOUR_LAT, YOUR_LON)]
        public void FindSouthAtlanticOrigin(double dcsX, double dcsY, double expectedLat, double expectedLon)
        {
            double bestError = double.MaxValue;
            double bestRefLat = 0;
            double bestRefLon = 0;

            Console.WriteLine("=== BRUTE FORCE SEARCH: SOUTH ATLANTIC ===");

            // Search grid roughly around the Falklands
            for (double refLat = -53.0; refLat <= -51.0; refLat += 0.0001)
            {
                for (double refLon = -60.0; refLon <= -58.0; refLon += 0.0001)
                {
                    // Use the NEW WGS84 ConvertGeneric (Normal axes, no flips!)
                    var (calcLat, calcLon) = DcsMissionReader.Services.MissionProcessor.ConvertGeneric(dcsX, dcsY, refLat, refLon);

                    double error = Math.Abs(calcLat - expectedLat) + Math.Abs(calcLon - expectedLon);

                    if (error < bestError)
                    {
                        bestError = error;
                        bestRefLat = refLat;
                        bestRefLon = refLon;
                    }
                }
            }

            Console.WriteLine($"New WGS 84 Origin found!");
            Console.WriteLine($"refLat = {bestRefLat:F6}, refLon = {bestRefLon:F6}");
        }
        */
    }
}