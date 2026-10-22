using Logistics.Infrastructure;
using Logistics.Application.Abstractions;
using Logistics.Application.Features.Clients;
using Logistics.Api.Controllers;
using Logistics.Domain.Entities.Clients;
using Logistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Logistics.UnitTests;

public sealed class ClientSearchTranslationTests
{
    [Theory]
    [InlineData(null, 3)]
    [InlineData(true, 2)]
    [InlineData(false, 1)]
    public void Validation_is_optional_and_applied_before_count_and_paging(bool? validated, int expected)
    {
        var rows = new[] { new Client("C1", "S01", "Alpha"), new Client("C2", "S01", "Beta"), new Client("C3", "S01", "Gamma") };
        rows[0].Validate(false, false); rows[1].Validate(false, false);
        var query = ClientRepository.BuildSearchQuery(rows.AsQueryable(), null, null, null, null, validated);
        Assert.Equal(expected, query.Count());
        var firstPage = query.OrderBy(client => client.RaisonSociale).Take(1).ToArray();
        Assert.Single(firstPage);
        if (validated.HasValue) Assert.Equal(validated.Value, firstPage[0].Valide);
    }

    [Fact]
    public void Validation_combines_with_activity_and_search_without_filtering_only_one_page()
    {
        var draft = new Client("C1", "S01", "Atlas draft"); draft.Activate();
        var validated = new Client("C2", "S01", "Atlas validated"); validated.Activate(); validated.Validate(false, false);
        var inactive = new Client("C3", "S01", "Atlas inactive");
        var query = ClientRepository.BuildSearchQuery(new[] { draft, validated, inactive }.AsQueryable(), "Atlas", null, null, true, false);
        Assert.Equal("C1", Assert.Single(query).KeyClient);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Controller_and_service_forward_validation_to_the_repository(bool validated)
    {
        using var source = new CancellationTokenSource();
        var repository = PaymentApiContractTests.Proxy.Make<IClientRepository>((method, args) =>
        {
            Assert.Equal("SearchAsync", method.Name);
            Assert.Equal(source.Token, args![6]); Assert.Equal(validated, args[7]);
            return Task.FromResult(new PagedResult<Client>([], 2, 5, 0));
        });
        var service = new ClientService(repository, null!, null!, null!, null!);
        var result = await new ClientsController(service).Search("Atlas", null, null, true, 2, 5, source.Token, validated);
        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
    }

    [Theory]
    [InlineData("ALPHA", null, null, null, null)]
    [InlineData("CLI001", "Actif", "A", true, true)]
    [InlineData(null, null, null, false, false)]
    public void Search_filters_translate_to_sql_before_paging(string? search, string? status, string? classification, bool? active, bool? validated)
    {
        using var db = new LogisticsDbContext(new DbContextOptionsBuilder<LogisticsDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options);
        // This uses the actual repository predicate. KeyClient is an unmapped alias:
        // referencing it here used to throw before any SQL command could execute.
        var query = ClientRepository.BuildSearchQuery(db.Clients.AsNoTracking(), search, status, classification, active, validated)
            .OrderBy(client => client.RaisonSociale).Skip(0).Take(5);
        Assert.Contains("SELECT", query.ToQueryString());
    }
}
