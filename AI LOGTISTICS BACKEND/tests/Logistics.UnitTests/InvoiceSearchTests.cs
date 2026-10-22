using Logistics.Application.Abstractions;
using Logistics.Application.Features.Invoicing;
using Logistics.Api.Controllers;
using Logistics.Domain.Entities.Invoicing;
using Logistics.Infrastructure;
using Logistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Logistics.UnitTests;

public sealed class InvoiceSearchTests
{
    [Theory]
    [InlineData(" FV-2026 ", 2)]
    [InlineData("REF-A", 1)]
    [InlineData("Atlas", 2)]
    [InlineData("absent", 0)]
    [InlineData("  ", 3)]
    [InlineData(null, 3)]
    public void Partial_search_matches_number_reference_or_client_before_count_and_paging(string? search, int expected)
    {
        var rows = new[] { Invoice("REF-A", "FV-2026-01", "Atlas Import"), Invoice("REF-B", "FV-2026-02", "Atlas Export"), Invoice("REF-C", null, "Beta") };
        var query = FactureVenteRepository.ApplySearchFilter(rows.AsQueryable(), search);
        Assert.Equal(expected, query.Count());
        Assert.Equal(Math.Min(1, expected), query.Take(1).Count());
    }

    [Fact]
    public void Search_combines_with_other_predicates_and_translates_to_sql_using_the_mapped_key()
    {
        using var db = new LogisticsDbContext(new DbContextOptionsBuilder<LogisticsDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options);
        var query = FactureVenteRepository.ApplySearchFilter(db.EntetesFactureVente.AsNoTracking(), "FV-2026")
            .Where(f => f.KeyClient == "C1" && !f.FactRegle).OrderBy(f => f.DateFacture).Take(5);
        Assert.Contains("SELECT", query.ToQueryString());
    }

    [Fact]
    public async Task Controller_and_service_forward_search_and_existing_filters_to_repository()
    {
        using var cancel = new CancellationTokenSource();
        var repository = PaymentApiContractTests.Proxy.Make<IFactureVenteRepository>((method, args) =>
        {
            Assert.Equal("SearchAsync", method.Name); Assert.Equal("C1", args![0]);
            Assert.Equal(false, args[3]); Assert.Equal(cancel.Token, args[8]); Assert.Equal("FV-2026", args[9]);
            return Task.FromResult(new PagedResult<EnteteFactureVente>([], 2, 5, 0));
        });
        var service = new FactureVenteService(repository, null!, null!, null!, null!);
        var result = await new FacturesVenteController(service).Search("C1", null, null, false, null, null, 2, 5, cancel.Token, "FV-2026");
        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
    }

    private static EnteteFactureVente Invoice(string id, string? number, string name) => new(id, "S01", number, TypeFacture.Facture, "C1", name, "MAD", new DateTime(2026, 10, 21));
}
