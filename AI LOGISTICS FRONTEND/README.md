# AI Logistics Frontend

Application **React 18 / Vite 5**, avec un Design System sombre partagé par les écrans métier et IA.

[Présentation et captures du projet](../README.md)

## Installation

```powershell
Copy-Item .env.example .env.local
npm ci
npm run dev
```

Le backend doit être disponible sur l’adresse configurée par `AUTH_BACKEND_URL` et `CHAT_BACKEND_URL`. Le serveur de développement relaie les API, le Hub SignalR et MCP.

## Structure

- `src/services/` : authentification et accès HTTP séparés de la présentation.
- `src/Auth/` : session, connexion et protection des routes.
- `src/design-system/` : composants et styles partagés.
- Dossiers métier : pages, formulaires et hooks propres à chaque module.
- `tests/` : contrats des services et comportements UI avec Vitest et Testing Library.

## Commandes

```powershell
npm test
npm run build
npm run lint
```

Les états chargement, absence de résultat, erreur et nouvelle tentative font partie des composants existants. Les pages connectées utilisent les API disponibles ; les repositories mockés restent utiles aux tests et aux démonstrations.

## Documentation

[Design System](DESIGN-SYSTEM.md) · [Layout](LAYOUT.md) · [Authentification](AUTHENTICATION.md) · [Chat](CHAT.md) · [Knowledge Base](KNOWLEDGE-BASE.md) · [MCP](MCP-TOOLS.md) · [Agent](AI-AGENT.md)

`.env.local`, `node_modules`, les bundles et les journaux sont exclus de Git. Les valeurs `VITE_*` sont publiques dans le bundle : ne pas y placer de secret.
