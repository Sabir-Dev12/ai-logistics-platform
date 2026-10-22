# Backend RAG

Le module retourne des passages documentaires pertinents pour une question.
Il reste indépendant du chatbot, des controllers, de SignalR, de MCP et des
services métier. Aucun endpoint HTTP ou génération de réponse LLM n'est ajouté.

## Organisation

Dans `Logistics.Application/AI/RAG` :

- `IDocumentLoader` : charge un document depuis un flux fourni par l'appelant.
- `IChunkingService` / `CharacterChunkingService` : découpe le texte avec chevauchement.
- `IEmbeddingService` : adapte le fournisseur d'embeddings choisi.
- `IVectorStore` : remplace un document et recherche ses passages vectorisés.
- `IRetrievalService` / `RetrievalService` : vectorise la question puis retourne les passages pertinents.
- `IDocumentIndexingService` / `DocumentIndexingService` : orchestre chargement, découpage, embeddings et stockage.

Dans `Logistics.Infrastructure/AI/RAG` :

- `TextDocumentLoader` : charge du texte UTF-8 (`.txt`, Markdown ou texte déjà extrait), en laissant le flux ouvert.
- `InMemoryVectorStore` : stockage vectoriel en mémoire avec similarité cosinus.
- `RagServiceCollectionExtensions` : enregistrement DI indépendant via `AddRagBackend()`.
- `UnconfiguredEmbeddingService` : signale explicitement l'absence d'un fournisseur configuré.

## Indexation

```text
DocumentSource
    → IDocumentLoader
    → IChunkingService
    → IEmbeddingService pour chaque passage
    → IVectorStore.ReplaceDocumentAsync
```

L'indexation calcule tous les embeddings avant de remplacer le document.
Une erreur de chargement, de découpage, d'embedding, de compatibilité ou une
annulation avant validation du remplacement conserve l'ancien index. Réindexer
un document remplace également ses anciens passages devenus inutiles et ne crée
pas de doublons. Les autres documents de la collection sont conservés.

Chaque passage conserve son identifiant, celui du document, son index, son texte
exact, sa source et ses positions dans le texte original. Les positions sont en
unités UTF-16, avec une fin exclusive. Les fenêtres ne séparent pas les deux
caractères d'une paire de substitution Unicode. Ce découpage se mesure en
caractères, pas en tokens du modèle.

Par défaut : 1000 caractères par passage, chevauchement de 150 caractères,
maximum de 10 000 passages et de 1 000 000 caractères par document.

## Recherche

```text
RetrievalRequest
    → IEmbeddingService pour la question
    → IVectorStore.SearchAsync
    → IReadOnlyList<RetrievedPassage>
```

Un `RetrievedPassage` contient `Chunk` et `Score`. Les scores de similarité
cosinus sont compris entre -1 et 1. Les passages sont triés par score décroissant,
filtrés par `MinimumScore` et limités par `TopK`. Les égalités sont ordonnées de
manière déterministe par document et index de passage. Par défaut : `TopK = 5`
et `MinimumScore = 0`. Une recherche sans résultat retourne une liste vide.
Les scores expriment une similarité, pas une probabilité ou une garantie de réponse.

Les collections sont isolées par identifiant exact. L'appelant doit construire
`CollectionId` depuis son contexte autorisé ; le module interne ne décide pas
des permissions de l'utilisateur. Chaque collection utilise un seul modèle,
une seule version et un seul nombre de dimensions. Un changement nécessite une
réindexation dans une autre collection.

## Fournisseur et configuration

Le fournisseur d'embeddings doit être enregistré via `IEmbeddingService`, en
utilisant le **même modèle/version pour les documents et les questions**. Son
adaptateur gère son SDK, ses credentials et ses appels réseau. Les vecteurs sont
copiés, immuables, non nuls, de dimension positive et ne contiennent aucune
valeur NaN ou infinie. Utiliser un `ModelId` stable identifiant le modèle et sa version.

L'API enregistre déjà `AddRagBackend()`. En l'absence d'adaptateur, indexation et
retrieval lèvent `EmbeddingNotConfiguredException` ; aucun embedding factice
n'est utilisé en production. Un adaptateur personnalisé enregistré avant
`AddRagBackend()` est conservé. Il peut également être enregistré après pour
remplacer le choix par défaut de DI.

Exemple d'utilisation, une fois votre adaptateur d'embeddings configuré :

```csharp
// Dans une portée DI existante :
var indexer = scope.ServiceProvider.GetRequiredService<IDocumentIndexingService>();
var retrieval = scope.ServiceProvider.GetRequiredService<IRetrievalService>();

using var content = new MemoryStream(Encoding.UTF8.GetBytes(
    "Le délai de livraison standard est de trois jours."));
await indexer.IndexAsync("S01:procedures", new DocumentSource(
    "guide-livraison", content, "guide-livraison.txt"), cancellationToken);

var passages = await retrieval.RetrieveAsync(new RetrievalRequest(
    "S01:procedures", "Quel est le délai de livraison ?", TopK: 3, MinimumScore: 0.5),
    cancellationToken);

foreach (var passage in passages)
{
    Console.WriteLine($"{passage.Chunk.SourceName} ({passage.Score:F3})");
    Console.WriteLine(passage.Chunk.Text);
}
```

Pour ajuster les limites dans un autre hôte :

```csharp
services.AddRagBackend(
    new ChunkingOptions { ChunkSize = 800, Overlap = 100, MaximumChunks = 5000 },
    new DocumentLoadingOptions { MaximumCharacters = 500_000 });
```

Le stockage fourni est **en mémoire** : les données disparaissent au redémarrage.
Pour une persistance, injecter un adaptateur `IVectorStore` respectant l'isolation,
le classement, la compatibilité des embeddings et le remplacement atomique.
Les formats PDF/Word nécessitent un adaptateur `IDocumentLoader` d'extraction de
texte. Les flux appartiennent à l'appelant et doivent être fermés par celui-ci.

## Tests

Les suites `RagDocumentLoaderTests`, `RagChunkingTests`, `RagVectorStoreTests`,
`RagRetrievalTests`, `RagIndexingTests` et `RagRegistrationTests` couvrent :
chargement UTF-8, limites, sources, Unicode, chevauchement, compatibilité,
classement, isolation, réindexation, atomicité, concurrence, erreurs, annulation
et injection des adaptateurs. Le parcours indexation → retrieval est testé avec
des embeddings de fixture déterministes ; il ne mesure pas la qualité d'un
fournisseur d'embeddings externe.

```powershell
dotnet build Logistics.slnx --no-restore -m:1
dotnet test tests/Logistics.UnitTests/Logistics.UnitTests.csproj --no-build --no-restore --filter FullyQualifiedName~Rag
```

Pour l'environnement où VSTest échoue avant découverte avec
`Win32Exception (5)`, le runner existant exécute les mêmes tests xUnit sans
processus testhost et sans accès à la base ou ouverture d'un port :

```powershell
dotnet build tests/Logistics.Verify/Logistics.Verify.csproj --no-restore -m:1
dotnet tests/Logistics.Verify/bin/Debug/net10.0/Logistics.Verify.dll --unit-tests-only
```
