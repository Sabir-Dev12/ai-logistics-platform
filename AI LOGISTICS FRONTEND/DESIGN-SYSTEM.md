# Design System — AI LOGISTICS FRONTEND

Le dashboard source est conservé. Son layout, ses données de démonstration et ses graphiques restent identiques. Seul le conteneur blanc des graphiques est extrait en `Card`, et les paramètres de grille sont lus dans les tokens. Aucune nouvelle page n'est créée.

## Source de vérité

`src/design-system/tokens.json` définit les valeurs réutilisables. `tokens.js` fournit un objet immuable à React. `tokens.css` est généré depuis le JSON : modifier le JSON, puis exécuter `npm run tokens`. Le build vérifie que le CSS est à jour.

| Élément | Valeur | Origine |
| --- | --- | --- |
| Surface | `#ffffff` | `bg-white` du dashboard |
| Bordure card | `#b45309` | `border-amber-700` |
| Bordure page | `#fdba74` | `border-orange-300` |
| Texte axes / bordure graphique | `#666666` / `#cccccc` | Valeurs par défaut de Recharts 2.12.7 |
| Graphiques | `#8884d8`, `#82ca9d`, `#ffc658` | BarChart et AreaChart existants |
| Fond widget | `rgba(0, 132, 255, 0.473)` | `.reactGridItem` |
| Axe / voile modal | `rgba(0, 0, 0, 0.3)` | Axe des graphiques |
| Rayon | `0px` | Cards existantes sans arrondi |
| Ombre | `0 4px 6px -1px rgb(0 0 0 / 0.1), 0 2px 4px -2px rgb(0 0 0 / 0.1)` | `shadow-md`, Tailwind 3.4.10 |
| Espacements | `3`, `5`, `6`, `10`, `13`, `20`, `30` px | Marges de grille, graphiques et tooltip |
| Police widget | `Arial, Helvetica, sans-serif` | `.reactGridItem` |
| Police globale / code | Piles de polices originales | `src/index.css` |
| Taille axes | `10px` | `text-[10px]` |
| Taille / graisse des contrôles | `inherit` | Aucune valeur globale supplémentaire imposée |
| Transition / chargement | `200ms` / `1500ms` | Grille / animation par défaut Recharts |

La référence ne comporte pas de couleurs sémantiques de boutons, badges ou alertes. Les nouveaux composants réutilisent sa palette : bouton principal ambre, information violet, succès vert, avertissement jaune, erreur rouge (couleur du placeholder de la grille). Ces associations sont des conventions nouvelles, pas des composants préexistants du dashboard. La modal utilise une couche `1000` pour passer devant la grille ; cette valeur est un ajout fonctionnel.

## Composants

Importer depuis `src/design-system/index.js` :

```jsx
import { Button, Card, Input, Select, Badge, Alert, Table,
  Modal, Dropdown, Pagination, Spinner, LoadingState,
  EmptyState, ErrorState, tokens } from "./design-system";
```

| Composant | API principale |
| --- | --- |
| `Button` | `variant="primary\|secondary\|ghost"`, `loading`, props HTML, ref ; type `button` par défaut |
| `Input`, `Select` | `label`, `hint`, `error`, props HTML natives, ref ; IDs et descriptions accessibles |
| `Card` | Props HTML, ref, `className`, children ; aucun padding ou dimension forcé |
| `Badge`, `Alert` | `variant="info\|success\|warning\|error"` ; Alert accepte `title`, `onDismiss` |
| `Table` | `columns`, `rows`, `rowKey`, `caption`, `loading`, `emptyState` ; colonne `{ key, header, render? }` |
| `Modal` | `open`, `onClose`, `title`, `children`, `actions` ; portal, Échap, clic extérieur, focus et blocage du scroll |
| `Dropdown` | `label`, `items`, `disabled` ; item `{ id, label, disabled?, onSelect? }`, navigation clavier |
| `Pagination` | `page`, `totalPages`, `onPageChange` ; pagination contrôlée, cinq numéros au maximum |
| `Spinner`, `LoadingState` | `label` ; annonce accessible et animation désactivée si mouvement réduit |
| `EmptyState` | `title`, `description`, `action` |
| `ErrorState` | `title`, `message`, `onRetry` |
| `BarChart`, `AreaChart` | Réexports des composants existants, sans duplication |

Les composants ne chargent aucune donnée métier. La page garde la responsabilité des requêtes, de la validation métier et du changement de page.

```jsx
<Card className="p-5">
  <Input label="Référence" value={reference}
    onChange={(event) => setReference(event.target.value)} error={error} />
  <Select label="Mode" value={mode} onChange={(event) => setMode(event.target.value)}>
    <option value="road">Route</option><option value="sea">Mer</option>
  </Select>
  <Button variant="primary" loading={saving} onClick={save}>Enregistrer</Button>
</Card>

<Table caption="Clients" columns={[
  { key: "name", header: "Client" },
  { key: "status", header: "Statut", render: (value) => <Badge>{value}</Badge> },
]} rows={clients} loading={loading} />
<Pagination page={page} totalPages={totalPages} onPageChange={setPage} />
<Modal open={open} onClose={() => setOpen(false)} title="Détails">
  {selectedClient && <p>{selectedClient.name}</p>}
</Modal>
```

## CSS et responsive

Les nouvelles classes portent le préfixe `ds-` et utilisent les variables `--ds-*`. Les styles globaux existants sont conservés. Le dashboard garde `h-full w-full` sur ses cards. Les champs prennent la largeur disponible, les tables peuvent défiler horizontalement, la pagination revient à la ligne, la modal est limitée à `768px` et à la hauteur du viewport avec une marge de `13px`. Aucun nouveau breakpoint n'est introduit.

| Breakpoint de grille | Largeur minimale | Colonnes | Marge x / y |
| --- | --- | --- | --- |
| lg | 1200px | 6 | 6 / 13px |
| md | 996px | 6 | 6 / 13px |
| sm | 768px | 3 | 3 / 13px |
| xs | 480px | 2 | 6 / 13px |
| xxs | 0px | 2 | 6 / 13px |

## Vérification et développement

```sh
npm ci
npm run build
npm test
npm run lint
npm run dev
```

`npm test` vérifie les états, les labels, les tables, les bornes de pagination, la fermeture des overlays et la gestion du focus. Ces tests DOM ne remplacent pas une comparaison visuelle dans un navigateur.

Fichiers principaux : `src/design-system/tokens.json`, `tokens.css`, `tokens.js`, `styles.css`, `primitives.jsx`, `overlays.jsx`, `useLayer.js`, `index.js`, `scripts/generate-tokens.mjs`, `tests/components.test.jsx`. Référence conservée dans `src/App.css`, `src/index.css`, `src/components/Layout.jsx`, `src/components/BarChart.jsx` et `src/components/AreaChart.jsx`.
