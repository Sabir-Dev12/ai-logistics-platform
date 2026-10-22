# Connexion du chatbot aux ressources

Le chemin de production est déjà enregistré dans Program.cs et AddChatSignalR :

```text
React Chatbot → SignalR /hubs/chat → ChatService → AgentService
                                                  ├─ Ollama : plan JSON validé
                                                  ├─ AgentMcpTools → dispatcher MCP → services métier → repositories → SQL Server
                                                  └─ RetrievalService → embeddings Ollama → vector store
```

MCP expose GetClient, GetInvoice, GetShipment, GetTracking et GetBooking. Les outils sont en lecture seule, valident les paramètres et réutilisent les services existants. GetShipment consulte un dossier d’expédition avec keyDossier. Les données dynamiques ne sont affichées qu’à partir d’un résultat MCP réel ; une erreur ne devient pas une valeur supposée.

RAG consulte la collection de la société authentifiée, `<SocieteKey>:documents`. L’upload Knowledge Base accepte PDF, DOCX, TXT, Markdown et CSV. `KnowledgeTextExtractor` extrait le texte (notamment `word/document.xml` pour DOCX), puis `IDocumentIndexingService` effectue le découpage, les embeddings et le stockage vectoriel. Le loader d’indexation reçoit ce texte extrait en UTF-8. La même version du modèle d’embeddings doit être utilisée pour indexer et rechercher.

Le backend dispose d’une gestion documentaire persistante via `/api/v1/ai/documents` (voir Knowledge-Base-backend.md). L’upload y extrait le texte et utilise l’indexation existante ; les documents au statut `indexed` alimentent la même collection que celle consultée par le chatbot. Les métadonnées, textes et vecteurs sont conservés sur disque après redémarrage. Vérifier le statut réel retourné par le serveur avant d’interroger un document.

Le chatbot présente les données MCP sous forme de messages français structurés : introduction, listes avec des libellés lisibles, puis indication de la source consultée. Le JSON technique des tools n’est plus affiché dans la conversation. Les champs absents ou nuls sont omis ; les valeurs zéro et les états faux restent affichés lorsqu’ils sont fournis. Les montants ne sont ni recalculés ni complétés, et les dates conservent leur décalage horaire explicite.

`AgentResponseFormatter` est indépendant de SignalR et du frontend. Il formate les cinq tools autorisés et les erreurs métier en texte destiné à l’utilisateur. Les codes techniques restent dans l’audit. Les passages RAG restent des extraits documentaires avec leur nom de source et un numéro de passage lisible. Ollama sélectionne les ressources via un plan JSON interne ; les valeurs métier affichées proviennent uniquement de MCP, sans narration libre susceptible d’ajouter des faits. Le contrat `ChatResponse.Message` et le streaming existant sont conservés.

Le plan Ollama est désormais contraint par un JSON Schema construit à partir des schémas MCP.
Les références client explicitement libellées (par exemple « ref client : REF001 ») sont conservées
littéralement dans ce schéma et transmises à GetClient avec `byReference: true` ; elles ne sont pas
converties en clés supposées. La résolution exacte et la détection des références ambiguës sont
effectuées par le service Clients, dans la société authentifiée. Un plan contenant une clé absente
de la question peut être réparé une seule fois avant tout appel métier ; le contrôle de présence
explicite reste obligatoire après cette réparation. Aucun SQL libre n’est accepté.

Une demande purement documentaire ne produit plus un avertissement d’absence de données MCP.
En l’absence de passage RAG pertinent, le chatbot invite à vérifier l’indexation dans Knowledge Base.
Les questions mentionnant explicitement un CV, un document, un fichier ou Knowledge Base, ainsi que les questions de compétences, formation, parcours et identité (« qui est »), sans entité métier MCP, sont orientées directement vers RAG. Elles ne dépendent pas de la classification du petit modèle. Les questions mixtes restent planifiées avec les contrôles MCP habituels. Un plan valide sans source sélectionnée pour une question non métier déclenche aussi une recherche documentaire, afin de ne jamais terminer avec un message vide. Le seuil de similarité du chatbot reste à 0,5 ; aucun résultat n’est inventé en l’absence de passage pertinent.

Exemples de questions, avec des clés réelles de votre base :

- « Donne les informations du client CLI001. » → GetClient.
- « Consulte la facture FAC001. » → GetInvoice.
- « Quel est le dossier DOS001 ? » → GetShipment.
- « Quel est le suivi du dossier DOS001 ? » → GetTracking.
- « Consulte le booking BKG001. » → GetBooking.
- « Quelle est la procédure en cas de retard ? » → RAG, si le document a été indexé.
- « Quel est le suivi de DOS001 et quelle procédure appliquer en cas de retard ? » → MCP + RAG.

Les clés ci-dessus sont des exemples ; elles ne sont pas créées par le code. Une réponse MCP nécessite une connexion SQL Server valide, un compte autorisé et un enregistrement existant. Ollama doit fonctionner et disposer des modèles configurés. La réponse du petit modèle n’est pas garantie : un plan JSON invalide est refusé.

Tests ChatResourceIntegrationTests : les vrais ChatService, AgentService, AgentMcpTools, dispatcher MCP, indexation, RetrievalService et vector store sont exercés ensemble avec des fixtures LLM/embeddings/services métier. Ils vérifient la réponse mixte, la conservation des sources RAG en cas d’erreur MCP et l’isolation documentaire entre sociétés. Aucun faux agent ne remplace l’orchestration dans ces tests.
