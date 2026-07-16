#nullable enable

using System;
using System.Collections.Generic;

namespace AtomicLandPirate.Simulation
{
    public sealed class TechnicalRunResult
    {
        internal TechnicalRunResult(
            TechnicalState state,
            TechnicalDeltaApplied[] domainEvents,
            TechnicalReadModel readModel)
        {
            State = state
                ?? throw new System.ArgumentNullException(nameof(state));
            if (domainEvents == null)
            {
                throw new ArgumentNullException(nameof(domainEvents));
            }

            DomainEvents = Array.AsReadOnly(
                (TechnicalDeltaApplied[])domainEvents.Clone());
            ReadModel = readModel
                ?? throw new System.ArgumentNullException(nameof(readModel));
        }

        public TechnicalState State { get; }

        public IReadOnlyList<TechnicalDeltaApplied> DomainEvents { get; }

        public TechnicalReadModel ReadModel { get; }
    }
}
