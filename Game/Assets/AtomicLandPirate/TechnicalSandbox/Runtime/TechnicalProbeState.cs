#nullable enable

using System;

namespace AC21.Sasha.TechnicalSandbox
{
    /// <summary>
    /// Presentation-only state for the WP-0003 interaction probe. This is not
    /// authoritative game state and is never serialized.
    /// </summary>
    public sealed class TechnicalProbeState
    {
        public const int SupportedProbeCount = 15;
        public const string PersistenceDisabledCode =
            "WP0003_PERSISTENCE_DISABLED";

        private readonly int[] _activationCounts =
            new int[SupportedProbeCount];

        public int SelectedProbeIndex { get; private set; } = -1;

        public int InteractionCount { get; private set; }

        public string LastStatus { get; private set; } =
            "READY - select a technical probe";

        public int GetActivationCount(int probeIndex)
        {
            ValidateProbeIndex(probeIndex);
            return _activationCounts[probeIndex];
        }

        public void ActivateProbe(int probeIndex)
        {
            ValidateProbeIndex(probeIndex);

            checked
            {
                _activationCounts[probeIndex]++;
                InteractionCount++;
            }

            SelectedProbeIndex = probeIndex;
            LastStatus = "PROBE ACTIVE - presentation state only";
        }

        public PersistenceProbeResult AttemptPersistence()
        {
            LastStatus = PersistenceDisabledCode + " - 0 bytes written";
            return new PersistenceProbeResult(
                succeeded: false,
                code: PersistenceDisabledCode,
                bytesWritten: 0);
        }

        public void Reset()
        {
            Array.Clear(_activationCounts, 0, _activationCounts.Length);
            SelectedProbeIndex = -1;
            InteractionCount = 0;
            LastStatus = "RESET - presentation state cleared";
        }

        private static void ValidateProbeIndex(int probeIndex)
        {
            if (probeIndex < 0 || probeIndex >= SupportedProbeCount)
            {
                throw new ArgumentOutOfRangeException(nameof(probeIndex));
            }
        }
    }

    public readonly struct PersistenceProbeResult
    {
        public PersistenceProbeResult(
            bool succeeded,
            string code,
            int bytesWritten)
        {
            Succeeded = succeeded;
            Code = code ?? throw new ArgumentNullException(nameof(code));
            BytesWritten = bytesWritten;
        }

        public bool Succeeded { get; }

        public string Code { get; }

        public int BytesWritten { get; }
    }
}
