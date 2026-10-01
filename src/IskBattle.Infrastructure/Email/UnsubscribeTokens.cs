using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace IskBattle.Infrastructure.Email;

/// <summary>HMAC-signed, non-expiring tokens identifying the user to unsubscribe (ARCHITECTURE.md §8).</summary>
public sealed class UnsubscribeTokens(IOptions<EmailOptions> options)
{
    private const string Purpose = "unsubscribe:v1:";

    private readonly byte[] _key = Encoding.UTF8.GetBytes(
        options.Value.UnsubscribeKey is { Length: >= 32 } key
            ? key
            : throw new InvalidOperationException("Email:UnsubscribeKey must be at least 32 characters."));

    public string Create(Guid userId)
    {
        var id = userId.ToByteArray();
        return $"{Base64Url(id)}.{Base64Url(Sign(id))}";
    }

    public Guid? Validate(string? token)
    {
        var parts = token?.Split('.');
        if (parts is not [var idPart, var signaturePart])
            return null;
        try
        {
            var id = Convert.FromBase64String(Pad(idPart));
            var signature = Convert.FromBase64String(Pad(signaturePart));
            return id.Length == 16 && CryptographicOperations.FixedTimeEquals(signature, Sign(id)) ? new Guid(id) : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private byte[] Sign(byte[] id) => HMACSHA256.HashData(_key, (byte[])[.. Encoding.UTF8.GetBytes(Purpose), .. id]);

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Pad(string base64Url)
    {
        var s = base64Url.Replace('-', '+').Replace('_', '/');
        return s + new string('=', (4 - s.Length % 4) % 4);
    }
}
