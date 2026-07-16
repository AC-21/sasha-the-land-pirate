#nullable enable

namespace AtomicLandPirate.Simulation
{
    /// <summary>
    /// Read-only projection for presentation adapters.
    /// </summary>
    public sealed class TechnicalReadModel
    {
        private TechnicalReadModel(
            long tick,
            long appliedCommandCount,
            long accumulatorMilli)
        {
            Tick = tick;
            AppliedCommandCount = appliedCommandCount;
            AccumulatorMilli = accumulatorMilli;
        }

        public long Tick { get; }

        public long AppliedCommandCount { get; }

        public long AccumulatorMilli { get; }

        internal static TechnicalReadModel FromState(TechnicalState state)
        {
            if (state == null)
            {
                throw new System.ArgumentNullException(nameof(state));
            }

            return new TechnicalReadModel(
                state.Tick,
                state.NextSequence,
                state.AccumulatorMilli);
        }
    }
}
