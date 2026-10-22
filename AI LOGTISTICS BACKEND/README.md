# AI Logistics Backend

Backend **ASP.NET Core / .NET 10** pour la gestion logistique, les paiements et l’assistance IA locale.

Consulter le [README principal](../README.md) pour les captures, l’installation complète et les commandes de lancement.

## Organisation

| Projet | Rôle |
| --- | --- |
| Logistics.Domain | Entités, règles métier et exceptions. |
| Logistics.Application | Services métier, DTOs, contrats, Chat et Agent. |
| Logistics.Infrastructure | EF Core SQL Server, repositories, audit, Ollama et stockage RAG. |
| Logistics.Api | REST, authentification, SignalR, MCP et gestion des erreurs. |

La chaîne métier suit `Controller → Service → Repository → SQL Server`. Le chatbot suit `ChatHub → ChatService → AgentService`, puis consulte les sources documentaires ou les outils MCP autorisés. Le Hub ne contient pas de logique métier.

## Exécution

Configurer `ConnectionStrings__LogisticsDatabase` et `Authentication__SigningKey` dans l’environnement avant de lancer :

```powershell
dotnet restore
dotnet run --project src/Logistics.Api --launch-profile http
```

Les paramètres `AI:Ollama` définissent le serveur et les modèles locaux. Les documents importés restent dans `App_Data/rag`, exclu du dépôt.

## Vérification

```powershell
dotnet build Logistics.slnx
dotnet test tests/Logistics.UnitTests
dotnet run --project tests/Logistics.Verify -- --unit-tests-only
```

Le dernier runner est une alternative pour les environnements restreints où VSTest ne peut pas lancer son processus de test. Le lancer sans flag peut exécuter des scénarios de base de données.

## Ressources

- [Scripts et guide SQL Server](Database/README.md)
- [Contrats et collection Postman](Postman/README.md)
- [Chatbot, MCP et RAG](docs/Chatbot-MCP-RAG.md)
- [Knowledge Base](docs/Knowledge-Base-backend.md)
- [Ollama local](docs/Ollama-local.md)

La configuration de publication utilise NuGet.org pour permettre une restauration sur un autre poste. Les clés de signature, identifiants locaux, sauvegardes SQL et documents personnels ne sont pas publiés.
