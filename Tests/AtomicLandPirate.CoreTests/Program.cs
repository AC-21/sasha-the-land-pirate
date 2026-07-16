#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using AtomicLandPirate.Save;
using AtomicLandPirate.Simulation;

namespace AtomicLandPirate.CoreTests
{
    internal static class Program
    {
        private const string GoldenHash =
            "e53cb4f293f17fcfaa2a2717cc1c6730f54b2c41b6bbc105f488cfb92d3db65f";

        private static readonly TechnicalCommand[] GoldenCommands =
        {
            new TechnicalCommand(0, 25),
            new TechnicalCommand(1, -5),
            new TechnicalCommand(2, 80),
            new TechnicalCommand(3, -100),
            new TechnicalCommand(4, 1),
        };

        public static int Main()
        {
            var tests = new (string Name, Action Run)[]
            {
                ("identical sequences produce identical hashes", IdenticalSequencesMatch),
                ("golden canonical hash is stable", GoldenHashMatches),
                ("different state produces a different hash", DifferentStateDiffers),
                ("one input advances one fixed tick", TickAdvancesOnce),
                ("command sequence mismatch fails closed", SequenceMismatchFailsClosed),
                ("stale command sequence fails closed", StaleSequenceFailsClosed),
                ("transition emits event and read model", TransitionEmitsOutputs),
                ("run outputs remain ordered and coherent", RunOutputsStayCoherent),
                ("checked arithmetic fails closed", OverflowFailsClosed),
                ("canonical formatting ignores current culture", FormattingIsInvariant),
                ("persistence is explicitly disabled", PersistenceIsDisabled),
            };

            var failures = new List<string>();
            foreach (var test in tests)
            {
                try
                {
                    test.Run();
                    Console.WriteLine("PASS " + test.Name);
                }
                catch (Exception exception)
                {
                    failures.Add(test.Name + ": " + exception.Message);
                    Console.Error.WriteLine(
                        "FAIL " + test.Name + ": " + exception);
                }
            }

            Console.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "RESULT passed={0} failed={1}",
                    tests.Length - failures.Count,
                    failures.Count));

            return failures.Count == 0 ? 0 : 1;
        }

        private static TechnicalRunResult RunGoldenScenario()
        {
            return new SimulationKernel().Run(
                TechnicalState.Initial,
                GoldenCommands);
        }

        private static void IdenticalSequencesMatch()
        {
            var first = CanonicalState.ComputeSha256(
                RunGoldenScenario().State);
            var second = CanonicalState.ComputeSha256(
                RunGoldenScenario().State);
            AssertEqual(first, second, "identical sequence hash");
        }

        private static void GoldenHashMatches()
        {
            var state = RunGoldenScenario().State;
            AssertEqual(5L, state.Tick, "golden tick");
            AssertEqual(5L, state.NextSequence, "golden next sequence");
            AssertEqual(1L, state.AccumulatorMilli, "golden accumulator");
            AssertEqual(
                "tick=5;next_sequence=5;accumulator_milli=1",
                CanonicalState.Format(state),
                "canonical state");
            AssertEqual(
                GoldenHash,
                CanonicalState.ComputeSha256(state),
                "golden hash");
        }

        private static void DifferentStateDiffers()
        {
            var kernel = new SimulationKernel();
            var first = kernel.Run(
                TechnicalState.Initial,
                new[]
                {
                    new TechnicalCommand(0, 1),
                    new TechnicalCommand(1, 0),
                });
            var second = kernel.Run(
                TechnicalState.Initial,
                new[]
                {
                    new TechnicalCommand(0, 0),
                    new TechnicalCommand(1, 2),
                });

            AssertNotEqual(
                CanonicalState.ComputeSha256(first.State),
                CanonicalState.ComputeSha256(second.State),
                "different state hash");
        }

        private static void TickAdvancesOnce()
        {
            var transition = new SimulationKernel().Step(
                TechnicalState.Initial,
                new TechnicalCommand(0, 0));

            AssertEqual(1L, transition.State.Tick, "tick");
            AssertEqual(1L, transition.State.NextSequence, "next sequence");
        }

        private static void SequenceMismatchFailsClosed()
        {
            AssertThrows<InvalidOperationException>(
                () => new SimulationKernel().Step(
                    TechnicalState.Initial,
                    new TechnicalCommand(1, 0)));
        }

        private static void StaleSequenceFailsClosed()
        {
            var kernel = new SimulationKernel();
            var accepted = kernel.Step(
                TechnicalState.Initial,
                new TechnicalCommand(0, 3));

            AssertThrows<InvalidOperationException>(
                () => kernel.Step(
                    accepted.State,
                    new TechnicalCommand(0, 9)));
        }

        private static void TransitionEmitsOutputs()
        {
            var transition = new SimulationKernel().Step(
                TechnicalState.Initial,
                new TechnicalCommand(0, 17));

            AssertEqual(0L, transition.DomainEvent.Sequence, "event sequence");
            AssertEqual(1L, transition.DomainEvent.Tick, "event tick");
            AssertEqual(17L, transition.DomainEvent.DeltaMilli, "event delta");
            AssertEqual(
                17L,
                transition.DomainEvent.AccumulatorMilli,
                "event accumulator");
            AssertEqual(1L, transition.ReadModel.Tick, "read-model tick");
            AssertEqual(
                1L,
                transition.ReadModel.AppliedCommandCount,
                "read-model command count");
            AssertEqual(
                17L,
                transition.ReadModel.AccumulatorMilli,
                "read-model accumulator");
        }

        private static void RunOutputsStayCoherent()
        {
            var result = RunGoldenScenario();
            AssertEqual(
                GoldenCommands.Length,
                result.DomainEvents.Count,
                "run event count");
            AssertEqual(
                false,
                result.DomainEvents is TechnicalDeltaApplied[],
                "run event collection exposes raw array");
            for (var index = 0; index < GoldenCommands.Length; index++)
            {
                var domainEvent = result.DomainEvents[index];
                AssertEqual(
                    GoldenCommands[index].Sequence,
                    domainEvent.Sequence,
                    "run event sequence");
                AssertEqual(
                    index + 1L,
                    domainEvent.Tick,
                    "run event tick");
                AssertEqual(
                    GoldenCommands[index].DeltaMilli,
                    domainEvent.DeltaMilli,
                    "run event delta");
            }

            AssertEqual(
                result.State.Tick,
                result.ReadModel.Tick,
                "run read-model tick");
            AssertEqual(
                result.State.NextSequence,
                result.ReadModel.AppliedCommandCount,
                "run read-model command count");
            AssertEqual(
                result.State.AccumulatorMilli,
                result.ReadModel.AccumulatorMilli,
                "run read-model accumulator");
        }

        private static void OverflowFailsClosed()
        {
            var state = new TechnicalState(long.MaxValue, 0, 0);
            AssertThrows<OverflowException>(
                () => new SimulationKernel().Step(
                    state,
                    new TechnicalCommand(0, 0)));
        }

        private static void FormattingIsInvariant()
        {
            var originalCulture = CultureInfo.CurrentCulture;
            var originalUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
                CultureInfo.CurrentUICulture = new CultureInfo("fr-FR");
                AssertEqual(
                    "tick=5;next_sequence=5;accumulator_milli=1",
                    CanonicalState.Format(RunGoldenScenario().State),
                    "culture-invariant format");
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUiCulture;
            }
        }

        private static void PersistenceIsDisabled()
        {
            ISavePort port = new NonPersistingSavePort();
            var result = port.TryPersist();

            AssertEqual(
                SaveCapability.Disabled,
                port.Capability,
                "save capability");
            AssertEqual(false, result.Succeeded, "save success");
            AssertEqual(
                SaveAttemptResult.PersistenceDisabledCode,
                result.Code,
                "save rejection code");
            AssertEqual(0L, result.BytesWritten, "save bytes");
        }

        private static void AssertEqual<T>(
            T expected,
            T actual,
            string label)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    label + " expected " + expected + " but received " + actual);
            }
        }

        private static void AssertNotEqual<T>(
            T first,
            T second,
            string label)
        {
            if (EqualityComparer<T>.Default.Equals(first, second))
            {
                throw new InvalidOperationException(
                    label + " unexpectedly matched " + first);
            }
        }

        private static void AssertThrows<TException>(Action action)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Expected " + typeof(TException).Name);
        }

    }
}
