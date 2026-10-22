# Filtre de validation des clients

`GET /api/v1/clients` accepte le paramètre facultatif `valide` :

- omis : tous les états de validation ;
- `valide=true` : clients validés ;
- `valide=false` : clients à valider.

Ce critère se combine avec `search`, `actif`, `statut` et `classe`. Le repository applique les critères avant le comptage et la pagination (`page`, `pageSize`). Les filtres existants de société et de suppression restent appliqués. Aucun changement de schéma SQL n’est nécessaire.

Exemple : `/api/v1/clients?actif=true&valide=false&page=1&pageSize=5`.

Le frontend transmet l’état « Validation du client » dans ce paramètre et remet la pagination à la première page lors d’un changement de filtre. La réinitialisation retire tous les critères. Un résultat filtré vide affiche « Aucun résultat ».
