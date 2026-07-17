using System.IO;
using AtomicLandPirate.Save.LastBearing;
using AtomicLandPirate.Simulation.LastBearing;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing
{
    /// <summary>
    /// The sole Unity-to-SaveContracts bridge. Its public surface deliberately
    /// exposes no path, root, profile, filesystem, or store override.
    /// </summary>
    public sealed class LastBearingSaveAdapter
    {
        private readonly LastBearingProfileStore _store;

        private LastBearingSaveAdapter(LastBearingProfileStore store)
        {
            _store = store;
        }

        public static LastBearingSaveAdapter Create()
        {
            string exactProfileDirectory = Path.Combine(
                Application.persistentDataPath,
                LastBearingProfileContract.ProfileName);

            return new LastBearingSaveAdapter(
                LastBearingProfileStore.OpenFixedProfileDirectory(
                    exactProfileDirectory));
        }

        public LastBearingPersistResult TryPersist(LastBearingState state)
        {
            byte[] canonicalPayload = LastBearingCanonicalCodec.Encode(state);
            return _store.TryPersist(canonicalPayload);
        }

        public LastBearingAdapterLoadResult TryLoad()
        {
            LastBearingLoadResult loaded = _store.TryLoad(
                payload => LastBearingCanonicalCodec.TryDecode(payload).Succeeded);

            if (!loaded.Succeeded || loaded.CanonicalPayload == null)
            {
                return new LastBearingAdapterLoadResult(loaded.Code, null);
            }

            LastBearingDecodeResult decoded =
                LastBearingCanonicalCodec.TryDecode(loaded.CanonicalPayload);
            if (!decoded.Succeeded || decoded.State == null)
            {
                return new LastBearingAdapterLoadResult(decoded.Code, null);
            }

            return new LastBearingAdapterLoadResult(loaded.Code, decoded.State);
        }
    }

    public sealed class LastBearingAdapterLoadResult
    {
        public LastBearingAdapterLoadResult(string code, LastBearingState? state)
        {
            Code = code;
            State = state;
        }

        public string Code { get; }

        public LastBearingState? State { get; }

        public bool Succeeded => State != null;
    }
}
