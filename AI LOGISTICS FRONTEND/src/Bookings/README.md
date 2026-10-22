# Bookings / Réservations — API

La route de l’application utilise ConnectedBookingsPage et bookingsService. Les composants de présentation ne font aucun appel HTTP. Le jeton et les refus 401/403 sont gérés par la session existante, sans nouveau stockage.

## Contrat

- GET /api/v1/bookings : numBooking, keyStatutBooking, typeBooking, page et pageSize. Recherche par numéro uniquement ; pagination serveur et totalCount.
- GET /api/v1/bookings/{clé} : fiche complète avant toute action. La liste ne fournit pas l’état de dépotage : elle affiche « Voir la fiche ».
- POST /api/v1/bookings ; PUT et DELETE /api/v1/bookings/{clé}.
- POST /valider, /dupliquer, /annuler ; PUT /equipements et /colisages.
- POST /voyage ; DELETE /voyage/{cléVoyage} (clé relue avant la désaffectation).
- POST /dossier?keyDossier=… ; DELETE /dossier.
- POST /depotage ; DELETE /depotage.
- GET /api/v1/bookings/capacite/{cléVoyage} : capacité calculée par le backend, jamais depuis une seule page de réservations.
- GET /api/v1/voyages et /api/v1/dossiers : références avec recherche et pagination de 20 éléments.
- GET /api/v1/reference/types-dossier : types réels (typeDossierCode, intituleType).

## Formulaires et limites existantes

Le PUT ne modifie que le chargement, l’incoterm, le mode, la validité, le commentaire et la nature d’opération. Les champs des parties et de marchandise restent consultables, mais ne sont pas proposés en modification car le backend ne les accepte pas.

La création utilise les références expéditeur, destinataire et payeur ; le backend déduit le client selon le type. Les champs port/départ/disponibilité ne sont pas envoyés car absents du contrat d’écriture. Les équipements/colisages doivent être ajoutés après création, puis le chargement sélectionné par modification : la validation backend refuse Complet/Groupage sans lignes. Aucune suite automatique de plusieurs écritures n’est lancée.

Le DTO backend est complété avec les champs de commentaire, itinéraire et dépotage déjà présents dans l’entité. Recompiler et redémarrer le backend. Une ancienne réponse sans commentaire/natureOperation bloque le PUT afin d’éviter leur effacement.

## Erreurs

Chargement, liste vide, erreurs métier, accès interdit, réseau et délai de 15 secondes. Les lectures périmées sont annulées. Une réponse d’écriture incertaine bloque la répétition et propose de vérifier la fiche ou la liste. Aucun retry automatique des écritures. Une écriture confirmée conserve son message de succès même si le rafraîchissement échoue.

## Vérification

Tests unitaires de service et composants avec réponses HTTP simulées dans le processus. La vérification visuelle utilise exclusivement des données fictives. Aucun client ou booking réel n’est modifié par ces tests. Les opérations authentifiées sur la base réelle restent à vérifier avec un compte de test. Les simulations Postman et les identifiants refusés lors de la tâche précédente ne sont pas réutilisés.
