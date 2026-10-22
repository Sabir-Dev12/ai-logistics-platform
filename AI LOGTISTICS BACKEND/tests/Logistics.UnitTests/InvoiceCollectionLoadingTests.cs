using Logistics.Domain.Entities.Invoicing;
using Logistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Logistics.UnitTests;

public sealed class InvoiceCollectionLoadingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Tracking_populates_invoice_lines_and_vat_in_either_loading_order(bool childrenFirst)
    {
        using var db = Context();
        var invoice = Invoice();
        var line = Line();
        var vat = Vat();
        if (!childrenFirst) db.Attach(invoice);
        db.Attach(line);
        db.Attach(vat);
        if (childrenFirst) db.Attach(invoice);

        Assert.Same(line, Assert.Single(invoice.Lignes));
        Assert.Same(vat, Assert.Single(invoice.TvaDetail));
        Assert.All(db.ChangeTracker.Entries(), entry => Assert.Equal(EntityState.Unchanged, entry.State));
    }

    [Fact]
    public void Query_materialization_can_populate_both_collections()
    {
        using var db = Context();
        var invoice = Invoice();
        var line = Line();
        var vat = Vat();
        var entity = db.Model.FindEntityType(typeof(EnteteFactureVente))!;

        entity.FindNavigation(nameof(EnteteFactureVente.Lignes))!.GetCollectionAccessor()!
            .Add(invoice, line, forMaterialization: true);
        entity.FindNavigation(nameof(EnteteFactureVente.TvaDetail))!.GetCollectionAccessor()!
            .Add(invoice, vat, forMaterialization: true);

        Assert.Same(line, Assert.Single(invoice.Lignes));
        Assert.Same(vat, Assert.Single(invoice.TvaDetail));
    }

    [Fact]
    public void Public_collections_remain_read_only()
    {
        var invoice = Invoice();
        Assert.Throws<NotSupportedException>(() => ((ICollection<DetailFactureVente>)invoice.Lignes).Add(Line()));
        Assert.Throws<NotSupportedException>(() => ((ICollection<TvaFactureVente>)invoice.TvaDetail).Add(Vat()));
    }

    private static LogisticsDbContext Context() => new(new DbContextOptionsBuilder<LogisticsDbContext>()
        .UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options);
    private static EnteteFactureVente Invoice() => new("F1", "S01", "TEST-F1", TypeFacture.Facture,
        "C1", "Client test", "MAD", new DateTime(2026, 10, 19));
    private static DetailFactureVente Line() => new("L1", "F1", "Fret", NatureLigne.Produit, 100m, 0m, 1m, 0.2m);
    private static TvaFactureVente Vat() => new("F1", "20%", "S01", 100m, 20m);
}
