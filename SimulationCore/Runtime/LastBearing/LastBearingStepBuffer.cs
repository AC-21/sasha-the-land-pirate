#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;

namespace AtomicLandPirate.Simulation.LastBearing
{
    internal abstract class LastBearingEventSink
    {
        internal abstract int Count { get; }

        internal abstract LastBearingDomainEvent this[int index] { get; }

        internal abstract void Emit(
            LastBearingEventKind kind,
            LastBearingEventCause cause,
            long globalTick,
            long domainTick,
            long commandSequence,
            string subjectId,
            long beforeValue,
            long afterValue);
    }

    internal sealed class LastBearingAllocatingEventSink : LastBearingEventSink
    {
        private readonly List<LastBearingDomainEvent> _events =
            new List<LastBearingDomainEvent>();

        internal override int Count => _events.Count;

        internal override LastBearingDomainEvent this[int index] =>
            _events[index];

        internal override void Emit(
            LastBearingEventKind kind,
            LastBearingEventCause cause,
            long globalTick,
            long domainTick,
            long commandSequence,
            string subjectId,
            long beforeValue,
            long afterValue)
        {
            _events.Add(
                new LastBearingDomainEvent(
                    kind,
                    cause,
                    globalTick,
                    domainTick,
                    commandSequence,
                    subjectId,
                    beforeValue,
                    afterValue));
        }

        internal LastBearingDomainEvent[] ToArray()
        {
            return _events.ToArray();
        }
    }

    internal sealed class LastBearingReusableEventSink :
        LastBearingEventSink,
        IReadOnlyList<LastBearingDomainEvent>
    {
        private const int InitialCapacity = 8;

        private LastBearingDomainEvent[] _events;
        private int _count;

        internal LastBearingReusableEventSink()
        {
            _events = new LastBearingDomainEvent[InitialCapacity];
            for (var index = 0; index < _events.Length; index++)
            {
                _events[index] = new LastBearingDomainEvent();
            }
        }

        internal override int Count => _count;

        int IReadOnlyCollection<LastBearingDomainEvent>.Count => _count;

        internal override LastBearingDomainEvent this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return _events[index];
            }
        }

        LastBearingDomainEvent IReadOnlyList<LastBearingDomainEvent>.this[
            int index] => this[index];

        internal void Clear()
        {
            _count = 0;
        }

        internal override void Emit(
            LastBearingEventKind kind,
            LastBearingEventCause cause,
            long globalTick,
            long domainTick,
            long commandSequence,
            string subjectId,
            long beforeValue,
            long afterValue)
        {
            EnsureCapacity();
            _events[_count].Reset(
                kind,
                cause,
                globalTick,
                domainTick,
                commandSequence,
                subjectId,
                beforeValue,
                afterValue);
            _count++;
        }

        public IEnumerator<LastBearingDomainEvent> GetEnumerator()
        {
            for (var index = 0; index < _count; index++)
            {
                yield return _events[index];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private void EnsureCapacity()
        {
            if (_count < _events.Length)
            {
                return;
            }

            int oldLength = _events.Length;
            Array.Resize(ref _events, checked(oldLength * 2));
            for (var index = oldLength; index < _events.Length; index++)
            {
                _events[index] = new LastBearingDomainEvent();
            }
        }
    }

    /// <summary>
    /// Reusable storage for the engine-facing StepInto path. Its state, read
    /// model, and events are transient and must never cross a public retention
    /// boundary without first being copied to the immutable public model.
    /// </summary>
    internal sealed class LastBearingStepBuffer
    {
        private readonly LastBearingStateBuilder _builder =
            new LastBearingStateBuilder();
        private readonly LastBearingReusableEventSink[] _eventSlots =
        {
            new LastBearingReusableEventSink(),
            new LastBearingReusableEventSink()
        };

        private readonly LastBearingState?[] _stateSlots =
            new LastBearingState?[2];
        private readonly LastBearingReadModel?[] _readModelSlots =
            new LastBearingReadModel?[2];
        private int _committedSlot = -1;
        private int _workingSlot;

        internal LastBearingState? State { get; private set; }

        internal LastBearingReadModel? ReadModel { get; private set; }

        internal IReadOnlyList<LastBearingDomainEvent> DomainEvents =>
            _committedSlot < 0
                ? Array.Empty<LastBearingDomainEvent>()
                : _eventSlots[_committedSlot];

        internal LastBearingStateBuilder Begin(LastBearingState source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            _builder.CopyFrom(source);
            EnsureSlots();
            _workingSlot = _committedSlot == 0 ? 1 : 0;
            _eventSlots[_workingSlot].Clear();
            return _builder;
        }

        internal LastBearingEventSink WorkingEvents =>
            _eventSlots[_workingSlot];

        internal void Commit()
        {
            LastBearingState state = _stateSlots[_workingSlot]
                ?? throw new InvalidOperationException(
                    "LAST_BEARING_STEP_BUFFER_STATE_UNAVAILABLE");
            LastBearingReadModel readModel = _readModelSlots[_workingSlot]
                ?? throw new InvalidOperationException(
                    "LAST_BEARING_STEP_BUFFER_READ_MODEL_UNAVAILABLE");

            _builder.BuildInto(state);
            readModel.RefreshFrom(state);

            State = state;
            ReadModel = readModel;
            _committedSlot = _workingSlot;
        }

        private void EnsureSlots()
        {
            if (_stateSlots[0] != null)
            {
                return;
            }

            _stateSlots[0] = new LastBearingState(_builder);
            _stateSlots[1] = new LastBearingState(_builder);
            _readModelSlots[0] = LastBearingReadModel.CreateReusable(
                _stateSlots[0]!);
            _readModelSlots[1] = LastBearingReadModel.CreateReusable(
                _stateSlots[1]!);
        }
    }
}
