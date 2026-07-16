#nullable enable

namespace AtomicLandPirate.Save
{
    public sealed class SaveAttemptResult
    {
        public const string PersistenceDisabledCode =
            "WP0003_PERSISTENCE_DISABLED";

        private SaveAttemptResult(
            bool succeeded,
            string code,
            long bytesWritten)
        {
            Succeeded = succeeded;
            Code = code;
            BytesWritten = bytesWritten;
        }

        public static SaveAttemptResult PersistenceDisabled { get; } =
            new SaveAttemptResult(
                false,
                PersistenceDisabledCode,
                0);

        public bool Succeeded { get; }

        public string Code { get; }

        public long BytesWritten { get; }
    }
}
