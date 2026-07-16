#nullable enable

namespace AtomicLandPirate.Save
{
    public interface ISavePort
    {
        SaveCapability Capability { get; }

        SaveAttemptResult TryPersist();
    }
}
