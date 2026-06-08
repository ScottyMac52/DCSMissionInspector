using DcsMissionReader.Models;
using DcsMissionReader.Services.Interfaces;

namespace DcsMissionReader.Services.Generators
{
    /// <summary>
    /// Export strategy for generating Google Earth KML post-briefing output
    /// from a zipped Tacview ACMI file.
    /// </summary>
    public sealed class PostBriefingExportGenerator(
        IPostBriefingService postBriefingService) : IMissionExportStrategy
    {
        public bool ShouldExport(AppOptions options)
        {
            return options.PostBrief;
        }

        public void Export(MissionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.Options is null)
            {
                throw new InvalidOperationException(
                    "Post-briefing export requires AppOptions on the MissionContext.");
            }

            string? acmiZipFilePath = context.Options.PostBriefAcmiZipFilePath;

            if (string.IsNullOrWhiteSpace(acmiZipFilePath))
            {
                throw new InvalidOperationException(
                    "Post-briefing export requires a zipped Tacview ACMI file path.");
            }

            if (!File.Exists(acmiZipFilePath))
            {
                throw new FileNotFoundException(
                    "Post-briefing ACMI zip file was not found.",
                    acmiZipFilePath);
            }

            Directory.CreateDirectory(context.ReportDir);

            string outputKmlFilePath = ResolveOutputKmlPath(context);

            var kmlOptions = new PostBriefingKmlOptions
            {
                MaxTrackPointsPerObject = context.Options.PostBriefMaxTrackPoints <= 0
                    ? 75
                    : context.Options.PostBriefMaxTrackPoints,

                TreatTacviewEnemiesAsRed = false,
                TreatTacviewAlliesAsBlue = false,
                InferBlueForKnownUsNavalAssets = true
            };

            var result = postBriefingService.CreatePostBriefingKml(
                acmiZipFilePath,
                outputKmlFilePath,
                kmlOptions);

            Console.WriteLine($" 🗺️ Post-brief KML created → {result.OutputKmlFilePath}");
            Console.WriteLine($"    Group tracks:        {result.GroupTrackCount}");
            Console.WriteLine($"    Weapon employments:  {result.WeaponEmploymentCount}");
            Console.WriteLine($"    Weapon results:      {result.WeaponResultCount}");
        }

        private static string ResolveOutputKmlPath(MissionContext context)
        {
            string? explicitOutputPath = context.Options?.PostBriefKmlOutputPath;

            if (!string.IsNullOrWhiteSpace(explicitOutputPath))
            {
                return explicitOutputPath;
            }

            string sortie = !string.IsNullOrWhiteSpace(context.Sortie)
                ? context.Sortie
                : GetSortieNameFromAcmiZipPath(context.Options?.PostBriefAcmiZipFilePath);

            string cleanName = MissionUtils.SanitizeFileName(sortie);

            return Path.Combine(
                context.ReportDir,
                $"{cleanName}.postbrief.kml");
        }

        private static string GetSortieNameFromAcmiZipPath(string? acmiZipFilePath)
        {
            if (string.IsNullOrWhiteSpace(acmiZipFilePath))
            {
                return "Tacview_PostBrief";
            }

            string fileName = Path.GetFileName(acmiZipFilePath);

            if (fileName.EndsWith(".acmi.zip", StringComparison.OrdinalIgnoreCase))
            {
                return fileName[..^".acmi.zip".Length];
            }

            if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return fileName[..^".zip".Length];
            }

            return Path.GetFileNameWithoutExtension(fileName);
        }
    }
}