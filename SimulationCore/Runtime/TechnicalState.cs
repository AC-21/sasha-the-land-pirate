#nullable enable

namespace AtomicLandPirate.Simulation
{
    /// <summary>
    /// Minimal immutable state for the WP-0003 deterministic-core smoke.
    /// It is not a game-state or save-schema commitment.
    /// </summary>
    public sealed class TechnicalState
    {
        internal TechnicalState(
            long tick,
            long nextSequence,
            long accumulatorMilli)
        {
            if (tick < 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(tick));
            }

            if (nextSequence < 0)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(nextSequence));
            }

            Tick = tick;
            NextSequence = nextSequence;
            AccumulatorMilli = accumulatorMilli;
        }

        public static TechnicalState Initial { get; } =
            new TechnicalState(0, 0, 0);

        public long Tick { get; }

        public long NextSequence { get; }

        public long AccumulatorMilli { get; }
    }
}
