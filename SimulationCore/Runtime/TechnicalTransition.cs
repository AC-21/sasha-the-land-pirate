#nullable enable

namespace AtomicLandPirate.Simulation
{
    public sealed class TechnicalTransition
    {
        internal TechnicalTransition(
            TechnicalState state,
            TechnicalDeltaApplied domainEvent,
            TechnicalReadModel readModel)
        {
            State = state
                ?? throw new System.ArgumentNullException(nameof(state));
            DomainEvent = domainEvent
                ?? throw new System.ArgumentNullException(nameof(domainEvent));
            ReadModel = readModel
                ?? throw new System.ArgumentNullException(nameof(readModel));
        }

        public TechnicalState State { get; }

        public TechnicalDeltaApplied DomainEvent { get; }

        public TechnicalReadModel ReadModel { get; }
    }
}
