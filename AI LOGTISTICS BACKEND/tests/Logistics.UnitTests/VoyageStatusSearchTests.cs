using Logistics.Application.Abstractions;
using Logistics.Application.Features.Shipments;
using Logistics.Api.Controllers;
using Logistics.Domain.Common;
using Logistics.Domain.Entities.Shipments;
using Logistics.Infrastructure;
using Logistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Logistics.UnitTests;

public sealed class VoyageStatusSearchTests
{
    [Theory]
    [InlineData(null, 3)]
    [InlineData(VoyageSearchStatus.Brouillon, 1)]
    [InlineData(VoyageSearchStatus.Valide, 1)]
    [InlineData(VoyageSearchStatus.Cloture, 1)]
    public void Statuses_are_distinct_and_applied_before_count_and_paging(VoyageSearchStatus? status, int count)
    {
        var rows = Rows();
        var filtered = VoyageRepository.ApplyStatusFilter(rows.AsQueryable(), status);
        Assert.Equal(count, filtered.Count());
        if (status.HasValue) Assert.Equal(rows[(int)status.Value].Id, Assert.Single(filtered.Take(1)).Id);
        // The previous API deliberately included closed voyages in validatedOnly.
        Assert.Equal(2, VoyageRepository.ApplyStatusFilter(rows.AsQueryable(), null).Count(v => v.DateVoyage != null));
        rows[2].Reopen();
        Assert.Equal(2, VoyageRepository.ApplyStatusFilter(rows.AsQueryable(), VoyageSearchStatus.Valide).Count());
    }

    [Theory]
    [InlineData(VoyageSearchStatus.Brouillon)]
    [InlineData(VoyageSearchStatus.Valide)]
    [InlineData(VoyageSearchStatus.Cloture)]
    public void Status_query_translates_to_sql(VoyageSearchStatus status)
    {
        using var db = new LogisticsDbContext(new DbContextOptionsBuilder<LogisticsDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options);
        var sql = VoyageRepository.ApplyStatusFilter(db.Voyages.AsNoTracking(), status).OrderBy(v => v.DateCreation).Take(5).ToQueryString();
        Assert.Contains("SELECT", sql); Assert.Contains("IS NULL", sql.Replace("IS NOT NULL", "IS NULL"));
    }

    [Theory]
    [InlineData(VoyageSearchStatus.Brouillon)]
    [InlineData(VoyageSearchStatus.Valide)]
    [InlineData(VoyageSearchStatus.Cloture)]
    public async Task Controller_and_service_forward_status_and_legacy_flag(VoyageSearchStatus status)
    {
        using var cancel = new CancellationTokenSource();
        var repository = PaymentApiContractTests.Proxy.Make<IVoyageRepository>((method, args) =>
        {
            Assert.Equal("SearchAsync", method.Name); Assert.Equal(true, args![5]);
            Assert.Equal(cancel.Token, args[8]); Assert.Equal(status, args[9]);
            return Task.FromResult(new PagedResult<Voyage>([], 1, 5, 0));
        });
        var service = new VoyageService(repository, null!, null!, null!, null!);
        var controller = new VoyagesController(service, null!, null!);
        var result = await controller.Search(null, null, null, null, null, true, 1, 5, cancel.Token, status);
        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task Unknown_status_is_rejected_before_repository_access()
    {
        var service = new VoyageService(null!, null!, null!, null!, null!);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => service.SearchAsync(null, null, null, null, null, null, 1, 5, statut: (VoyageSearchStatus)99));
        Assert.Contains("Statut", error.Message);
    }

    private static Voyage[] Rows()
    {
        var date = new DateTime(2026, 10, 21);
        var rows = Enumerable.Range(0, 3).Select(i => new Voyage($"V{i}", "S01", null, $"Voyage {i}", date)).ToArray();
        rows[1].Validate(date); rows[2].Validate(date); rows[2].Close(date, false);
        return rows;
    }
}
