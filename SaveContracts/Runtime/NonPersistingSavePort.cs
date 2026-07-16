#nullable enable

namespace AtomicLandPirate.Save
{
    /// <summary>
    /// Explicitly rejects persistence while WP-0003 remains S0.
    /// </summary>
    public sealed class NonPersistingSavePort : ISavePort
    {
        public SaveCapability Capability => SaveCapability.Disabled;

        public SaveAttemptResult TryPersist()
        {
            return SaveAttemptResult.PersistenceDisabled;
        }
    }
}
