# Carburant — API

Route protégée : transport authentifié existant, aucun nouveau stockage de token.

Listes serveur : citernes (recherche et produit), approvisionnements et bons (dates), sorties (citerne et dates). Pagination et totaux serveur, sans filtrage local d’une page partielle. Les filtres non pris en charge sont désactivés et signalés.

Ratios : GET /api/v1/carburant/ratios/{keyMateriel} exige une clé de matériel. La réponse ne contient ni produit ni devise : ces valeurs ne sont jamais inventées. POST du même chemin passe le produit en query string ; 204 signifie qu’aucune fenêtre de deux pleins n’est encore calculable.

Actions : POST citernes, POST citernes/{key}/reception, POST approvisionnements, POST bons, POST citernes/{key}/sorties, DELETE sorties/{key}, POST citernes/{key}/jauge. La réception ne reçoit aucune date dans ce contrat. Les références sont saisies par clé existante ; les devises des bons viennent de /api/v1/reference/devises. Les calculs PMP, allocations et ratios restent au backend.

Limites existantes : aucun GET pour les réceptions, les jauges ou le journal des mouvements. Ces rubriques affichent une information explicite, pas un faux historique vide synchronisé. Les actions réception et jauge fonctionnent ; le résultat confirmé est affiché, sans fabrication de persistance frontend. Ajouter ces historiques nécessite des endpoints backend supplémentaires.

Lectures annulables, délai de 15 s et reprise manuelle. Aucune répétition automatique des écritures ; confirmation incertaine bloque tout nouvel envoi jusqu’à actualisation explicite et vérification. Une écriture confirmée reste un succès si la lecture suivante échoue. Tests HTTP isolés et aperçu local ne prouvent pas la connexion authentifiée à la base réelle.
