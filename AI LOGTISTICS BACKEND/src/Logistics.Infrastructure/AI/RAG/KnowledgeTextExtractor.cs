using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Logistics.Application.AI.RAG;
using UglyToad.PdfPig;

namespace Logistics.Infrastructure.AI.RAG;

public sealed class KnowledgeTextExtractor(DocumentLoadingOptions options) : IKnowledgeTextExtractor
{
    public Task<string> ExtractAsync(KnowledgeUpload upload, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        string text;
        try
        {
            var extension = Path.GetExtension(upload.Name).ToUpperInvariant();
            if (extension == ".PDF")
            {
                using var document = PdfDocument.Open(upload.Content);
                if (document.NumberOfPages > 300) throw new KnowledgeDocumentException("document_too_large");
                var builder = new StringBuilder();
                foreach (var page in document.GetPages())
                {
                    token.ThrowIfCancellationRequested();
                    builder.AppendLine(string.Join(" ", page.GetWords().Select(word => word.Text)));
                    if (builder.Length > options.MaximumCharacters) throw new KnowledgeDocumentException("document_too_large");
                }
                text = builder.ToString();
            }
            else if (extension == ".DOCX")
            {
                using var content = new MemoryStream(upload.Content);
                using var archive = new ZipArchive(content, ZipArchiveMode.Read);
                var entry = archive.GetEntry("word/document.xml") ?? throw new KnowledgeDocumentException("invalid_document");
                if (entry.Length > (long)options.MaximumCharacters * 4) throw new KnowledgeDocumentException("document_too_large");
                using var input = entry.Open();
                using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = (long)options.MaximumCharacters * 4 });
                var xml = XDocument.Load(reader);
                XNamespace word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                text = string.Join("\n", xml.Descendants(word + "p").Select(paragraph => string.Concat(paragraph.Descendants(word + "t").Select(run => run.Value))));
            }
            else text = new UTF8Encoding(false, true).GetString(upload.Content).TrimStart('\uFEFF');
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (KnowledgeDocumentException) { throw; }
        catch { throw new KnowledgeDocumentException("invalid_document"); }
        token.ThrowIfCancellationRequested();
        if (text.Length > options.MaximumCharacters) throw new KnowledgeDocumentException("document_too_large");
        if (string.IsNullOrWhiteSpace(text)) throw new KnowledgeDocumentException("no_extractable_text");
        return Task.FromResult(text);
    }
}
