namespace Logistics.Domain.Entities.Resources;

/// <summary>
/// The full-tank-to-full-tank cost and consumption window.
/// </summary>
/// <remarks>
/// <para>
/// Transcribed from <c>frm_Calcul_Ratio_CoutKm_Materiel.vb:220-247</c>. Fuel consumption is only measurable
/// between two <b>full tanks</b>: you know how many litres went in and how much they cost, and the odometer
/// tells you the distance covered. That is why the journal carries a <c>plein</c> flag at all — without it
/// there is no way to close a measurement window.
/// </para>
/// <para>
/// <b>The window is summed and marked with different bounds, and that asymmetry is deliberate.</b> The
/// closing fill-up's fuel is counted in this window, but the closing row is <em>not</em> marked as consumed,
/// so it becomes the opening fill-up of the next window. Marking it too would end the chain after one
/// measurement; making both bounds the same the other way would count the opening fill's fuel twice.
/// </para>
/// </remarks>
public static class RatioCoutKm
{
    /// <summary>
    /// Whether a movement falls inside the window for <b>totalling</b>.
    /// </summary>
    /// <remarks>
    /// Open at the start, closed at the end: <c>date &gt; debut &amp;&amp; date &lt;= fin</c>. The legacy sums
    /// with exactly these bounds.
    /// </remarks>
    public static bool DansLaSomme(DateTime date, DateTime debut, DateTime fin) => date > debut && date <= fin;

    /// <summary>
    /// Whether a movement is <b>marked</b> as consumed.
    /// </summary>
    /// <remarks>
    /// Closed at the start, open at the end: <c>date &gt;= debut &amp;&amp; date &lt; fin</c>. The legacy marks
    /// with exactly these bounds — the closing fill-up stays unmarked on purpose, so the chain continues.
    /// </remarks>
    public static bool DansLeMarquage(DateTime date, DateTime debut, DateTime fin) => date >= debut && date < fin;

    /// <summary>
    /// The measurement from an opening and closing fill-up.
    /// </summary>
    /// <param name="compteurDebut">Odometer at the opening fill-up.</param>
    /// <param name="compteurFin">Odometer at the closing fill-up.</param>
    /// <param name="litres">Total litres put in across the window.</param>
    /// <param name="montant">Total cost across the window.</param>
    /// <returns>
    /// The distance covered, or null when the measurement cannot be made.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>All three of distance, litres and cost must be non-zero</b>, which is the legacy's own guard
    /// (line 244). Returning null rather than a row of zeros matters: a zero-cost or zero-distance row would
    /// look like a real measurement reading zero, and the ratio reports would average it in.
    /// </para>
    /// <para>
    /// A closing odometer below the opening one yields null rather than a negative distance. The legacy would
    /// compute a negative <c>total_km</c> and write a nonsensical negative cost per kilometre; a meter that
    /// went backwards is a data problem, not a measurement.
    /// </para>
    /// </remarks>
    public static int? Distance(int compteurDebut, int compteurFin, decimal litres, decimal montant)
    {
        var distance = compteurFin - compteurDebut;

        if (distance <= 0 || litres == 0m || montant == 0m)
        {
            return null;
        }

        return distance;
    }

    /// <summary>
    /// Whether the closing fill-up can be identified at all.
    /// </summary>
    /// <remarks>
    /// A window needs a second full tank. With only one, the vehicle is still on the fuel it took on and
    /// nothing can be measured yet — the legacy reports the vehicle as uncosted rather than guessing.
    /// </remarks>
    public static bool FenetreFermable(DateTime? dateDebut, DateTime? dateFin) =>
        dateDebut.HasValue && dateFin.HasValue && dateFin.Value > dateDebut.Value;
}
