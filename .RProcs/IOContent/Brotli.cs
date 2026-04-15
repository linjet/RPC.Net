using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IOContent
{
    // From: https://www.prowaretech.com/articles/current/dot-net/compression-brotli
    public class Brotli
    {
        private static CompressionLevel GetCompressionLevel()
        {
            if (Enum.IsDefined(typeof(CompressionLevel), 3)) // NOTE: CompressionLevel.SmallestSize == 3 is not supported in .NET Core 3.1
            {
                return (CompressionLevel)3;
            }
            return CompressionLevel.Optimal;
        }
        public static byte[] CompressBytes(byte[] bytes) => CompressBytesAsync(bytes).GetAwaiter().GetResult();
        public static async Task<byte[]> CompressBytesAsync(byte[] bytes, CancellationToken cancel = default(CancellationToken))
        {
            using (var outputStream = new MemoryStream())
            {
                using (var compressionStream = new BrotliStream(outputStream, GetCompressionLevel()))
                {
                    await compressionStream.WriteAsync(bytes, 0, bytes.Length, cancel);
                }
                return outputStream.ToArray();
            }
        }
        public static void CompressFile(string originalFileName, string compressedFileName) => CompressFileAsync(originalFileName, compressedFileName).GetAwaiter().GetResult();
        public static async Task CompressFileAsync(string originalFileName, string compressedFileName, CancellationToken cancel = default(CancellationToken))
        {
            using (FileStream originalStream = File.Open(originalFileName, FileMode.Open))
            {
                using (FileStream compressedStream = File.Create(compressedFileName))
                {
                    await CompressStreamAsync(originalStream, compressedStream, cancel);
                }
            }
        }
        public static void CompressStream(Stream originalStream, Stream compressedStream) => CompressStreamAsync(originalStream, compressedStream).GetAwaiter().GetResult();
        public static async Task CompressStreamAsync(Stream originalStream, Stream compressedStream, CancellationToken cancel = default(CancellationToken))
        {
            using (var compressor = new BrotliStream(compressedStream, GetCompressionLevel()))
            {
                await originalStream.CopyToAsync(compressor, cancel);
            }
        }
        public static byte[] DecompressBytes(byte[] bytes) => DecompressBytesAsync(bytes).GetAwaiter().GetResult();
        public static async Task<byte[]> DecompressBytesAsync(byte[] bytes, CancellationToken cancel = default(CancellationToken))
        {
            using (var inputStream = new MemoryStream(bytes))
            {
                using (var outputStream = new MemoryStream())
                {
                    using (var compressionStream = new BrotliStream(inputStream, CompressionMode.Decompress))
                    {
                        await compressionStream.CopyToAsync(outputStream, cancel);
                    }
                    return outputStream.ToArray();
                }
            }
        }
        public static void DecompressFile(string compressedFileName, string outputFileName) => DecompressFileAsync(compressedFileName, outputFileName).GetAwaiter().GetResult();
        public static async Task DecompressFileAsync(string compressedFileName, string outputFileName, CancellationToken cancel = default(CancellationToken))
        {
            using (FileStream compressedFileStream = File.Open(compressedFileName, FileMode.Open))
            {
                using (FileStream outputFileStream = File.Create(outputFileName))
                {
                    await DecompressStreamAsync(compressedFileStream, outputFileStream, cancel);
                }
            }
        }
        public static void DecompressStream(Stream compressedStream, Stream outputStream) => DecompressStreamAsync(compressedStream, outputStream).GetAwaiter().GetResult();
        public static async Task DecompressStreamAsync(Stream compressedStream, Stream outputStream, CancellationToken cancel = default(CancellationToken))
        {
            using (var decompressor = new BrotliStream(compressedStream, CompressionMode.Decompress))
            {
                await decompressor.CopyToAsync(outputStream, cancel);
            }
        }
    }
}
