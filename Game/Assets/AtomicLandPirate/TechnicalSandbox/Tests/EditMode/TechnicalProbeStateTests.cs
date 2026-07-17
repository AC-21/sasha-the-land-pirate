#nullable enable

using NUnit.Framework;

namespace AC21.Sasha.TechnicalSandbox.Tests
{
    public sealed class TechnicalProbeStateTests
    {
        [Test]
        public void NewStateIsNeutralAndNonPersisting()
        {
            var state = new TechnicalProbeState();

            Assert.That(state.SelectedProbeIndex, Is.EqualTo(-1));
            Assert.That(state.InteractionCount, Is.Zero);
            Assert.That(state.GetActivationCount(0), Is.Zero);
        }

        [Test]
        public void ActivationIsBoundedAndInspectable()
        {
            var state = new TechnicalProbeState();

            state.ActivateProbe(4);
            state.ActivateProbe(4);

            Assert.That(state.SelectedProbeIndex, Is.EqualTo(4));
            Assert.That(state.InteractionCount, Is.EqualTo(2));
            Assert.That(state.GetActivationCount(4), Is.EqualTo(2));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => state.ActivateProbe(
                    TechnicalProbeState.SupportedProbeCount));
        }

        [Test]
        public void ResetClearsPresentationState()
        {
            var state = new TechnicalProbeState();
            state.ActivateProbe(2);

            state.Reset();

            Assert.That(state.SelectedProbeIndex, Is.EqualTo(-1));
            Assert.That(state.InteractionCount, Is.Zero);
            Assert.That(state.GetActivationCount(2), Is.Zero);
        }

        [Test]
        public void PersistenceProbeFailsClosedAndWritesZeroBytes()
        {
            var state = new TechnicalProbeState();

            var result = state.AttemptPersistence();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.Code,
                Is.EqualTo(TechnicalProbeState.PersistenceDisabledCode));
            Assert.That(result.BytesWritten, Is.Zero);
        }
    }
}
