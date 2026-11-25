using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Crecisol.Application.Identity;

/// <summary>Closed content contract for existing identity media and case receipts.
/// No filesystem path, storage key, URL or authorization capability is emitted.
/// This contract does not grant access: the use case must authorize the owner.</summary>
public static class C06ProtectedContent
{
    private const int RowSize = 64 * 4;
    private const int PixelCount = 64 * RowSize;
    private const int ScanCount = 64 * (RowSize + 1);
    private const int MaxPng = 24576;
    private static readonly byte[] Signature = [137,80,78,71,13,10,26,10];
    private static readonly byte[] Header = [0,0,0,64,0,0,0,64,8,6,0,0,0];

    public static bool IsDisplayReference(string? value) =>
        value is { Length: >= 1 and <= 120 } &&
        char.IsAsciiLetterOrDigit(value[0]) &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private static InvalidOperationException Invalid() => new("Invalid protected content.");

    public static byte[] EncodePhoto(byte[] pixels)
    {
        if (pixels.Length != PixelCount) throw Invalid();
        using var result = new MemoryStream();
        result.Write(Signature);
        Chunk(result, "IHDR"u8, Header);
        using var compressed = new MemoryStream();
        using (var zip = new ZLibStream(compressed, CompressionLevel.Optimal, true))
            for (var y = 0; y < 64; y++)
            {
                zip.WriteByte(0);
                zip.Write(pixels, y * RowSize, RowSize);
            }
        Chunk(result, "IDAT"u8, compressed.ToArray());
        Chunk(result, "IEND"u8, []);
        return result.ToArray();
    }

    // Reconstruct, never forward the persisted container or compressed bytes.
    // Strict three-chunk structure, checksums and fixed decompression budget;
    // even ancillary metadata or a second appended container cannot leave API.
    public static byte[] NormalizePhoto(byte[] png)
    {
        if (png.Length is < 57 or > MaxPng || !png.AsSpan(0, 8).SequenceEqual(Signature)) throw Invalid();
        int position = 8;
        var header = ReadChunk(png, ref position, "IHDR"u8);
        if (!header.SequenceEqual(Header)) throw Invalid();
        var data = ReadChunk(png, ref position, "IDAT"u8);
        if (data.IsEmpty || !ReadChunk(png, ref position, "IEND"u8).IsEmpty || position != png.Length) throw Invalid();
        var compressed = data.ToArray();
        var scan = new byte[ScanCount];
        var pixels = new byte[PixelCount];
        try
        {
            using var stream = new MemoryStream(compressed, writable: false);
            using var zip = new ZLibStream(stream, CompressionMode.Decompress);
            zip.ReadExactly(scan);
            if (zip.ReadByte() != -1) throw Invalid();
            for (var y = 0; y < 64; y++)
            {
                var offset = y * (RowSize + 1);
                if (scan[offset] != 0) throw Invalid();
                scan.AsSpan(offset + 1, RowSize).CopyTo(pixels.AsSpan(y * RowSize, RowSize));
            }
            return EncodePhoto(pixels);
        }
        catch (InvalidDataException) { throw Invalid(); }
        catch (EndOfStreamException) { throw Invalid(); }
        finally
        {
            CryptographicOperations.ZeroMemory(compressed);
            CryptographicOperations.ZeroMemory(scan);
            CryptographicOperations.ZeroMemory(pixels);
        }
    }

    public static string NormalizePhotoBase64(string value)
    {
        if (value.Length is < 1 or > 32768) throw Invalid();
        byte[]? bytes = null;
        byte[]? normalized = null;
        try
        {
            bytes = Convert.FromBase64String(value);
            if (!string.Equals(Convert.ToBase64String(bytes), value, StringComparison.Ordinal)) throw Invalid();
            normalized = NormalizePhoto(bytes);
            return Convert.ToBase64String(normalized);
        }
        catch (FormatException) { throw Invalid(); }
        finally
        {
            if (bytes is not null) CryptographicOperations.ZeroMemory(bytes);
            if (normalized is not null) CryptographicOperations.ZeroMemory(normalized);
        }
    }

    private static ReadOnlySpan<byte> ReadChunk(byte[] png, ref int position, ReadOnlySpan<byte> name)
    {
        if (position < 0 || position > png.Length - 12) throw Invalid();
        var remaining = png.AsSpan(position);
        var size = BinaryPrimitives.ReadInt32BigEndian(remaining);
        if (size < 0 || size > remaining.Length - 12 || !remaining.Slice(4, 4).SequenceEqual(name)) throw Invalid();
        var data = remaining.Slice(8, size);
        var checksum = BinaryPrimitives.ReadUInt32BigEndian(remaining.Slice(8 + size, 4));
        if (checksum != Checksum(name, data)) throw Invalid();
        position += size + 12;
        return data;
    }

    private static void Chunk(Stream output, ReadOnlySpan<byte> name, byte[] data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        output.Write(number); output.Write(name); output.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(number, Checksum(name, data));
        output.Write(number);
    }

    private static uint Checksum(ReadOnlySpan<byte> name, ReadOnlySpan<byte> data)
    {
        uint crc = uint.MaxValue;
        foreach (var b in name) crc = Update(crc, b);
        foreach (var b in data) crc = Update(crc, b);
        return ~crc;
    }

    private static uint Update(uint crc, byte value)
    {
        crc ^= value;
        for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0xEDB88320u : 0u);
        return crc;
    }
}
