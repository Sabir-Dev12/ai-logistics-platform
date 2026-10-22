# Voyages — intégration API

ConnectedVoyagesPage utilise voyagesService avec la requête authentifiée de la session existante. Aucun nouveau stockage de jeton ni appel HTTP dans les composants de présentation. Les données mockées restent réservées aux tests et prévisualisations explicites.

## Endpoints

- GET /api/v1/voyages : search (numéro, clé ou navire), categorie, validesSeulement, page et pageSize. Pagination serveur et totalCount. Le filtre validé inclut les voyages clôturés ; l’API ne filtre pas séparément brouillons et clôturés.
- GET /api/v1/voyages/{clé} avant modification ou action ; POST racine et PUT /{clé}.
- POST /{clé}/validate?dateVoyage=… ; POST /{clé}/close?dateCloture=… ; POST /{clé}/reopen.
- POST /{clé}/trajets et DELETE /{clé}/trajets/{cléTrajet}. Pas d’endpoint de modification ou de remplacement global : les trajets existants sont consultables et supprimables, les nouveaux sont ajoutés individuellement.
- POST /{clé}/equipements?typeEquipement=…&natureEquipement=… : chaque appel ajoute une unité ou incrémente la quantité. Pas de suppression ni de remplacement global d’équipements.
- GET /api/v1/dossiers/by-voyage/{clé} : tous les dossiers associés. POST et DELETE /api/v1/voyages/{clé}/dossiers/{cléDossier} pour associer/détacher. Le sélecteur de dossiers réutilise le composant existant, avec recherche et pagination. Le voyage actuel est indiqué avant une éventuelle réaffectation.
- GET /api/v1/reference/types-dossier et /api/v1/reference/agences : types et agences réels.

Le nombre de dossiers n’étant pas renvoyé dans la liste Voyages, la colonne invite à consulter la fiche ; aucune estimation depuis une page partielle ni requête supplémentaire par ligne.

## Coûts

GET /{clé}/cout/taux et GET /{clé}/operations-cout chargent les taux, avertissements et marqueurs réels. POST /{clé}/cout/apercu est une lecture : ses erreurs sont réessayables et ne sont pas présentées comme des écritures incertaines. Aucun calcul métier ni taux fictif côté React.

La génération POST /{clé}/cout/generer utilise les mêmes paramètres que l’aperçu confirmé (mois, frais bancaires, trajets, équipements, personnel). Elle nécessite une confirmation explicite et reste désactivée en présence d’avertissements sur les taux ou d’un aperçu non éligible. Le résultat de la dernière génération confirmée et les marqueurs sont affichés ; il n’existe pas d’endpoint d’historique détaillé des montants générés.

## Préservation des données

Le DTO backend retourne editableFields, constitué des 33 champs de SaveVoyageRequest. Le frontend fusionne les champs modifiés avec ce contrat complet avant PUT et exclut statuts, dates de validation/clôture, société et collections. Les heures d’un départ/arrivée non modifié sont conservées malgré l’affichage date seule. Les enums catégorie/nature sont envoyées en nombres, une nature nulle reste nulle.

Le backend conserve les valeurs hors contrat que les setters effaçaient auparavant : transbordement, dates douanières, libellés du personnel non changé, triptyque, tare, capacité de remorque et entrée MEAD. Le résumé restitue aussi provenance/destination. Recompiler et redémarrer le backend. Une ancienne réponse sans editableFields bloque le PUT pour éviter une perte de données.

## États et vérifications

Chargement, vide, validation, accès refusé, réseau, délai de 15 secondes, annulation des lectures périmées. Aucune répétition automatique des écritures. Une confirmation perdue bloque le renvoi et propose de vérifier le résultat ; un rafraîchissement raté n’annule pas une confirmation déjà reçue.

Tests de services et composants avec réponses simulées dans le processus. Test backend de modification via le vrai VoyageService, exécuté directement car VSTest est bloqué dans cet environnement Windows. Vérification visuelle sur des données fictives. Aucune génération financière ni modification réelle de voyage exécutée. Les opérations authentifiées sur la base réelle restent à vérifier avec un compte de test ; les identifiants et simulations précédemment refusés ne sont pas réutilisés.
