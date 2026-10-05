using System.Globalization;
using System.Text.RegularExpressions;

namespace IskBattle.Domain.Text;

public enum NameError { None, Length, NotAllowed }

/// <summary>
/// Display names and league names are shown to other players and sent in email subjects and bodies, so they
/// may not contain line breaks, control or invisible formatting characters (e.g. bidi overrides), web
/// addresses or email addresses.
/// </summary>
public static partial class NameRules
{
    public static NameError Validate(string name, int maxLength)
    {
        if (name.Length == 0 || name.Length > maxLength)
            return NameError.Length;
        return name.Any(IsForbiddenChar) || LinkLike().IsMatch(name) ? NameError.NotAllowed : NameError.None;
    }

    private static bool IsForbiddenChar(char c) => char.GetUnicodeCategory(c) is
        UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator
        or UnicodeCategory.ParagraphSeparator or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned;

    // "://", "www.", "@", or a dot directly followed by two letters after a non-space ("evil.se", "J.Doe").
    [GeneratedRegex(@"://|www\.|@|\S\.\p{L}{2}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LinkLike();
}
