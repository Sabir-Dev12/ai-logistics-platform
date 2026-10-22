# Authentification frontend

Le formulaire `/#login` utilise le Design System existant. Toutes les pages applicatives sont protégées ; un visiteur est redirigé vers la connexion puis vers la page initialement demandée après succès. Seules les destinations locales connues sont acceptées.

Contrat existant : `POST /api/v1/auth/token` avec `{ identifiant, motDePasse, societeKey? }`, puis `GET /api/v1/auth/moi` avec Bearer. Le token est opaque. `expiresAt` indique sa date d’expiration UTC. Le frontend ne prétend pas décoder un JWT. Le code société est masqué dans le frontend. Le backend doit configurer la société par défaut dans Authentication:ServiceAccount:SocieteKey ; la société reconnue reste conservée en interne pour la session.

Séparation : `services/authenticationService.js` (HTTP et erreurs), `Auth/authState.js` (session en mémoire, annulation, expiration), `AuthProvider`/`useAuth` (React), `ProtectedRoute` (garde), `LoginPage` (UI). Les erreurs ne montrent pas les détails internes du serveur. `auth.request()` ajoute le Bearer uniquement à une URL API de même origine, invalide la session sur 401 et expose une erreur Accès interdit sur 403.

Le token et l’identité minimale restent en mémoire ; aucun mot de passe, token ou profil n’est écrit dans localStorage/sessionStorage. Le mot de passe est effacé du formulaire à la soumission. Une recharge complète demande une nouvelle connexion. Il n’existe pas d’endpoint refresh/logout dans le backend : la déconnexion est locale et ne révoque pas le token côté serveur. Les connexions SignalR sont démontées au logout ; l’ancien bootstrap `/__chat/session` est supprimé pour empêcher une reconnexion automatique avec un token de développement.

Lancer le backend puis `npm run dev`. Le proxy Vite utilise `AUTH_BACKEND_URL`, sinon `CHAT_BACKEND_URL`, sinon `http://localhost:5192`. En production, publier le frontend et proxifier `/api` et `/hubs/chat` vers le backend sous la même origine, avec HTTPS. Ne mettre aucune credential dans une variable VITE_* ou un bundle.

Validation : tests HTTP mockés du contrat, erreurs et identité ; tests de session/race/expiration/401/403 et UI de login/logout/redirection/gardes. Ces tests ne valident pas les credentials réels ni la disponibilité de SQL Server. Le contrôle visuel utilise un service d’authentification fictif injecté uniquement dans le fichier de vérification temporaire.
