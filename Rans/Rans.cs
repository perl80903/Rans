
namespace Rans
{
    class SymbolStats(int symbolCount)
    {
        public uint[] SymbolFrequencies = new uint[symbolCount];      
        public uint[] CumulativeSymbolFrequencies = new uint[symbolCount + 1];
        public int SymbolCount => SymbolFrequencies.Length;

        public byte[] BuildCumulativeToSymTable()
        {
            byte[] result = new byte[Rans.PROB_SCALE];
            for (int s = 0; s < SymbolCount; s++)
            {
                for (uint i = CumulativeSymbolFrequencies[s]; i < CumulativeSymbolFrequencies[s + 1]; i++)
                {
                    result[i] = (byte)s;
                }
            }
            return result;
        }
    }

    internal class EncodingSymbol
    {
        internal uint XMax;
        internal uint RcpFrequency;
        internal uint Bias;
        internal ushort CmplFrequency;
        internal ushort RcpShift;

        internal EncodingSymbol(uint start, uint freq) 
        {
            if (start > (1u << (int)Rans.PROB_BITS))
                throw new ArgumentOutOfRangeException(nameof(start), "start must be less than or equal to 1 << scaleBits.");
            if (freq > (1u << (int)Rans.PROB_BITS) - start)
                throw new ArgumentOutOfRangeException(nameof(freq), "freq must be less than or equal to (1 << scaleBits) - start.");
            XMax = ((Rans.RANS_BYTE_L >> (int)Rans.PROB_BITS) << 8) * freq;
            CmplFrequency = (ushort)((1 << (int)Rans.PROB_BITS) - freq);
            if (freq < 2)
            {
                RcpFrequency = ~0u;
                RcpShift = 0;
                Bias = (uint)(start + (1 << (int)Rans.PROB_BITS) - 1);
            }
            else
            {
                uint shift = 0;
                while (freq > (1u << (int)shift))
                    shift++;
                RcpFrequency = (uint)(((1UL << (int)(shift + 31)) + freq - 1) / freq);
                RcpShift = (ushort)(shift - 1);
                Bias = start;
            }
        }

        internal uint PutSymbol(uint r, ref int outputIndex, byte[] outputBuffer)
        {
            if (XMax == 0)
                throw new InvalidOperationException("Cannot encode symbol with freq=0.");
            
            uint x = r;
            if (x >= XMax)
            {
                do
                {
                    outputIndex--;
                    outputBuffer[outputIndex] = (byte)(x & 0xff);
                    x >>= 8;
                } while (x >= XMax);
            }
            uint q = (uint)(((ulong)x * RcpFrequency) >> 32) >> RcpShift;
            r = x + Bias + q * CmplFrequency;
            return r;
        }
    }

    public class Rans
    {
        internal const uint RANS_BYTE_L = 1 << 23;  
        internal const uint PROB_BITS = 14;
        internal const uint PROB_SCALE = 1u << (int)PROB_BITS;

        // encoding
        static SymbolStats BuildStatsFromBytes(byte[] inputBuffer, out uint[] symbolFrequencies)
        {
            SymbolStats stats = new(256);
            for (int i = 0; i < inputBuffer.Length; i++)
                stats.SymbolFrequencies[inputBuffer[i]]++;
            symbolFrequencies = (uint[])stats.SymbolFrequencies.Clone();

            NormalizeFrequencies(stats);

            return stats;
        }

        // decoding
        static SymbolStats BuildStatsFromFrequencies(uint[] symbolFrequencies)
        {
            SymbolStats stats = new(256);
            stats.SymbolFrequencies = (uint[])symbolFrequencies.Clone();

            NormalizeFrequencies(stats);

            return stats;
        }

        static void CalculateCumulativeFrequencies(SymbolStats stats)
        {
            stats.CumulativeSymbolFrequencies[0] = 0;
            for (int i = 0; i < stats.SymbolFrequencies.Length; i++)
            {
                stats.CumulativeSymbolFrequencies[i + 1] = stats.CumulativeSymbolFrequencies[i] + stats.SymbolFrequencies[i];
            }
        }

        static void NormalizeFrequencies(SymbolStats stats)
        {
            CalculateCumulativeFrequencies(stats);

            uint curTotal = stats.CumulativeSymbolFrequencies[stats.SymbolCount];
            for (int i = 1; i <= stats.SymbolCount; i++)
                stats.CumulativeSymbolFrequencies[i] = (uint)((ulong)PROB_SCALE * stats.CumulativeSymbolFrequencies[i] / curTotal);

            for (int i = 0; i < stats.SymbolCount; i++)
            {
                if (stats.SymbolFrequencies[i] != 0 && stats.CumulativeSymbolFrequencies[i + 1] == stats.CumulativeSymbolFrequencies[i])
                {
                    uint bestFreq = ~0u;
                    int bestSteal = -1;
                    for (int j = 0; j < stats.SymbolCount; j++)
                    {
                        uint freq = stats.CumulativeSymbolFrequencies[j + 1] - stats.CumulativeSymbolFrequencies[j];
                        if (freq > 1 && freq < bestFreq)
                        {
                            bestFreq = freq;
                            bestSteal = j;
                        }
                    }
                    if (bestSteal == -1)
                        throw new InvalidOperationException("Failed to find a symbol to steal frequency from.");
                    if (bestSteal < i)
                    {
                        for (int j = bestSteal + 1; j <= i; j++)
                            stats.CumulativeSymbolFrequencies[j]--;
                    }
                    else
                    {
                        if (bestSteal == i)
                            throw new InvalidOperationException("bestSteal should not be equal to i.");
                        for (int j = i + 1; j <= bestSteal; j++)
                            stats.CumulativeSymbolFrequencies[j]++;
                    }
                }
            }

            for (int i = 0; i < 256; i++)
            {
                if (stats.SymbolFrequencies[i] == 0)
                {
                    if (stats.CumulativeSymbolFrequencies[i + 1] != stats.CumulativeSymbolFrequencies[i])
                        throw new InvalidOperationException("Symbol with zero frequency has non-zero cumulative frequency.");
                }
                else
                {
                    if (stats.CumulativeSymbolFrequencies[i + 1] <= stats.CumulativeSymbolFrequencies[i])
                        throw new InvalidOperationException("Symbol with non-zero frequency has non-increasing cumulative frequency.");
                }

                stats.SymbolFrequencies[i] = stats.CumulativeSymbolFrequencies[i + 1] - stats.CumulativeSymbolFrequencies[i];
            }
        }

        public static byte[] Encode(byte[] bytes, out uint[] symbolFrequencies)
        {
            SymbolStats stats = BuildStatsFromBytes(bytes, out symbolFrequencies);

            int outputBufferSize = (bytes.Length + 1024); // input size + some overhead
            // temporary output work buffer
            byte[] outputBuffer = new byte[outputBufferSize];

            EncodingSymbol[] esyms = new EncodingSymbol[256];
            for (int i = 0; i < 256; i++)
            {
                esyms[i] = new EncodingSymbol(stats.CumulativeSymbolFrequencies[i], stats.SymbolFrequencies[i]);
            }

            uint rans = RANS_BYTE_L;
            int outputIndex = outputBufferSize;

            // reader and writer indices both move backwards here
            for (int i = bytes.Length - 1; i >= 0; i--) 
            {
                rans = esyms[bytes[i]].PutSymbol(rans, ref outputIndex, outputBuffer);
            }

            outputIndex -= 4;
            outputBuffer[outputIndex + 0] = (byte)(rans >> 0);
            outputBuffer[outputIndex + 1] = (byte)(rans >> 8);
            outputBuffer[outputIndex + 2] = (byte)(rans >> 16);
            outputBuffer[outputIndex + 3] = (byte)(rans >> 24);

            int compressedSize = outputBufferSize - outputIndex;
            byte[] compressedData = new byte[compressedSize];
            Array.Copy(outputBuffer, outputIndex, compressedData, 0, compressedSize);

            return compressedData;
        }

        static uint DecodeAdvance(uint r, ref int inputBufferIndex, byte[] inputBuffer, uint start, uint freq)
        {
            uint mask = (1u << (int)PROB_BITS) - 1;
            uint x = r;
            x = freq * (x >> (int)PROB_BITS) + (x & mask) - start;
            if (x < RANS_BYTE_L)
            {
                do
                {
                    x = (x << 8) | inputBuffer[inputBufferIndex];
                    inputBufferIndex++;
                } while (x < RANS_BYTE_L);
            }
            return x;
        }

        public static byte[] Decode(byte[] inputBuffer, uint[] symbolFrequencies)
        {
            int finalOutputLength = 0;
            for (int i = 0; i < symbolFrequencies.Length; i++)
                finalOutputLength += (int)symbolFrequencies[i];

            SymbolStats stats = BuildStatsFromFrequencies(symbolFrequencies);

            byte[] outputBuffer = new byte[finalOutputLength];
            byte[] cumulativeToSymbolMapping = stats.BuildCumulativeToSymTable();

            uint rans = inputBuffer[0] | (uint)(inputBuffer[1] << 8) | (uint)(inputBuffer[2] << 16) | (uint)(inputBuffer[3] << 24);
            int inputBufferIndex = 4;

            for (int outputIndex = 0; outputIndex < finalOutputLength; outputIndex++)
            {
                uint s = cumulativeToSymbolMapping[rans & ((1u << (int)PROB_BITS) - 1)];
                outputBuffer[outputIndex] = (byte)s;
                rans = DecodeAdvance(rans, ref inputBufferIndex, inputBuffer, stats.CumulativeSymbolFrequencies[s], stats.SymbolFrequencies[s]);
            }
            return outputBuffer;
        }
    }
}
