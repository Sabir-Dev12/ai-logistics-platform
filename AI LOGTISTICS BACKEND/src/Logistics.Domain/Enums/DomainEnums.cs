namespace Logistics.Domain.Enums;

/// <summary>
/// Customs direction of a dossier type.
/// </summary>
/// <remarks>
/// Legacy storage is the free-text French label <c>t_type_dossier.SENS</c> with the exact values
/// "Import" / "Export" / "National"; queries filter with <c>where SENS in (...)</c>.
/// </remarks>
public enum Sens
{
    Import,
    Export,
    National
}

/// <summary>
/// Discriminator on <c>t_entete_reglement</c> deciding which ledger the counterparty belongs to.
/// Legacy values: 'Client' | 'Fournisseur' | 'Etrangers' | 'Personnel' (compared UPPER()-wrapped).
/// </summary>
public enum TypeReglement
{
    Client,
    Fournisseur,
    Etrangers,
    Personnel
}

/// <summary>
/// <c>t_entete_reglement.nature_reglement</c>. An AVANCE is an unallocated customer advance that
/// becomes a REGLEMENT once its lettrage is validated.
/// </summary>
public enum NatureReglement
{
    Reglement,
    Avance
}

/// <summary>
/// <c>t_entete_reglement.statut</c>. The default on creation is NOT hard-coded: it is read from
/// <c>t_mode_paiement.statut_client | statut_frs | statut_agent</c>, or from the company parameter
/// <c>param_statut_modePaiement_etrangers</c> for foreign wire transfers.
/// </summary>
public enum StatutReglement
{
    Saisie,
    Portefeuille,
    RemisEnBanque,
    RemisAuFournisseur,
    Signe,
    Regle,
    Impaye
}

/// <summary>
/// <c>t_detail_reglement.sens</c>: direction of an allocation. Amounts are stored positive; the
/// sign lives here. 'V' = vente (client side), 'A' = achat (supplier side).
/// </summary>
public enum SensReglement
{
    Vente,
    Achat
}

/// <summary><c>t_entete_reglement.type_lettrage</c> ('T' or 'P').</summary>
public enum TypeLettrage
{
    T,
    P
}

/// <summary><c>t_tresorie.type_tresorie</c>: 'B' = Banque, 'C' = Caisse.</summary>
public enum TypeTresorie
{
    Banque,
    Caisse
}

/// <summary>
/// How often a client wants a statement of account
/// (<c>t_clients.releve_facture</c>: 'H' Hebdomadaire, 'Q' Quinzaine, 'M' Mensuel, '' none).
/// </summary>
public enum PeriodiciteReleve
{
    Aucune,
    Hebdomadaire,
    Quinzaine,
    Mensuel
}

/// <summary>
/// Financial direction of a payment as exposed by the <c>v_reglement</c> view:
/// 'Encaissement' when <c>montant_devise &gt; 0</c>, 'Décaissement' when negative.
/// </summary>
public enum SensFinancier
{
    Encaissement,
    Decaissement
}

/// <summary>
/// VAT-declaration lifecycle shared by <c>t_entete_reglement.statut_declaration_tva</c> and
/// <c>t_frais_bancaires.statut_declaration</c>.
/// </summary>
public enum StatutDeclarationTva
{
    AttenteDeclaration,
    Prepare,
    Declare
}

/// <summary>
/// Polymorphic owner discriminator used by <c>t_contact_tiers</c>, <c>t_banque_tiers</c> and
/// <c>t_document_tiers</c>: 'C' client, 'F' fournisseur, 'A' agent, 'T' tiers.
/// </summary>
public enum TypeTiers
{
    Client,
    Fournisseur,
    Agent,
    Tiers
}
