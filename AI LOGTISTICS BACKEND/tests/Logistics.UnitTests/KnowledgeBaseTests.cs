using System.IO.Compression;
using System.Text;
using Logistics.Application.AI;
using Logistics.Application.AI.Agent;
using Logistics.Application.AI.RAG;
using Logistics.Domain.Common;
using Logistics.Infrastructure.AI.RAG;
using UglyToad.PdfPig.Writer;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;

namespace Logistics.UnitTests;

public sealed class KnowledgeBaseTests
{
    [Fact]
    public async Task Extracted_text_can_be_updated_in_the_persistent_catalog()
    {
        using var fixture = new Fixture();
        var text = await new KnowledgeTextExtractor(new()).ExtractAsync(File("guide.txt", "texte"));
        Assert.Equal("texte", text);
        await fixture.Store.CreateAsync("S01", new(new("id", "guide.txt", "TXT", 5, DateTimeOffset.UtcNow, "processing"), null, []));
        await fixture.Store.UpdateAsync("S01", "id", document => document with { Text = text });
        Assert.Equal(text, (await fixture.Store.GetAsync("S01", "id")).Text);
    }
    [Fact]
    public async Task Pdf_with_text_is_extracted_and_indexed()
    {
        using var fixture = new Fixture();
        var builder = new PdfDocumentBuilder(); var page = builder.AddPage(PageSize.A4);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        page.AddText("Procedure PDF livraison", 12, new PdfPoint(30, 700), font);
        var document = Assert.Single(await fixture.Service.UploadAsync("S01", [new("guide.pdf", builder.Build())]));
        Assert.Equal("indexed", document.Status); Assert.Contains("livraison", (await fixture.Store.GetAsync("S01", document.Id)).Text);
    }
    [Theory]
    [InlineData("guide.txt")]
    [InlineData("guide.md")]
    [InlineData("guide.csv")]
    public async Task Upload_indexes_real_text_and_exposes_matching_frontend_metadata(string name)
    {
        using var fixture = new Fixture();
        var document = Assert.Single(await fixture.Service.UploadAsync("S01:documents", [File(name, "Procédure de livraison.")]));
        Assert.Equal(name, document.Name); Assert.Equal("indexed", document.Status); Assert.Equal("fixture", document.ModelId);
        Assert.True(document.Size > 0); Assert.True(document.ChunkCount > 0);
        var passages = await fixture.Retrieval.RetrieveAsync(new("S01:documents", "procédure"));
        Assert.Equal(document.Id, Assert.Single(passages).Chunk.DocumentId); Assert.Equal(name, passages[0].Chunk.SourceName);
    }
    [Fact]
    public async Task Docx_is_extracted_from_word_xml_without_executing_content()
    {
        using var fixture = new Fixture(); using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        { var entry = zip.CreateEntry("word/document.xml"); using var writer = new StreamWriter(entry.Open()); writer.Write("<w:document xmlns:w='http://schemas.openxmlformats.org/wordprocessingml/2006/main'><w:body><w:p><w:r><w:t>Procédure documentaire DOCX</w:t></w:r></w:p></w:body></w:document>"); }
        var document = Assert.Single(await fixture.Service.UploadAsync("S01", [new("guide.docx", stream.ToArray())]));
        Assert.Equal("indexed", document.Status);
        Assert.Contains("DOCX", (await fixture.Store.GetAsync("S01", document.Id)).Text);
    }
    [Fact]
    public async Task Uploaded_docx_is_available_to_chat_after_store_restart_and_remains_company_isolated()
    {
        using var fixture = new Fixture(); using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("word/document.xml"); using var writer = new StreamWriter(entry.Open());
            writer.Write("<w:document xmlns:w='http://schemas.openxmlformats.org/wordprocessingml/2006/main'><w:body><w:p><w:r><w:t>CV de Camille : compétences C# et ASP.NET Core.</w:t></w:r></w:p></w:body></w:document>");
        }
        var uploaded = Assert.Single(await fixture.Service.UploadAsync("S01:documents", [new("cv-test.docx", stream.ToArray())]));
        Assert.Equal("indexed", uploaded.Status);
        var retrieval = new RetrievalService(fixture.Embeddings, new FileKnowledgeStore(fixture.Options));
        var agent = new AgentService(new UnexpectedPlanner(), retrieval, new NoBusinessTools(), new AgentContext("S01:documents"), new NoAudit());
        var response = await agent.GenerateResponseAsync("Quelles sont les compétences dans le CV de Camille ?");
        Assert.Contains("C# et ASP.NET Core", response);
        Assert.Contains("cv-test.docx, passage 1", response);
        Assert.DoesNotContain("Précisez la clé", response);
        var otherCompany = new AgentService(new UnexpectedPlanner(), retrieval, new NoBusinessTools(), new AgentContext("S99:documents"), new NoAudit());
        var unavailable = await otherCompany.GenerateResponseAsync("Quelles sont les compétences dans le CV de Camille ?");
        Assert.Contains("Aucun passage documentaire pertinent", unavailable);
        Assert.DoesNotContain("Camille :", unavailable);
    }

    private sealed class UnexpectedPlanner : ILLMService
    {
        public Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Explicit document questions must not require business planning.");
        public IAsyncEnumerable<string> StreamResponseAsync(string prompt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class NoBusinessTools : IAgentMcpTools
    {
        public IReadOnlyList<AgentToolDefinition> List() => [];
        public Task<AgentToolResult> CallAsync(string name, IReadOnlyDictionary<string, System.Text.Json.JsonElement> arguments, CancellationToken cancellationToken = default) => throw new InvalidOperationException("A CV does not require a business read.");
    }
    private sealed record AgentContext(string DocumentCollectionId) : IAgentContext;
    private sealed class NoAudit : IAgentToolAudit { public void Record(AgentToolAuditEvent entry) { } }

    [Fact]
    public async Task Invalid_pdf_and_empty_extracted_text_are_failed_not_simulated_success()
    {
        using var fixture = new Fixture();
        var documents = await fixture.Service.UploadAsync("S01", [File("invalid.pdf", "not a PDF"), File("blank.txt", "   ")]);
        Assert.All(documents, document => Assert.Equal("failed", document.Status));
        Assert.Equal("invalid_document", documents[0].ErrorCode); Assert.Equal("no_extractable_text", documents[1].ErrorCode);
        Assert.Empty(await fixture.Retrieval.RetrieveAsync(new("S01", "question")));
    }
    [Fact]
    public async Task Search_combines_accent_insensitive_name_type_status_and_pagination()
    {
        using var fixture = new Fixture();
        await fixture.Service.UploadAsync("S01", [File("Expéditions.txt", "texte"), File("Guide.md", "texte"), File("Erreur.pdf", "invalid")]);
        var match = await fixture.Service.ListAsync("S01", new("EXPEDITIONS", "txt", "indexed", 1, 1));
        Assert.Equal("Expéditions.txt", Assert.Single(match.Items).Name); Assert.Equal(1, match.TotalCount);
        var all = await fixture.Service.ListAsync("S01", new(Page: 2, PageSize: 1)); Assert.Equal(3, all.TotalCount); Assert.Single(all.Items);
        Assert.Single((await fixture.Service.ListAsync("S01", new(Status: "failed"))).Items);
    }
    [Fact]
    public async Task Delete_removes_metadata_text_and_vectors_and_cannot_cross_company_boundary()
    {
        using var fixture = new Fixture(); var document = Assert.Single(await fixture.Service.UploadAsync("S01", [File("guide.txt", "Privé S01")]));
        Assert.Empty((await fixture.Service.ListAsync("S99", new())).Items);
        await Assert.ThrowsAsync<EntityNotFoundException>(() => fixture.Service.DeleteAsync("S99", document.Id));
        await fixture.Service.DeleteAsync("S01", document.Id);
        Assert.Empty((await fixture.Service.ListAsync("S01", new())).Items); Assert.Empty(await fixture.Retrieval.RetrieveAsync(new("S01", "question")));
    }
    [Fact]
    public async Task Catalog_and_vectors_survive_a_new_store_instance()
    {
        using var fixture = new Fixture(); var document = Assert.Single(await fixture.Service.UploadAsync("S01", [File("guide.txt", "Persistance documentaire")]));
        var restarted = new FileKnowledgeStore(fixture.Options);
        Assert.Equal("indexed", (await restarted.GetAsync("S01", document.Id)).Metadata.Status);
        Assert.Single(await restarted.SearchAsync(new("S01", new("fixture", [1f, 0f]))));
    }
    [Fact]
    public async Task Interrupted_processing_recovers_to_failed_instead_of_remaining_stuck()
    {
        using var fixture = new Fixture();
        await fixture.Store.CreateAsync("S01", new(new("interrupted", "guide.txt", "TXT", 4, DateTimeOffset.UtcNow, "processing"), "text", []));
        var restarted = new FileKnowledgeStore(fixture.Options); var recovered = await restarted.GetAsync("S01", "interrupted");
        Assert.Equal("failed", recovered.Metadata.Status); Assert.Equal("indexing_interrupted", recovered.Metadata.ErrorCode);
    }
    [Fact]
    public async Task Provider_failure_is_visible_and_reindex_recovers_without_duplicate_chunks()
    {
        using var fixture = new Fixture(); fixture.Embeddings.Fail = true;
        var document = Assert.Single(await fixture.Service.UploadAsync("S01", [File("guide.txt", "Document à indexer")]));
        Assert.Equal("failed", document.Status); Assert.Equal("indexing_failed", document.ErrorCode);
        fixture.Embeddings.Fail = false; var reindexed = await fixture.Service.ReindexAsync("S01", document.Id); Assert.Equal("indexed", reindexed.Status);
        await fixture.Service.ReindexAsync("S01", document.Id);
        Assert.Single(await fixture.Retrieval.RetrieveAsync(new("S01", "question")));
    }
    [Fact]
    public async Task Pending_status_is_observable_and_deletion_during_indexing_cannot_resurrect_it()
    {
        using var fixture = new Fixture(); var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Embeddings.Gate = async token => { started.TrySetResult(); await release.Task.WaitAsync(token); };
        var upload = fixture.Service.UploadAsync("S01", [File("guide.txt", "texte")]); await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var pending = Assert.Single((await fixture.Service.ListAsync("S01", new())).Items); Assert.Equal("processing", pending.Status);
        await fixture.Service.DeleteAsync("S01", pending.Id); release.SetResult();
        await Assert.ThrowsAsync<EntityNotFoundException>(() => upload); Assert.Empty((await fixture.Service.ListAsync("S01", new())).Items);
    }
    [Theory]
    [InlineData("script.exe", "text")]
    [InlineData("../guide.txt", "text")]
    [InlineData("empty.txt", "")]
    public async Task Invalid_batch_does_not_write_partial_metadata(string name, string text)
    {
        using var fixture = new Fixture();
        await Assert.ThrowsAsync<BusinessRuleException>(() => fixture.Service.UploadAsync("S01", [File("valid.txt", "text"), File(name, text)]));
        Assert.Empty((await fixture.Service.ListAsync("S01", new())).Items);
    }
    [Fact]
    public async Task Cancellation_marks_the_document_failed_and_does_not_publish_vectors()
    {
        using var fixture = new Fixture(); using var source = new CancellationTokenSource();
        fixture.Embeddings.Gate = _ => { source.Cancel(); source.Token.ThrowIfCancellationRequested(); return Task.CompletedTask; };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Service.UploadAsync("S01", [File("guide.txt", "texte")], source.Token));
        var document = Assert.Single((await fixture.Service.ListAsync("S01", new())).Items); Assert.Equal("failed", document.Status);
    }

    private static KnowledgeUpload File(string name, string text) => new(name, Encoding.UTF8.GetBytes(text));
    [Fact]
    public async Task Batch_quota_is_checked_atomically_before_any_document_is_accepted()
    {
        using var fixture = new Fixture(); fixture.Options.MaximumDocuments = 1;
        await Assert.ThrowsAsync<BusinessRuleException>(() => fixture.Service.UploadAsync("S01", [File("first.txt", "text"), File("second.txt", "text")]));
        Assert.Empty((await fixture.Service.ListAsync("S01", new())).Items);
    }
    private sealed class Embeddings : IEmbeddingService
    {
        public bool Fail { get; set; }
        public Func<CancellationToken, Task>? Gate { get; set; }
        public async Task<EmbeddingVector> GenerateAsync(string text, CancellationToken cancellationToken = default)
        { if (Gate is not null) await Gate(cancellationToken); if (Fail) throw new InvalidOperationException("private provider failure"); return new("fixture", [1f, 0f]); }
    }
    private sealed class Fixture : IDisposable
    {
        public KnowledgeBaseOptions Options { get; } = new() { DataDirectory = Path.Combine(AppContext.BaseDirectory, "rag-test-data", Guid.NewGuid().ToString("N")) };
        public FileKnowledgeStore Store { get; }
        public Embeddings Embeddings { get; } = new();
        public KnowledgeBaseService Service { get; }
        public RetrievalService Retrieval { get; }
        public Fixture()
        {
            Store = new(Options);
            var index = new DocumentIndexingService(new TextDocumentLoader(), new CharacterChunkingService(), Embeddings, Store);
            Service = new(Store, new KnowledgeTextExtractor(new()), index, Options); Retrieval = new(Embeddings, Store);
        }
        public void Dispose() { if (Directory.Exists(Options.DataDirectory)) Directory.Delete(Options.DataDirectory, recursive: true); }
    }
}
