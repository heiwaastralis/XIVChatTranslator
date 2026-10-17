using System.Globalization;
using System.Text;

namespace ChatTranslator;

/// <summary>
/// Le chat du jeu n'accepte que les lettres latines de base et les alphabets japonais.
/// Remplace les lettres accentuées par leur lettre de base (ł -> l, ß -> ss, é -> e...)
/// et les signes typographiques courants par leur équivalent ASCII.
/// Le japonais, les chiffres et les autres caractères sont conservés tels quels.
/// </summary>
public static class TextSanitizer
{
    public static string ToLatin(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var sb = new StringBuilder(text.Length + 8);
        foreach (var ch in text.Normalize(NormalizationForm.FormD))
        {
            // Supprime les accents (marques combinantes) laissés par la décomposition.
            // Exception : les marques japonaises (dakuten/handakuten, U+3099 et U+309A) sont conservées,
            // sinon が deviendrait か. La recomposition finale (FormC) les réattache à leur kana.
            if (ch != '\u3099' && ch != '\u309A'
                && CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;

            sb.Append(Map(ch));
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string Map(char c) => c switch
    {
        // Lettres latines sans décomposition Unicode.
        'ł' => "l", 'Ł' => "L",
        'ø' => "o", 'Ø' => "O",
        'đ' => "d", 'Đ' => "D",
        'ð' => "d", 'Ð' => "D",
        'ħ' => "h", 'Ħ' => "H",
        'ı' => "i", 'İ' => "I",
        'ŧ' => "t", 'Ŧ' => "T",
        'ß' => "ss", 'ẞ' => "SS",
        'æ' => "ae", 'Æ' => "AE",
        'œ' => "oe", 'Œ' => "OE",
        'þ' => "th", 'Þ' => "Th",

        // Typographie.
        '‘' or '’' or '‚' or '′' => "'",
        '“' or '”' or '„' or '″' or '«' or '»' => "\"",
        '–' or '—' or '−' => "-",
        '…' => "...",
        ' ' or ' ' or ' ' => " ",
        '€' => "EUR",

        _ => c.ToString(),
    };
}
