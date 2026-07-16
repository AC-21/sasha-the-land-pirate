#nullable enable

namespace AtomicLandPirate.Simulation
{
    /// <summary>
    /// Technical domain event emitted by one accepted command.
    /// </summary>
    public sealed class TechnicalDeltaApplied
    {
        internal TechnicalDeltaApplied(
            long sequence,
            long tick,
            long deltaMilli,
            long accumulatorMilli)
        {
            Sequence = sequence;
            Tick = tick;
            DeltaMilli = deltaMilli;
            AccumulatorMilli = accumulatorMilli;
        }

        public long Sequence { get; }

        public long Tick { get; }

        public long DeltaMilli { get; }

        public long AccumulatorMilli { get; }
    }
}
