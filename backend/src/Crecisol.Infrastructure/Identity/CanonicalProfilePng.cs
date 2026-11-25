using Crecisol.Application.Identity;

namespace Crecisol.Infrastructure.Identity;

/// <summary>One shared closed raster contract for the persistence and API boundaries.</summary>
internal static class CanonicalProfilePng
{
    public static byte[] Encode(byte[] pixels) => C06ProtectedContent.EncodePhoto(pixels);
    public static byte[] Normalize(byte[] png) => C06ProtectedContent.NormalizePhoto(png);
}
