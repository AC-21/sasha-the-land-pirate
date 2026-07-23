#nullable enable

using System;

namespace AtomicLandPirate.Simulation.LastBearing
{
    public sealed class LastBearingDomainEvent
    {
        public const long AutonomousCommandSequence = -1;

        internal LastBearingDomainEvent()
        {
            SubjectId = string.Empty;
        }

        internal LastBearingDomainEvent(
            LastBearingEventKind kind,
            LastBearingEventCause cause,
            long globalTick,
            long domainTick,
            long commandSequence,
            string subjectId,
            long beforeValue,
            long afterValue)
            : this()
        {
            Reset(
                kind,
                cause,
                globalTick,
                domainTick,
                commandSequence,
                subjectId,
                beforeValue,
                afterValue);
        }

        internal void Reset(
            LastBearingEventKind kind,
            LastBearingEventCause cause,
            long globalTick,
            long domainTick,
            long commandSequence,
            string subjectId,
            long beforeValue,
            long afterValue)
        {
            if (globalTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(globalTick));
            }

            if (domainTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(domainTick));
            }

            if (commandSequence < AutonomousCommandSequence)
            {
                throw new ArgumentOutOfRangeException(nameof(commandSequence));
            }

            if (string.IsNullOrWhiteSpace(subjectId))
            {
                throw new ArgumentException(
                    "LAST_BEARING_EVENT_SUBJECT_REQUIRED",
                    nameof(subjectId));
            }

            Kind = kind;
            Cause = cause;
            GlobalTick = globalTick;
            DomainTick = domainTick;
            CommandSequence = commandSequence;
            SubjectId = subjectId;
            BeforeValue = beforeValue;
            AfterValue = afterValue;
        }

        public LastBearingEventKind Kind { get; private set; }

        public LastBearingEventCause Cause { get; private set; }

        public long GlobalTick { get; private set; }

        public long DomainTick { get; private set; }

        public long CommandSequence { get; private set; }

        public string SubjectId { get; private set; }

        public long BeforeValue { get; private set; }

        public long AfterValue { get; private set; }
    }
}
