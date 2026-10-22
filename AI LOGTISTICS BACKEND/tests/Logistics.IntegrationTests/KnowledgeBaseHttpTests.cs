using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Logistics.Api.AI.Agent;
using Logistics.Api.Authentication;
using Logistics.Api.Controllers;
using Logistics.Api.Middleware;
using Logistics.Application.AI.Agent;
using Logistics.Application.AI.RAG;
using Logistics.Application.Features.Authentication;
using Logistics.Infrastructure.AI.RAG;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Logistics.IntegrationTests;

public sealed class KnowledgeBaseHttpTests
{
    [Fact]
    public async Task Authenticated_upload_list_filter_retrieval_and_deletion_use_the_signed_company()
    {
        await using var server = await Server.Start(); using var http = new HttpClient { BaseAddress = new Uri(server.Url) };
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/v1/ai/documents")).StatusCode);
        http.DefaultRequestHeaders.Authorization = new("Bearer", server.Token("S01"));
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("Procédure d’expédition et livraison.")), "files", "Expéditions.txt");
        using var upload = await http.PostAsync("/api/v1/ai/documents", form); Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var documents = await upload.Content.ReadFromJsonAsync<KnowledgeDocument[]>(); var id = Assert.Single(documents!).Id;
        Assert.Equal("indexed", documents![0].Status);
        using var list = await http.GetAsync("/api/v1/ai/documents?search=EXPEDITIONS&type=TXT&status=indexed&page=1&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var listed = JsonDocument.Parse(await list.Content.ReadAsStringAsync()); Assert.Equal(1, listed.RootElement.GetProperty("totalCount").GetInt32());
        using var search = await http.PostAsJsonAsync("/api/v1/ai/documents/search", new { question = "Procédure ?", topK = 5, collectionId = "S99:documents" });
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        var passages = await search.Content.ReadFromJsonAsync<RetrievedPassage[]>(); Assert.Equal(id, Assert.Single(passages!).Chunk.DocumentId);
        http.DefaultRequestHeaders.Authorization = new("Bearer", server.Token("S99"));
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("/api/v1/ai/documents/" + id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.DeleteAsync("/api/v1/ai/documents/" + id)).StatusCode);
        http.DefaultRequestHeaders.Authorization = new("Bearer", server.Token("S01"));
        Assert.Equal(HttpStatusCode.NoContent, (await http.DeleteAsync("/api/v1/ai/documents/" + id)).StatusCode);
        using var afterDelete = await http.PostAsJsonAsync("/api/v1/ai/documents/search", new { question = "Procédure ?" });
        Assert.Empty((await afterDelete.Content.ReadFromJsonAsync<RetrievedPassage[]>())!);
    }
    [Fact]
    public async Task Invalid_files_are_rejected_and_provider_failure_is_a_visible_failed_document()
    {
        await using var server = await Server.Start(failEmbeddings: true); using var http = new HttpClient { BaseAddress = new Uri(server.Url) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", server.Token("S01"));
        using var invalid = new MultipartFormDataContent(); invalid.Add(new ByteArrayContent([1, 2]), "files", "script.exe");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await http.PostAsync("/api/v1/ai/documents", invalid)).StatusCode);
        using var valid = new MultipartFormDataContent(); valid.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("texte documentaire")), "files", "guide.md");
        using var upload = await http.PostAsync("/api/v1/ai/documents", valid); Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var document = Assert.Single((await upload.Content.ReadFromJsonAsync<KnowledgeDocument[]>())!);
        Assert.Equal("failed", document.Status); Assert.Equal("indexing_failed", document.ErrorCode);
        Assert.DoesNotContain("private", await upload.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync("/api/v1/ai/documents?pageSize=0")).StatusCode);
    }

    private sealed class Context(IHttpContextAccessor accessor) : IAgentContext
    { public string DocumentCollectionId => accessor.HttpContext!.User.FindFirst(ApiAuthentication.Claims.Societe)!.Value + ":documents"; }
    private sealed class Embeddings(bool fail) : IEmbeddingService
    {
        public Task<EmbeddingVector> GenerateAsync(string text, CancellationToken cancellationToken = default)
            => fail ? Task.FromException<EmbeddingVector>(new InvalidOperationException("private provider error")) : Task.FromResult(new EmbeddingVector("fixture", [1f, 0f]));
    }
    private sealed class Server(WebApplication app, string url, string directory) : IAsyncDisposable
    {
        public string Url => url;
        public string Token(string company) => app.Services.GetRequiredService<ILogisticsTokenService>().Issue(new CallerIdentity(company, null, "P001", "TEST", null, DateTime.UtcNow.AddMinutes(5)));
        public static async Task<Server> Start(bool failEmbeddings = false)
        {
            var directory = Path.Combine(AppContext.BaseDirectory, "rag-http-test-data", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development, ContentRootPath = directory });
            builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Authentication:SigningKey"] = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)), ["Authentication:AllowHeaderFallback"] = "false" });
            builder.Services.AddApiAuthentication(builder.Configuration, false); builder.Services.AddHttpContextAccessor();
            builder.Services.AddControllers().AddApplicationPart(typeof(KnowledgeBaseController).Assembly);
            builder.Services.AddScoped<IAgentContext, Context>(); builder.Services.AddSingleton<IEmbeddingService>(new Embeddings(failEmbeddings));
            builder.Services.AddKnowledgeBase(builder.Configuration, directory); builder.Services.AddRagBackend();
            var app = builder.Build(); app.UseMiddleware<ExceptionHandlingMiddleware>(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
            try { await app.StartAsync(); return new(app, app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single(), directory); }
            catch { await app.DisposeAsync(); Directory.Delete(directory, true); throw; }
        }
        public async ValueTask DisposeAsync() { await app.StopAsync(); await app.DisposeAsync(); Directory.Delete(directory, true); }
    }
}
