#nullable enable

using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AtomicLandPirate.Save.LastBearing
{
    internal enum LastBearingDecodeStatus
    {
        Success = 0,
        Corrupt = 1,
        UnknownVersion = 2,
    }

    internal sealed class LastBearingGenerationRecord
    {
        private readonly byte[] _payload;
        private readonly byte[] _encodedBytes;
        private readonly byte[] _wholeDigest;

        internal LastBearingGenerationRecord(
            ulong generation,
            LastBearingEntryName fileName,
            byte[] payload,
            byte[] encodedBytes,
            byte[] wholeDigest)
        {
            Generation = generation;
            FileName = fileName;
            _payload = (byte[])payload.Clone();
            _encodedBytes = (byte[])encodedBytes.Clone();
            _wholeDigest = (byte[])wholeDigest.Clone();
        }

        internal ulong Generation { get; }

        internal LastBearingEntryName FileName { get; }

        internal byte[] Payload => (byte[])_payload.Clone();

        internal byte[] EncodedBytes => (byte[])_encodedBytes.Clone();

        internal byte[] WholeDigest => (byte[])_wholeDigest.Clone();
    }

    internal sealed class LastBearingPointerRecord
    {
        private readonly byte[] _wholeGenerationDigest;
        private readonly byte[] _encodedBytes;

        internal LastBearingPointerRecord(
            ulong generation,
            LastBearingEntryName generationFileName,
            byte[] wholeGenerationDigest,
            byte[] encodedBytes)
        {
            Generation = generation;
            GenerationFileName = generationFileName;
            _wholeGenerationDigest = (byte[])wholeGenerationDigest.Clone();
            _encodedBytes = (byte[])encodedBytes.Clone();
        }

        internal ulong Generation { get; }

        internal LastBearingEntryName GenerationFileName { get; }

        internal byte[] WholeGenerationDigest =>
            (byte[])_wholeGenerationDigest.Clone();

        internal byte[] EncodedBytes => (byte[])_encodedBytes.Clone();
    }

    internal static class LastBearingGenerationCodec
    {
        private static readonly byte[] GenerationMagic =
            Encoding.ASCII.GetBytes("ALPLBG01");

        private static readonly byte[] PointerMagic =
            Encoding.ASCII.GetBytes("ALPLBP01");

        private static readonly byte[] ProfileNameBytes =
            Encoding.ASCII.GetBytes(LastBearingProfileContract.ProfileName);

        internal static LastBearingGenerationRecord EncodeGeneration(
            ulong generation,
            byte[] canonicalPayload)
        {
            if (generation == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(generation));
            }

            if (canonicalPayload is null)
            {
                throw new ArgumentNullException(nameof(canonicalPayload));
            }

            if (canonicalPayload.Length == 0 ||
                canonicalPayload.Length >
                LastBearingProfileContract.MaxCanonicalPayloadBytes)
            {
                throw new ArgumentException(
                    "Canonical payload length is outside the Last Bearing bound.",
                    nameof(canonicalPayload));
            }

            byte[] payload = (byte[])canonicalPayload.Clone();
            byte[] payloadDigest = ComputeSha256(payload);
            byte[] encoded = new byte[
                LastBearingProfileContract.GenerationHeaderBytes + payload.Length];

            Copy(GenerationMagic, encoded, 0);
            WriteUInt16(
                encoded,
                8,
                LastBearingProfileContract.GenerationEnvelopeVersion);
            WriteUInt16(encoded, 10, checked((ushort)ProfileNameBytes.Length));
            WriteUInt64(encoded, 12, generation);
            WriteUInt32(encoded, 20, checked((uint)payload.Length));
            Copy(payloadDigest, encoded, 24);
            Copy(ProfileNameBytes, encoded, 56);
            Copy(payload, encoded, LastBearingProfileContract.GenerationHeaderBytes);

            byte[] wholeDigest = ComputeSha256(encoded);
            LastBearingEntryName fileName =
                LastBearingEntryName.ForGeneration(
                    generation,
                    ToLowerHex(wholeDigest));

            return new LastBearingGenerationRecord(
                generation,
                fileName,
                payload,
                encoded,
                wholeDigest);
        }

        internal static LastBearingDecodeStatus TryDecodeGeneration(
            LastBearingEntryName expectedFileName,
            byte[] encodedBytes,
            out LastBearingGenerationRecord? record)
        {
            record = null;
            if (encodedBytes is null ||
                encodedBytes.Length <= LastBearingProfileContract.GenerationHeaderBytes)
            {
                return LastBearingDecodeStatus.Corrupt;
            }

            if (!Matches(encodedBytes, 0, GenerationMagic))
            {
                return LastBearingDecodeStatus.Corrupt;
            }

            ushort version = ReadUInt16(encodedBytes, 8);
            if (version != LastBearingProfileContract.GenerationEnvelopeVersion)
            {
                return LastBearingDecodeStatus.UnknownVersion;
            }

            if (ReadUInt16(encodedBytes, 10) != ProfileNameBytes.Length)
            {
                return LastBearingDecodeStatus.Corrupt;
            }

            ulong generation = ReadUInt64(encodedBytes, 12);
            uint payloadLength = ReadUInt32(encodedBytes, 20);
            if (generation == 0 ||
                payloadLength == 0 ||
                payloadLength > LastBearingProfileContract.MaxCanonicalPayloadBytes ||
                encodedBytes.Length !=
                LastBearingProfileContract.GenerationHeaderBytes +
                (long)payloadLength ||
                !Matches(encodedBytes, 56, ProfileNameBytes))
            {
                return LastBearingDecodeStatus.Corrupt;
            }

            byte[] payload = new byte[checked((int)payloadLength)];
            Buffer.BlockCopy(
                encodedBytes,
                LastBearingProfileContract.GenerationHeaderBytes,
                payload,
                0,
                payload.Length);

            byte[] expectedPayloadDigest = Slice(encodedBytes, 24, 32);
            if (!ByteArraysEqual(expectedPayloadDigest, ComputeSha256(payload)))
            {
                return LastBearingDecodeStatus.Corrupt;
            }

            byte[] wholeDigest = ComputeSha256(encodedBytes);
            LastBearingEntryName canonicalName =
                LastBearingEntryName.ForGeneration(
                    generation,
                    ToLowerHex(wholeDigest));
            if (!canonicalName.Equals(expectedFileName))
            {
                return LastBearingDecodeStatus.Corrupt;
            }

            LastBearingGenerationRecord decoded =
                new LastBearingGenerationRecord(
                    generation,
                    canonicalName,
                    payload,
                    encodedBytes,
                    wholeDigest);
            LastBearingGenerationRecord canonical =
                EncodeGeneration(generation, payload);
            if (!ByteArraysEqual(canonical.EncodedBytes, encodedBytes))
            {
                return LastBearingDecodeStatus.Corrupt;
            }

            record = decoded;
            return LastBearingDecodeStatus.Success;
        }

        internal static LastBearingPointerRecord EncodePointer(
            LastBearingGenerationRecord generation)
        {
            if (generation is null)
            {
                throw new ArgumentNullException(nameof(generation));
            }

            byte[] fileNameBytes =
                Encoding.ASCII.GetBytes(generation.FileName.Value);
            if (fileNameBytes.Length !=
                LastBearingProfileContract.GenerationFileNameBytes)
            {
                throw new InvalidOperationException(
                    "Generation filename is not canonical.");
            }

            byte[] digest = generation.WholeDigest;
            byte[] encoded = new byte[LastBearingProfileContract.PointerBytes];
            Copy(PointerMagic, encoded, 0);
            WriteUInt16(encoded, 8, LastBearingProfileContract.PointerVersion);
            WriteUInt16(encoded, 10, checked((ushort)ProfileNameBytes.Length));
            WriteUInt64(encoded, 12, generation.Generation);
            WriteUInt16(encoded, 20, checked((ushort)fileNameBytes.Length));
            Copy(digest, encoded, 22);
            Copy(ProfileNameBytes, encoded, 54);
            Copy(fileNameBytes, encoded, 73);

            return new LastBearingPointerRecord(
                generation.Generation,
                generation.FileName,
                digest,
                encoded);
        }

        internal static LastBearingDecodeStatus TryDecodePointer(
            byte[] encodedBytes,
            out LastBearingPointerRecord? record)
        {
            record = null;
            if (encodedBytes is null ||
                encodedBytes.Length != LastBearingProfileContract.PointerBytes ||
                !Matches(encodedBytes, 0, PointerMagic))
            {
                return LastBearingDecodeStatus.Corrupt;
            }

            ushort version = ReadUInt16(encodedBytes, 8);
            if (version != LastBearingProfileContract.PointerVersion)
            {
                return LastBearingDecodeStatus.UnknownVersion;
            }

            ulong generation = ReadUInt64(encodedBytes, 12);
            if (ReadUInt16(encodedBytes, 10) != ProfileNameBytes.Length ||
                generation == 0 ||
                ReadUInt16(encodedBytes, 20) !=
                LastBearingProfileContract.GenerationFileNameBytes ||
                !Matches(encodedBytes, 54, ProfileNameBytes) ||
                !IsAscii(encodedBytes, 73,
                    LastBearingProfileContract.GenerationFileNameBytes))
            {
                return LastBearingDecodeStatus.Corrupt;
            }

            string fileName = Encoding.ASCII.GetString(
                encodedBytes,
                73,
                LastBearingProfileContract.GenerationFileNameBytes);
            if (!LastBearingEntryName.TryParseGeneration(
                    fileName,
                    out LastBearingEntryName entryName,
                    out ulong nameGeneration,
                    out string nameDigest) ||
                nameGeneration != generation)
            {
                return LastBearingDecodeStatus.Corrupt;
            }

            byte[] digest = Slice(encodedBytes, 22, 32);
            if (!string.Equals(
                    nameDigest,
                    ToLowerHex(digest),
                    StringComparison.Ordinal))
            {
                return LastBearingDecodeStatus.Corrupt;
            }

            var decoded = new LastBearingPointerRecord(
                generation,
                entryName,
                digest,
                encodedBytes);
            byte[] canonical = EncodePointerBytes(
                generation,
                entryName,
                digest);
            if (!ByteArraysEqual(canonical, encodedBytes))
            {
                return LastBearingDecodeStatus.Corrupt;
            }

            record = decoded;
            return LastBearingDecodeStatus.Success;
        }

        internal static byte[] ComputeSha256(byte[] bytes)
        {
            using SHA256 sha256 = SHA256.Create();
            return sha256.ComputeHash(bytes);
        }

        internal static string ToLowerHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (int index = 0; index < bytes.Length; index++)
            {
                builder.Append(
                    bytes[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        internal static bool ByteArraysEqual(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left.Length != right.Length)
            {
                return false;
            }

            int difference = 0;
            for (int index = 0; index < left.Length; index++)
            {
                difference |= left[index] ^ right[index];
            }

            return difference == 0;
        }

        private static byte[] EncodePointerBytes(
            ulong generation,
            LastBearingEntryName fileName,
            byte[] digest)
        {
            byte[] fileNameBytes = Encoding.ASCII.GetBytes(fileName.Value);
            byte[] encoded = new byte[LastBearingProfileContract.PointerBytes];
            Copy(PointerMagic, encoded, 0);
            WriteUInt16(encoded, 8, LastBearingProfileContract.PointerVersion);
            WriteUInt16(encoded, 10, checked((ushort)ProfileNameBytes.Length));
            WriteUInt64(encoded, 12, generation);
            WriteUInt16(encoded, 20, checked((ushort)fileNameBytes.Length));
            Copy(digest, encoded, 22);
            Copy(ProfileNameBytes, encoded, 54);
            Copy(fileNameBytes, encoded, 73);
            return encoded;
        }

        private static bool IsAscii(byte[] bytes, int offset, int count)
        {
            for (int index = offset; index < offset + count; index++)
            {
                if (bytes[index] > 0x7f)
                {
                    return false;
                }
            }

            return true;
        }

        private static void Copy(byte[] source, byte[] destination, int offset)
        {
            Buffer.BlockCopy(source, 0, destination, offset, source.Length);
        }

        private static byte[] Slice(byte[] source, int offset, int count)
        {
            var result = new byte[count];
            Buffer.BlockCopy(source, offset, result, 0, count);
            return result;
        }

        private static bool Matches(byte[] source, int offset, byte[] expected)
        {
            if (offset < 0 || source.Length - offset < expected.Length)
            {
                return false;
            }

            int difference = 0;
            for (int index = 0; index < expected.Length; index++)
            {
                difference |= source[offset + index] ^ expected[index];
            }

            return difference == 0;
        }

        private static void WriteUInt16(byte[] bytes, int offset, ushort value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
        }

        private static ushort ReadUInt16(byte[] bytes, int offset)
        {
            return (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
        }

        private static void WriteUInt32(byte[] bytes, int offset, uint value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }

        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            return (uint)(
                bytes[offset] |
                (bytes[offset + 1] << 8) |
                (bytes[offset + 2] << 16) |
                (bytes[offset + 3] << 24));
        }

        private static void WriteUInt64(byte[] bytes, int offset, ulong value)
        {
            for (int index = 0; index < 8; index++)
            {
                bytes[offset + index] = (byte)(value >> (index * 8));
            }
        }

        private static ulong ReadUInt64(byte[] bytes, int offset)
        {
            ulong value = 0;
            for (int index = 0; index < 8; index++)
            {
                value |= (ulong)bytes[offset + index] << (index * 8);
            }

            return value;
        }
    }
}
