# Filtre de statut des voyages

`GET /api/v1/voyages` accepte le critère facultatif `statut` :

- omis : tous les statuts ;
- `Brouillon` : ni date de validation ni date de clôture ;
- `Valide` : date de validation présente et date de clôture absente ;
- `Cloture` : date de clôture présente.

Les états correspondent aux badges existants de l’interface : la clôture prévaut sur la validation. Les filtres sont appliqués avant le comptage et la pagination, avec la recherche, la catégorie, l’agence et les dates. Les filtres de société et de suppression restent inchangés.

Le paramètre historique `validesSeulement=true` conserve son sens : date de validation présente, avec ou sans clôture. Il reste compatible avec les anciens consommateurs. Si les deux critères sont fournis, ils se combinent par intersection. La nouvelle interface utilise uniquement `statut` pour afficher des états distincts.

Exemple : `/api/v1/voyages?statut=Valide&page=1&pageSize=5`.

Le changement de filtre remet la page à 1 ; la réinitialisation retire les critères. Un résultat filtré vide affiche « Aucun résultat ». Aucun changement de schéma ou de règles métier de validation, clôture et réouverture n’est nécessaire.
