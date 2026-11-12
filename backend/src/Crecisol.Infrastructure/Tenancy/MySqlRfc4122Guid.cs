namespace Crecisol.Infrastructure.Tenancy;

/// <summary>
/// Explicit RFC 4122/network byte order for MySQL BINARY(16) tenant, branch,
/// resource and event IDs. .NET Guid's legacy little-endian byte layout is
/// NEVER used. This pure codec is exercised by vectors and MySQL round trips.
/// </summary>
public static class MySqlRfc4122Guid
{
    public static byte[] Encode(Guid value)
    {
        if (value == Guid.Empty)
            throw new InvalidOperationException("Empty UUID is not a storage key.");
        return value.ToByteArray(bigEndian: true);
    }

    public static Guid Decode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length != 16)
            throw new InvalidOperationException("MySQL UUID must have exactly 16 bytes.");
        var guid = new Guid(bytes, bigEndian: true);
        if (guid == Guid.Empty)
            throw new InvalidOperationException("Empty MySQL UUID is invalid.");
        return guid;
    }
}
