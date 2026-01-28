using System.Text;

namespace Rans
{
    internal class Program
    {
        static void Main()
        {   
            string input = File.ReadAllText("theraven.txt");
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            Console.WriteLine("input: {0:N0} bytes", bytes.Length);

            Console.WriteLine("\nr/ANS compression/decompression");

            var compressedData = Rans.Encode(bytes, out uint[] symbolFrequencies);
            Console.WriteLine("Encoded: {0:N0} bytes (+ symbol frequencies)", compressedData.Length);
            double ratio = (double)compressedData.Length / (double)bytes.Length;
            Console.WriteLine($"Compression ratio: {100 * ratio:F4}%");

            var decompressedData = Rans.Decode(compressedData, symbolFrequencies);

            // check decode results
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] != decompressedData[i])
                {
                    throw new Exception("decode failed!");
                }
            }
            Console.WriteLine("Decode verified!");            

            Console.WriteLine("\nPackaged r/ANS:");
            compressedData = PackagedRans.Encode(bytes);
            Console.WriteLine("input: {0:N0} bytes", bytes.Length);
            Console.WriteLine("Encoded: {0:N0} bytes (includes packaged symbol frequencies)", compressedData.Length);
            ratio = (double)compressedData.Length / (double)bytes.Length;
            Console.WriteLine($"Compression ratio: {100 * ratio:F4}%");
            decompressedData = PackagedRans.Decode(compressedData);

            // check decode results
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] != decompressedData[i])
                {
                    throw new Exception("decode failed!");
                }
            }
            Console.WriteLine("Decode verified!");
        }
    }
}

