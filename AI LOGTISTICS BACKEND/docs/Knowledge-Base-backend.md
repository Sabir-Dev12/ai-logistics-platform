# Knowledge Base / RAG — backend documentaire

Le backend couvre les fonctionnalités de la page Knowledge Base : liste, recherche de nom insensible à la casse/aux accents, filtres type/statut combinés, upload multiple, statut, date, type, taille, suppression et pagination. Le frontend reste mocké pour cette tâche ; aucune page ni aucun style frontend n’est modifié.

Toutes les API nécessitent un token Bearer. La société vient de l’identité signée, jamais d’un code société ou collection envoyé par le frontend. La collection est la même que celle du chatbot : `<société>:documents`.

| Méthode | Route | Fonction |
|---|---|---|
| GET | `/api/v1/ai/documents?search=&type=&status=&page=1&pageSize=20` | Liste paginée : items, page, pageSize, totalCount, totalPages |
| POST | `/api/v1/ai/documents` | Multipart avec champ `files` répété ; retourne 201 et un tableau de résultats |
| GET | `/api/v1/ai/documents/{id}` | Métadonnées et statut du document |
| DELETE | `/api/v1/ai/documents/{id}` | Supprime métadonnées, texte et vecteurs ; 204 |
| POST | `/api/v1/ai/documents/{id}/reindex` | Réindexe le texte extrait après une erreur d’embeddings |
| POST | `/api/v1/ai/documents/search` | Recherche sémantique : `{ "question": "...", "topK": 5 }` |

Métadonnées compatibles avec la table frontend : id, name, type, size (octets), date (UTC), status (`processing`, `indexed`, `failed`). Ajouts : chunkCount, modelId, errorCode. Aucun contenu complet, vecteur ou chemin local n’est exposé par la liste/détail. La recherche sémantique retourne les passages autorisés, leurs références et scores.

Le POST valide tout le lot avant d’accepter un document. Une fois accepté, chaque document expose son propre résultat d’indexation : **201 ne signifie pas que tous les documents sont indexés**. Vérifier `status`. Le document est visible en `processing` pendant l’opération ; le frontend pourra actualiser la liste/détail. Les traitements sont synchrones et annulables, sans worker/queue externe. Après un arrêt interrompant une indexation, le premier accès la marque `failed/indexing_interrupted` au lieu de la laisser bloquée.

Formats : PDF avec couche texte (PdfPig 0.1.13), DOCX (`word/document.xml`, XML sans DTD), TXT/MD/CSV UTF-8. Un PDF scanné sans texte nécessite un OCR non inclus. Le texte extrait est limité à 1 million de caractères, un PDF à 300 pages. Les fichiers vides, noms avec chemins et formats non autorisés sont refusés. Une extraction invalide devient `failed/invalid_document` ou `failed/no_extractable_text`, pas une indexation simulée.

Limites par défaut dans `AI:KnowledgeBase` : 10 MiB/fichier, 10 fichiers/lot, 50 MiB/requête HTTP, 500 documents et 100 MiB de tailles d’upload cumulées par société. PageSize : 1–200. Le quota des tailles source ne constitue pas une limite de taille physique du fichier d’index, qui contient aussi texte et vecteurs.

Stockage local durable : `App_Data/rag`, relatif à la racine du backend, configurable via `AI__KnowledgeBase__DataDirectory`. Les noms des fichiers d’index sont des hashes de collection ; aucun nom de fichier envoyé n’est utilisé comme chemin disque. Métadonnées, texte extrait et vecteurs sont sauvegardés atomiquement avec verrouillage par société. Les fichiers originaux ne sont pas conservés : une extraction échouée sans texte doit être corrigée puis réimportée. Une suppression retire également les passages du chatbot. Le vector store réutilise la validation/modèle/dimensions et la similarité du store RAG existant.

Ce stockage convient à une instance backend locale. Pour plusieurs instances, remplacer IKnowledgeDocumentStore/IVectorStore par un stockage partagé transactionnel. Protéger/sauvegarder le répertoire ; le monter sur un volume persistant en conteneur. App_Data est ignoré par Git. Aucun schéma SQL métier ni MCP/SignalR/frontend n’est modifié.

Ollama doit fonctionner avec le modèle d’embeddings configuré (`embeddinggemma:300m` par défaut). Si le fournisseur est indisponible, le document est `failed/indexing_failed`, avec son texte conservé pour réessayer `/reindex` après correction. Les détails internes et contenus privés ne sont pas renvoyés dans les erreurs. Changer de modèle nécessite un index cohérent ; réindexer les documents et vérifier leurs statuts.

Dans Postman, importer la collection existante mise à jour, obtenir le token avec auth/token, ouvrir le dossier **Knowledge Base / RAG**, sélectionner un fichier local dans `files`, puis envoyer l’upload. Le script retient documentId pour consulter, réindexer ou supprimer uniquement le document de test. Ne pas exécuter toute la collection métier en bloc.

Pour préparer la future connexion frontend : adapter le repository de la Knowledge Base aux routes ci-dessus, utiliser `items/totalCount` pour la pagination serveur, envoyer FormData `files`, et afficher le statut réel retourné. Aucun code société à afficher/saisir. Loading correspond à une requête en attente ; empty à items vide ; error aux erreurs HTTP et aux documents failed.

Compilation : restaurer la dépendance PdfPig via NuGet officiel si elle est absente du cache (`dotnet restore --source https://api.nuget.org/v3/index.json`). Sur ce PC, utiliser le dossier séparé de vérification tant que le serveur verrouille ses DLL. Tests via le runner existant : `--unit-tests-only` et `--rag-http-tests-only`. Les tests d’embeddings utilisent des fixtures déterministes ; ils ne certifient pas la qualité sémantique des modèles réels.
