# Personnel — API

La route protégée injecte `createPersonnelService` avec le transport authentifié existant. Aucun token supplémentaire ni profil sensible n’est stocké dans localStorage.

- GET /api/v1/resources/personnel : recherche (nom/prénom/matricule), clé fonction, agence, actifs ou tous, page/pageSize ; pagination serveur, jamais un filtrage local d’une page partielle. Le backend ne propose pas « sortis uniquement » ni recherche email.
- GET /api/v1/resources/personnel/{key} : fiche complète avant modification.
- POST /api/v1/resources/personnel ; PUT /{key} ; POST /{key}/sortie ; PUT /{key}/cout.
- GET /api/v1/reference/agences et /devises : choix réels. Les clés fonction/service restent saisissables faute de catalogue API ; aucun référentiel fictif n’est envoyé.

Les noms/prénoms séparés et le département sont exposés par le backend. Une modification préserve les coordonnées et attributs absents du formulaire ; ville/agence sont des champs optionnels ajoutés au PUT. Une date d’embauche vide sur modification conserve la date serveur. La sortie révoque l’accès et conserve la fiche et le coût ; pas de suppression physique.

Les lectures sont annulables, expirent après 15 s et proposent une reprise manuelle. Les écritures ne sont jamais relancées automatiquement. En cas de réponse perdue/5xx, confirmation incertaine : les écritures restent bloquées même après fermeture de la fiche, jusqu’à une actualisation explicite et vérification des données. Une écriture confirmée reste un succès si la lecture suivante échoue.

Le repository mock reste uniquement une dépendance explicite de tests/prévisualisation. Les tests HTTP isolés ne prouvent pas l’accès à la base réelle.
