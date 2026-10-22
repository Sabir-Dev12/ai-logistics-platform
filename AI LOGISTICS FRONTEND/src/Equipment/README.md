# Matériel — API

La route protégée injecte le service HTTP via le transport authentifié existant. Pas de nouveau stockage de token.

- GET /api/v1/resources/materiel : pagination serveur, recherche immatriculation/intitulé, code type, statut et includeRetires. Aucune recherche locale sur une page partielle. Les références/natures ne sont pas recherchables via ce contrat.
- GET /{key} : détail complet avant édition. Les champs moyenPropre et dateFinGarantie nécessitent le backend mis à jour.
- POST collection / PUT /{key} : identité, type, nature, moyen propre et dates. La création est une écriture unique.
- POST /{key}/sortie : sortie du parc sans suppression physique ; historique et coût conservés.
- POST /{key}/reparation/entree et /sortie : transitions contrôlées.
- PUT /{key}/cout ; GET /api/v1/reference/devises : montant mensuel, zéro distinct d’un coût absent, devises réelles.

Sans catalogue API de types, le code existant est saisi dans un Input du Design System ; aucun choix fictif envoyé. Le filtre Tous peut inclure des lignes supprimées logiquement selon le contrat backend : affichées « Supprimé », leur fiche est désactivée car GET ne les expose pas.

Lectures annulables, délai de 15 s, reprise manuelle. Pas de répétition automatique des écritures. Une confirmation perdue/5xx bloque les écritures jusqu’à une actualisation explicite et vérification. Un succès confirmé reste acquis si la lecture suivante échoue. Le mock sert uniquement aux tests/prévisualisations injectés.

Les tests HTTP isolés et la vérification visuelle ne prouvent pas l’accès authentifié à la base réelle.
