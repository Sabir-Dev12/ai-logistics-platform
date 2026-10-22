"""Generate a Postman collection from the local API's captured OpenAPI document.

No API write request is sent by this script. Run from the backend root.
"""
from collections import Counter, OrderedDict
from pathlib import Path
import json
import re
import uuid
import zipfile

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Postman"
SPEC = json.loads((OUT / "Logistics.Api.openapi.json").read_text(encoding="utf-8-sig"))
SCHEMAS = SPEC.get("components", {}).get("schemas", {})
METHODS = {"get", "post", "put", "patch", "delete", "head", "options"}

# OpenAPI currently represents C# enums as integers without enumerating their members.
# Read their actual values from the source instead of inventing string enum payloads.
ENUMS = {}
for source_file in (ROOT / "src").rglob("*.cs"):
    if "bin" in source_file.parts or "obj" in source_file.parts:
        continue
    source = source_file.read_text(encoding="utf-8-sig")
    source = re.sub(r"/\*.*?\*/|//[^\n]*", "", source, flags=re.S)
    for name, body in re.findall(r"\benum\s+(\w+)(?:\s*:\s*\w+)?\s*\{([^}]+)\}", source):
        members, value = [], -1
        for part in body.split(","):
            part = part.strip()
            match = re.fullmatch(r"(\w+)(?:\s*=\s*(-?\d+))?", part)
            if not match:
                continue
            value = int(match[2]) if match[2] is not None else value + 1
            members.append({"name": match[1], "value": value})
        if members:
            ENUMS[name] = members

# The current OpenAPI query metadata drops nullability/required flags. Recover mandatory
# scalar query parameters from controller signatures so action URLs contain the needed values.
REQUIRED_QUERIES = {}
for source_file in (ROOT / "src" / "Logistics.Api" / "Controllers").glob("*.cs"):
    sections = re.split(r'\[Route\("([^"]+)"\)\]', source_file.read_text(encoding="utf-8-sig"))
    for index in range(1, len(sections), 2):
        base, source = sections[index], sections[index + 1]
        routes = list(re.finditer(r'\[Http(Get|Post|Put|Patch|Delete)(?:\("([^"]*)"\))?\]', source))
        for position, route in enumerate(routes):
            block = source[route.end():routes[position + 1].start() if position + 1 < len(routes) else len(source)]
            path = "/" + base + ("/" + route[2] if route[2] else "")
            path = re.sub(r"\{([^}:]+):[^}]+\}", r"{\1}", path)
            required = set()
            for value_type, name, default in re.findall(
                r'\[FromQuery\]\s*([\w<>\[\]?.]+)\s+(\w+)(?:\s*=\s*([^,\r\n)]+))?', block):
                if "?" not in value_type and not default:
                    required.add(name)
            REQUIRED_QUERIES[(route[1].lower(), path)] = required

VALUES = OrderedDict([
    ("baseUrl", "http://localhost:5192"),
    ("token", ""),
    ("identifiant", "admin"),
    ("motDePasse", "Admin123!"),
    ("societeKey", "S01"),
    ("agenceKey", "AG01"),
    ("devise", "MAD"),
    ("codePays", "MA"),
    ("ville", "Casablanca"),
    ("dateTest", "2026-10-12T10:00:00Z"),
    ("dateFin", "2026-12-31T18:00:00Z"),
    ("moisCalcul", "10/2026"),
    ("typeDossier", "I"),
    ("typeBooking", "I"),
    ("codeRubrique", "A_REMPLACER"),
    ("codeTva", "A_REMPLACER"),
    ("keyAgence", "AG01"),
    ("modePaiement", "A_REMPLACER"),
    ("typeMateriel", "A_REMPLACER"),
    ("keyStatut", "A_REMPLACER"),
    ("keyClient", "CLI001"),
    ("keyVoyage", "A_REMPLACER"),
    ("keyDossier", "A_REMPLACER"),
    ("keyBooking", "A_REMPLACER"),
    ("keyCotation", "A_REMPLACER"),
    ("keyFacture", "A_REMPLACER"),
    ("keyReglement", "A_REMPLACER"),
    ("keyBareme", "A_REMPLACER"),
    ("keyFournisseur", "A_REMPLACER"),
    ("keyMateriel", "A_REMPLACER"),
    ("keyPersonnel", "P001"),
    ("keyCiterne", "A_REMPLACER"),
    ("keyTresorie", "A_REMPLACER"),
    ("ancienMotDePasse", "Admin123!"),
    ("nouveauMotDePasse", "Test-Postman-2026!"),
    ("entite", "Client"),
    ("cle", "CLI001"),
    ("kind", "Pays"),
    ("key", "MA"),
    ("keyLigneColisage", "1"),
    ("typeEquipement", "A_REMPLACER"),
    ("incoterm", "FOB"),
    ("portDepart", "MACAS"),
    ("portArrivee", "MAPTM"),
    ("classeClient", "A"),
])

FR_GROUPS = {
    "Authentication": "01 - Authentification",
    "Clients": "02 - Clients",
    "Reference data": "03 - Référentiels",
    "Voyages": "04 - Voyages",
    "Dossiers": "05 - Dossiers",
    "Bookings": "06 - Bookings",
    "Quotations": "07 - Cotations",
    "Invoicing": "08 - Factures de vente",
    "Payments": "09 - Règlements",
    "Resources": "10 - Personnel et matériel",
    "Carburant": "11 - Carburant",
    "Tracking": "12 - Tracking",
    "EDI": "13 - EDI STC",
}


def variable(name, default="A_REMPLACER"):
    if name not in VALUES:
        VALUES[name] = default
    return "{{" + name + "}}"


def types(schema):
    value = schema.get("type", [])
    return value if isinstance(value, list) else [value]


def sample(schema, name="", required=True, depth=0):
    if depth > 12:
        raise ValueError("Recursive request schema is unsupported")
    if "$ref" in schema:
        type_name = schema["$ref"].rsplit("/", 1)[-1]
        if type_name in ENUMS:
            return ENUMS[type_name][0]["value"]
        return sample(SCHEMAS[type_name], name, required, depth + 1)
    if "example" in schema:
        return schema["example"]
    if "default" in schema:
        return schema["default"]
    if "oneOf" in schema or "anyOf" in schema:
        choices = schema.get("oneOf", schema.get("anyOf", []))
        non_null = [choice for choice in choices if types(choice) != ["null"]]
        if not required and len(non_null) < len(choices):
            return None
        return sample(non_null[0], name, required, depth + 1)
    if "allOf" in schema:
        merged = {"type": "object", "properties": {}, "required": []}
        for part in schema["allOf"]:
            if "$ref" in part:
                part = SCHEMAS[part["$ref"].rsplit("/", 1)[-1]]
            merged["properties"].update(part.get("properties", {}))
            merged["required"].extend(part.get("required", []))
        return sample(merged, name, required, depth + 1)
    options = types(schema)
    if not required and "null" in options:
        return None
    if "object" in options or "properties" in schema:
        mandatory = set(schema.get("required", []))
        return {key: sample(value, key, key in mandatory, depth + 1)
                for key, value in schema.get("properties", {}).items()}
    if "array" in options:
        return [sample(schema.get("items", {}), name, True, depth + 1)]
    if "enum" in schema:
        return next((item for item in schema["enum"] if item is not None), None)
    low = name.lower()
    if "integer" in options:
        return 1 if low not in {"anneecreation", "nbdecimal"} else (2020 if low == "anneecreation" else 2)
    if "number" in options:
        if low.startswith("taux"):
            return 0.2 if "tva" in low else (0 if "remise" in low else 1)
        if low.startswith("montant") or low in {"mtdevise", "mttotaldevise"}:
            return 1000
        if low in {"quantite", "qte", "nbcolis", "colisdepote"}:
            return 10
        if "poids" in low:
            return 100
        if "kilometrage" in low or "compteur" in low:
            return 1000
        return 1
    if "boolean" in options:
        return low in {"gerbable", "marchandisegerbable", "gagnee", "iscostable"}
    if "string" in options:
        if schema.get("format") == "date-time":
            return variable("dateFin" if "validite" in low else "dateTest")
        if schema.get("format") == "date":
            return "2026-10-12"
        if schema.get("format") == "uuid":
            return str(uuid.UUID(int=1))
        if low.startswith("key"):
            return variable("keyClient" if low == "keytiers" else name)
        if "devise" in low:
            return variable("devise")
        if low in {"codepays", "pays", "paysclient"}:
            return variable("codePays")
        if low == "societekey" or low == "socrubrique":
            return variable("societeKey")
        if low in {"typedossier", "typebooking", "typemateriel", "coderubrique", "codetva", "modepaiement"}:
            return variable(name)
        if low == "moiscalcul":
            return variable("moisCalcul")
        if low in {"identifiant", "motdepasse", "ancienmotdepasse", "nouveaumotdepasse"}:
            return variable(name)
        if low.startswith("num") or low == "referencepaiement":
            return "PM-{{$timestamp}}"
        text = {
            "raisonsociale": "Client Test Postman {{$timestamp}}", "ville": "Casablanca",
            "prenom": "Youssef", "nom": "Test Postman", "cp": "20000",
            "email": "test.postman@example.com", "motif": "Test Postman",
            "departement": "75", "basecalcul": "Forfait", "typecolis": "Palette",
            "commentaire": "Test Postman", "commentairefacture": "Facture de test Postman",
            "typeequipement": "A_REMPLACER", "operation": "TRANSPORT",
        }.get(low, "Test Postman")
        if schema.get("maxLength") is not None and "{{" not in text:
            text = text[:schema["maxLength"]]
        return text
    return None


def body_example(schema):
    body = sample(schema)
    name = schema.get("$ref", "").rsplit("/", 1)[-1]
    # Realistic examples for the most useful workflows; exact remaining fields come from OpenAPI.
    overrides = {
        "TokenRequest": {"societeKey": variable("societeKey"), "agenceKey": variable("agenceKey")},
        "SaveClientRequest": {"adresse1Client": "10 rue de test", "villeClient": variable("ville"),
                              "cpClient": "20000", "paysClient": variable("codePays"),
                              "deviseFacturation": variable("devise"), "telClient": "+212600000000",
                              "classeClient": variable("classeClient")},
        "CreateReglementRequest": {"keyTiers": variable("keyClient"), "tauxDevise": 1,
                                   "modePaiement": variable("modePaiement"), "dateOperation": variable("dateTest")},
        "CreateFactureRequest": {"dateFacture": variable("dateTest"), "tauxDeviseFacture": 1},
        "CreateBookingRequest": {"numBooking": "BKG-{{$timestamp}}", "dateValidite": variable("dateFin"),
                                 "keyExpediteur": variable("keyClient"), "intituleExpediteur": "ALPHA IMPORT SARL",
                                 "keyAgent": None, "intituleAgent": None, "keyCompagnie": None,
                                 "intituleCompagnie": None, "commercialBooking": None, "nomCommercialBooking": None,
                                 "incoterm": variable("incoterm"), "mode": None, "typeChargement": None},
        "DuplicateBookingRequest": {"numBooking": "BKC-{{$timestamp}}", "dateValidite": variable("dateFin")},
        "CreateBaremeAchatRequest": {"libelleBareme": "Barème test Postman", "devise": variable("devise"),
                                    "debutValidite": variable("dateTest"), "finValidite": variable("dateFin")},
        "SaveVoyageRequest": {"numVoyage": "VOY-{{$timestamp}}", "provenance": "Casablanca",
                              "destination": "Tanger", "dateDepartPrevu": variable("dateTest")},
        "SaveDossierRequest": {"numDossier": "DOS-{{$timestamp}}", "keyClient": variable("keyClient"),
                               "descriptionMarchandise": "Marchandise de test", "nbColis": 10,
                               "poidsBrut": 100, "volume": 2, "marchandiseGerbable": True},
        "CreateApprovisionnementRequest": {"numBl": "BL-{{$timestamp}}",
                                          "lignes": [{"keyCiterne": variable("keyCiterne"), "quantite": 100, "indexCompteur": None}]},
        "CreateBonGasoilRequest": {"keyFournisseur": variable("keyFournisseur"), "montant": 1000,
                                  "devise": variable("devise"), "numBon": "BON-{{$timestamp}}"},
        "CreateMaterielRequest": {"immatriculation": "TEST-{{$timestamp}}", "typeMateriel": variable("typeMateriel")},
        "CreateCotationRequest": {"incoterm": variable("incoterm"), "appliquerConditions": False},
        "PaysRequest": {"codePays": "ZZ", "intitulePays": "Pays de test Postman", "alpha2": "ZZ", "accord": None, "zonePays": None},
        "DeviseRequest": {"codeDevise": "TST", "libelleDevise": "Devise test", "libelleFacture": "Unité test", "libelleCent": "Centime", "nbDecimal": 2},
    }
    if isinstance(body, dict):
        body.update(overrides.get(name, {}))
    return body


def parameter_value(parameter):
    name = parameter["name"]
    low = name.lower()
    schema = parameter.get("schema", {})
    if parameter["in"] == "path":
        return variable(name)
    if low in {"page", "pagesize", "take"}:
        return {"page": "1", "pagesize": "20", "take": "100"}[low]
    if low.startswith("key") or low in {"fournisseur", "codetva", "typeequipement"}:
        canonical = "keyFournisseur" if low == "fournisseur" else name[0].lower() + name[1:]
        if low == "keytiers":
            canonical = "keyClient"
        return variable(canonical)
    if schema.get("$ref", "").endswith("TrackingEntity") or low == "entites":
        return "Client"
    if low == "produit":
        return "Gasoil"
    if low == "departement":
        return "75"
    if low in {"montantmad", "montantdevise", "valeur"}:
        return "1000"
    if low in {"dateRemise".lower(), "du", "from"}:
        return variable("dateTest")
    if low in {"au", "to"}:
        return variable("dateFin")
    value = sample(schema, name)
    if value is None:
        value = "Test Postman"
    return str(value).lower() if isinstance(value, bool) else str(value)


def tests_for(path, method, operation):
    codes = [int(c) for c in operation.get("responses", {}) if c.isdigit() and 200 <= int(c) < 300] or [200, 201, 204]
    script = [
        f"pm.test('Réponse HTTP attendue : {', '.join(map(str, codes))}', function () {{",
        f"  pm.expect(pm.response.code).to.be.oneOf({json.dumps(codes)});",
        "});",
    ]
    if path == "/api/v1/auth/token":
        script += [
            "if (pm.response.code === 200) {",
            "  const data = pm.response.json();",
            "  pm.test('Jeton présent', function () { pm.expect(data.accessToken).to.be.a('string').and.not.empty; });",
            "  if (data.accessToken) {",
            "    pm.collectionVariables.set('token', data.accessToken);",
            "    pm.environment.set('token', data.accessToken);",
            "  }",
            "}",
        ]
    save_keys = {
        "/api/v1/clients": "keyClient", "/api/v1/voyages": "keyVoyage",
        "/api/v1/dossiers": "keyDossier", "/api/v1/bookings": "keyBooking",
        "/api/v1/cotations": "keyCotation", "/api/v1/factures-vente": "keyFacture",
        "/api/v1/reglements": "keyReglement", "/api/v1/baremes-achat": "keyBareme",
        "/api/v1/carburant/citernes": "keyCiterne", "/api/v1/carburant/approvisionnements": "keyApprovisionnement",
        "/api/v1/carburant/bons": "keyBon", "/api/v1/resources/personnel": "keyPersonnel",
        "/api/v1/resources/materiel": "keyMateriel",
    }
    if method == "post" and path in save_keys:
        key = save_keys[path]
        script += [
            "if (pm.response.code >= 200 && pm.response.code < 300) {",
            "  const data = pm.response.json();",
            f"  if (data.{key}) {{",
            f"    pm.collectionVariables.set('{key}', data.{key});",
            f"    pm.environment.set('{key}', data.{key});",
            "  }",
            "}",
        ]
    return [{"listen": "test", "script": {"type": "text/javascript", "exec": script}}]


GROUPS, ROWS = {}, []
BODY_COUNT, UPLOAD_COUNT = 0, 0
for path, entry in SPEC["paths"].items():
    for method, operation in entry.items():
        if method not in METHODS:
            continue
        # Carburant shares the Resources tag in this API; its URL identifies the proper folder.
        tag = "Carburant" if path.startswith("/api/v1/carburant") else operation.get("tags", ["Autres"])[0]
        group = FR_GROUPS.get(tag, tag)
        summary = operation.get("summary", "")
        parameters = json.loads(json.dumps(operation.get("parameters", [])))
        for parameter in parameters:
            if parameter["in"] == "query" and parameter["name"] in REQUIRED_QUERIES.get((method, path), set()):
                parameter["required"] = True
        query = []
        url_path = path
        for parameter in parameters:
            if parameter["in"] == "path":
                url_path = url_path.replace("{" + parameter["name"] + "}", parameter_value(parameter))
            elif parameter["in"] == "query":
                enabled = parameter.get("required", False) or parameter["name"].lower() in {"page", "pagesize", "take"}
                query.append({"key": parameter["name"], "value": parameter_value(parameter),
                              "disabled": not enabled,
                              "description": ("Obligatoire. " if parameter.get("required") else "Facultatif : cochez pour activer. ") + parameter.get("description", "")})
        enabled_query = "&".join(f"{q['key']}={q['value']}" for q in query if not q["disabled"])
        raw_url = "{{baseUrl}}" + url_path + ("?" + enabled_query if enabled_query else "")
        note = summary + "\n\n" + operation.get("description", "")
        request = {"method": method.upper(), "header": [{"key": "Accept", "value": "application/json"}],
                   "url": {"raw": raw_url, "host": ["{{baseUrl}}"], "path": url_path.lstrip("/").split("/"), "query": query},
                   "description": note.strip()}
        if path == "/api/v1/auth/token":
            request["auth"] = {"type": "noauth"}
        request_body = operation.get("requestBody", {}).get("content", {})
        schema, example, body_kind = None, None, "Aucun corps"
        if "application/json" in request_body:
            schema = request_body["application/json"].get("schema", {})
            example = body_example(schema)
            request["header"].append({"key": "Content-Type", "value": "application/json"})
            request["body"] = {"mode": "raw", "raw": json.dumps(example, ensure_ascii=False, indent=2),
                               "options": {"raw": {"language": "json"}}}
            body_kind = "JSON"
            BODY_COUNT += 1
        elif "multipart/form-data" in request_body:
            request["body"] = {"mode": "formdata", "formdata": [
                {"key": "fichier", "type": "file", "src": [], "description": "Sélectionner un vrai fichier STC : lignes de 1000 caractères, 20 Mo maximum."}]}
            request["description"] += "\n\nBody > form-data > fichier (File). Sélectionnez votre fichier STC. Ne pas définir Content-Type manuellement."
            body_kind = "Fichier STC : form-data, champ fichier"
            UPLOAD_COUNT += 1
        elif request_body:
            raise ValueError(f"Unhandled content type: {method} {path}: {request_body.keys()}")
        if method in {"post", "put", "patch", "delete"}:
            request["description"] += "\n\nCette requête modifie des données ou déclenche une opération métier, sauf les aperçus/analyse explicitement documentés. Utiliser des enregistrements de test."
        if path == "/api/v1/auth/mot-de-passe":
            request["description"] += "\nLe compte de service dev ne peut pas changer son mot de passe ici : utiliser le jeton d'un utilisateur personnel authentifié."
        if path.startswith("/api/v1/reference"):
            request["description"] += "\nLa suppression exige le rôle/permission de paramétrage selon la politique de l'API."
        item = {"name": method.upper() + " " + path, "request": request,
                "event": tests_for(path, method, operation), "response": []}
        GROUPS.setdefault(group, []).append(item)
        ROWS.append({"group": group, "path": path, "method": method.upper(), "summary": summary,
                     "parameters": parameters, "example": example, "schema": schema, "kind": body_kind,
                     "request": request, "responses": operation.get("responses", {})})

# These four routes exist in Program.cs, outside the controller operations in this OpenAPI snapshot.
for path, description in [("/health/live", "Processus API en cours d'exécution."),
                          ("/health/ready", "Connexion à la base attendue ; ne vérifie pas toutes les tables."),
                          ("/swagger", "Explorateur de documentation HTML."),
                          ("/openapi/v1.json", "Document OpenAPI de l'API.")]:
    if path in SPEC["paths"]:
        continue
    request = {"method": "GET", "auth": {"type": "noauth"}, "header": [],
               "url": {"raw": "{{baseUrl}}" + path, "host": ["{{baseUrl}}"], "path": path.lstrip("/").split("/")},
               "description": description}
    GROUPS.setdefault("00 - Santé et documentation", []).append({
        "name": "GET " + path, "request": request,
        "event": tests_for(path, "get", {"responses": {"200": {}}}), "response": []})
    ROWS.append({"group": "00 - Santé et documentation", "path": path, "method": "GET", "summary": description,
                 "parameters": [], "example": None, "schema": None, "kind": "Aucun corps", "responses": {"200": {}}, "request": request})

for row in ROWS:
    for name in re.findall(r"\{\{([^{}]+)\}\}", json.dumps(row["request"], ensure_ascii=False)):
        if not name.startswith("$") and name not in VALUES:
            raise ValueError(f"Undeclared variable: {name}")

collection = {
    "info": {"_postman_id": str(uuid.uuid4()), "name": "Logistics API - Tous les endpoints",
             "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json",
             "description": "159 opérations des contrôleurs + santé et documentation. Exemples générés depuis l'OpenAPI de votre API locale et vérifiés contre les routes sources. Importer l'environnement, envoyer auth/token puis les GET. Adapter les références A_REMPLACER à vos données réelles. Les opérations métier doivent être envoyées individuellement sur des enregistrements de test ; cette collection n'est pas un scénario de création/suppression à exécuter intégralement."},
    "auth": {"type": "bearer", "bearer": [{"key": "token", "value": "{{token}}", "type": "string"}]},
    "variable": [{"key": key, "value": value, "type": "string"} for key, value in VALUES.items()],
    "item": [{"name": name, "item": items} for name, items in sorted(GROUPS.items())],
}
environment = {"id": str(uuid.uuid4()), "name": "Logistics - Local HTTP",
               "values": [{"key": key, "value": value, "type": "secret" if "passe" in key.lower() or key == "token" else "default", "enabled": True}
                          for key, value in VALUES.items()],
               "_postman_variable_scope": "environment", "_postman_exported_using": "Codex"}
for filename, data in [("Logistics.postman_collection.json", collection), ("Logistics.local.postman_environment.json", environment),
                       ("Enums.json", ENUMS)]:
    (OUT / filename).write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

catalog = ["# Toutes les API Logistics avec exemples Postman", "",
           f"{len(ROWS)} requêtes : {len(ROWS)-4} opérations des contrôleurs, 2 endpoints de santé et 2 endpoints de documentation.",
           f"{BODY_COUNT} corps JSON ; {UPLOAD_COUNT} imports de fichiers STC. Base URL : `{{{{baseUrl}}}}` = `http://localhost:5192`.",
           "", "Les exemples respectent les modèles JSON ; les clés et codes A_REMPLACER doivent exister dans votre base. Les règles métier et états (validation, stock, facture réglée, booking affecté…) peuvent refuser une requête pourtant correcte au format JSON.",
           "Les enums du corps JSON sont des nombres. Les enums en paramètres peuvent utiliser le nom C# (exemple TrackingEntity : Client). Voir Enums.json pour les valeurs.", ""]
catalog += ["Les variables sont préremplies avec les données vérifiées : admin, S01, AG01, CLI001, P001, typeBooking=I, typeDossier=I, MAD, MA, FOB. Adapter le mot de passe si vous l'avez changé. Les références `A_REMPLACER` ne sont pas présentes dans le jeu de données initial. Le calcul de coût attend `moisCalcul` au format `MM/yyyy` (exemple : `10/2026`).", ""]
for group in sorted(GROUPS):
    catalog += ["## " + group, ""]
    for row in (row for row in ROWS if row["group"] == group):
        catalog += ["### " + row["method"] + " " + row["path"], "", row["summary"] or "Requête exposée par le contrôleur.", "",
                    "URL : `" + row["request"]["url"]["raw"] + "`", "",
                    "Authentification : " + ("aucune." if row["request"].get("auth", {}).get("type") == "noauth" else "Bearer `{{token}}`."), ""]
        if row["parameters"]:
            catalog += ["| Paramètre | Emplacement | Obligatoire | Exemple |", "|---|---|---|---|"]
            for parameter in row["parameters"]:
                catalog.append(f"| {parameter['name']} | {parameter['in']} | {'Oui' if parameter.get('required') else 'Non'} | `{parameter_value(parameter)}` |")
            catalog.append("")
        if row["example"] is not None:
            catalog += ["Body → raw → JSON :", "", "```json", json.dumps(row["example"], ensure_ascii=False, indent=2), "```", ""]
        else:
            catalog += ["Corps : " + row["kind"] + ".", ""]
        codes = sorted(key for key in row["responses"] if key.isdigit())
        catalog += ["Codes documentés : " + ", ".join(codes) + ".", ""]
(OUT / "Toutes-les-API.md").write_text("\n".join(catalog) + "\n", encoding="utf-8")

counts = Counter(row["group"] for row in ROWS)
guide = """# Tester Logistics dans Postman

1. Démarrer Logistics.Api avec le profil **http** et l'environnement **Development**.
2. Dans Postman, **Import**, puis sélectionner `Logistics.postman_collection.json` et `Logistics.local.postman_environment.json`.
3. Sélectionner l'environnement **Logistics - Local HTTP** en haut de Postman. Vérifier `baseUrl=http://localhost:5192`.
4. Envoyer **00 - Santé et documentation → GET /health/live**, puis **GET /health/ready**. Attendu : HTTP 200. La deuxième requête teste la connexion, pas l'intégralité du schéma.
5. Envoyer **01 - Authentification → POST /api/v1/auth/token**. Le JSON utilise le compte personnel de test `admin`/`Admin123!`, la société `S01` et l'agence `AG01`. Si vous avez déjà changé le mot de passe, mettre sa valeur actuelle dans `motDePasse` et `ancienMotDePasse`. Le script de réponse enregistre automatiquement `accessToken` dans la variable `token`. Les autres requêtes héritent de Bearer `{{token}}`.
6. Envoyer les GET de listes, puis copier les clés utiles dans les variables de l'environnement (`keyClient`, `keyVoyage`, `keyDossier`, etc.). Les POST de création enregistrent automatiquement la clé principale de leur réponse après réussite.
7. Ouvrir une requête POST/PUT, vérifier son **Body → raw → JSON**, adapter les codes/références aux données de votre société, puis cliquer **Send**. Les exemples contenant `A_REMPLACER` demandent vos valeurs réelles. Une syntaxe correcte ne garantit pas que les règles métier accepteront l'opération.
8. Les paramètres facultatifs apparaissent décochés dans **Params** ; cocher uniquement les filtres voulus. Les paramètres obligatoires et la pagination sont activés.
9. Pour EDI, utiliser **Body → form-data**, champ `fichier`, type **File**, puis choisir un fichier STC réel (1000 caractères par ligne, maximum 20 Mo). Ne pas envoyer de JSON et ne pas définir Content-Type manuellement.
10. Les tests de l'onglet **Scripts → Post-response** vérifient les codes de succès. Un statut 400/401/403/404/409/422/500/503 reste un test en échec, même s'il est documenté. Cette collection complète est un catalogue pour tester manuellement, pas un scénario métier à lancer entièrement avec Runner : certaines requêtes annulent/suppriment des données, changent un mot de passe ou consomment du staging EDI.

Le compte `admin` est le personnel P001 de votre jeu de données ; `dupont`/`User123!` est P002, sans rôle administrateur. Ces deux comptes existent dans la base et ne sont pas bloqués. Leur mot de passe n'a pas été changé ni vérifié par cette génération. Les variables existantes ont été préremplies, dont `keyClient=CLI001`, `keyPersonnel=P001`, `keyAgence=AG01`, `typeDossier=I`, `typeBooking=I`. Les références restantes `A_REMPLACER` nécessitent les données correspondantes ou une création préalable via les POST. Pour tester une sortie/annulation/suppression, utiliser un enregistrement de test adapté à l'état métier requis.

Les fichiers ont été vérifiés pour la couverture des routes, les paramètres, le format JSON et les scripts. Les 159 opérations métier n'ont pas été exécutées contre vos données.

`Toutes-les-API.md` contient chaque route et chaque JSON d'exemple. `Enums.json` donne les valeurs C# réelles. `Logistics.Api.openapi.json` est le document capturé depuis votre API locale.

| Module | Nombre de requêtes |
|---|---|
"""
for name, count in sorted(counts.items()):
    guide += f"| {name} | {count} |\n"
guide += "\nLe code `typeBooking=I` existe dans votre jeu de données. `moisCalcul` utilise le format `MM/yyyy` (exemple : `10/2026`). Les codes `ZZ` (pays) et `TST` (devise) sont des exemples de création ; s'ils existent déjà, choisir d'autres codes.\n"
guide += "\nRéférences : [Variables Postman](https://learning.postman.com/docs/use/send-requests/variables/environment-variables), [scripts de variables](https://learning.postman.com/docs/tests-and-scripts/write-scripts/postman-sandbox-reference/pm-variables).\n"
(OUT / "README.md").write_text(guide, encoding="utf-8")

def curl_quote(value):
    return "'" + value.replace("'", "'\"'\"'") + "'"


curl_examples = ["# cURL pour importer chaque requête dans Postman", "",
                 "Postman → Import → Raw text : coller un seul bloc cURL à la fois. Sélectionner l'environnement Logistics - Local HTTP.",
                 "Les variables sont conservées lors de l'import. Obtenir d'abord le jeton avec la collection ; le script du POST /auth/token remplit token automatiquement.", ""]
for row in sorted(ROWS, key=lambda row: (row["group"], row["path"], row["method"])):
    request = row["request"]
    command = ["curl --request " + row["method"] + " " + curl_quote(request["url"]["raw"])]
    if request.get("auth", {}).get("type") != "noauth":
        command.append("  --header 'Authorization: Bearer {{token}}'")
    for header in request["header"]:
        command.append("  --header " + curl_quote(header["key"] + ": " + header["value"]))
    if request.get("body", {}).get("mode") == "raw":
        command.append("  --data-raw " + curl_quote(request["body"]["raw"]))
    elif request.get("body", {}).get("mode") == "formdata":
        command.append("  --form 'fichier=@/chemin/vers/votre-fichier.stc'")
    curl_examples += ["## " + row["method"] + " " + row["path"], "", "```bash",
                      " \\\n".join(command), "```", ""]
(OUT / "Toutes-les-API-cURL.md").write_text("\n".join(curl_examples), encoding="utf-8")
data = {
    "source": "Clés/codes vérifiés en lecture seule dans LogisticsDb. Mots de passe initiaux provenant du script fourni par l'utilisateur, non vérifiés et potentiellement déjà changés.",
    "connexion": {"baseUrl": VALUES["baseUrl"], "societeKey": "S01", "agenceKey": "AG01"},
    "comptes": [{"identifiant": "admin", "motDePasseInitial": "Admin123!", "keyPersonnel": "P001", "administrateur": True},
                {"identifiant": "dupont", "motDePasseInitial": "User123!", "keyPersonnel": "P002", "administrateur": False}],
    "clients": ["CLI001", "CLI002"], "agences": ["AG01", "AG02"], "typesDossierEtBooking": ["I", "E"],
    "devises": ["MAD", "EUR", "USD"], "incoterms": ["FOB", "CIF", "EXW"],
    "ports": ["MACAS", "MAPTM"], "classesClient": ["A", "B"],
    "references_a_fournir": [key for key, value in VALUES.items() if value == "A_REMPLACER"],
    "note": "Les clés des nouveaux clients, voyages, dossiers, bookings, cotations, factures, règlements, barèmes, citernes, approvisionnements, bons, personnel et matériel sont récupérées par les scripts après un POST de création réussi. Les fournisseurs, modes de paiement, trésoreries, rubriques et taux TVA doivent aussi exister pour les opérations concernées. Aucun enregistrement n'a été créé par la préparation de ce pack."
}
(OUT / "Donnees-de-test.json").write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

with zipfile.ZipFile(OUT / "Logistics-Postman.zip", "w", compression=zipfile.ZIP_DEFLATED) as archive:
    for filename in ["Logistics.postman_collection.json", "Logistics.local.postman_environment.json", "Toutes-les-API.md", "Toutes-les-API-cURL.md", "Donnees-de-test.json", "README.md", "Enums.json", "Logistics.Api.openapi.json"]:
        archive.write(OUT / filename, arcname=filename)
print(json.dumps({"requests": len(ROWS), "json_bodies": BODY_COUNT, "file_uploads": UPLOAD_COUNT,
                  "groups": dict(counts), "variables": len(VALUES)}, ensure_ascii=False))
