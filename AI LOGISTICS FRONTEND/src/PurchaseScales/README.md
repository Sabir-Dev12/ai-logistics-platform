# Barèmes d’achat — démonstration frontend

Route protégée : `#purchase-scales`. Aucun appel HTTP et aucune persistance navigateur. Les données restent en mémoire pendant le montage de la page ; Actualiser relit ce dépôt, recharger le navigateur réinitialise la démonstration.

Composants existants : Card, Badge, Button, Input, Select, Table, Modal, Pagination, Alert et états Loading/Empty/Error. Les classes partagées de Bookings sont réutilisées, sans nouvelles règles CSS ni modification des autres pages.

Le dépôt mock est injectable. `useScales` sépare chargement, annulation, mutations et erreurs de la présentation ; ScaleEditor mutualise les formulaires du module. Les suppressions sont simulées par un statut Supprimé et exclues de la liste active. Les copies démarrent Non validé avec des lignes indépendantes. Comme le backend actuel, seuls les barèmes validés sont modifiables et duplicables. La validation nécessite une colonne. La suppression d’une colonne retire ses cellules. Les couples département/colonne et numéros de colonnes sont uniques.

Recherche : début inclus, fin exclue, bornes nulles ouvertes, premier numéro de colonne croissant en cas de chevauchement. Zéro est un tarif trouvé ; cellule absente ou montant null signifie aucun tarif. Formules, minimums, arrondis, dates de validité et prestations ne sont pas appliqués par la résolution, conformément à `BaremeAchatService.ResoudreAsync`. Aucune formule n’est exécutée.

## Limite à traiter avant intégration API

Le frontend mock crée l’entête et ses colonnes initiales en une seule opération locale afin de permettre la validation. Le backend actuel crée uniquement l’entête NonValide, mais SetColonnes exige déjà le statut Valide, et Valider exige une colonne. Il faudra résoudre ce blocage de cycle côté backend avant de connecter la création réelle (opération atomique ou autorisation de saisir les colonnes avant validation). Cette étape ne modifie aucun backend et ne prétend pas que cette opération composite est disponible par API.

Tests : `npm test -- tests/purchase-scales.test.jsx`. Les scénarios couvrent recherche, pagination, création/validation/modification, colonnes et cascade, cellules, prestations, duplication/suppression, résolution, annulation, erreurs et protection de route.
