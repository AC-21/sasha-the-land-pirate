#nullable enable

using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AtomicLandPirate.Simulation
{
    /// <summary>
    /// Canonical technical-state formatting and hashing for determinism proof.
    /// These bytes are test-oracle bytes, not a persisted save contract.
    /// </summary>
    public static class CanonicalState
    {
        internal static string Format(TechnicalState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            return string.Concat(
                "tick=",
                state.Tick.ToString(CultureInfo.InvariantCulture),
                ";next_sequence=",
                state.NextSequence.ToString(CultureInfo.InvariantCulture),
                ";accumulator_milli=",
                state.AccumulatorMilli.ToString(CultureInfo.InvariantCulture));
        }

        public static string ComputeSha256(TechnicalState state)
        {
            var canonicalBytes = ToCanonicalBytes(state);
            using (var sha256 = SHA256.Create())
            {
                var digest = sha256.ComputeHash(canonicalBytes);
                var builder = new StringBuilder(digest.Length * 2);
                foreach (var value in digest)
                {
                    builder.Append(
                        value.ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private static byte[] ToCanonicalBytes(TechnicalState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var bytes = new byte[24];
            WriteInt64LittleEndian(bytes, 0, state.Tick);
            WriteInt64LittleEndian(bytes, 8, state.NextSequence);
            WriteInt64LittleEndian(bytes, 16, state.AccumulatorMilli);
            return bytes;
        }

        private static void WriteInt64LittleEndian(
            byte[] destination,
            int offset,
            long value)
        {
            unchecked
            {
                var unsigned = (ulong)value;
                for (var index = 0; index < sizeof(long); index++)
                {
                    destination[offset + index] =
                        (byte)(unsigned >> (index * 8));
                }
            }
        }
    }
}
