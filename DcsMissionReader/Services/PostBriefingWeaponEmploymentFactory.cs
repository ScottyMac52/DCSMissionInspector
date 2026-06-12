using DcsMissionReader.Models;

namespace DcsMissionReader.Services
{
    internal static class PostBriefingWeaponEmploymentFactory
    {
        public static TacviewWeaponEmployment CreateWeaponEmployment(
            TacviewObjectTrack weapon,
            IReadOnlyDictionary<string, TacviewObjectTrack> objects)
        {
            TacviewObjectTrack? shooter = ResolveWeaponShooter(weapon, objects);

            return new TacviewWeaponEmployment
            {
                WeaponObjectId = weapon.ObjectId,
                WeaponName = weapon.Name,
                WeaponType = weapon.Type,
                ParentObjectId = weapon.ParentObjectId,

                ParentName = shooter is null
                    ? null
                    : GetDisplayName(shooter),

                Position = weapon.Start!
            };
        }

        public static TacviewObjectTrack? ResolveWeaponShooter(
            TacviewObjectTrack weapon,
            IReadOnlyDictionary<string, TacviewObjectTrack> objects)
        {
            return ResolveParentCandidate(weapon, objects);
        }

        private static TacviewObjectTrack? ResolveParentCandidate(
            TacviewObjectTrack weapon,
            IReadOnlyDictionary<string, TacviewObjectTrack> objects)
        {
            if (string.IsNullOrWhiteSpace(weapon.ParentObjectId))
            {
                return null;
            }

            string parentId = weapon.ParentObjectId.Trim();

            if (objects.TryGetValue(parentId, out TacviewObjectTrack? directParent))
            {
                return directParent;
            }

            string normalizedParentId = NormalizeTacviewObjectId(parentId);

            return objects
                .FirstOrDefault(pair =>
                    NormalizeTacviewObjectId(pair.Key)
                        .Equals(normalizedParentId, StringComparison.OrdinalIgnoreCase))
                .Value;
        }

        private static string NormalizeTacviewObjectId(string value)
        {
            return value
                .Trim()
                .TrimStart('#')
                .Trim('{', '}')
                .Trim();
        }

        private static string GetDisplayName(TacviewObjectTrack track)
        {
            return TacviewObjectDisplayName.GetDisplayName(track);
        }
    }
}
