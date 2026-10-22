# Tracking / Suivi — API

ConnectedTrackingPage fournit trackingService avec les requêtes authentifiées de la session existante. Aucun nouveau stockage de jeton ni appel HTTP dans les composants de présentation. Les mocks restent réservés aux tests et prévisualisations explicites.

## Lecture

GET /api/v1/tracking/{entité}/{clé}?take=500 fournit la chronologie ; GET /{entité}/{clé}/statut fournit le dernier statut indépendamment des filtres de chronologie. Les sept types sont Client, Voyage, Dossier, FactureVente, Reglement, Booking, Cotation. Les enums numériques renvoyées par ASP.NET sont converties pour l’affichage.

Les auteurs et les dates utilisateur/système viennent du serveur. Les dates sans suffixe retournées par le backend sont traitées en UTC, comme sur le Dashboard. L’affichage et les filtres de dates d’événement utilisent le calendrier local. Le tri se fonde sur la date d’enregistrement système, pas sur la date métier.

L’API borne l’historique à 500 lignes et n’a pas de pagination historique. La pagination de la page, l’ordre et la période opèrent sur les événements chargés. La limite est explicitement signalée lorsqu’elle est atteinte ; le nombre de lignes chargées n’est pas présenté comme un total global. NbEvenements du résumé correspond au petit historique retourné par cet endpoint, pas au nombre total d’événements.

Les références sont chargées avec pagination depuis clients, voyages, dossiers, factures-vente, reglements, bookings et cotations. La recherche utilise search pour clients/voyages/dossiers, numBooking pour bookings et numCotation pour cotations. Factures et règlements ne proposent pas de recherche textuelle dans leur API actuelle : leurs références restent parcourables par pages. Une clé métier peut être saisie directement, y compris pour une référence sans historique. Le backend vérifie l’existence et la société avant l’écriture.

## Ajout

GET /api/v1/tracking/statuts fournit les codes. Seuls les statuts utilisateur non système dont l’utilisation est vide (commune) ou correspond au nom de l’entité sont proposés. Les codes système restent visibles dans l’historique. Aucun code n’est inventé lorsque le catalogue est vide.

POST /{entité}/{clé}/evenements envoie seulement keyStatut, commentaire et dateEvenement. La date locale saisie est convertie en ISO UTC. Le serveur fixe l’auteur, la société, l’agence et la date système. Le formulaire ne change pas l’état métier de l’entité.

Le backend vérifie les codes système/inconnus/retirés/hors périmètre, le commentaire et l’existence de la référence dans la société de session. Le chemin d’écriture explicite préserve la date métier et propage les erreurs de persistance. Il retourne la ligne insérée, sans relire un ancien événement comme confirmation. L’audit automatique des autres services reste inchangé. Les statuts partagés ayant une société vide sont désormais inclus dans le catalogue et les libellés historiques.

Recompiler et redémarrer le backend pour charger ces corrections.

## États

Chargement, liste vide, référence introuvable, erreur métier, accès refusé, réseau, délai de 15 secondes, lectures périmées annulées. Aucun retry automatique des écritures. Une confirmation perdue bloque l’envoi et propose de vérifier la chronologie ; un succès confirmé reste affiché même si le rafraîchissement échoue ensuite.

## Vérifications

Tests frontend des services et composants avec réponses HTTP simulées dans le processus. Tests backend du service, des statuts refusés et de la repository avec une interception d’échec avant tout accès à la base ; exécution directe car VSTest est bloqué dans cet environnement Windows. Contrôle visuel sur des données fictives. Aucun événement réel créé. Les opérations authentifiées sur la base réelle restent à vérifier avec un compte de test. Les identifiants et simulations précédemment refusés ne sont pas réutilisés.
