using System.Buffers.Binary;
using System.IO.Compression;

namespace Crecisol.Infrastructure.Identity;

/// <summary>Canonical 64x64 RGBA8 raster. The API never accepts an uploaded
/// container, MIME claim, SVG, compressed stream, path, HTML, or original EXIF.
/// Fixed-size raw pixels are encoded from scratch into IHDR/IDAT/IEND only.</summary>
internal static class CanonicalProfilePng
{
    public static byte[] Encode(byte[] pixels)
    {
        if (pixels.Length != 64 * 64 * 4) throw new InvalidOperationException("Bounded RGBA8 pixels required.");
        using var result = new MemoryStream(); result.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var header = new byte[13]; BinaryPrimitives.WriteInt32BigEndian(header, 64); BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), 64);
        header[8] = 8; header[9] = 6; Chunk(result, "IHDR"u8, header);
        using var compressed = new MemoryStream();
        using (var zip = new ZLibStream(compressed, CompressionLevel.Optimal, true))
            for (var y = 0; y < 64; y++) { zip.WriteByte(0); zip.Write(pixels, y * 256, 256); }
        Chunk(result, "IDAT"u8, compressed.ToArray()); Chunk(result, "IEND"u8, []);
        return result.ToArray();
    }
    private static void Chunk(Stream output, ReadOnlySpan<byte> name, byte[] data)
    {
        Span<byte> number = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(number, data.Length); output.Write(number); output.Write(name); output.Write(data);
        uint crc = uint.MaxValue;
        foreach (var b in name) crc = Update(crc, b);
        foreach (var b in data) crc = Update(crc, b);
        BinaryPrimitives.WriteUInt32BigEndian(number, ~crc); output.Write(number);
    }
    private static uint Update(uint crc, byte value)
    {
        crc ^= value; for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0xEDB88320u : 0u);
        return crc;
    }
}
