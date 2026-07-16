#nullable enable

using System;
using System.Collections.Generic;

namespace AtomicLandPirate.Simulation
{
    /// <summary>
    /// Applies exactly one state transition per supplied fixed-tick input.
    /// </summary>
    public sealed class SimulationKernel
    {
        public TechnicalTransition Step(
            TechnicalState state,
            TechnicalCommand command)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (command.Sequence != state.NextSequence)
            {
                throw new InvalidOperationException(
                    "TECHNICAL_SEQUENCE_MISMATCH");
            }

            checked
            {
                var nextState = new TechnicalState(
                    state.Tick + 1,
                    state.NextSequence + 1,
                    state.AccumulatorMilli + command.DeltaMilli);
                var domainEvent = new TechnicalDeltaApplied(
                    command.Sequence,
                    nextState.Tick,
                    command.DeltaMilli,
                    nextState.AccumulatorMilli);

                return new TechnicalTransition(
                    nextState,
                    domainEvent,
                    TechnicalReadModel.FromState(nextState));
            }
        }

        public TechnicalRunResult Run(
            TechnicalState initialState,
            IEnumerable<TechnicalCommand> commands)
        {
            if (initialState == null)
            {
                throw new ArgumentNullException(nameof(initialState));
            }

            if (commands == null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            var state = initialState;
            var domainEvents = new List<TechnicalDeltaApplied>();
            foreach (var command in commands)
            {
                var transition = Step(state, command);
                state = transition.State;
                domainEvents.Add(transition.DomainEvent);
            }

            return new TechnicalRunResult(
                state,
                domainEvents.ToArray(),
                TechnicalReadModel.FromState(state));
        }
    }
}
