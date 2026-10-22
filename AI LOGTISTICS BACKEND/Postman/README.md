# Tester Logistics dans Postman

1. Démarrer Logistics.Api avec le profil **http** et l'environnement **Development**.
2. Dans Postman, **Import**, puis sélectionner `Logistics.postman_collection.json` et `Logistics.local.postman_environment.json`.
3. Sélectionner l'environnement **Logistics - Local HTTP** en haut de Postman. Vérifier `baseUrl=http://localhost:5192`.
4. Envoyer **00 - Santé et documentation → GET /health/live**, puis **GET /health/ready**. Attendu : HTTP 200. La deuxième requête teste la connexion, pas l'intégralité du schéma.
5. Envoyer **01 - Authentification → POST /api/v1/auth/token**. Le JSON utilise le compte personnel de test `admin`/`Admin123!`, la société `S01` et l'agence `AG01`. Si vous avez déjà changé le mot de passe, mettre sa valeur actuelle dans `motDePasse` et `ancienMotDePasse`. Le script de réponse enregistre automatiquement `accessToken` dans la variable `token`. Les autres requêtes héritent de Bearer `{{token}}`.
6. Envoyer les GET de listes, puis copier les clés utiles dans les variables de l'environnement (`keyClient`, `keyVoyage`, `keyDossier`, etc.). Les POST de création enregistrent automatiquement la clé principale de leur réponse après réussite.
7. Ouvrir une requête POST/PUT, vérifier son **Body → raw → JSON**, adapter les codes/références aux données de votre société, puis cliquer **Send**. Les exemples contenant `A_REMPLACER` demandent vos valeurs réelles. Une syntaxe correcte ne garantit pas que les règles métier accepteront l'opération.
8. Les paramètres facultatifs apparaissent décochés dans **Params** ; cocher uniquement les filtres voulus. Les paramètres obligatoires et la pagination sont activés.
9. Pour EDI, utiliser **Body → form-data**, champ `fichier`, type **File**, puis choisir un fichier STC réel (1000 caractères par ligne, maximum 20 Mo). Ne pas envoyer de JSON et ne pas définir Content-Type manuellement.
10. Les tests de l'onglet **Scripts → Post-response** vérifient les codes de succès. Un statut 400/401/403/404/409/422/500/503 reste un test en échec, même s'il est documenté. Cette collection complète est un catalogue pour tester manuellement, pas un scénario métier à lancer entièrement avec Runner : certaines requêtes annulent/suppriment des données, changent un mot de passe ou consomment du staging EDI.

Le compte `admin` est le personnel P001 de votre jeu de données ; `dupont`/`User123!` est P002, sans rôle administrateur. Ces deux comptes existent dans la base et ne sont pas bloqués. Leur mot de passe n'a pas été changé ni vérifié par cette génération. Les variables existantes ont été préremplies, dont `keyClient=CLI001`, `keyPersonnel=P001`, `keyAgence=AG01`, `typeDossier=I`, `typeBooking=I`. Les références restantes `A_REMPLACER` nécessitent les données correspondantes ou une création préalable via les POST. Pour tester une sortie/annulation/suppression, utiliser un enregistrement de test adapté à l'état métier requis.

Les fichiers ont été vérifiés pour la couverture des routes, les paramètres, le format JSON et les scripts. Les 159 opérations métier n'ont pas été exécutées contre vos données.

`Toutes-les-API.md` contient chaque route et chaque JSON d'exemple. `Enums.json` donne les valeurs C# réelles. `Logistics.Api.openapi.json` est le document capturé depuis votre API locale.

| Module | Nombre de requêtes |
|---|---|
| 00 - Santé et documentation | 4 |
| 01 - Authentification | 3 |
| 02 - Clients | 10 |
| 03 - Référentiels | 18 |
| 04 - Voyages | 19 |
| 05 - Dossiers | 12 |
| 06 - Bookings | 17 |
| 07 - Cotations | 28 |
| 08 - Factures de vente | 6 |
| 09 - Règlements | 7 |
| 10 - Personnel et matériel | 14 |
| 11 - Carburant | 16 |
| 12 - Tracking | 5 |
| 13 - EDI STC | 4 |

Le code `typeBooking=I` existe dans votre jeu de données. `moisCalcul` utilise le format `MM/yyyy` (exemple : `10/2026`). Les codes `ZZ` (pays) et `TST` (devise) sont des exemples de création ; s'ils existent déjà, choisir d'autres codes.

Références : [Variables Postman](https://learning.postman.com/docs/use/send-requests/variables/environment-variables), [scripts de variables](https://learning.postman.com/docs/tests-and-scripts/write-scripts/postman-sandbox-reference/pm-variables).
