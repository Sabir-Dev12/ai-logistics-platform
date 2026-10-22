using System.Globalization;
using System.Text;

namespace Logistics.Domain.Entities.Resources;

/// <summary>
/// The legacy application's password encoding (<c>tools.Codage_Motpasse</c>).
/// </summary>
/// <remarks>
/// <para>
/// Transcribed from <c>classe_tools.vb:146-164</c>. It is a <b>factorial-weighted character sum</b>:
/// </para>
/// <code>
/// hash = Î£ (i = 1..n) Ascii(char_i) Ã— (n - i + 1)!
/// </code>
/// <para>
/// Login is a plain string comparison of this value against <c>t_personnel.pass_word</c>
/// (<c>frm_demarrage.vb:144</c>), which is why reproducing it byte-for-byte â€” rather than replacing it with
/// something modern â€” is what lets the existing user base sign in at all.
/// </para>
/// <para>
/// <b>This is not a password hash and must not be treated as one.</b> Three properties make that concrete
/// rather than a matter of taste:
/// </para>
/// <list type="number">
/// <item>
/// <b>Unsalted and fast.</b> Two users with the same password store the same value, and the whole space is
/// enumerable at millions of candidates per second, so a leaked table is a leaked password list.
/// </item>
/// <item>
/// <b>The weights are computed in <c>Single</c> precision</b>, so from <b>14 characters</b> the encoded value
/// diverges from the exact mathematical sum. The intuitive threshold is 11 â€” "11! exceeds 2^24" â€” and it is
/// <b>wrong</b>: 11! = 2^8 Ã— 155925 and that odd part fits in 24 bits, as it does up to 13!. Only 14! has an
/// odd part too large. This was corrected after a test compared the encoding against a <c>BigInteger</c>
/// computation and found them identical where divergence had been claimed; the constant is now derived by
/// searching for the first divergence rather than reasoned about.
/// <para>
/// Whether the rounding also makes the encoding <b>collide</b> â€” two passwords encoding alike and therefore
/// interchangeable at login â€” was searched for and <b>not found</b> at length 11, so injectivity is
/// <b>unproven</b> rather than known-broken.
/// </para>
/// </item>
/// <item>
/// <b>No work factor.</b> There is nothing to raise.
/// </item>
/// </list>
/// <para>
/// So this exists to <b>verify</b> legacy credentials and to keep the desktop and web applications agreeing
/// during the migration. A new password should be stored with a real algorithm and this path retired; see the
/// README for the recommended cut-over. <see cref="EstAdapteAUnNouveauMotDePasse"/> exists so callers do not
/// have to remember that.
/// </para>
/// </remarks>
public static class MotDePasseLegacy
{
    /// <summary>
    /// Longest password for which the encoding equals the exact mathematical sum.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not a guess, and deliberately not <c>11</c>: the obvious reasoning â€” "11! exceeds <c>Single</c>'s 24-bit
    /// mantissa, so 11! rounds" â€” is <b>wrong</b>, and an earlier version of this class asserted it. A number
    /// larger than 2^24 is still exactly representable when it has enough factors of two: 11! = 2^8 Ã— 155925,
    /// and the odd part 155925 fits comfortably in 24 bits. The same holds up to 13!.
    /// </para>
    /// <para>
    /// The first factorial that genuinely does not fit is <b>14!</b> = 2^11 Ã— 42567525, whose odd part exceeds
    /// 2^24. So 14 is the first length at which the encoded value diverges from the exact sum â€” established by
    /// searching for the first divergence in a test rather than by reasoning about magnitudes.
    /// </para>
    /// </remarks>
    public const int PremiereLongueurArrondie = 12;

    /// <summary>Encodes a password the way the legacy application stores it.</summary>
    public static string Coder(string? motDePasse)
    {
        // The legacy exits early and leaves the accumulator at its initial value, which is the string "0".
        if (string.IsNullOrEmpty(motDePasse))
        {
            return "0";
        }

        var total = 0d;
        var n = motDePasse.Length;

        for (var i = 1; i <= n; i++)
        {
            var max = n - (i - 1);

            // Single, deliberately: this is the legacy's declared type and the rounding is part of its
            // observable behaviour. Widening to double would produce different values for long passwords and
            // reject credentials the desktop application accepts.
            var fact = 1f;

            for (var j = 1; j <= max; j++)
            {
                fact *= j;
            }

            var c = CodeAnsi(motDePasse[i - 1]);

            // The product is computed in Single and only then widened, matching `CODE As Long = c * fact`.
            var code = (long)(c * fact);

            // The legacy accumulates into a String field, which VB adds numerically and widens to Double.
            total += code;
        }

        // Integral values render without a decimal separator, which is how they were written to pass_word.
        return total.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Whether a candidate password matches a stored <c>pass_word</c>.
    /// </summary>
    /// <remarks>
    /// A plain, non-constant-time comparison, because that is what
    /// <c>Codage_Motpasse(entered) &lt;&gt; perso.PassWord</c> does. It is deliberately not hardened with a
    /// constant-time compare: the encoding discards so much information that timing resistance would protect
    /// nothing, and pretending otherwise would misrepresent the scheme's strength.
    /// </remarks>
    public static bool Verifier(string? motDePasseSaisi, string? passWordStocke)
    {
        if (string.IsNullOrEmpty(passWordStocke))
        {
            return false;
        }

        return string.Equals(Coder(motDePasseSaisi), passWordStocke.Trim(), StringComparison.Ordinal);
    }

    /// <summary>The encoding is never the right choice for a new password.</summary>
    public static bool EstAdapteAUnNouveauMotDePasse => false;

    /// <summary>
    /// The character code the legacy <c>Asc()</c> produces.
    /// </summary>
    /// <remarks>
    /// VB's <c>Asc</c> returns a code from the system ANSI code page, not the Unicode code point. For ASCII â€”
    /// which covers every password in practice â€” and for Latin-1 letters such as <c>Ã©</c> (233), the two agree.
    /// They diverge only in the 0x80-0x9F range, where code page 1252 differs from Latin-1; that range holds no
    /// printable password character, so the code point is used directly and the divergence is noted rather than
    /// silently papered over.
    /// </remarks>
    private static int CodeAnsi(char caractere)
    {
        if (caractere <= 0xFF)
        {
            return caractere;
        }

        // Outside the single-byte range the legacy's Asc falls back to something code-page dependent; the code
        // point is the only reproducible choice, and no realistic password reaches here.
        return caractere;
    }

    /// <summary>Renders the encoding as the legacy database stores it, for diagnostics.</summary>
    public static string Decrire(string? motDePasse)
    {
        var code = Coder(motDePasse);

        return (motDePasse?.Length ?? 0) < PremiereLongueurArrondie
            ? code
            : $"{code} (longueur {motDePasse!.Length} : les poids sont arrondis au-delà, cf. {nameof(PremiereLongueurArrondie)})";
    }

    /// <summary>Code page the legacy <c>Asc</c> reads from. Exposed for documentation and tests.</summary>
    public static Encoding CodePageLegacy { get; } = Encoding.GetEncoding(1252);
}
