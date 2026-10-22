using System.Reflection;
using System.Text.Json;
using Logistics.Api.Mcp;
using Logistics.Application.Abstractions;
using Logistics.Application.Features.Clients;
using Logistics.Domain.Common;
using Logistics.Domain.Entities.Clients;
using Microsoft.Extensions.Logging.Abstractions;

namespace Logistics.UnitTests;

public sealed class ClientReferenceLookupTests
{
    [Fact]
    public async Task Reference_resolution_uses_an_exact_repository_lookup_then_the_normal_detail_read()
    {
        var client = new Client("CLI001", "S01", "Test client");
        var calls = new List<string>();
        using var cancellation = new CancellationTokenSource();
        var repository = PaymentApiContractTests.Proxy.Make<IClientRepository>((method, args) =>
        {
            calls.Add(method.Name);
            Assert.Equal(cancellation.Token, args![^1]);
            if (method.Name == "FindByReferenceAsync")
            {
                Assert.Equal("REF001", args[0]);
                return Task.FromResult<IReadOnlyList<Client>>([client]);
            }
            Assert.Equal("GetAsync", method.Name);
            Assert.Equal("CLI001", args[0]); Assert.Equal(true, args[1]);
            return Task.FromResult<Client?>(client);
        });
        var detail = await new ClientService(repository, null!, null!, null!, null!).GetByReferenceAsync("REF001", cancellation.Token);
        Assert.Equal("CLI001", detail.KeyClient);
        Assert.Equal(new[] { "FindByReferenceAsync", "GetAsync" }, calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Missing_or_ambiguous_references_never_select_an_arbitrary_client(int count)
    {
        var repository = PaymentApiContractTests.Proxy.Make<IClientRepository>((method, _) =>
        {
            Assert.Equal("FindByReferenceAsync", method.Name);
            return Task.FromResult<IReadOnlyList<Client>>(Enumerable.Range(0, count).Select(i => new Client("C" + i, "S01", "Test")).ToArray());
        });
        var service = new ClientService(repository, null!, null!, null!, null!);
        if (count == 0) await Assert.ThrowsAsync<EntityNotFoundException>(() => service.GetByReferenceAsync("REF001"));
        else Assert.Equal("client.reference.ambiguous", (await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetByReferenceAsync("REF001"))).Rule);
    }

    [Fact]
    public async Task Mcp_reference_mode_delegates_to_the_business_service_without_guessing_a_key()
    {
        var service = DispatchProxy.Create<IClientService, McpToolTests.ServiceSpy>();
        var spy = (McpToolTests.ServiceSpy)(object)service;
        spy.Handler = (method, args) =>
        {
            Assert.Equal("GetByReferenceAsync", method.Name);
            Assert.Equal("REF001", args[0]);
            return Task.FromResult(JsonSerializer.Deserialize<ClientDetail>("""{"keyClient":"CLI001","raisonSociale":"Test","adresses":[],"banques":[]}""", new JsonSerializerOptions(JsonSerializerDefaults.Web))!);
        };
        var tool = new GetClientTool(service, NullLogger<GetClientTool>.Instance);
        var result = await tool.ExecuteAsync(new Dictionary<string, JsonElement>
        { ["keyClient"] = JsonSerializer.SerializeToElement("REF001"), ["byReference"] = JsonSerializer.SerializeToElement(true) });
        Assert.False(result.IsError);
        Assert.Single(spy.Calls);
        Assert.Equal("CLI001", result.StructuredContent!.Value.GetProperty("data").GetProperty("keyClient").GetString());
    }

    [Theory]
    [InlineData("\"true\"")]
    [InlineData("1")]
    [InlineData("null")]
    public async Task Reference_flag_must_be_a_boolean_before_any_service_call(string flag)
    {
        var service = DispatchProxy.Create<IClientService, McpToolTests.ServiceSpy>();
        var tool = new GetClientTool(service, NullLogger<GetClientTool>.Instance);
        var result = await tool.ExecuteAsync(new Dictionary<string, JsonElement>
        { ["keyClient"] = JsonSerializer.SerializeToElement("REF001"), ["byReference"] = JsonSerializer.Deserialize<JsonElement>(flag) });
        Assert.True(result.IsError);
        Assert.Equal("validation_failed", result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString());
        Assert.Empty(((McpToolTests.ServiceSpy)(object)service).Calls);
    }
}
