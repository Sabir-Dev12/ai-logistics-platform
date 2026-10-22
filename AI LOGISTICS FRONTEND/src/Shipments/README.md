# Dossiers / Expéditions — API

La route de l’application utilise ConnectedShipmentsPage et shipmentsService. La session existante fournit les requêtes authentifiées et gère les refus 401/403. Aucun nouveau stockage de jeton. Les composants de présentation n’effectuent pas d’appels HTTP.

## Contrats utilisés

- GET /api/v1/dossiers : search, keyClient, keyVoyage, typeDossier, statutFacturation, page, pageSize. Pagination serveur, totalCount, annulation des lectures périmées.
- GET /api/v1/dossiers/{clé} : fiche complète avant modification et action. Le dépotage absent de la réponse liste est indiqué « Voir la fiche ».
- POST /api/v1/dossiers ; PUT et DELETE /api/v1/dossiers/{clé}.
- POST /{clé}/depotage ; POST /{clé}/depotage/annulation.
- POST /{clé}/colisages ; DELETE /{clé}/colisages/{entier}. L’API n’a pas de remplacement global ni de modification d’une ligne : l’interface propose ajout et suppression explicite confirmée. Aucune suite automatique de suppressions/réinsertions.
- GET /api/v1/dossiers/by-voyage/{clé} : tous les dossiers du voyage.
- GET /api/v1/dossiers/by-voyage/{clé}/repartition?montantMad=…&montantDevise=… : montants calculés uniquement par le backend, sans écriture ni calcul métier dans React.
- GET /api/v1/clients et /api/v1/voyages : sélection des références avec recherche et pagination, via le sélecteur existant étendu aux clients.
- GET /api/v1/reference/types-dossier : types réels, sans restriction aux quatre anciennes valeurs mockées.

Les enums numériques ASP.NET sont converties pour l’affichage ; le lieu de dépotage est envoyé sous sa valeur numérique. Les montants et mesures sont envoyés en nombres ou null.

## Préservation à la modification

Le backend retourne désormais editableFields (les 41 champs du SaveDossierRequest), constatDepotage et refDepotage. Le service frontend fusionne les champs visibles modifiés avec ces valeurs avant le PUT, en excluant les champs de statut, société, voyage, booking et dépotage. Une ancienne réponse sans editableFields bloque l’enregistrement et demande la mise à jour du backend.

Le service métier conserve aussi les valeurs hors contrat d’écriture que ses setters effaçaient : agences commerciale/saisie, référence agent, valeur/devise marchandise, équivalences, MPL et informations DUM. Les règles de validation restent dans le backend. Recompiler et redémarrer le backend pour charger ces corrections.

## États et erreurs

Chargement, liste vide, erreurs métier, doublon, accès interdit, erreur réseau et délai de 15 secondes. Aucune répétition automatique des écritures ; une confirmation perdue bloque le renvoi et propose une vérification. Le succès d’une écriture reste confirmé même si le rafraîchissement échoue.

## Vérifications

Tests frontend du service HTTP et des composants avec réponses simulées dans le processus ; aucun document réel modifié. Test backend de conservation des champs lors d’un GET/PUT via le vrai DossierService et une repository de test. Exécution directe du test disponible lorsque VSTest est bloqué sur cet environnement Windows. Contrôle visuel sur des données fictives. Les lectures/écritures authentifiées sur la base réelle restent à vérifier avec un compte de test. Les identifiants et simulations refusés précédemment ne sont pas réutilisés.
