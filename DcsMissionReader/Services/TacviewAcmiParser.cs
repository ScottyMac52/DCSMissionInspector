using DcsMissionReader.Models;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace DcsMissionReader.Services
{
    internal static class TacviewAcmiParser
    {
        public static TacviewAcmiParseData ParseZippedAcmi(string acmiZipFilePath)
        {
            using ZipArchive archive = ZipFile.OpenRead(acmiZipFilePath);

            ZipArchiveEntry? directAcmiEntry = archive.Entries
                .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                .FirstOrDefault(e =>
                    e.FullName.EndsWith(".acmi", StringComparison.OrdinalIgnoreCase)
                    && !e.FullName.EndsWith(".zip.acmi", StringComparison.OrdinalIgnoreCase));

            if (directAcmiEntry is not null)
            {
                using Stream stream = directAcmiEntry.Open();
                using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                return ParseAcmi(reader);
            }

            ZipArchiveEntry? nestedZipEntry = archive.Entries
                .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                .FirstOrDefault(e =>
                    e.FullName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                    || e.FullName.EndsWith(".zip.acmi", StringComparison.OrdinalIgnoreCase));

            if (nestedZipEntry is not null)
            {
                using MemoryStream nestedZipBytes = new();

                using (Stream nestedZipStream = nestedZipEntry.Open())
                {
                    nestedZipStream.CopyTo(nestedZipBytes);
                }

                nestedZipBytes.Position = 0;

                using ZipArchive nestedArchive = new(nestedZipBytes, ZipArchiveMode.Read, leaveOpen: false);

                ZipArchiveEntry? nestedAcmiEntry = nestedArchive.Entries
                    .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                    .FirstOrDefault(e =>
                        e.FullName.EndsWith(".acmi", StringComparison.OrdinalIgnoreCase)
                        && !e.FullName.EndsWith(".zip.acmi", StringComparison.OrdinalIgnoreCase))
                    ?? nestedArchive.Entries.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.Name));

                if (nestedAcmiEntry is null)
                {
                    throw new InvalidDataException("The nested zip file does not contain an ACMI entry.");
                }

                using Stream stream = nestedAcmiEntry.Open();
                using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                return ParseAcmi(reader);
            }

            ZipArchiveEntry? fallbackEntry = archive.Entries
                .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.Name));

            if (fallbackEntry is null)
            {
                throw new InvalidDataException("The zip file does not contain an ACMI entry.");
            }

            using Stream fallbackStream = fallbackEntry.Open();
            using StreamReader fallbackReader = new(fallbackStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            return ParseAcmi(fallbackReader);
        }

        private static TacviewAcmiParseData ParseAcmi(TextReader reader)
        {
            Dictionary<string, TacviewObjectTrack> objects = new(StringComparer.OrdinalIgnoreCase);
            List<TacviewEventRecord> events = new();
            List<TacviewRemovalRecord> removals = new();
            List<TacviewHealthChangeRecord> healthChanges = new();
            TacviewMissionInfo mission = new();

            double currentTimeSeconds = 0;
            DateTimeOffset? referenceTimeUtc = null;
            double referenceLongitude = 0;
            double referenceLatitude = 0;

            foreach (string rawLine in ReadLogicalAcmiLines(reader))
            {
                string line = rawLine.Trim();

                if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith("FileType=", StringComparison.OrdinalIgnoreCase))
                {
                    mission.FileType = line["FileType=".Length..].Trim();
                    continue;
                }

                if (line.StartsWith("FileVersion=", StringComparison.OrdinalIgnoreCase))
                {
                    mission.FileVersion = line["FileVersion=".Length..].Trim();
                    continue;
                }

                if (line.StartsWith("#", StringComparison.Ordinal))
                {
                    if (double.TryParse(
                            line[1..],
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out double parsedTime))
                    {
                        currentTimeSeconds = parsedTime;
                    }

                    continue;
                }

                if (line.StartsWith("-", StringComparison.Ordinal))
                {
                    string removedObjectId = line[1..].Trim();

                    if (!string.IsNullOrWhiteSpace(removedObjectId))
                    {
                        removals.Add(new TacviewRemovalRecord(
                            removedObjectId,
                            currentTimeSeconds,
                            AddSeconds(referenceTimeUtc, currentTimeSeconds)));
                    }

                    continue;
                }

                int commaIndex = line.IndexOf(',');

                if (commaIndex <= 0)
                {
                    continue;
                }

                string objectId = line[..commaIndex].Trim();
                string payload = line[(commaIndex + 1)..].Trim();

                if (objectId == "0")
                {
                    ParseGlobalOrEventPayload(
                        payload,
                        currentTimeSeconds,
                        referenceTimeUtc,
                        events,
                        mission,
                        ref referenceLongitude,
                        ref referenceLatitude,
                        ref referenceTimeUtc);

                    continue;
                }

                TacviewObjectTrack track = GetOrCreateObject(objects, objectId);

                foreach (string token in SplitPropertyTokens(payload))
                {
                    int equalsIndex = token.IndexOf('=');

                    if (equalsIndex <= 0)
                    {
                        continue;
                    }

                    string key = token[..equalsIndex].Trim();
                    string value = token[(equalsIndex + 1)..].Trim();

                    ApplyObjectProperty(
                        track,
                        key,
                        value,
                        currentTimeSeconds,
                        referenceTimeUtc,
                        referenceLongitude,
                        referenceLatitude,
                        healthChanges);
                }
            }

            return new TacviewAcmiParseData(
                mission,
                objects,
                events,
                removals,
                healthChanges,
                referenceTimeUtc);
        }

        private static void ParseGlobalOrEventPayload(
            string payload,
            double currentTimeSeconds,
            DateTimeOffset? currentReferenceTimeUtc,
            List<TacviewEventRecord> events,
            TacviewMissionInfo mission,
            ref double referenceLongitude,
            ref double referenceLatitude,
            ref DateTimeOffset? referenceTimeUtc)
        {
            foreach (string token in SplitPropertyTokens(payload))
            {
                int equalsIndex = token.IndexOf('=');

                if (equalsIndex <= 0)
                {
                    continue;
                }

                string key = token[..equalsIndex].Trim();
                string value = token[(equalsIndex + 1)..].Trim();

                if (key.Equals("ReferenceTime", StringComparison.OrdinalIgnoreCase))
                {
                    if (DateTimeOffset.TryParse(
                            value,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                            out DateTimeOffset parsedReferenceTime))
                    {
                        referenceTimeUtc = parsedReferenceTime;
                        mission.ReferenceTimeUtc = parsedReferenceTime;
                    }

                    continue;
                }

                if (key.Equals("RecordingTime", StringComparison.OrdinalIgnoreCase))
                {
                    if (DateTimeOffset.TryParse(
                            value,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                            out DateTimeOffset parsedRecordingTime))
                    {
                        mission.RecordingTimeUtc = parsedRecordingTime;
                    }

                    continue;
                }

                if (key.Equals("ReferenceLongitude", StringComparison.OrdinalIgnoreCase))
                {
                    referenceLongitude = ParseDoubleOrDefault(value);
                    mission.ReferenceLongitude = referenceLongitude;
                    continue;
                }

                if (key.Equals("ReferenceLatitude", StringComparison.OrdinalIgnoreCase))
                {
                    referenceLatitude = ParseDoubleOrDefault(value);
                    mission.ReferenceLatitude = referenceLatitude;
                    continue;
                }

                if (key.Equals("Title", StringComparison.OrdinalIgnoreCase))
                {
                    mission.Title = value;
                    continue;
                }

                if (key.Equals("DataRecorder", StringComparison.OrdinalIgnoreCase))
                {
                    mission.DataRecorder = value;
                    continue;
                }

                if (key.Equals("DataSource", StringComparison.OrdinalIgnoreCase))
                {
                    mission.DataSource = value;
                    continue;
                }

                if (key.Equals("Author", StringComparison.OrdinalIgnoreCase))
                {
                    mission.Author = value;
                    continue;
                }

                if (key.Equals("Comments", StringComparison.OrdinalIgnoreCase))
                {
                    mission.Comments = value;
                    continue;
                }

                if (key.Equals("Category", StringComparison.OrdinalIgnoreCase))
                {
                    mission.Category = value;
                    continue;
                }

                if (key.Equals("Briefing", StringComparison.OrdinalIgnoreCase))
                {
                    mission.Briefing = value;
                    continue;
                }

                if (key.Equals("Event", StringComparison.OrdinalIgnoreCase))
                {
                    events.Add(ParseEvent(value, currentTimeSeconds, currentReferenceTimeUtc));
                    continue;
                }
            }
        }

        private static TacviewEventRecord ParseEvent(
            string value,
            double currentTimeSeconds,
            DateTimeOffset? referenceTimeUtc)
        {
            string[] parts = value
                .Split('|', StringSplitOptions.None)
                .Select(p => p.Trim())
                .ToArray();

            string eventType = parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0])
                ? parts[0]
                : "Unknown";

            string? text = parts.Length > 1 ? parts[^1] : null;

            return new TacviewEventRecord
            {
                TimeSeconds = currentTimeSeconds,
                AbsoluteTimeUtc = AddSeconds(referenceTimeUtc, currentTimeSeconds),
                EventType = eventType,
                Parts = parts,
                Text = text
            };
        }

        private static TacviewObjectTrack GetOrCreateObject(
            Dictionary<string, TacviewObjectTrack> objects,
            string objectId)
        {
            if (objects.TryGetValue(objectId, out TacviewObjectTrack? existing))
            {
                return existing;
            }

            var created = new TacviewObjectTrack
            {
                ObjectId = objectId
            };

            objects[objectId] = created;

            return created;
        }

        private static void ApplyObjectProperty(
            TacviewObjectTrack track,
            string key,
            string value,
            double currentTimeSeconds,
            DateTimeOffset? referenceTimeUtc,
            double referenceLongitude,
            double referenceLatitude,
            List<TacviewHealthChangeRecord> healthChanges)
        {
            switch (key)
            {
                case "Name":
                    track.Name = value;
                    break;

                case "Type":
                    track.Type = value;
                    break;

                case "Group":
                    track.Group = value;
                    break;

                case "Parent":
                    track.ParentObjectId = value;
                    break;

                case "Coalition":
                    track.Coalition = value;
                    break;

                case "Color":
                    track.Color = value;
                    break;

                case "Health":
                    if (double.TryParse(
                            value,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out double health))
                    {
                        double? previousHealth = track.Health;
                        track.Health = health;

                        if (previousHealth is not null && health < previousHealth.Value)
                        {
                            healthChanges.Add(new TacviewHealthChangeRecord(
                                track.ObjectId,
                                previousHealth.Value,
                                health,
                                currentTimeSeconds,
                                AddSeconds(referenceTimeUtc, currentTimeSeconds),
                                track.End));
                        }
                    }
                    break;

                case "T":
                    TacviewPositionSample? sample = ParseTransform(
                        value,
                        currentTimeSeconds,
                        referenceTimeUtc,
                        referenceLongitude,
                        referenceLatitude);

                    if (sample is not null)
                    {
                        track.Samples.Add(sample);
                    }

                    break;
            }
        }

        private static TacviewPositionSample? ParseTransform(
            string value,
            double currentTimeSeconds,
            DateTimeOffset? referenceTimeUtc,
            double referenceLongitude,
            double referenceLatitude)
        {
            string[] parts = value.Split('|', StringSplitOptions.None);

            if (parts.Length < 2)
            {
                return null;
            }

            if (!TryParseNullableDouble(parts[0], out double? lonOffset)
                || !TryParseNullableDouble(parts[1], out double? latOffset))
            {
                return null;
            }

            if (lonOffset is null || latOffset is null)
            {
                return null;
            }

            double? altitude = null;

            if (parts.Length >= 3 && TryParseNullableDouble(parts[2], out double? parsedAltitude))
            {
                altitude = parsedAltitude;
            }

            return new TacviewPositionSample
            {
                TimeSeconds = currentTimeSeconds,
                AbsoluteTimeUtc = AddSeconds(referenceTimeUtc, currentTimeSeconds),
                Longitude = referenceLongitude + lonOffset.Value,
                Latitude = referenceLatitude + latOffset.Value,
                AltitudeMeters = altitude
            };
        }

        private static IEnumerable<string> ReadLogicalAcmiLines(TextReader reader)
        {
            StringBuilder current = new();

            while (reader.ReadLine() is string line)
            {
                bool continues = line.EndsWith("\\", StringComparison.Ordinal);

                if (continues)
                {
                    current.Append(line[..^1]);
                    current.AppendLine();
                    continue;
                }

                if (current.Length == 0)
                {
                    yield return line;
                    continue;
                }

                current.Append(line);
                yield return current.ToString();
                current.Clear();
            }

            if (current.Length > 0)
            {
                yield return current.ToString();
            }
        }

        private static IReadOnlyList<string> SplitPropertyTokens(string payload)
        {
            List<string> tokens = new();
            StringBuilder current = new();

            bool escaping = false;

            foreach (char ch in payload)
            {
                if (escaping)
                {
                    current.Append(ch);
                    escaping = false;
                    continue;
                }

                if (ch == '\\')
                {
                    escaping = true;
                    continue;
                }

                if (ch == ',')
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current.ToString().Trim());
                        current.Clear();
                    }

                    continue;
                }

                current.Append(ch);
            }

            if (escaping)
            {
                current.Append('\\');
            }

            if (current.Length > 0)
            {
                tokens.Add(current.ToString().Trim());
            }

            return tokens;
        }

        private static double ParseDoubleOrDefault(string value)
        {
            return double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double parsed)
                ? parsed
                : 0;
        }

        private static bool TryParseNullableDouble(string value, out double? parsed)
        {
            parsed = null;

            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            if (!double.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double concrete))
            {
                return false;
            }

            parsed = concrete;
            return true;
        }

        private static DateTimeOffset? AddSeconds(DateTimeOffset? referenceTimeUtc, double seconds)
        {
            return referenceTimeUtc?.AddSeconds(seconds);
        }
    }
}
