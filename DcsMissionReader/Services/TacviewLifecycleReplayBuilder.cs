using System.Globalization;
using DcsMissionReader.Models;

namespace DcsMissionReader.Services
{
    internal static class TacviewLifecycleReplayBuilder
    {
        public static TacviewLifecycleReplay Build(TextReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);

            var replay = new TacviewLifecycleReplay();

            double currentTimeSeconds = 0.0;
            TacviewLifecycleFrame? currentFrame = null;

            string? line;

            while ((line = reader.ReadLine()) is not null)
            {
                line = line.Trim();

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line.StartsWith("FileType=", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("FileVersion=", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (line.StartsWith('#'))
                {
                    currentTimeSeconds = ParseFrameTime(line);
                    currentFrame = new TacviewLifecycleFrame
                    {
                        TimeSeconds = currentTimeSeconds
                    };

                    replay.Frames.Add(currentFrame);
                    continue;
                }

                if (line.StartsWith('-'))
                {
                    currentFrame ??= GetOrCreateInitialFrame(replay, currentTimeSeconds);

                    TacviewObjectRemoval removal =
                        ParseRemoval(line, currentTimeSeconds, replay);

                    replay.Removals.Add(removal);
                    currentFrame.Removals.Add(removal);

                    if (replay.Objects.TryGetValue(removal.ObjectId, out TacviewLifecycleObject? removedObject))
                    {
                        removedObject.RemovedSeconds = currentTimeSeconds;
                        removedObject.LastSeenSeconds = currentTimeSeconds;
                    }

                    continue;
                }

                if (!IsObjectUpdateLine(line))
                {
                    continue;
                }

                currentFrame ??= GetOrCreateInitialFrame(replay, currentTimeSeconds);

                TacviewLifecycleObjectUpdate update =
                    ParseObjectUpdate(line, currentTimeSeconds, replay);

                currentFrame.Updates.Add(update);
            }

            return replay;
        }

        private static TacviewLifecycleFrame GetOrCreateInitialFrame(
            TacviewLifecycleReplay replay,
            double currentTimeSeconds)
        {
            if (replay.Frames.Count > 0)
            {
                return replay.Frames[^1];
            }

            var frame = new TacviewLifecycleFrame
            {
                TimeSeconds = currentTimeSeconds
            };

            replay.Frames.Add(frame);

            return frame;
        }

        private static bool IsObjectUpdateLine(string line)
        {
            int commaIndex = line.IndexOf(',');

            if (commaIndex <= 0)
            {
                return false;
            }

            string objectId = line[..commaIndex].Trim();

            if (string.IsNullOrWhiteSpace(objectId))
            {
                return false;
            }

            // Global ACMI properties are usually written against object id 0.
            // They are not simulation objects for this lifecycle replay.
            if (objectId.Equals("0", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private static double ParseFrameTime(string line)
        {
            string value = line[1..].Trim();

            return double.Parse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture);
        }

        private static TacviewObjectRemoval ParseRemoval(
            string line,
            double currentTimeSeconds,
            TacviewLifecycleReplay replay)
        {
            string objectId = line[1..].Trim();

            replay.Objects.TryGetValue(objectId, out TacviewLifecycleObject? existingObject);

            return new TacviewObjectRemoval
            {
                ObjectId = objectId,
                TimeSeconds = currentTimeSeconds,
                ObjectName = existingObject?.Name,
                ObjectPilot = existingObject?.Pilot,
                ObjectGroup = existingObject?.Group,
                ObjectType = existingObject?.Type,
                LastSample = existingObject?.End
            };
        }

        private static TacviewLifecycleObjectUpdate ParseObjectUpdate(
            string line,
            double currentTimeSeconds,
            TacviewLifecycleReplay replay)
        {
            int commaIndex = line.IndexOf(',');

            string objectId = line[..commaIndex].Trim();
            string payload = line[(commaIndex + 1)..];

            if (!replay.Objects.TryGetValue(objectId, out TacviewLifecycleObject? lifecycleObject))
            {
                lifecycleObject = new TacviewLifecycleObject
                {
                    ObjectId = objectId,
                    FirstSeenSeconds = currentTimeSeconds
                };

                replay.Objects.Add(objectId, lifecycleObject);
            }

            bool wasWeapon = lifecycleObject.IsWeapon;

            Dictionary<string, string> properties = ParseProperties(payload);

            TacviewLifecycleSample? sample = null;

            if (properties.TryGetValue("T", out string? transform))
            {
                sample = ParseSample(objectId, currentTimeSeconds, transform);

                lifecycleObject.Samples.Add(sample);

                lifecycleObject.Start ??= sample;
                lifecycleObject.End = sample;
            }

            ApplyIdentityProperties(lifecycleObject, properties);

            lifecycleObject.LastSeenSeconds = currentTimeSeconds;

            if (!wasWeapon && lifecycleObject.IsWeapon)
            {
                var weaponBirth = new TacviewWeaponBirth
                {
                    WeaponObjectId = lifecycleObject.ObjectId,
                    WeaponName = lifecycleObject.Name,
                    WeaponType = lifecycleObject.Type,
                    WeaponCoalition = lifecycleObject.Coalition,
                    WeaponCountry = lifecycleObject.Country,
                    TimeSeconds = currentTimeSeconds,
                    BirthSample = sample ?? lifecycleObject.End
                };

                replay.WeaponBirths.Add(weaponBirth);
            }

            return new TacviewLifecycleObjectUpdate
            {
                ObjectId = objectId,
                TimeSeconds = currentTimeSeconds,
                RawLine = line,
                Sample = sample
            };
        }

        private static Dictionary<string, string> ParseProperties(string payload)
        {
            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string rawPart in payload.Split(','))
            {
                string part = rawPart.Trim();

                if (string.IsNullOrWhiteSpace(part))
                {
                    continue;
                }

                int equalsIndex = part.IndexOf('=');

                if (equalsIndex <= 0)
                {
                    continue;
                }

                string key = part[..equalsIndex].Trim();
                string value = part[(equalsIndex + 1)..].Trim();

                properties[key] = value;
            }

            return properties;
        }

        private static void ApplyIdentityProperties(
            TacviewLifecycleObject lifecycleObject,
            IReadOnlyDictionary<string, string> properties)
        {
            if (properties.TryGetValue("Name", out string? name))
            {
                lifecycleObject.Name = name;
            }

            if (properties.TryGetValue("Pilot", out string? pilot))
            {
                lifecycleObject.Pilot = pilot;
            }

            if (properties.TryGetValue("Group", out string? group))
            {
                lifecycleObject.Group = group;
            }

            if (properties.TryGetValue("Type", out string? type))
            {
                lifecycleObject.Type = type;
            }

            if (properties.TryGetValue("Color", out string? color))
            {
                lifecycleObject.Color = color;
            }

            if (properties.TryGetValue("Coalition", out string? coalition))
            {
                lifecycleObject.Coalition = coalition;
            }

            if (properties.TryGetValue("Country", out string? country))
            {
                lifecycleObject.Country = country;
            }
        }

        private static TacviewLifecycleSample ParseSample(
            string objectId,
            double currentTimeSeconds,
            string transform)
        {
            string[] fields = transform.Split('|');

            double? longitudeOffset = ParseNullableDouble(GetField(fields, 0));
            double? latitudeOffset = ParseNullableDouble(GetField(fields, 1));
            double? altitudeMeters = ParseNullableDouble(GetField(fields, 2));

            double? localX;
            double? localY;
            double? headingDegrees;

            if (fields.Length >= 8)
            {
                // Full Tacview transform:
                // lon|lat|alt|roll?|pitch?|heading?|localX|localY|heading?
                localX = ParseNullableDouble(GetField(fields, 6));
                localY = ParseNullableDouble(GetField(fields, 7));
                headingDegrees =
                    ParseNullableDouble(GetField(fields, 8))
                    ?? ParseNullableDouble(GetField(fields, 5));
            }
            else
            {
                // Compact Tacview update:
                // lon|lat|alt|localX|localY
                localX = ParseNullableDouble(GetField(fields, 3));
                localY = ParseNullableDouble(GetField(fields, 4));
                headingDegrees = null;
            }

            return new TacviewLifecycleSample
            {
                ObjectId = objectId,
                TimeSeconds = currentTimeSeconds,
                LongitudeOffset = longitudeOffset,
                LatitudeOffset = latitudeOffset,
                AltitudeMeters = altitudeMeters,
                HeadingDegrees = headingDegrees,
                LocalX = localX,
                LocalY = localY,
                RawTransform = transform
            };
        }

        private static string? GetField(string[] fields, int index)
        {
            if (index < 0 || index >= fields.Length)
            {
                return null;
            }

            return fields[index];
        }

        private static double? ParseNullableDouble(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (double.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double parsedValue))
            {
                return parsedValue;
            }

            return null;
        }
    }
}