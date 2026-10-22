# AI Agent — interface de démonstration

Ouvrir **AI Agent** dans la navigation ou `/#agent`. La page réutilise MessageList, UserMessage et AssistantMessage du chatbot ainsi que Card, Badge, Select, Button, Table, Modal, LoadingState, EmptyState et ErrorState du Design System. Aucun token visuel n’est modifié ; le CSS ajouté couvre uniquement la disposition, les espaces et le retour à la ligne.

Trois scénarios statiques sont sélectionnables : réponse terminée, exécution en cours et échec MCP. Ils affichent la question et la réponse, le statut, le temps de réponse simulé, les étapes ordonnées User → Agent → RAG Search → MCP GetShipment → LLM → Response, les sources documentaires et les tools utilisés. Les détails montrent le passage cité ou les paramètres/résultats/erreurs structurés du tool.

Le scénario en cours reste volontairement en attente ; aucune orchestration réelle ou minuterie n’est lancée. Lors de l’échec, LLM et Response sont marqués non exécutés ; le message assistant explique l’échec sans inventer de statut métier. Les temps, identifiants et contenus sont fictifs.

Frontend uniquement : aucun appel réseau, intégration backend, SQL, connexion SignalR ou invocation RAG/MCP/LLM. Les données sont dans `src/Agent/mockAgentRuns.js` ; la présentation dans `AgentPage.jsx` et `agent.css`.

Vérifier avec `npm run build`, `npm test`, `npm run lint` et les trois scénarios sur desktop/mobile.
