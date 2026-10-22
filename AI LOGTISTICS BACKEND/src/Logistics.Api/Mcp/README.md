# MCP Logistics

Endpoint : **`/mcp`**, transport Streamable HTTP du SDK officiel
`ModelContextProtocol.AspNetCore` 2.2.0. Authentification avec le bearer token
existant de l'API. Les tokens dans la query string ne sont pas acceptés.

Le serveur est sans session : chaque requête conserve le scope HTTP et le
`CallerContext` authentifié. Il réutilise les contrôles et le comportement des
services métier et repositories existants. Il n'ajoute pas de règles métier,
de contrôleur, de connexion au chatbot, de RAG ou de traitement SignalR.

| Tool | Arguments | Service métier appelé |
| --- | --- | --- |
| `GetClient` | `keyClient`, `byReference` facultatif (booléen) | `IClientService.GetAsync` ou `GetByReferenceAsync` |
| `GetInvoice` | `keyFacture` | `IFactureVenteService.GetAsync` |
| `GetShipment` | `keyDossier` | `IDossierService.GetAsync` |
| `GetTracking` | `entity`, `entityKey`, `take` facultatif | `ITrackingService.GetTimelineAsync` |
| `GetBooking` | `keyBooking` | `IBookingService.GetAsync` |

`GetShipment` désigne un **dossier d'expédition**, pas un voyage.
Pour une référence client, passer sa valeur exacte dans `keyClient` et `byReference: true`.
Le service recherche cette référence dans la société courante puis réutilise la lecture normale
de la fiche. Une référence absente ou ambiguë est refusée ; aucune clé interne n’est inventée.
Sans ce drapeau, `keyClient` reste une clé interne, comme auparavant.
`entity` accepte exactement `Client`, `Voyage`, `Dossier`, `FactureVente`,
`Reglement`, `Booking`, `Cotation`. `take` vaut 100 par défaut et accepte un
entier de 1 à 500. Une liste de suivi vide conserve le sens donné par le service :
aucun événement visible ; elle ne constitue pas une preuve d'existence de l'entité.

## Contrat et sécurité

`tools/list` publie les cinq `inputSchema` fermés (`additionalProperties: false`),
les `outputSchema` générés depuis les DTO métier et les annotations de lecture
seule. Les identifiants sont des chaînes non vides, sans caractères de contrôle,
limitées à 256 caractères à la frontière MCP. Les services continuent à décider
des règles métier et de l'existence des entités.

La liste de tools est explicite. Aucun tool SQL, nom de table, connexion,
expression de filtre ou changement de société n'est exposé. Les arguments
inconnus sont refusés avant tout appel métier. Les classes MCP ne dépendent
d'aucun repository, `DbContext` ou exécuteur SQL. Les lectures passent par les
services, puis leurs repositories existants.

Les requêtes contenant un en-tête `Origin` doivent correspondre à une entrée
exacte de `Cors:AllowedOrigins`, sinon elles reçoivent HTTP 403. Un client MCP
natif sans `Origin` reste utilisable avec son bearer token.

## Résultats et erreurs

Chaque résultat contient `structuredContent` et sa représentation JSON dans
un bloc texte, comme le prévoit la [documentation officielle des tools MCP](https://github.com/modelcontextprotocol/csharp-sdk/blob/v2.2.0/docs/concepts/tools/tools.md).

```json
{
  "success": true,
  "data": { "keyClient": "CLI001", "raisonSociale": "..." },
  "error": null,
  "correlationId": "..."
}
```

`data` reprend le DTO complet du service ; l'extrait ci-dessus est abrégé.
Pour `GetTracking`, `data` est un tableau d'événements.

En cas d'échec, `isError` vaut `true`, `success` vaut `false`, `data` vaut `null`
et `error` contient `code`, `message` et éventuellement `field`. Les codes
incluent `validation_failed`, `not_found`, `forbidden`, les codes des exceptions
métier existantes, `database_unavailable` pour une `DbException` et `server_error`
pour les échecs inattendus. Les messages internes du provider ne sont pas exposés.
Un tool inconnu produit une erreur de protocole JSON-RPC `-32602`.

L'annulation est transmise au service et propagée au SDK, sans fabriquer un
résultat métier. Les journaux contiennent le nom du tool, sa durée, son résultat,
le type d'exception et son identifiant de corrélation. Ils ne contiennent ni
arguments, ni DTO, ni message interne d'exception. Chercher `McpCorrelationId`
ou l'identifiant retourné dans les journaux.

## Essai avec un client MCP ou Postman

Utiliser l'URL `http://localhost:<port>/mcp` et l'en-tête :

```http
Authorization: Bearer <token de /api/v1/auth/token>
Content-Type: application/json
Accept: application/json, text/event-stream
MCP-Protocol-Version: 2025-11-25
```

Pour un essai JSON-RPC manuel compatible avec la révision 2025-11-25, envoyer
successivement les messages suivants en POST sur la même URL. La réponse HTTP
peut être un flux SSE contenant la réponse JSON-RPC.

```json
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"Postman","version":"1.0"}}}
```

```json
{"jsonrpc":"2.0","method":"notifications/initialized"}
```

```json
{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}
```

```json
{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"GetClient","arguments":{"keyClient":"CLI001"}}}
```

Autres valeurs de `params` pour `tools/call` (remplacer les clés par celles de
votre base ; `FAC001`, `DOS001` et `BK001` sont des exemples) :

```json
{"name":"GetInvoice","arguments":{"keyFacture":"FAC001"}}
```

```json
{"name":"GetShipment","arguments":{"keyDossier":"DOS001"}}
```

```json
{"name":"GetTracking","arguments":{"entity":"Dossier","entityKey":"DOS001","take":25}}
```

```json
{"name":"GetBooking","arguments":{"keyBooking":"BK001"}}
```

Un client SDK actuel peut aussi négocier la révision 2026-07-28 ; le SDK gère sa
découverte et ses métadonnées par requête.

## Compilation et tests

Le `NuGet.config` du dépôt désactive les sources distantes. Sur une machine où
le SDK MCP n'est pas encore en cache, restaurer explicitement depuis NuGet :

```powershell
dotnet restore Logistics.slnx --source https://api.nuget.org/v3/index.json --disable-parallel
dotnet build Logistics.slnx --no-restore -m:1
dotnet test tests/Logistics.UnitTests/Logistics.UnitTests.csproj --no-build --no-restore
dotnet test tests/Logistics.IntegrationTests/Logistics.IntegrationTests.csproj --no-build --no-restore --filter FullyQualifiedName~McpHttpTests
```

`McpToolTests` couvre chaque tool : délégation, sortie structurée, validation,
arguments SQL refusés, erreurs du service, logging et annulation.
`McpHttpTests` utilise le vrai client MCP officiel et Kestrel avec des services
simulés : découverte, deux versions du protocole, authentification, origines,
scope de l'appelant, erreurs et reprise après échec. Aucun test MCP ne modifie
la base ni n'appelle un LLM.

Si VSTest échoue avant la découverte des tests avec `Win32Exception (5)`, le
runner existant exécute les mêmes méthodes xUnit dans le processus courant :

```powershell
dotnet restore tests/Logistics.Verify/Logistics.Verify.csproj
dotnet build tests/Logistics.Verify/Logistics.Verify.csproj --no-restore -m:1
dotnet tests/Logistics.Verify/bin/Debug/net10.0/Logistics.Verify.dll --unit-tests-only
dotnet tests/Logistics.Verify/bin/Debug/net10.0/Logistics.Verify.dll --mcp-tests-only
```

`--unit-tests-only` ne lance aucun serveur HTTP. `--mcp-tests-only` lance aussi
les tests HTTP avec un port local temporaire et nécessite que cet usage soit
autorisé par l'environnement d'exécution.
