# Connexion aux API existantes

La présentation reste dans LoginPage ; authenticationService réalise les appels HTTP ; authState gère la session en mémoire ; ProtectedRoute gère l’accès et le retour à la page demandée. Aucun CSS modifié.

1. Lancer le backend avec son profil HTTP (port 5192 par défaut).
2. Configurer `AUTH_BACKEND_URL=http://localhost:5192` dans l’environnement local du frontend si nécessaire, puis relancer Vite. Le proxy `/api` existant est réutilisé. L’option publique `VITE_AUTH_SOCIETE_KEY` fournit le contexte société si le backend n’a pas de valeur par défaut ; elle n’est pas affichée dans le formulaire. Aucun mot de passe, token ou secret ne doit être préfixé `VITE_`.
3. Lancer le frontend avec `npm run dev`, saisir les identifiants valides dans la page Connexion.

POST `/api/v1/auth/token` envoie `identifiant`, `motDePasse` et éventuellement `societeKey`. Le mot de passe est préservé exactement. GET `/api/v1/auth/moi` vérifie ensuite l’identité avec le Bearer opaque reçu. La session n’est publiée qu’après validation de ces deux réponses et de l’expiration. Le token n’est ni décodé comme JWT ni conservé dans localStorage/sessionStorage.

États : saisie vide bloquée, validation locale, HTTP 400/422, identifiants invalides 401, accès interdit 403, limitation 429, erreur serveur, réseau indisponible, délai maximal de 15 secondes par appel, réponse vide ou invalide et annulation. Les détails techniques serveur ne sont pas affichés. Pas de répétition automatique d’une tentative de connexion : après une erreur transitoire, ressaisir le mot de passe et cliquer à nouveau sur Se connecter. Le champ mot de passe est effacé après chaque tentative. Succès : redirection existante ; déconnexion/expiration : session et token supprimés.

En production, `/api` doit être transmis au backend par le serveur web/reverse proxy ; le proxy Vite sert au développement. Aucune nouvelle route backend ou configuration CORS permissive n’est ajoutée.

Tests : `npm test` couvre les contrats, les états UI et la session. Les tests HTTP optionnels utilisent `AUTH_SIMULATION_URL` avec `postman/simulations/login.sim.yaml`, ou `AUTH_REAL_BACKEND_URL` et `AUTH_REAL_CONFIG_PATH` pour le compte de développement local. Les valeurs des identifiants sont lues uniquement en mémoire, sans journalisation. Ne jamais utiliser une configuration de production pour ces tests.
