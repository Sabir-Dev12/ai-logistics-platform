# Orchestration AI Agent

```text
ChatHub → IChatService / ChatService → IAgentService / AgentService
                                           ├── ILLMService : plan des sources
                                           ├── IRetrievalService : connaissances documentaires
                                           └── IAgentMcpTools : données métier via le dispatcher MCP existant
```

L'agent est indépendant de SignalR, des controllers et des repositories. Les
tools conservent leur chaîne MCP → service métier → repository → base.
`ChatHub` est inchangé et ne contient aucune règle métier.

## Contrat du LLM

L'adaptateur `ILLMService` existant doit retourner un plan JSON strict depuis
`GenerateResponseAsync`. Il reçoit le contrat, la question et les noms,
descriptions et input schemas des tools disponibles.

```json
{
  "requiresBusinessData": true,
  "useRag": true,
  "toolCalls": [
    { "name": "GetInvoice", "arguments": { "keyFacture": "FAC001" } }
  ]
}
```

Pour une question documentaire : `requiresBusinessData: false`, `useRag: true`
et `toolCalls: []`. Pour une question métier, le plan doit demander MCP.
Les champs inconnus, les réponses libres du modèle, les plans vides/malformés
et plus de quatre calls sont refusés. Le modèle ne peut proposer que les cinq
tools en lecture seule : `GetClient`, `GetInvoice`, `GetShipment`, `GetTracking`
et `GetBooking`. Chaque clé métier doit être explicitement présente dans la
question ; sinon l'agent demande la clé ou signale `identifier_required`.

Exemples de questions :

- `Quel est le montant actuel de FAC001 ?`
- `Donne le suivi du dossier DOS001.`
- `Quel est le statut du booking BK001 et quelle procédure s'applique ?`
- `Quelle est la procédure documentaire de livraison ?`

## Données vérifiées et rendu

Le LLM planifie les sources. Le rendu final est construit par le serveur :

- Les données métier sont **uniquement** les objets/tableaux retournés avec
  succès par les tools MCP. Aucune narration libre du LLM ne peut ajouter un
  montant, une date, un statut ou une autre donnée métier.
- Les passages RAG sont reproduits avec document, index et positions ; ils
  sont étiquetés comme connaissances documentaires et ne prouvent pas un état
  métier actuel.
- Un échec, un résultat manquant ou l'absence de tool est explicitement signalé.
  L'agent ne remplace pas une donnée dynamique indisponible par un passage RAG
  ou une supposition.

Ce mode retourne des données métier structurées et des extraits documentaires,
plutôt qu'une synthèse libre. La sélection des sources dépend du plan du modèle ;
si celui-ci omet MCP, aucune donnée métier dynamique n'est annoncée comme confirmée.
Cette implémentation réalise un plan borné suivi de son exécution, sans boucle
automatique de correction ou conversation persistante.

`StreamResponseAsync` publie chaque résultat vérifié dès qu'il est disponible,
avant l'exécution des tools suivants, puis les passages documentaires. Il ne
découpe pas une réponse LLM préalablement générée. `ChatService` continue à
transmettre les fragments au Hub avec le même contrat `ChatResponse.Message`.

## MCP, contexte et erreurs

`AgentMcpTools` utilise directement `LogisticsMcpDispatcher` dans la portée DI
courante : les mêmes tools, schemas, validations et erreurs sont réutilisés.
Aucune connexion SQL ou requête arbitraire n'est accessible à l'agent.
Les arguments supplémentaires comme `sql`, `table` ou `societeKey` restent
refusés par les schemas/validateurs MCP existants.

`AgentContext` construit la collection RAG `<SocieteKey>:documents` depuis
`ITenantContext` ; le modèle ne peut pas choisir une autre société ou collection.
Indexer les documents destinés à l'agent dans cette collection. Le filtre
`ChatCallerContextFilter` transfère l'identité HTTP déjà authentifiée vers la
portée de l'invocation SignalR, sans activer de fallback par headers.

Chaque tentative de tool produit des événements `started`, puis `succeeded`,
`failed` ou `cancelled`, avec un identifiant de call et la corrélation MCP
disponible. Les arguments, DTO, question, SQL et credentials ne sont pas loggés
par l'audit de l'agent. Les erreurs des tools sont conservées sous forme de codes
sanitisés ; les messages internes d'exception ne sont pas retournés. Un tool
qui échoue n'empêche pas le traitement des autres sources prévues.

L'annulation se propage au LLM, à MCP et à RAG. Elle interrompt le traitement
au lieu de fabriquer une réponse. Les calls répétés avec les mêmes arguments
dans un plan ne relisent pas les données.

## Configuration et vérification

L'API enregistre l'agent avec le chat, le backend RAG et MCP. Les adaptateurs
`ILLMService` et `IEmbeddingService` restent à configurer pour un usage réel.
Le fournisseur LLM doit respecter le plan JSON ci-dessus ; RAG utilise le
modèle d'embeddings configuré et le stockage vectoriel précédemment enregistré.

Les tests `AgentOrchestrationTests` contrôlent les plans, les appels mixtes,
les erreurs, l'absence d'invention, l'allowlist, les identifiants, les citations,
l'annulation et le streaming. `AgentMcpAdapterTests` vérifie la réutilisation
du vrai dispatcher/tool MCP, la validation et le passage de l'identité à la
portée SignalR. Les dépendances LLM/RAG et la base sont simulées : aucune
requête externe, ouverture de port ou modification de base n'est nécessaire.

```powershell
dotnet build Logistics.slnx --no-restore -m:1
dotnet test tests/Logistics.UnitTests/Logistics.UnitTests.csproj --no-build --no-restore --filter FullyQualifiedName~Agent
```

Si VSTest est bloqué par l'environnement, exécuter les mêmes méthodes xUnit
avec le runner local existant :

```powershell
dotnet build tests/Logistics.Verify/Logistics.Verify.csproj --no-restore -m:1
dotnet tests/Logistics.Verify/bin/Debug/net10.0/Logistics.Verify.dll --unit-tests-only
```
