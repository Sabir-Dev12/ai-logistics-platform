# AI Agent connecté

La route applicative utilise ConnectedAgentPage → useAgentConversation → useChatSignalR/createSignalRService et useChat. Le transport et les composants MessageList, MessageInput et StreamingIndicator du chatbot sont réutilisés, sans nouveau client HTTP, protocole SignalR, stockage de token ni CSS.

Contrat backend existant : /hubs/chat, invocation stream("StreamMessage", {message}), fragments {message:string}. ChatHub → IChatService → AgentService utilise déjà LLM, RAG et MCP. L’authentification et la reconnexion passent par les mécanismes du chatbot existant. Les questions restent indépendantes côté backend : l’historique affiché n’est pas une mémoire serveur.

L’interface affiche progressivement les fragments, peut arrêter une réponse, réessayer explicitement après une erreur et créer une nouvelle conversation. Une reconnexion ne renvoie pas une question automatiquement. La limite frontend de 8 000 caractères reprend celle de l’agent ; la validation serveur reste autoritaire.

Les seuls événements observables sont l’envoi et le flux de réponse. La durée est mesurée dans le navigateur du début du flux à sa fin ou son arrêt ; ce n’est ni une durée serveur ni un temps par tool. « Terminé » signifie que le flux est achevé, sans garantir la réussite des opérations métier : les erreurs MCP éventuelles restent affichées dans le texte exact de l’agent.

ChatResponse ne contient actuellement que Message. Les sources RAG, résultats MCP et erreurs métier sont intégrés au texte par le backend, mais aucun événement structuré de sources, tool calls, paramètres, corrélations ou étapes internes n’est diffusé. Les tables l’indiquent explicitement : aucune déduction à partir du texte, aucun appel supplémentaire à MCP/RAG, aucune métadonnée inventée. Pour des tables détaillées alimentées en temps réel, il faudra un contrat backend de télémétrie structurée.

AgentPage sans session conserve les scénarios explicites de démonstration pour les tests isolés ; la route de production injecte toujours la session connectée. Les nouvelles vérifications composent le vrai transport frontend SignalR et un Hub contrôlé en mémoire. Elles ne prouvent pas une connexion authentifiée au serveur réel ni le fonctionnement d’Ollama ou SQL Server.
