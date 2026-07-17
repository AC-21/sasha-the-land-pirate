#nullable enable

using System;
using System.Collections.Generic;

namespace AtomicLandPirate.Simulation.LastBearing
{
    public sealed class LastBearingTickResult
    {
        internal LastBearingTickResult(
            LastBearingState state,
            LastBearingDomainEvent[] domainEvents,
            LastBearingReadModel readModel)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            if (domainEvents == null)
            {
                throw new ArgumentNullException(nameof(domainEvents));
            }

            DomainEvents = Array.AsReadOnly(
                (LastBearingDomainEvent[])domainEvents.Clone());
            ReadModel = readModel
                ?? throw new ArgumentNullException(nameof(readModel));
        }

        public LastBearingState State { get; }

        public IReadOnlyList<LastBearingDomainEvent> DomainEvents { get; }

        public LastBearingReadModel ReadModel { get; }
    }
}
