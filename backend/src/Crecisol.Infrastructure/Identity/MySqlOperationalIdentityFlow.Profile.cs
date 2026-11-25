using System.Security.Cryptography;
using System.Text.Json;
using Crecisol.Application.Identity;

namespace Crecisol.Infrastructure.Identity;

public sealed partial class MySqlOperationalIdentityFlow
{
    private sealed record ScopedProfile(string DisplayName, string Language, string TimeZone);
    private async Task<OperationalIdentityResult> Profile(Unit u, byte[] tenant, byte[] actor, OperationalIdentityRequest r, bool platformRealm = false)
    {
        // All identifiers below are closed server constants. No table, scope,
        // owner, realm, or tenant identifier is accepted from a browser field.
        var table = platformRealm ? "c05_platform_scoped_profile" : "c05_identity_scoped_profile";
        var scope = platformRealm ? "operator_id=@a" : "tenant_id=@t AND account_id=@a";
        var purpose = platformRealm ? "platform-profile:" + Hex(actor) : "scoped-profile:" + Hex(tenant) + Hex(actor);
        var row = await u.One($"SELECT * FROM {table} WHERE {scope} FOR UPDATE", ("t", tenant), ("a", actor));
        var profile = new ScopedProfile("", "es-HN", "America/Tegucigalpa");
        if (row is not null)
        {
            var plain = Unprotect(Bytes(row, "protected_profile"), purpose);
            try { profile = JsonSerializer.Deserialize<ScopedProfile>(plain) ?? throw new InvalidOperationException("Profile unavailable."); }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }
        if (r.Operation == "profile/read") return new(true, new { profile.DisplayName, profile.Language, profile.TimeZone, hasPhoto = row is not null && Number(row, "photo_state") == 2 });
        if (r.Operation == "profile/photo")
        {
            if (row is null || Number(row, "photo_state") != 2) return new(false);
            byte[]? canonical = null;
            try
            {
                canonical = CanonicalProfilePng.Normalize(Bytes(row, "photo_png"));
                return new(true, new { png = Convert.ToBase64String(canonical), mime = "image/png", width = 64, height = 64 });
            }
            catch (InvalidOperationException) { return new(false); }
            finally { if (canonical is not null) CryptographicOperations.ZeroMemory(canonical); }
        }
        if (r.Operation == "profile/save")
        {
            var name = Field(r, "displayName").Trim(); var language = Field(r, "language"); var zone = Field(r, "timeZone");
            if (name.Length is < 1 or > 80 || name.Any(char.IsControl) || language is not ("es-HN" or "es" or "en") ||
                zone.Length > 64 || zone.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '/' or '_' or '+' or '-')) ||
                !TimeZoneInfo.TryFindSystemTimeZoneById(zone, out _)) return new(false);
            profile = new(name, language, zone);
        }
        var envelope = Protect(JsonSerializer.SerializeToUtf8Bytes(profile), purpose);
        var columns = platformRealm ? "operator_id,protected_profile,photo_state,updated_at_utc" : "tenant_id,account_id,protected_profile,photo_state,updated_at_utc";
        var values = platformRealm ? "@a,@p,0,UTC_TIMESTAMP(6)" : "@t,@a,@p,0,UTC_TIMESTAMP(6)";
        await u.Run($"INSERT INTO {table}({columns}) VALUES({values}) ON DUPLICATE KEY UPDATE protected_profile=@p,updated_at_utc=UTC_TIMESTAMP(6)", ("t", tenant), ("a", actor), ("p", envelope));
        if (r.Operation == "profile/photo-upload")
        {
            var pixels = Convert.FromBase64String(Field(r, "pixels"));
            try
            {
                var canonical = CanonicalProfilePng.Encode(pixels);
                await u.Run($"UPDATE {table} SET photo_png=NULL,photo_state=1 WHERE {scope}", ("t", tenant), ("a", actor));
                await u.Run($"UPDATE {table} SET photo_png=@png,photo_state=2 WHERE {scope}", ("t", tenant), ("a", actor), ("png", canonical));
            }
            finally { CryptographicOperations.ZeroMemory(pixels); }
        }
        if (r.Operation == "profile/photo-remove")
            await u.Run($"UPDATE {table} SET photo_png=NULL,photo_state=0 WHERE {scope}", ("t", tenant), ("a", actor));
        return new(true, new { accepted = true });
    }
}
