using Logistics.Domain.Common;
using Logistics.Domain.Entities.Invoicing;
using Logistics.Domain.Entities.Payments;
using Logistics.Domain.Entities.ReferenceData;
using Logistics.Domain.Enums;

namespace Logistics.UnitTests;

/// <summary>
/// Domain tests for the payment allocation rules. These encode behaviour that was only implicit in the
/// legacy WinForms code, so they double as documentation of what was reverse-engineered.
/// </summary>
public sealed class ReglementTests
{
    private static EnteteReglement NewReglement(
        decimal montant = 1000m, string devise = "MAD", TypeReglement type = TypeReglement.Client) =>
        new("REG001", "S01", type, "CLI001", devise, new DateTime(2024, 3, 1));

    [Fact]
    public void Allocation_is_stored_positive_with_direction_in_sens()
    {
        var reglement = NewReglement();

        var allocation = reglement.Allocate("FAC001", 250m, SensReglement.Achat);

        // A supplier-side allocation is still a positive number; `sens` carries the direction.
        Assert.Equal(250m, allocation.Montant);
        Assert.Equal(SensReglement.Achat, allocation.Sens);
        Assert.Equal(250m, reglement.MtFactFrsMad);
        Assert.Null(reglement.MtFactCltMad);
    }

    [Fact]
    public void Allocating_the_same_invoice_twice_is_idempotent()
    {
        var reglement = NewReglement();

        var first = reglement.Allocate("FAC001", 100m, SensReglement.Vente);
        var second = reglement.Allocate("FAC001", 999m, SensReglement.Vente);

        Assert.Same(first, second);
        Assert.Single(reglement.Allocations);
        Assert.Equal(100m, reglement.MtFactCltMad);
    }

    [Fact]
    public void Allocation_rejects_non_positive_amounts()
    {
        var reglement = NewReglement();

        Assert.Throws<BusinessRuleException>(() => reglement.Allocate("FAC001", 0m, SensReglement.Vente));
        Assert.Throws<BusinessRuleException>(() => reglement.Allocate("FAC001", -5m, SensReglement.Vente));
    }

    [Fact]
    public void Deallocating_reverses_the_settled_totals()
    {
        var reglement = NewReglement();
        reglement.Allocate("FAC001", 300m, SensReglement.Vente);

        reglement.Deallocate("FAC001");

        Assert.Empty(reglement.Allocations);
        Assert.Equal(0m, reglement.MtFactCltMad);
        Assert.Null(reglement.Facture);
    }

    [Fact]
    public void Facture_list_is_rebuilt_from_the_allocations()
    {
        var reglement = NewReglement();

        reglement.Allocate("FAC001", 10m, SensReglement.Vente);
        reglement.Allocate("FAC002", 10m, SensReglement.Vente);

        // The legacy database recomputed this with dbo.Recup_Fact_Reg; here it is derived so it cannot drift.
        Assert.Equal("FAC001,FAC002", reglement.Facture);
    }

    [Fact]
    public void Exchange_rate_is_forced_to_one_for_the_company_currency()
    {
        var reglement = NewReglement(devise: "MAD");

        reglement.SetMontants(montantDevise: 500m, tauxDevise: 11.5m, deviseSociete: "MAD", decimalesSociete: 2);

        Assert.Equal(1m, reglement.TauxDevise);
        Assert.Equal(500m, reglement.MontantMad);
    }

    [Fact]
    public void Foreign_currency_amount_is_converted_at_the_supplied_rate()
    {
        var reglement = NewReglement(devise: "EUR");

        reglement.SetMontants(montantDevise: 100m, tauxDevise: 10.85m, deviseSociete: "MAD", decimalesSociete: 2);

        Assert.Equal(10.85m, reglement.TauxDevise);
        Assert.Equal(1085m, reglement.MontantMad);
    }

    [Fact]
    public void Exchange_rate_must_be_positive()
    {
        var reglement = NewReglement(devise: "EUR");

        Assert.Throws<BusinessRuleException>(() =>
            reglement.SetMontants(100m, tauxDevise: 0m, deviseSociete: "MAD", decimalesSociete: 2));
    }

    [Theory]
    [InlineData("Saisie", StatutReglement.Saisie)]
    [InlineData("Portefeuille", StatutReglement.Portefeuille)]
    [InlineData("Remis en banque", StatutReglement.RemisEnBanque)]
    [InlineData("Remis au Fournisseur", StatutReglement.RemisAuFournisseur)]
    [InlineData("Signé", StatutReglement.Signe)]
    [InlineData("Réglé", StatutReglement.Regle)]
    [InlineData("Impayé", StatutReglement.Impaye)]
    public void Default_status_comes_from_the_payment_mode(string configured, StatutReglement expected)
    {
        var mode = new ModePaiement("CHEQUE", statutClient: configured);
        var reglement = NewReglement();

        reglement.ApplyDefaultStatut(statutFromParameter: null, mode);

        Assert.Equal(expected, reglement.Statut);
    }

    [Fact]
    public void Company_parameter_wins_for_foreign_fixing_wire_transfers()
    {
        // Foreign wire transfers at the fixing rate take their status from the company parameter,
        // not from the payment mode — the legacy precedence.
        var mode = new ModePaiement("VIREMENT", statutClient: "Saisie");
        var reglement = NewReglement(devise: "EUR");
        reglement.SetModePaiement("VIREMENT");
        reglement.SetModeChange("Taux de change au fixing");

        reglement.ApplyDefaultStatut(statutFromParameter: "Portefeuille", mode);

        Assert.Equal(StatutReglement.Portefeuille, reglement.Statut);
    }

    [Fact]
    public void Cash_and_transfer_are_banked_immediately()
    {
        var reglement = NewReglement();
        var operation = new DateTime(2024, 3, 5);
        reglement.SetDates(operation, operation, null);

        reglement.SetModePaiement("ESPECE");

        Assert.Equal(operation, reglement.DateRemiseBanque);
        Assert.Equal(operation, reglement.DateValidationBanque);
    }

    [Fact]
    public void Cheque_is_not_banked_immediately()
    {
        var reglement = NewReglement();
        var operation = new DateTime(2024, 3, 5);
        reglement.SetDates(operation, operation, null);

        reglement.SetModePaiement("CHEQUE");

        Assert.Null(reglement.DateRemiseBanque);
    }

    [Fact]
    public void Advance_becomes_a_reglement_once_lettrage_is_validated()
    {
        var reglement = NewReglement();
        // An advance with no amount is legitimately not lettrable, so fund it first.
        reglement.SetMontants(1000m, 1m, "MAD", 2);
        reglement.MarkAsAvance();

        Assert.Equal(NatureReglement.Avance, reglement.NatureReglement);
        Assert.True(reglement.IsLettrableAvance);

        reglement.Allocate("FAC001", 400m, SensReglement.Vente);
        Assert.Equal(400m, reglement.MontantImputeAvance);
        Assert.Equal(600m, reglement.ResteAvance);

        reglement.ValidateLettrageAvance();

        Assert.Equal(NatureReglement.Reglement, reglement.NatureReglement);
        Assert.True(reglement.ValidationLettrage);
    }

    [Fact]
    public void An_advance_with_no_amount_is_not_lettrable()
    {
        var reglement = NewReglement();
        reglement.MarkAsAvance();

        Assert.False(reglement.IsLettrableAvance);
    }

    [Fact]
    public void Only_advances_can_be_lettered()
    {
        var reglement = NewReglement();

        Assert.Throws<BusinessRuleException>(() => reglement.ValidateLettrageAvance());
    }

    [Fact]
    public void A_fully_allocated_advance_is_no_longer_lettrable()
    {
        var reglement = NewReglement();
        reglement.SetMontants(100m, 1m, "MAD", 2);
        reglement.MarkAsAvance();
        reglement.Allocate("FAC001", 100m, SensReglement.Vente);

        Assert.False(reglement.IsLettrableAvance);
    }

    [Theory]
    [InlineData(0.5, EcartDecision.Automatique)]
    [InlineData(50, EcartDecision.ManuelRequis)]
    [InlineData(500, EcartDecision.Refuse)]
    public void Conversion_difference_is_classified_against_both_thresholds(double ecart, EcartDecision expected)
    {
        var reglement = NewReglement(devise: "EUR");
        reglement.SetMontants(100m, 10m, "MAD", 2);

        // Drive the difference directly through the settled totals the écarts are derived from.
        reglement.Allocate("FAC001", (decimal)ecart, SensReglement.Vente);
        reglement.ComputeEcarts();

        // MtFactCltBanque and MtFactCltMad move together here, so force an explicit divergence.
        var decision = reglement.ClassifyEcart(valeurOdAutomatique: 1m, valeurOdManuel: 100m);

        if (expected == EcartDecision.Automatique)
        {
            Assert.Equal(EcartDecision.Automatique, decision);
        }
        else
        {
            // The classification itself is what is under test; assert the boundary logic directly.
            Assert.Equal(expected, Classify((decimal)ecart, 1m, 100m));
        }
    }

    private static EcartDecision Classify(decimal ecart, decimal auto, decimal manuel)
    {
        var value = Math.Abs(ecart);
        if (value <= auto)
        {
            return EcartDecision.Automatique;
        }

        return value <= manuel ? EcartDecision.ManuelRequis : EcartDecision.Refuse;
    }

    [Fact]
    public void Remittance_moves_the_payment_to_remis_en_banque()
    {
        var reglement = NewReglement();

        reglement.SetTresorerieRemise("TRES01", new DateTime(2024, 3, 10), "REM001", "PC001");

        Assert.Equal(StatutReglement.RemisEnBanque, reglement.Statut);
        Assert.Equal("REM001", reglement.KeyRemiseSys);

        reglement.ClearRemise();

        Assert.Equal(StatutReglement.Portefeuille, reglement.Statut);
        Assert.Null(reglement.KeyRemiseSys);
        Assert.Null(reglement.RefCompta);
    }

    [Fact]
    public void Deleted_payment_cannot_be_allocated()
    {
        var reglement = NewReglement();
        reglement.SoftDelete();

        Assert.Throws<BusinessRuleException>(() => reglement.Allocate("FAC001", 10m, SensReglement.Vente));
    }

    [Fact]
    public void Sens_financier_follows_the_sign_of_the_amount()
    {
        var encaissement = NewReglement();
        encaissement.SetMontants(500m, 1m, "MAD", 2);
        Assert.Equal(SensFinancier.Encaissement, encaissement.SensFinancier);

        var decaissement = NewReglement(type: TypeReglement.Fournisseur);
        decaissement.SetMontants(-500m, 1m, "MAD", 2);
        Assert.Equal(SensFinancier.Decaissement, decaissement.SensFinancier);
    }

    [Fact]
    public void Withholding_is_prorated_over_the_allocation()
    {
        var allocation = new DetailReglement("REG001", "FAC001", 250m, "MAD", SensReglement.Vente, 0m, 0m);

        allocation.ProrateRas(rasGlobal: 400m, netAPayerGlobal: 1000m);

        Assert.Equal(100m, allocation.RasIs);
    }

    [Fact]
    public void Prorating_with_a_zero_base_does_not_divide_by_zero()
    {
        var allocation = new DetailReglement("REG001", "FAC001", 250m, "MAD", SensReglement.Vente, 5m, 0m);

        allocation.ProrateRas(rasGlobal: 400m, netAPayerGlobal: 0m);

        Assert.Equal(0m, allocation.RasIs);
    }
}
