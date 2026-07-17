#nullable enable

using System;
using System.Collections.Generic;

namespace AtomicLandPirate.Simulation.LastBearing
{
    public sealed class ResidentRecord : IEquatable<ResidentRecord>
    {
        public ResidentRecord(string stableId, ResidentKind kind)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                throw new ArgumentException(
                    "LAST_BEARING_RESIDENT_ID_REQUIRED",
                    nameof(stableId));
            }

            StableId = stableId;
            Kind = kind;
        }

        public string StableId { get; }

        public ResidentKind Kind { get; }

        public bool Equals(ResidentRecord? other)
        {
            return other != null
                && Kind == other.Kind
                && string.Equals(
                    StableId,
                    other.StableId,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as ResidentRecord);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.Ordinal.GetHashCode(StableId) * 397)
                    ^ (int)Kind;
            }
        }
    }

    public sealed class ResidentRoster : IEquatable<ResidentRoster>
    {
        public const string HumanResidentId = "resident:human:0001";
        public const string RobotResidentId = "resident:robot:0001";

        private readonly ResidentRecord[] _residents;
        private readonly IReadOnlyList<ResidentRecord> _readOnlyResidents;

        public ResidentRoster(IReadOnlyList<ResidentRecord> residents)
        {
            if (residents == null)
            {
                throw new ArgumentNullException(nameof(residents));
            }

            if (residents.Count == 0 || residents.Count > 2)
            {
                throw new ArgumentException(
                    "LAST_BEARING_ROSTER_SIZE_INVALID",
                    nameof(residents));
            }

            _residents = new ResidentRecord[residents.Count];
            string? previousId = null;
            for (var index = 0; index < residents.Count; index++)
            {
                var resident = residents[index]
                    ?? throw new ArgumentException(
                        "LAST_BEARING_ROSTER_NULL_RESIDENT",
                        nameof(residents));
                if (previousId != null
                    && string.CompareOrdinal(previousId, resident.StableId) >= 0)
                {
                    throw new ArgumentException(
                        "LAST_BEARING_ROSTER_NOT_STRICTLY_SORTED",
                        nameof(residents));
                }

                ValidateCanonicalResident(resident);
                _residents[index] = resident;
                previousId = resident.StableId;
            }

            Composition = DeriveComposition(_residents);
            _readOnlyResidents = Array.AsReadOnly(_residents);
        }

        public ColonyComposition Composition { get; }

        public IReadOnlyList<ResidentRecord> Residents => _readOnlyResidents;

        public static ResidentRoster CreateForComposition(
            ColonyComposition composition)
        {
            switch (composition)
            {
                case ColonyComposition.HumanOnly:
                    return new ResidentRoster(
                        new[]
                        {
                            new ResidentRecord(
                                HumanResidentId,
                                ResidentKind.HumanCohort),
                        });
                case ColonyComposition.RobotOnly:
                    return new ResidentRoster(
                        new[]
                        {
                            new ResidentRecord(
                                RobotResidentId,
                                ResidentKind.UtilityRobot),
                        });
                case ColonyComposition.Mixed:
                    return new ResidentRoster(
                        new[]
                        {
                            new ResidentRecord(
                                HumanResidentId,
                                ResidentKind.HumanCohort),
                            new ResidentRecord(
                                RobotResidentId,
                                ResidentKind.UtilityRobot),
                        });
                default:
                    throw new ArgumentOutOfRangeException(nameof(composition));
            }
        }

        public bool Contains(string stableId)
        {
            if (stableId == null)
            {
                throw new ArgumentNullException(nameof(stableId));
            }

            foreach (var resident in _residents)
            {
                if (string.Equals(
                    resident.StableId,
                    stableId,
                    StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public bool Equals(ResidentRoster? other)
        {
            if (other == null || Composition != other.Composition
                || _residents.Length != other._residents.Length)
            {
                return false;
            }

            for (var index = 0; index < _residents.Length; index++)
            {
                if (!_residents[index].Equals(other._residents[index]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as ResidentRoster);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Composition;
                foreach (var resident in _residents)
                {
                    hash = (hash * 397) ^ resident.GetHashCode();
                }

                return hash;
            }
        }

        private static void ValidateCanonicalResident(ResidentRecord resident)
        {
            if (resident.Kind == ResidentKind.HumanCohort
                && string.Equals(
                    resident.StableId,
                    HumanResidentId,
                    StringComparison.Ordinal))
            {
                return;
            }

            if (resident.Kind == ResidentKind.UtilityRobot
                && string.Equals(
                    resident.StableId,
                    RobotResidentId,
                    StringComparison.Ordinal))
            {
                return;
            }

            throw new ArgumentException(
                "LAST_BEARING_RESIDENT_NOT_CANONICAL",
                nameof(resident));
        }

        private static ColonyComposition DeriveComposition(
            IReadOnlyList<ResidentRecord> residents)
        {
            if (residents.Count == 1)
            {
                return residents[0].Kind == ResidentKind.HumanCohort
                    ? ColonyComposition.HumanOnly
                    : ColonyComposition.RobotOnly;
            }

            if (residents.Count == 2
                && residents[0].Kind == ResidentKind.HumanCohort
                && residents[1].Kind == ResidentKind.UtilityRobot)
            {
                return ColonyComposition.Mixed;
            }

            throw new ArgumentException("LAST_BEARING_ROSTER_COMPOSITION_INVALID");
        }
    }
}
