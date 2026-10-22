# Chatbot React connecté à SignalR

Ouvrir **AI Chatbot** dans le menu ou `/#chat`. Les composants, CSS et tokens existants sont conservés. Le badge existant affiche l'état de connexion. Les erreurs réutilisent `ErrorState`, et le bouton d'envoi est désactivé lorsque la connexion n'est pas prête ; le brouillon reste éditable.

## Contrat backend vérifié dans le code

- Hub : `/hubs/chat`, authentification Bearer obligatoire.
- Appel : `connection.stream("StreamMessage", { message: "Question" })`.
- Chaque élément reçu est `{ message: "fragment" }` : il faut concaténer les deltas.
- `complete` termine la réponse ; `error` signale une réponse interrompue.
- `subscription.dispose()` annule l'invocation. Le backend relie aussi l'annulation à la déconnexion.
- Codes reconnus : `chat_cancelled`, `chat_invalid_request`, `chat_llm_not_configured`, `chat_generation_failed`.
- Le contrat actuel ne prend pas d'historique et ne reprend pas une réponse interrompue.

## Séparation des responsabilités

`services/signalRService.js` gère le SDK, la négociation, les retries, la reconnexion et les abonnements. `hooks/useChatSignalR.js` gère la durée de vie de la connexion React, y compris StrictMode. `Chat/useChat.js` conserve l'orchestration des messages et le streaming progressif. Aucun composant de message ou de saisie n'est dupliqué.

Les retries initiaux et automatiques utilisent 0, 2, 10 et 30 secondes. Les sessions absentes ou rejetées ne sont pas réessayées en boucle. Une coupure conserve le texte partiel et signale l'erreur. La reconnexion autorise les nouveaux envois, mais ne renvoie jamais automatiquement la question interrompue. Réessayer est une action explicite de l'utilisateur. Démontage, arrêt et changement de session libèrent les abonnements.

## URL et authentification

Le frontend utilise `/hubs/chat` par défaut. Vite proxyfie ce chemin (HTTP et WebSocket) vers `CHAT_BACKEND_URL`, par défaut `http://localhost:5192`, valeur du profil backend HTTP. Pour un déploiement, proxyfier le même chemin ou définir `VITE_CHAT_HUB_URL` et configurer les origines CORS côté serveur. Ne pas désactiver l'authentification.

La future connexion utilisateur injecte le token brut obtenu via `/api/v1/auth/token` :

```js
import { setChatAccessToken } from "./services/chatAccessToken";
setChatAccessToken(response.accessToken);
// À la déconnexion :
setChatAccessToken(null);
```

Le token est conservé seulement en mémoire par le client. Un changement de session recrée la connexion. Il n'est jamais journalisé ou stocké dans localStorage par ces modules.

Pour le développement local uniquement, `.env.local` (ignoré par `.gitignore`) peut contenir `CHAT_DEV_ACCESS_TOKEN`. Le serveur Vite le transmet à la session du navigateur via un endpoint loopback avec vérification de l'origine et du header local ; ce mécanisme n'existe pas dans le build de production. Ne jamais préfixer un token avec `VITE_`, ni le mettre dans un fichier versionné. Redémarrer Vite après remplacement d'un token expiré. `.env.example` contient uniquement des valeurs non sensibles et un token vide.

## Vérification

```sh
npm ci
npm run build
npm test
npm run lint
npm run dev
```

Les tests unitaires couvrent connexion initiale, retry, session rejetée, fragments, completion, cancellation, déconnexion, reconnexion et StrictMode. `tests/signalr-live.test.js` est activé uniquement avec `SIGNALR_TEST_URL` et `SIGNALR_TEST_TOKEN_FILE` (fichier de fixture `{ "token": "..." }`). Il utilise le SDK officiel contre le vrai ChatHub hébergé localement avec un LLM de test, puis coupe réellement le TCP pour vérifier la reconnexion. Il ne doit pas cibler un serveur de production.

Les mocks restent disponibles par injection de `transport` pour les tests UI ; la page normale utilise SignalR. Une connexion réussie ne garantit pas qu'un LLM est configuré : le code `chat_llm_not_configured` est affiché explicitement.

Références : [client JavaScript SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client?view=aspnetcore-10.0), [streaming SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/streaming?view=aspnetcore-10.0).
