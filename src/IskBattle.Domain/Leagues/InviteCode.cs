using System.Security.Cryptography;

namespace IskBattle.Domain.Leagues;

public static class InviteCode
{
    /// <summary>No 0/O, 1/I/L or U/V lookalikes.</summary>
    public const string Alphabet = "ABCDEFGHJKMNPQRSTWXYZ23456789";

    public const int Length = 8;

    public static string New() => RandomNumberGenerator.GetString(Alphabet, Length);

    /// <summary>Uppercases, drops spaces and dashes. Returns null if the result can't be a code.</summary>
    public static string? Normalize(string? input)
    {
        if (input is null)
            return null;
        var code = new string(input.Where(c => c is not (' ' or '-')).ToArray()).ToUpperInvariant();
        return code.Length == Length && code.All(Alphabet.Contains) ? code : null;
    }
}
