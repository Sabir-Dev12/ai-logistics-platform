using System.Text.Json;
using Logistics.Application.AI;
using Logistics.Application.AI.Agent;
using Logistics.Application.AI.RAG;

namespace Logistics.UnitTests;

public sealed class AgentReadableResponseTests
{
    [Fact]
    public async Task Client_details_are_readable_and_omit_empty_technical_fields_without_inventing_values()
    {
        var text = await Agent("GetClient", """{"keyClient":"CLI001","raisonSociale":"ALPHA IMPORT SARL","refClient":"REF001","villeClient":"Casablanca","telClient":"\u002B212 522 000 001","actif":true,"valide":false,"societeKey":"PRIVATE-TENANT","faxClient":null,"banques":[],"adresses":[],"connectionString":"private-secret"}""").GenerateResponseAsync("ID001");
        Assert.Contains("Voici les informations du client « ALPHA IMPORT SARL »", text);
        Assert.Contains("Référence client : REF001", text);
        Assert.Contains("Téléphone : +212 522 000 001", text);
        Assert.Contains("Situation : Actif", text); Assert.Contains("Validation : Non validé", text);
        Assert.Contains("Source : fiche client", text);
        foreach (var unwanted in new[] { "{", "}", "null", "raisonSociale", "societeKey", "PRIVATE-TENANT", "private-secret", "Fax :", "Banque :", "\\u002B" })
            Assert.DoesNotContain(unwanted, text);
    }

    [Theory]
    [InlineData("GetInvoice", "{\"numFacture\":\"F-10\",\"ttcMad\":1234.56,\"tvaMad\":0,\"factRegle\":false,\"lignes\":[{\"libelleRubrique\":\"Fret\",\"mtHtMad\":1234.56}]}", "Total TTC (MAD) : 1234,56")]
    [InlineData("GetShipment", "{\"numDossier\":\"D-10\",\"libelleStatutDossier\":\"En transit\",\"nbColis\":3,\"colisages\":[{\"typeColis\":\"Palette\",\"nbColis\":3}]}", "Statut : En transit")]
    [InlineData("GetBooking", "{\"numBooking\":\"B-10\",\"intituleClient\":\"Client A\",\"nbEquipements\":2,\"equipements\":[{\"typeEquipement\":\"Conteneur\",\"qte\":2}]}", "Nombre d’équipements : 2")]
    [InlineData("GetTracking", "[{\"cleEntite\":\"D-10\",\"libelleStatut\":\"Livré\",\"dateTraceUser\":\"2026-10-20T10:30:00+01:00\",\"utilisateur\":\"Karim\",\"commentaire\":\"Réception confirmée\"}]", "Date de l’événement : 20/10/2026 à 10:30 UTC+01:00")]
    public async Task All_business_tools_render_human_labels_and_preserve_verified_values(string tool, string json, string expected)
    {
        var text = await Agent(tool, json).GenerateResponseAsync("ID001");
        Assert.Contains(expected, text); Assert.Contains("Source :", text);
        Assert.DoesNotContain("{", text); Assert.DoesNotContain("}", text); Assert.DoesNotContain("\"", text);
    }

    [Fact]
    public async Task Missing_fields_are_not_assumed_to_be_zero_inactive_or_settled()
    {
        var text = await Agent("GetInvoice", """{"numFacture":"F-10","ttcMad":null}""").GenerateResponseAsync("ID001");
        Assert.DoesNotContain("0,00", text); Assert.DoesNotContain("réglée", text); Assert.DoesNotContain("Total TTC", text);
        var client = await Agent("GetClient", """{"raisonSociale":"Client A","actif":null}""").GenerateResponseAsync("ID001");
        Assert.DoesNotContain("Situation", client); Assert.DoesNotContain("Inactif", client);
    }

    [Fact]
    public async Task Zero_negative_and_fractional_amounts_are_not_recalculated_or_rounded_away()
    {
        var text = await Agent("GetInvoice", """{"numFacture":"A-10","ttcMad":-120.125,"tvaMad":0}""").GenerateResponseAsync("ID001");
        Assert.Contains("Total TTC (MAD) : -120,125", text); Assert.Contains("TVA (MAD) : 0,00", text);
    }

    [Fact]
    public async Task Empty_tracking_does_not_assert_that_a_shipment_exists_or_is_delivered()
    {
        var text = await Agent("GetTracking", "[]").GenerateResponseAsync("ID001");
        Assert.Contains("Aucun événement de suivi", text); Assert.DoesNotContain("Livré", text); Assert.DoesNotContain("[]", text);
    }

    [Fact]
    public async Task Unknown_nested_fields_never_fall_back_to_raw_json()
    {
        var text = await Agent("GetClient", """{"unexpected":{"sql":"SELECT private","secret":"password"}}""").GenerateResponseAsync("ID001");
        Assert.Contains("aucune information renseignée", text); Assert.DoesNotContain("SELECT", text); Assert.DoesNotContain("secret", text); Assert.DoesNotContain("{", text);
    }

    [Theory]
    [InlineData("not_found", "Je n’ai trouvé aucun résultat")]
    [InlineData("forbidden", "Vous n’avez pas l’autorisation")]
    [InlineData("database_unavailable", "service de données est indisponible")]
    [InlineData("validation_failed", "Vérifiez la référence")]
    public async Task Errors_are_readable_and_do_not_expose_codes_or_error_payloads(string code, string expected)
    {
        var text = await Agent("GetClient", "{\"secret\":\"private-error\"}", code).GenerateResponseAsync("ID001");
        Assert.Contains(expected, text); Assert.DoesNotContain(code, text); Assert.DoesNotContain("private-error", text); Assert.DoesNotContain("{", text);
    }

    private static AgentService Agent(string tool, string data, string? error = null) => new(new Planner(tool), new NoRag(), new Tool(tool, data, error), new Context(), new Audit());
    private sealed class Planner(string tool) : ILLMService
    {
        public Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default)
        {
            var field = tool switch { "GetClient" => "keyClient", "GetInvoice" => "keyFacture", "GetShipment" => "keyDossier", "GetBooking" => "keyBooking", _ => "entityKey" };
            return Task.FromResult(JsonSerializer.Serialize(new { requiresBusinessData = true, useRag = false, toolCalls = new[] { new { name = tool, arguments = new Dictionary<string, string> { [field] = "ID001" } } } }));
        }
        public IAsyncEnumerable<string> StreamResponseAsync(string prompt, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Verified values must not be rewritten by a freeform model.");
    }
    private sealed class Tool(string tool, string data, string? error) : IAgentMcpTools
    {
        public IReadOnlyList<AgentToolDefinition> List() => [new(tool, "Fixture", JsonSerializer.SerializeToElement(new { type = "object" }))];
        public Task<AgentToolResult> CallAsync(string name, IReadOnlyDictionary<string, JsonElement> arguments, CancellationToken cancellationToken = default)
            => Task.FromResult(new AgentToolResult(error is null, JsonSerializer.Deserialize<JsonElement>(data), error));
    }
    private sealed class NoRag : IRetrievalService
    {
        public Task<IReadOnlyList<RetrievedPassage>> RetrieveAsync(RetrievalRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Context : IAgentContext { public string DocumentCollectionId => "S01:documents"; }
    private sealed class Audit : IAgentToolAudit { public void Record(AgentToolAuditEvent entry) { } }
}
