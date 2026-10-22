# Recherche des factures de vente

`GET /api/v1/factures-vente` accepte le paramètre facultatif `search`. Il recherche une portion du numéro (`NumFacture`), de la référence interne (`Id`) ou du nom du client enregistré sur la facture (`IntituleClient`). Les espaces en début et fin sont ignorés ; une recherche vide conserve tous les résultats.

La sensibilité à la casse et aux accents suit la collation SQL Server de la base. Les critères existants `keyClient`, `type`, `statut`, `factRegle`, `from` et `to` sont combinés à cette recherche, avant comptage et pagination. Les filtres de société et de suppression ne changent pas.

Exemple : `/api/v1/factures-vente?search=T45&factRegle=false&page=1&pageSize=5`.

Le frontend utilise cette liste pour rechercher et conserve la route de détail `/{keyFacture}` pour ouvrir le document sélectionné. Aucun total n’est calculé dans cette recherche et aucune facture n’est modifiée.
