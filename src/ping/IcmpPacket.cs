using System;

namespace ping
{
    public sealed class IcmpPacket
    {
        public const int IcmpHeaderSize = 8;
        public const int IpHeaderSize = 20;

        public byte Type { get; }
        public byte Code { get; }
        public ushort Identifier { get; }
        public ushort SequenceNumber { get; }
        public byte[] Data { get; }

        private IcmpPacket(byte type, byte code, ushort identifier, ushort sequenceNumber, byte[] data)
        {
            Type = type;
            Code = code;
            Identifier = identifier;
            SequenceNumber = sequenceNumber;
            Data = data;
        }

        public static IcmpPacket CreateEchoRequest(ushort identifier, ushort sequenceNumber, ReadOnlySpan<byte> data) => new IcmpPacket(8, 0, identifier, sequenceNumber, data.ToArray());

        // TODO: convert this and other methods to work with ReadOnlySpan<byte> instead of copying arrays
        public static IcmpPacket FromBytes(byte[] buffer) => !HasValidChecksum(buffer)
                ? throw new IcmpChecksumException()
                : new IcmpPacket(buffer[20], buffer[21], BitConverter.ToUInt16(buffer, 24), BitConverter.ToUInt16(buffer, 26), buffer[28..]);

        public static implicit operator byte[](IcmpPacket packet)
        {
            var size = IcmpHeaderSize + packet.Data.Length;
            var result = new byte[size];
            var index = 0;

            void CopyAndAdvance(byte[] bytes)
            {
                Array.Copy(bytes, 0, result, index, bytes.Length);
                index += bytes.Length;
            }

            // set Type, Code
            result[index++] = packet.Type;
            result[index++] = packet.Code;

            // skip two-byte Checksum field
            index += 2;

            // set Identifier, SequenceNumber, Data
            CopyAndAdvance(BitConverter.GetBytes(packet.Identifier));
            CopyAndAdvance(BitConverter.GetBytes(packet.SequenceNumber));
            CopyAndAdvance(packet.Data);

            // calculate Internet Checksum
            var checksum = GetChecksum(result);

            // copy two-byte checksum into 3rd and 4th bytes of the resulting array
            var checksumBytes = BitConverter.GetBytes(checksum);
            result[2] = checksumBytes[0];
            result[3] = checksumBytes[1];

            return result;
        }

        /// <summary>
        /// Computes the Internet Checksum as defined in RFC1071 (https://tools.ietf.org/html/rfc1071)
        /// </summary>
        /// <param name="buffer">byte array representing the packet</param>
        /// <returns>checksum as 16-bit unsigned integer</returns>
        private static ushort GetChecksum(byte[] buffer)
        {
            int sum = 0;
            var index = 0;
            while (index < buffer.Length - 1)
            {
                sum += BitConverter.ToUInt16(buffer, index);
                index += 2;
            }

            if (index == buffer.Length - 1)
            {
                sum += buffer[^1];
            }

            while (sum >> 16 > 0)
            {
                sum = (sum & 0xffff) + (sum >> 16);
            }

            return (ushort)~sum;
        }

        public static bool HasValidChecksum(byte[] buffer) => GetChecksum(buffer) == 0;
    }
}
