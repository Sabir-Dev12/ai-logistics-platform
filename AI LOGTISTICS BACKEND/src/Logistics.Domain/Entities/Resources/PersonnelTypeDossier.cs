using Logistics.Domain.Common;

namespace Logistics.Domain.Entities.Resources;

/// <summary>
/// Which dossier types an employee may handle (<c>t_personnel_type_dossier</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>No tenant column and no surrogate key observed</b> — the pair <c>(key_personnel, type_dossier)</c>
/// is the identity, and company scope is reached through the employee.
/// </para>
/// <para>
/// <b>The empty-set semantics are unverified.</b> The schema extraction established the shape and the
/// columns but not what it means for an employee to have <em>no</em> rows here: it could mean "unrestricted"
/// or "nothing at all". Guessing wrong either leaks dossier types within a company or locks every operator
/// out the moment the API is deployed. The enforcement therefore treats an <b>empty set as unrestricted</b>
/// and says so at token issuance, because a false restriction is an outage while a false permission is a
/// visible, correctable gap — and the desktop application remains the authority meanwhile. Confirm with:
/// <c>select count(*) from t_personnel_type_dossier</c> against the live database.
/// </para>
/// </remarks>
public sealed class PersonnelTypeDossier : Entity
{
    private PersonnelTypeDossier() { }

    public PersonnelTypeDossier(string keyPersonnel, string typeDossier)
    {
        Id = $"{keyPersonnel}:{typeDossier}";
        KeyPersonnel = Require(keyPersonnel, nameof(keyPersonnel));
        TypeDossier = Require(typeDossier, nameof(typeDossier));
    }

    public string KeyPersonnel { get; private set; } = string.Empty;
    /// <summary>Dossier type code, matching <c>t_type_dossier.type_dossier</c>.</summary>
    public string TypeDossier { get; private set; } = string.Empty;

    private static string Require(string value, string param) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", param) : value.Trim();
}
