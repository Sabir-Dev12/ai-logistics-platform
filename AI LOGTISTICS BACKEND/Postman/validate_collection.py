"""Check the generated files without executing any application endpoint."""
from pathlib import Path
import hashlib
import json
import re
import subprocess
import zipfile
from jsonschema import Draft202012Validator, Draft7Validator, FormatChecker

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Postman"


def read(name):
    return json.loads((OUT / name).read_text(encoding="utf-8-sig"))


collection = read("Logistics.postman_collection.json")
environment = read("Logistics.local.postman_environment.json")
spec = read("Logistics.Api.openapi.json")
schema = read("postman-collection-v2.1.schema.json")
collection_errors = [error.message for error in Draft7Validator(schema).iter_errors(collection)]
values = {value["key"]: str(value["value"]) for value in collection.get("variable", [])}
values.update({value["key"]: str(value["value"]) for value in environment["values"]})
items = [item for folder in collection["item"] for item in folder["item"]]


def resolve(text):
    return re.sub(r"\{\{([^{}]+)\}\}",
                  lambda match: "1791840000" if match[1].startswith("$") else values[match[1]], text)


body_errors, count = [], 0
for item in items:
    request = item["request"]
    method, path = item["name"].split(" ", 1)
    resolve(json.dumps(request))  # Detect undeclared variables in URLs, query parameters and bodies.
    if request.get("body", {}).get("mode") != "raw":
        continue
    body = json.loads(resolve(request["body"]["raw"]))
    request_schema = spec["paths"][path][method.lower()]["requestBody"]["content"]["application/json"]["schema"]
    full_schema = {**request_schema, "components": spec.get("components", {})}
    for error in Draft202012Validator(full_schema, format_checker=FormatChecker()).iter_errors(body):
        body_errors.append({"request": item["name"], "property": list(error.path), "error": error.message})
    count += 1

source_routes = set()
for file in (ROOT / "src" / "Logistics.Api" / "Controllers").glob("*.cs"):
    sections = re.split(r'\[Route\("([^"]+)"\)\]', file.read_text(encoding="utf-8-sig"))
    for index in range(1, len(sections), 2):
        for method, route in re.findall(r'\[Http(Get|Post|Put|Patch|Delete)(?:\("([^"]*)"\))?\]', sections[index + 1]):
            path = "/" + sections[index] + ("/" + route if route else "")
            path = re.sub(r"\{([^}:]+):[^}]+\}", r"{\1}", path)
            source_routes.add((method.upper(), path))
generated_routes = {tuple(item["name"].split(" ", 1)) for item in items}
missing = source_routes - generated_routes
assert len(generated_routes) == len(items), "Duplicate collection operation"

node_check = """
const fs = require('fs');
const collection = JSON.parse(fs.readFileSync(process.argv[1], 'utf8'));
let count = 0;
for (const folder of collection.item) for (const item of folder.item) {
  for (const event of item.event || []) {
    new Function('pm', event.script.exec.join('\\n'));
    count++;
  }
}
console.log(count);
"""
process = subprocess.run(["node", "-e", node_check, str(OUT / "Logistics.postman_collection.json")],
                         check=True, capture_output=True, text=True)
script_count = int(process.stdout.strip())
report = {
    "collection_schema": "Official Postman v2.1 schema",
    "collection_schema_errors": collection_errors,
    "json_bodies_validated_against_openapi": count,
    "json_body_errors": body_errors,
    "controller_operations": len(source_routes),
    "collection_requests": len(items),
    "missing_controller_operations": sorted(missing),
    "postman_scripts_validated_for_syntax": script_count,
    "api_business_operations_executed": False,
    "openapi_sha256": hashlib.sha256((OUT / "Logistics.Api.openapi.json").read_bytes()).hexdigest(),
}
(OUT / "Validation.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
assert not collection_errors and not body_errors and not missing, json.dumps(report, ensure_ascii=False)
with zipfile.ZipFile(OUT / "Logistics-Postman.zip", "a", compression=zipfile.ZIP_DEFLATED) as archive:
    if "Validation.json" not in archive.namelist():
        archive.write(OUT / "Validation.json", arcname="Validation.json")
print(json.dumps(report, ensure_ascii=False))
