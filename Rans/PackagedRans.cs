
namespace Rans
{
    internal class PackagedRans
    {

        public static byte[] Encode(byte[] bytes)
        {
            var compressedData = Rans.Encode(bytes, out uint[] symbolFrequencies);
            // turn symbolFrequencies into byte array
            byte[] freqBytes = new byte[symbolFrequencies.Length * 4];
            for (int i = 0; i < symbolFrequencies.Length; i++)
            {
                uint f = symbolFrequencies[i];
                byte[] freqByte = BitConverter.GetBytes(f);
                Array.Copy(freqByte, 0, freqBytes, i * 4, 4);
            }
            // expand freqBytes into an array of bytes, one for each bit in freqBytes
            byte[] freqBits = new byte[freqBytes.Length * 8];
            for (int i = 0; i < freqBytes.Length; i++)
            {
                for (int bit = 0; bit < 8; bit++)
                {
                    freqBits[i * 8 + bit] = (byte)((freqBytes[i] >> bit) & 1);
                }
            }
            // rans-encode the freqBits array
            var compressedFreqBits = Rans.Encode(freqBits, out uint[] freqSymbolFrequencies);
            // verify that only 0 and 1 are used in freqSymbolFrequencies
            for (int i = 2; i < freqSymbolFrequencies.Length; i++)
            {
                if (freqSymbolFrequencies[i] != 0)
                {
                    throw new Exception("Frequency encoding failed: only two symbols should be used.");
                }
            }
            // turn the two uint values in freqSymbolFrequencies into bytes
            byte[] freqSymbolFreqBytes = new byte[8];
            for (int i = 0; i < 2; i++)
            {
                byte[] freqSymbolFreqByte = BitConverter.GetBytes(freqSymbolFrequencies[i]);
                Array.Copy(freqSymbolFreqByte, 0, freqSymbolFreqBytes, i * 4, 4);
            }
            // expand freqSymbolFreqBytes into an array of bytes, one for each bit in freqSymbolFreqBytes
            byte[] freqSymbolFreqBits = new byte[freqSymbolFreqBytes.Length * 8];
            for (int i = 0; i < freqSymbolFreqBytes.Length; i++)
            {
                for (int bit = 0; bit < 8; bit++)
                {
                    freqSymbolFreqBits[i * 8 + bit] = (byte)((freqSymbolFreqBytes[i] >> bit) & 1);
                }
            }
            // rans-encode the freqSymbolFreqBits array
            var compressedFreqSymbolFreqBits = Rans.Encode(freqSymbolFreqBits, out uint[] freqSymbolFreqSymbolFrequencies);

            // verify that only 0 and 1 are used in freqSymbolFreqSymbolFrequencies
            // and that both are less than 256
            if (freqSymbolFreqSymbolFrequencies[0] >= 256)
            {
                throw new Exception("Frequency encoding failed: frequencies should be less than 256.");
            }
            if (freqSymbolFreqSymbolFrequencies[1] >= 256)
            {
                throw new Exception("Frequency encoding failed: frequencies should be less than 256.");
            }
            for (int i = 2; i < freqSymbolFreqSymbolFrequencies.Length; i++)
            {
                if (freqSymbolFreqSymbolFrequencies[i] != 0)
                {
                    throw new Exception("Frequency encoding failed: only two symbols should be used.");
                }
            }
            // we can now package everything together with the two bit frequencies of the last encoding at the start
            // as bytes, followed by the three compressed arrays
            int totalLength = 3 + 4 + 4 + compressedFreqSymbolFreqBits.Length + compressedFreqBits.Length + compressedData.Length;
            byte[] result = new byte[totalLength];
            // first the two frequencies as bytes
            result[0] = (byte)freqSymbolFreqSymbolFrequencies[0];
            result[1] = (byte)freqSymbolFreqSymbolFrequencies[1];
            // then the three compressed arrays
            // need to store the lengths of each compressed array to be able to decode
            result[2] = (byte)compressedFreqSymbolFreqBits.Length;
            int offset = 3;
            Array.Copy(compressedFreqSymbolFreqBits, 0, result, offset, compressedFreqSymbolFreqBits.Length);
            byte[] lengthBytes = BitConverter.GetBytes(compressedFreqBits.Length);
            offset += compressedFreqSymbolFreqBits.Length;
            Array.Copy(lengthBytes, 0, result, offset, 4);
            offset += 4;
            Array.Copy(compressedFreqBits, 0, result, offset, compressedFreqBits.Length);
            offset += compressedFreqBits.Length;
            lengthBytes = BitConverter.GetBytes(compressedData.Length);
            Array.Copy(lengthBytes, 0, result, offset, 4);
            offset += 4;
            Array.Copy(compressedData, 0, result, offset, compressedData.Length);
            return result;
        }

        public static byte[] Decode(byte[] inputBuffer)
        {
            // read the first two bytes as the frequencies for the last encoding
            uint[] freqSymbolFreqSymbolFrequencies = new uint[256];
            freqSymbolFreqSymbolFrequencies[0] = inputBuffer[0];
            freqSymbolFreqSymbolFrequencies[1] = inputBuffer[1];
            // 3rd byte is the length of the compressed freqSymbolFreqBits
            byte compressedFreqSymbolFreqBitsLength = inputBuffer[2];
            // decode the freqSymbolFreqBits
            var freqSymbolFreqBits = Rans.Decode(inputBuffer.AsSpan(3, compressedFreqSymbolFreqBitsLength).ToArray(), freqSymbolFreqSymbolFrequencies);
            // turn freqSymbolFreqBits into freqSymbolFreqBytes
            byte[] freqSymbolFreqBytes = new byte[freqSymbolFreqBits.Length / 8];
            for (int i = 0; i < freqSymbolFreqBytes.Length; i++)
            {
                for (int bit = 0; bit < 8; bit++)
                {
                    freqSymbolFreqBytes[i] |= (byte)(freqSymbolFreqBits[i * 8 + bit] << bit);
                }
            }
            // turn freqSymbolFreqBytes into freqSymbolFrequencies
            uint[] freqSymbolFrequencies = new uint[256]; // only first two will be used, but need to allocate full array
            for (int i = 0; i < 2; i++)
            {
                freqSymbolFrequencies[i] = BitConverter.ToUInt32(freqSymbolFreqBytes, i * 4);
            }
            // next 4 bytes are the length of the compressed freqBits
            int offset = 3 + compressedFreqSymbolFreqBitsLength;
            int compressedFreqBitsLength = BitConverter.ToInt32(inputBuffer, offset);
            offset += 4;
            // decode the freqBits
            var freqBits = Rans.Decode(inputBuffer.AsSpan(offset, compressedFreqBitsLength).ToArray(), freqSymbolFrequencies);
            // turn freqBits into freqBytes
            byte[] freqBytes = new byte[freqBits.Length / 8];
            for (int i = 0; i < freqBytes.Length; i++)
            {
                for (int bit = 0; bit < 8; bit++)
                {
                    freqBytes[i] |= (byte)(freqBits[i * 8 + bit] << bit);
                }
            }
            // turn freqBytes into symbolFrequencies
            uint[] symbolFrequencies = new uint[freqBytes.Length / 4];
            for (int i = 0; i < symbolFrequencies.Length; i++)
            {
                symbolFrequencies[i] = BitConverter.ToUInt32(freqBytes, i * 4);
            }
            offset += compressedFreqBitsLength;
            // next 4 bytes are the length of the compressed data
            int compressedDataLength = BitConverter.ToInt32(inputBuffer, offset);
            //Console.WriteLine($"Compressed data length: {compressedDataLength}");
            offset += 4;
            // decode the data
            var decodedData = Rans.Decode(inputBuffer.AsSpan(offset, compressedDataLength).ToArray(), symbolFrequencies);
            return decodedData;
        }
    }
}
