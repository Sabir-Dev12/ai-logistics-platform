using Logistics.Api.Controllers;
using Logistics.Application.Abstractions;
using Logistics.Application.Features.Payments;
using Logistics.Domain.Entities.Clients;
using Logistics.Domain.Entities.Payments;
using Logistics.Domain.Entities.Resources;
using Logistics.Domain.Enums;
using Logistics.Infrastructure;
using Logistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Logistics.UnitTests;

public sealed class PaymentSearchTests
{
    [Theory]
    [InlineData(" VIR-12 ", null, 1)]
    [InlineData("PAY", null, 3)]
    [InlineData("Atlas", null, 1)]
    [InlineData(null, "Atlas", 1)]
    [InlineData(null, "Martin", 1)]
    [InlineData("VIR", "Atlas", 1)]
    [InlineData("VIR", "Martin", 0)]
    [InlineData("absent", null, 0)]
    [InlineData(" ", " ", 3)]
    public void Partial_search_and_party_filters_combine_before_paging(string? search, string? party, int expected)
    {
        var rows = new[] { Payment("PAY1", TypeReglement.Client, "C1", "VIR-123"), Payment("PAY2", TypeReglement.Fournisseur, "C1", "CHQ-1"), Payment("PAY3", TypeReglement.Personnel, "P1", null) };
        var clients = new[] { new Client("C1", "S01", "Atlas Import") }.AsQueryable();
        var people = new[] { new Personnel("P1", "S01", "Martin", "Ali") }.AsQueryable();
        var query = ReglementRepository.ApplyTextFilters(rows.AsQueryable(), clients, people, search, party);
        Assert.Equal(expected, query.Count());
        Assert.Equal(Math.Min(expected, 1), query.Take(1).Count());
    }

    [Fact]
    public void Search_with_directory_subqueries_translates_to_sql()
    {
        using var db = new LogisticsDbContext(new DbContextOptionsBuilder<LogisticsDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options);
        var query = ReglementRepository.ApplyTextFilters(db.EntetesReglement.AsNoTracking(), db.Clients, db.Personnel, "VIR", "Atlas")
            .Where(r => r.TypeReglement == TypeReglement.Client).OrderBy(r => r.DateOperation).Take(5);
        Assert.Contains("SELECT", query.ToQueryString());
    }

    [Fact]
    public async Task Controller_and_service_preserve_exact_key_filter_and_forward_new_text_filters()
    {
        using var cancel = new CancellationTokenSource();
        var repo = PaymentApiContractTests.Proxy.Make<IReglementRepository>((method, args) =>
        {
            Assert.Equal("SearchAsync", method.Name); Assert.Equal("C1", args![1]);
            Assert.Equal(cancel.Token, args[7]); Assert.Equal("VIR", args[8]); Assert.Equal("Atlas", args[9]);
            return Task.FromResult(new PagedResult<EnteteReglement>([], 2, 5, 0));
        });
        var service = new ReglementService(repo, null!, null!, null!, null!, null!);
        var result = await new ReglementsController(service).Search(TypeReglement.Client, "C1", null, null, null, 2, 5, cancel.Token, "VIR", "Atlas");
        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
    }

    private static EnteteReglement Payment(string id, TypeReglement type, string key, string? reference)
    {
        var payment = new EnteteReglement(id, "S01", type, key, "MAD", new DateTime(2026, 10, 21));
        payment.SetReferencePaiement(reference); return payment;
    }
}
