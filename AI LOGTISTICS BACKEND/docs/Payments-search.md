# Recherche des règlements et des tiers

GET `/api/v1/reglements` accepte deux paramètres facultatifs :

- `search` : portion de référence interne du règlement, référence de paiement, référence du tiers ou nom du tiers ;
- `tiers` : portion de référence ou nom du tiers.

Les noms sont recherchés dans le nom enregistré sur le règlement (`NomTier`), ainsi que dans la fiche Client pour un règlement Client et dans le nom/prénom de Personnel pour un règlement Personnel. Le répertoire client permet aussi une recherche par référence client. Pour Fournisseur et Etrangers, le nom doit être enregistré sur le règlement ; aucune correspondance avec un client homonyme n’est supposée.

Les recherches ignorent les espaces en début et fin ; casse et accents suivent la collation SQL Server existante. Les deux critères se combinent par intersection, avec `type`, `statut`, dates et l’ancien `keyTiers` exact. Tous sont appliqués avant le comptage et la pagination. Les sous-requêtes de répertoire conservent leurs filtres de société et suppression.

Le frontend utilise `search` et `tiers`. La route de détail par clé est conservée pour ouvrir un règlement sélectionné. Modifier un critère remet la page à 1 ; réinitialiser retire tous les filtres.

Exemple : `/api/v1/reglements?search=VIR&tiers=Atlas&type=0&page=1&pageSize=5`.

Aucune modification du schéma, des montants, des affectations ou de la création des règlements.
