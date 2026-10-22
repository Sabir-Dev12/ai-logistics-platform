# Layout global

`src/App/Layout` expose `AppLayout`, `Sidebar`, `Header` et `MainContent`. `App.jsx` place le dashboard existant dans `AppLayout`. Aucun nouveau module métier ni nouvelle page n'est ajouté.

```jsx
import { AppLayout } from "./App/Layout";

<AppLayout title="AI LOGISTICS" navigation={navigation} activeHref={currentHref}>
  <Page />
</AppLayout>
```

La navigation est fournie par l'appelant sous forme de `{ href, label }`. Utiliser seulement les routes effectivement présentes. Le lien actif reçoit `aria-current="page"`. `headerActions` permet d'insérer plus tard un composant existant dans le header. La référence n'ayant aucun breadcrumb, menu utilisateur ou notifications, ces fonctionnalités ne sont pas inventées.

Les surfaces utilisent `Card`, les contrôles utilisent `Button` et les styles du Design System. Les tokens de couleurs, polices, espacements, rayons et ombres ne changent pas. Le layout reprend les marges `6px / 13px`, la bordure orange de la page et une sidebar dont la largeur structurelle est `6 × 30px`. La référence ne définissait pas de largeur de sidebar.

À partir de `768px` (breakpoint `sm` existant), la sidebar est affichée à côté du contenu. En dessous, le bouton Menu du header ouvre la même sidebar dans la `Modal` existante : navigation clavier, focus, Échap, fermeture par clic extérieur et restauration du scroll sont réutilisés. Passer au desktop ferme la modal. Le header peut revenir à la ligne ; le contenu utilise `min-width: 0` pour permettre à la grille de se redimensionner.

Le dashboard conserve ses fichiers `components/Layout.jsx`, `BarChart.jsx`, `AreaChart.jsx`, ses données et son CSS. Les graphiques s'adaptent désormais à la largeur disponible dans le contenu principal, en conservant les mêmes breakpoints, colonnes et marges. Un lien accessible permet de passer directement au contenu.

Vérifications : `npm run build`, `npm test`, `npm run lint`, puis affichage aux largeurs mobile/tablette/desktop et ouverture/fermeture du menu mobile. Les détails des résultats figurent dans la réponse de livraison.
