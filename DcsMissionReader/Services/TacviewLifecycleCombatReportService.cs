using System.IO.Compression;
using DcsMissionReader.Models;

namespace DcsMissionReader.Services
{
    internal static class TacviewLifecycleCombatReportService
    {
        public static TacviewCombatReport BuildFromAcmiText(
            string acmiText)
        {
            ArgumentNullException.ThrowIfNull(acmiText);

            using var reader = new StringReader(acmiText);

            return BuildFromAcmiReader(reader);
        }

        public static TacviewCombatReport BuildFromAcmiReader(
            TextReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);

            TacviewLifecycleReplay replay =
                TacviewLifecycleReplayBuilder.Build(reader);

            IReadOnlyList<TacviewWeaponLaunch> launches =
                TacviewWeaponLaunchDetector.Detect(replay);

            IReadOnlyList<TacviewWeaponTerminalEvent> terminalEvents =
                TacviewWeaponTerminalCorrelator.Correlate(replay, launches);

            return TacviewCombatReportBuilder.Build(
                replay,
                launches,
                terminalEvents);
        }

        public static TacviewCombatReport BuildFromAcmiFile(
            string acmiPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(acmiPath);

            using StreamReader reader = File.OpenText(acmiPath);

            return BuildFromAcmiReader(reader);
        }

        public static TacviewCombatReport BuildFromZippedAcmiFile(
            string zipAcmiPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(zipAcmiPath);

            using ZipArchive archive = ZipFile.OpenRead(zipAcmiPath);

            ZipArchiveEntry? acmiEntry = archive.Entries
                .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                .FirstOrDefault(e =>
                    e.FullName.EndsWith(".acmi", StringComparison.OrdinalIgnoreCase)
                    || e.FullName.EndsWith(".txt.acmi", StringComparison.OrdinalIgnoreCase));

            if (acmiEntry is null)
            {
                throw new InvalidOperationException(
                    $"No ACMI entry was found in zip archive '{zipAcmiPath}'.");
            }

            using Stream stream = acmiEntry.Open();
            using var reader = new StreamReader(stream);

            return BuildFromAcmiReader(reader);
        }
    }
}