#nullable enable

namespace AtomicLandPirate.Simulation
{
    /// <summary>
    /// A gameplay-neutral, explicitly sequenced command for deterministic
    /// transition proof.
    /// </summary>
    public readonly struct TechnicalCommand
    {
        public TechnicalCommand(long sequence, long deltaMilli)
        {
            Sequence = sequence;
            DeltaMilli = deltaMilli;
        }

        public long Sequence { get; }

        public long DeltaMilli { get; }
    }
}
