"""Update the existing local API artifacts with the Knowledge Base contract. No network or credentials."""
import json
from pathlib import Path

root = Path(__file__).resolve().parent
collection_path = root / "Logistics.postman_collection.json"
spec_path = root / "Logistics.Api.openapi.json"
collection = json.loads(collection_path.read_text(encoding="utf-8-sig"))
collection["info"]["description"] = collection["info"]["description"].replace("159 opérations des contrôleurs", "Opérations des contrôleurs")
spec = json.loads(spec_path.read_text(encoding="utf-8-sig"))
base = "/api/v1/ai/documents"
ref = lambda name: {"$ref": f"#/components/schemas/{name}"}
document = ref("KnowledgeDocument")
schemas = spec.setdefault("components", {}).setdefault("schemas", {})
schemas["KnowledgeDocument"] = {"type": "object", "required": ["id", "name", "type", "size", "date", "status", "chunkCount"], "properties": {
    "id": {"type": "string"}, "name": {"type": "string", "maxLength": 200},
    "type": {"type": "string", "enum": ["PDF", "DOCX", "TXT", "MD", "CSV"]}, "size": {"type": "integer", "format": "int64"},
    "date": {"type": "string", "format": "date-time"}, "status": {"type": "string", "enum": ["indexed", "processing", "failed"]},
    "chunkCount": {"type": "integer"}, "modelId": {"type": ["string", "null"]}, "errorCode": {"type": ["string", "null"]}}}
schemas["KnowledgeDocumentPage"] = {"type": "object", "properties": {"items": {"type": "array", "items": document}, "page": {"type": "integer"}, "pageSize": {"type": "integer"}, "totalCount": {"type": "integer"}, "totalPages": {"type": "integer"}}}
schemas["KnowledgePassage"] = {"type": "object", "properties": {"score": {"type": "number"}, "chunk": {"type": "object", "properties": {"chunkId": {"type": "string"}, "documentId": {"type": "string"}, "index": {"type": "integer"}, "text": {"type": "string"}, "startOffset": {"type": "integer"}, "endOffset": {"type": "integer"}, "sourceName": {"type": ["string", "null"]}}}}}
def operation(summary, schema=None, status=200):
    response = {"description": summary}
    if schema:
        response["content"] = {"application/json": {"schema": schema}}
    return {"tags": ["Knowledge Base / RAG"], "summary": summary, "responses": {str(status): response, "401": {"description": "Bearer token required"}, "404": {"description": "Document not visible in the signed company"}, "422": {"description": "Invalid document, filter or quota"}}}
listing = operation("List company documents with combined filters and pagination", ref("KnowledgeDocumentPage"))
listing["parameters"] = [{"name": name, "in": "query", "schema": schema} for name, schema in [
    ("search", {"type": "string"}), ("type", {"type": "string", "enum": ["PDF", "DOCX", "TXT", "MD", "CSV"]}), ("status", {"type": "string", "enum": ["indexed", "processing", "failed"]}), ("page", {"type": "integer", "minimum": 1, "default": 1}), ("pageSize", {"type": "integer", "minimum": 1, "maximum": 200, "default": 20})]]
upload = operation("Upload 1–10 files; inspect each document status for indexing success/failure", {"type": "array", "items": document}, 201)
upload["requestBody"] = {"required": True, "content": {"multipart/form-data": {"schema": {"type": "object", "required": ["files"], "properties": {"files": {"type": "array", "minItems": 1, "maxItems": 10, "items": {"type": "string", "format": "binary"}}}}}}}
upload["responses"]["413"] = {"description": "Total request exceeds 50 MiB"}
id_parameter = {"name": "id", "in": "path", "required": True, "schema": {"type": "string"}}
detail = operation("Read document metadata/status", document); detail["parameters"] = [id_parameter]
delete = operation("Remove document metadata/text/vectors", status=204); delete["parameters"] = [id_parameter]
reindex = operation("Reindex extracted text after an embedding failure", document); reindex["parameters"] = [id_parameter]
search = operation("Retrieve relevant passages in the signed company", {"type": "array", "items": ref("KnowledgePassage")})
search["requestBody"] = {"required": True, "content": {"application/json": {"schema": {"type": "object", "required": ["question"], "properties": {"question": {"type": "string"}, "topK": {"type": "integer", "minimum": 1, "maximum": 1000, "default": 5}}}}}}
spec["paths"][base] = {"get": listing, "post": upload}
spec["paths"][base + "/{id}"] = {"get": detail, "delete": delete}
spec["paths"][base + "/{id}/reindex"] = {"post": reindex}
spec["paths"][base + "/search"] = {"post": search}

items = []
def request(name, method, suffix, body=None, expected=200, script=""):
    value = {"method": method, "header": [], "url": "{{baseUrl}}" + base + suffix,
             "description": name + ". Company is resolved from the signed token. No company/collection parameter is accepted. Use documents created for this test only."}
    if body:
        value["body"] = body
        if body["mode"] == "raw": value["header"] = [{"key": "Content-Type", "value": "application/json"}]
    assertions = [f"pm.test('HTTP {expected}', () => pm.response.to.have.status({expected}));"]
    if script: assertions.append(script)
    operation_path = (base + suffix.split("?", 1)[0]).replace("{{documentId}}", "{id}")
    items.append({"name": method + " " + operation_path, "request": value, "event": [{"listen": "test", "script": {"type": "text/javascript", "exec": assertions}}]})
request("Documents — liste et filtres", "GET", "?page=1&pageSize=20", script="pm.test('Document page', () => pm.expect(pm.response.json().items).to.be.an('array'));")
request("Documents — upload multiple", "POST", "", {"mode": "formdata", "formdata": [{"key": "files", "type": "file", "src": "guide.txt", "description": "Choose a local PDF/DOCX/TXT/MD/CSV file; repeat files for multiple upload."}]}, 201,
        "const docs = pm.response.json(); pm.test('Outcomes are explicit', () => docs.forEach(d => pm.expect(['indexed','failed']).to.include(d.status))); if(docs.length) pm.environment.set('documentId', docs[0].id);")
request("Documents — détail et statut", "GET", "/{{documentId}}")
request("Documents — passages pertinents", "POST", "/search", {"mode": "raw", "raw": json.dumps({"question": "Quelle est la procédure en cas de retard ?", "topK": 5}, ensure_ascii=False), "options": {"raw": {"language": "json"}}})
request("Documents — réindexation", "POST", "/{{documentId}}/reindex")
request("Documents — suppression du document de test", "DELETE", "/{{documentId}}", expected=204)
collection["item"] = [folder for folder in collection["item"] if folder["name"] != "Knowledge Base / RAG"] + [{"name": "Knowledge Base / RAG", "item": items}]
if not any(value["key"] == "documentId" for value in collection.get("variable", [])):
    collection.setdefault("variable", []).append({"key": "documentId", "value": "DOCUMENT_ID", "type": "string"})
for path, value in [(collection_path, collection), (spec_path, spec)]:
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("Updated existing OpenAPI and Postman artifacts: 6 Knowledge Base operations.")
