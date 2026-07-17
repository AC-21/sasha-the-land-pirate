#nullable enable

namespace AtomicLandPirate.Save.LastBearing
{
    public static class LastBearingProfileContract
    {
        public const string ProfileName = "last-bearing-dev-v1";
        public const string CurrentPointerName = "current.ptr";
        public const string LastGoodPointerName = "last-good.ptr";
        public const string WriterLockName = ".writer.lock";
        public const ushort GenerationEnvelopeVersion = 1;
        public const ushort PointerVersion = 1;
        public const int MaxCanonicalPayloadBytes = 1_048_576;

        internal const int GenerationHeaderBytes = 75;
        internal const int PointerBytes = 166;
        internal const int GenerationFileNameBytes = 93;
    }
}
