using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Snail.Toolkit.AI.Ollama.Tests;

/// <summary>
/// Encodes a single-colour PNG, so vision tests carry a picture with exactly one right answer and no fixture file.
/// </summary>
internal static class SolidPng
{
    private static readonly uint[] CrcTable = BuildCrcTable();

    /// <summary>
    /// An RGB image of the given size filled with one colour.
    /// </summary>
    public static byte[] Of(int width, int height, byte red, byte green, byte blue)
    {
        using var png = new MemoryStream();
        png.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;
        header[9] = 2;

        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", Compress(Scanlines(width, height, red, green, blue)));
        WriteChunk(png, "IEND", []);

        return png.ToArray();
    }

    private static byte[] Scanlines(int width, int height, byte red, byte green, byte blue)
    {
        int stride = 1 + width * 3;
        var pixels = new byte[stride * height];

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int offset = row * stride + 1 + column * 3;
                pixels[offset] = red;
                pixels[offset + 1] = green;
                pixels[offset + 2] = blue;
            }
        }

        return pixels;
    }

    private static byte[] Compress(byte[] raw)
    {
        using var compressed = new MemoryStream();

        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal))
        {
            zlib.Write(raw);
        }

        return compressed.ToArray();
    }

    private static void WriteChunk(Stream png, string type, byte[] data)
    {
        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        Span<byte> number = stackalloc byte[4];

        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        png.Write(number);
        png.Write(typeBytes);
        png.Write(data);

        BinaryPrimitives.WriteUInt32BigEndian(number, Crc([.. typeBytes, .. data]));
        png.Write(number);
    }

    private static uint Crc(byte[] bytes)
    {
        uint crc = 0xFFFFFFFF;

        foreach (byte value in bytes)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFF;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];

        for (uint index = 0; index < 256; index++)
        {
            uint entry = index;

            for (int bit = 0; bit < 8; bit++)
            {
                entry = (entry & 1) != 0 ? 0xEDB88320 ^ (entry >> 1) : entry >> 1;
            }

            table[index] = entry;
        }

        return table;
    }
}
