# Accueil Dashboard — AI LOGISTICS

Le contenu promotionnel du hero et les options marketing ont été remplacés par les statistiques et les liens de l’application.

## Structure

- `src/Home/HomePage.jsx` réutilise directement `DashboardPage` : mêmes données mockées, statistiques, filtres, tableaux, graphiques et états loading/empty/error. Aucune règle de calcul n’est dupliquée.
- `src/Home/HomeNavbar.jsx` reçoit la liste des huit pages depuis `App.jsx`. Les liens sont visibles sur desktop ; un menu modal les regroupe sur tablette/mobile. Le bouton de déconnexion réutilise l’authentification existante.
- `src/Home/HeroSection.jsx` conserve le fond vidéo sombre, la pause et les préférences de réduction des animations. Le contenu marketing a été retiré.
- `src/Home/home.css` définit la présentation de cet accueil avec les tokens et composants existants.

## Accès

L’URL racine, `#home` et `#home-content` affichent le dashboard d’accueil après connexion. Les visiteurs sans session sont dirigés vers `#login` puis reviennent à l’accueil. Les routes des autres pages restent inchangées et protégées. Les données métier restent mockées à cette étape ; aucun backend n’est modifié.

## Vérification

```powershell
npm run dev
npm run build
npm test
```

`tests/home.test.jsx` vérifie les statistiques, les huit liens, la navigation mobile, la pause, la protection de l’accueil, le retour après connexion et la déconnexion. Les tests des modules existants couvrent les filtres et états du dashboard. `tests/hero-video.test.jsx` couvre la lecture HLS et ses replis.

Les sources du thème restent `src/design-system/tokens.json`, `src/design-system/styles.css` et `src/theme/dark.css`. Le flux vidéo et le poster restent centralisés dans `src/Home/mediaAssets.js`.
