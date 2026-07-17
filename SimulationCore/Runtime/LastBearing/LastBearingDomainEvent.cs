#nullable enable

using System;

namespace AtomicLandPirate.Simulation.LastBearing
{
    public sealed class LastBearingDomainEvent
    {
        public const long AutonomousCommandSequence = -1;

        internal LastBearingDomainEvent(
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

        public LastBearingEventKind Kind { get; }

        public LastBearingEventCause Cause { get; }

        public long GlobalTick { get; }

        public long DomainTick { get; }

        public long CommandSequence { get; }

        public string SubjectId { get; }

        public long BeforeValue { get; }

        public long AfterValue { get; }
    }
}
