# Dashboard connecté

Le service `services/dashboardService.js` utilise le `request` authentifié de la session existante. App injecte ce même service dans le Dashboard dédié et dans le Dashboard de l’accueil. Aucun appel HTTP dans la présentation, aucun CSS modifié, aucune nouvelle API backend. Le hook ne crée plus de repository mock par défaut. Les fixtures de démonstration restent réservées aux tests/aperçus explicites.

GET `/api/v1/tracking/activite` avec `take=500`, `to`, `from` pour 7/30/90 jours calendaires et `entites` pour Client, Voyage, Dossier, FactureVente, Reglement, Booking ou Cotation. « Toutes les périodes » omet `from`. Les commandes et le suivi en tant qu’entités indépendantes ne sont pas proposés car l’API ne les expose pas dans ce flux. Chaque filtre provoque une nouvelle requête ; la requête précédente est annulée et les données obsolètes masquées.

Le backend sérialise actuellement TrackingEntity en enum numérique ; les noms sont aussi acceptés par l’adaptateur. La date utilisée pour les filtres et graphiques est `dateTraceSystem`, comme le backend ; `dateTraceUser` n’est pas substituée. Les dates sans fuseau sont interprétées UTC, cohérentes avec les nouvelles écritures d’audit DateTime.UtcNow. Les données historiques héritées d’un autre fuseau devront être normalisées côté serveur.

Les libellés de statut proviennent de `libelleStatut`, sinon du code brut `keyStatut`, sinon « Non renseigné ». Aucun texte/code d’événement n’est transformé arbitrairement en état métier « En cours », « Terminée », « À vérifier » : ce classement n’est pas fourni par le contrat. Les groupes sont non catégorisés et les indicateurs non calculables affichent « — ». Les synthèses utilisent le dernier événement chargé par entité dans la période, pas un état courant global.

Périmètre : comptes d’événements et d’entités distinctes du flux chargé uniquement. Maximum serveur 500, sans pagination serveur/offset/total : un avertissement apparaît si la limite est atteinte. La pagination UI parcourt ce lot ; les graphiques et cartes portent sur le même lot. Les indicateurs financiers restent explicitement indisponibles.

États : loading, succès, liste vide, corps vide/invalide, erreurs de filtres 400/422, accès interdit 403, serveur, réseau et délai 15 secondes. Les détails techniques ne sont pas exposés. Réessayer/Actualiser relancent un GET ; un 401 suit l’invalidation de session existante. Annulation au changement de filtre et au démontage.

Tests : contrats de requêtes et adaptation du DTO dans `tests/dashboard-service.test.js`, présentation et agrégations dans `tests/dashboard-page.test.jsx`, accueil dans `tests/home.test.jsx`. Les tests unitaires utilisent des réponses HTTP de test ; ils ne prouvent pas l’accès réel à la base métier. Le proxy `/api` et `AUTH_BACKEND_URL` restent ceux de Connexion.
