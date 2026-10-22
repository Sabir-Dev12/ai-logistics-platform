using System.Text.Json;
using Logistics.Application.Abstractions;
using Logistics.Application.Features.Clients;
using Logistics.Domain.Entities.Clients;

namespace Logistics.UnitTests;

public sealed class ClientDetailContractTests
{
    [Fact]
    public async Task Detail_preserves_assignment_fields_when_round_tripped_to_the_save_contract()
    {
        var client = new Client("C1", "S01", "Client contrat");
        client.Update(new ClientDetails
        {
            RaisonSociale = "Client contrat", TypeAffectCommercial = "Agence",
            KeyChargeClientele = "P42", Plafond = 1250m, FinMois = true
        });
        var service = new ClientService(new Repository(client), null!, null!, null!, null!);
        var detail = await service.GetAsync("C1");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var save = JsonSerializer.Deserialize<SaveClientRequest>(JsonSerializer.Serialize(detail, options), options)!;
        Assert.Equal("Agence", save.TypeAffectCommercial);
        Assert.Equal("P42", save.KeyChargeClientele);
        Assert.Equal(1250m, save.Plafond);
        Assert.True(save.FinMois);
    }

    private sealed class Repository(Client client) : IClientRepository
    {
        public Task<Client?> GetAsync(string keyClient, bool includeChildren, CancellationToken cancellationToken = default) => Task.FromResult<Client?>(client);
        public Task<IReadOnlyList<Client>> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Client?> GetByRaisonSocialeAsync(string raisonSociale, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> CompteComptableExistsAsync(string compteComptable, string? excludingClient, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<Client>> SearchAsync(string? search, string? statutClient, string? classeClient, bool? actif, int page, int pageSize, CancellationToken cancellationToken = default, bool? valide = null) => throw new NotSupportedException();
        public Task<IReadOnlyList<string>> FindReferencesAsync(string keyClient, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Add(Client value) => throw new NotSupportedException();
        public void Add(AdresseClient value) => throw new NotSupportedException();
        public void Add(BanqueTiers value) => throw new NotSupportedException();
        public void Remove(Client value) => throw new NotSupportedException();
        public void Remove(AdresseClient value) => throw new NotSupportedException();
        public void Remove(BanqueTiers value) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
