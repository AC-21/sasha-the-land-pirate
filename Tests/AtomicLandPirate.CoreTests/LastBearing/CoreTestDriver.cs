#nullable enable

using System;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal sealed class CoreTestDriver
    {
        private readonly LastBearingKernel _kernel = new LastBearingKernel();

        public CoreTestDriver(ColonyComposition composition, int worldSeed = 2011)
        {
            State = LastBearingScenarioFactory.CreateInitial(composition, worldSeed);
            View = LastBearingReadModel.FromState(State);
        }

        public CoreTestDriver(LastBearingState state)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            View = LastBearingReadModel.FromState(State);
        }

        public LastBearingState State { get; private set; }

        public LastBearingReadModel View { get; private set; }

        public LastBearingTickResult Apply(Func<long, LastBearingCommand> create)
        {
            LastBearingCommand command = create(State.NextCommandSequence);
            LastBearingTickResult result = _kernel.Step(
                State,
                new[] { command });
            State = result.State;
            View = result.ReadModel;
            return result;
        }

        public void Advance(int ticks)
        {
            if (ticks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ticks));
            }

            for (int index = 0; index < ticks; index++)
            {
                LastBearingTickResult result = _kernel.Step(
                    State,
                    Array.Empty<LastBearingCommand>());
                State = result.State;
                View = result.ReadModel;
            }
        }

        public void StartPreparation(
            string assignedResident,
            PreparationChoice choice,
            VehicleModule module)
        {
            Apply(sequence => new AssignResidentCommand(sequence, assignedResident));
            Apply(sequence => new ActivateSliceInfrastructureCommand(sequence));
            Apply(sequence => new SelectPreparationCommand(sequence, choice, module));
            Apply(sequence => new InstallVehicleModuleCommand(sequence, module));
        }

        public void OperateWreckLineIfAvailable()
        {
            if (!View.IsWreckLineModulePointAvailable)
            {
                return;
            }

            RouteActionKind action = View.RouteActionKind;
            Apply(sequence => new OperateWreckLineModuleCommand(
                sequence,
                action));
        }
    }
}
