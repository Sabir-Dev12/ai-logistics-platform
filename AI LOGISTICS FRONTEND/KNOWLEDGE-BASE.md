# Knowledge Base / RAG — interface mockée

Ouvrir **Knowledge Base** dans le menu ou `/#knowledge-base`. Le dashboard et le chatbot restent disponibles.

La page réutilise `Card`, `Input`, `Select`, `Table`, `Badge`, `Modal`, `Pagination`, `EmptyState` et `ErrorState` ; le chargement est celui de la Table existante. Aucune palette, police, bordure, ombre ou rayon n'est ajouté. `knowledge-base.css` définit uniquement les dispositions, les marges via tokens, le retour à la ligne et le défilement horizontal local du tableau sur mobile.

Fonctionnalités : recherche du nom insensible à la casse et aux accents, filtres type/statut combinés, pagination (5/10/20), sélection multiple de fichiers, taille et date, badges de statut, suppression avec confirmation et gestion des états loading/empty/error. Les filtres et mutations ramènent la pagination au début pour éviter une page vide.

Les 12 documents de départ sont fictifs. L'upload ajoute seulement le nom, le type, la taille et une date aux données en mémoire après un délai simulé. Aucun contenu n'est lu, parsé, vectorisé, persisté ou envoyé. « Indexé » désigne un statut de démonstration. Les formats simulés sont PDF, DOCX, TXT, MD et CSV ; les fichiers vides ou d'un autre format sont rejetés localement. Un rechargement de page ou une nouvelle entrée dans la page restaure les exemples.

`mockDocumentRepository.js` fournit les opérations asynchrones en mémoire. `useDocuments.js` gère leur cycle de vie et les annulations ; la présentation reste dans les composants `KnowledgeBase`. Aucun appel API ou changement backend n'est ajouté.

Pour tester un chargement vide ou une erreur récupérable :

```jsx
const empty = createMockDocumentRepository({ documents: [] });
const failingOnce = createMockDocumentRepository({ failListOnce: true });
// <KnowledgeBasePage repository={empty} />
// <KnowledgeBasePage repository={failingOnce} />
```

Vérifications : `npm run build`, `npm test`, `npm run lint`, puis contrôle desktop/mobile, recherche, pagination et confirmations. La sélection de fichiers sert uniquement la simulation locale.
