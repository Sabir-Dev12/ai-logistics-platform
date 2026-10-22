# Ollama local — petits modèles

Configuration du backend : `AI:Ollama` dans `src/Logistics.Api/appsettings.json`.

Modèles par défaut : `qwen3:1.7b` (chat et planification JSON de l’agent) et `embeddinggemma:300m` (embeddings RAG multilingues). Téléchargements approximatifs : 1,4 Go et 622 Mo. Ces tailles disque ne sont pas les besoins RAM. Ollama 0.11.10 ou plus récent est nécessaire pour EmbeddingGemma.

Dans PowerShell, après installation/démarrage d’Ollama :

```powershell
ollama pull qwen3:1.7b
ollama pull embeddinggemma:300m
ollama list
ollama run qwen3:1.7b
```

Quitter le chat CLI avec `/bye`. Ollama Windows fonctionne normalement en arrière-plan. Si le serveur n’est pas lancé, exécuter `ollama serve` dans un terminal séparé ; ne pas lancer une deuxième instance si le port 11434 est déjà occupé.

Le backend appelle uniquement `http://127.0.0.1:11434/`, `/api/chat` et `/api/embed`. Les modèles `:cloud` et les URLs non locales sont refusés. Aucun appel SQL n’est confié au modèle. MCP et les règles métier existantes restent responsables des données dynamiques.

Les adaptateurs implémentent ILLMService et IEmbeddingService. Le LLM propose aussi IStructuredLLMService pour le JSON du plan agent ; la validation stricte du plan et la liste fermée de tools sont conservées. Le streaming lit les fragments NDJSON au fil de leur arrivée et respecte l’annulation. `think=false`, contexte 4096, sortie maximale 1024 tokens et garde en mémoire 1 minute limitent les ressources. Une erreur Ollama ne devient jamais une réponse fictive.

Alternative très légère : `ollama pull qwen3:0.6b`, puis `AI:Ollama:ChatModel = qwen3:0.6b` ; ses résultats et sa planification peuvent être moins fiables. Le modèle `nomic-embed-text:latest` déjà présent sur ce PC peut être utilisé en changeant EmbeddingModel ; le choix multilingue par défaut reste EmbeddingGemma.

Overrides PowerShell, limités au terminal courant :

```powershell
$env:AI__Ollama__ChatModel = 'qwen3:0.6b'
$env:AI__Ollama__EmbeddingModel = 'nomic-embed-text:latest'
dotnet run --project src/Logistics.Api --launch-profile http
```

Changer ou mettre à jour le modèle d’embeddings exige de réindexer les documents. La Knowledge Base utilise désormais un stockage local persistant et expose une API d’upload, de recherche et de suppression ; voir [Knowledge-Base-backend.md](Knowledge-Base-backend.md). L’agent actuel retourne les preuves MCP/RAG ; il ne crée pas automatiquement des documents ou une synthèse métier inventée.

Erreurs utiles : `model_not_found` → installer le modèle configuré ; `unavailable` → démarrer Ollama ; `output_limit` → augmenter MaximumOutputTokens si nécessaire ; `incomplete_stream` → flux interrompu. L’annulation et la limite de temps interrompent aussi la requête amont.

Sources officielles : https://ollama.com/library/qwen3:1.7b, https://ollama.com/library/embeddinggemma, https://docs.ollama.com/api/chat, https://docs.ollama.com/api/embed, https://docs.ollama.com/windows.

## Vérification effectuée

Ollama 0.40.3 est présent sur ce PC. Qwen3 1.7b et EmbeddingGemma ne sont pas encore téléchargés. Un appel local réel avec nomic-embed-text:latest déjà installé a renvoyé un vecteur non nul de 768 dimensions ; il ne valide pas les modèles encore absents.

Build réussi sans avertissement ni erreur avec `dotnet build Logistics.slnx --no-restore -p:BaseOutputPath=bin/OllamaVerification/`. Ce dossier séparé évite les DLL verrouillées par le serveur en cours d’exécution. Arrêter puis relancer normalement le backend après installation des modèles pour appliquer le code et la configuration.

VSTest est bloqué par un refus d’accès Windows au processus parent. Le runner existant a exécuté 233 tests unitaires (14 cas Ollama), puis 8 tests HTTP MCP et 8 tests WebSocket SignalR, tous réussis. Les fixtures de transport ont été corrigées pour isoler l’agent de test et envoyer un Content-Type JSON valide.

```powershell
dotnet run --project tests/Logistics.Verify --no-restore -p:BaseOutputPath=bin/OllamaVerification/ -- --unit-tests-only
dotnet run --project tests/Logistics.Verify --no-restore -p:BaseOutputPath=bin/OllamaVerification/ -- --mcp-tests-only
dotnet run --project tests/Logistics.Verify --no-restore -p:BaseOutputPath=bin/OllamaVerification/ -- --signalr-tests-only
```

Un test d’intégration embedding optionnel est aussi fourni dans OllamaLiveTests pour une exécution VSTest normale hors de cette restriction : définir `OLLAMA_LIVE_TEST=1`, et éventuellement `OLLAMA_TEST_EMBEDDING_MODEL=nomic-embed-text:latest`. Les tests streaming du nouvel adaptateur utilisent un serveur HTTP simulé ; le streaming réel de qwen3:1.7b devra être confirmé une fois le modèle installé.
