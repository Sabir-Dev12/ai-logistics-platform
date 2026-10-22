# Toutes les API Logistics avec exemples Postman

163 requêtes : 159 opérations des contrôleurs, 2 endpoints de santé et 2 endpoints de documentation.
59 corps JSON ; 2 imports de fichiers STC. Base URL : `{{baseUrl}}` = `http://localhost:5192`.

Les exemples respectent les modèles JSON ; les clés et codes A_REMPLACER doivent exister dans votre base. Les règles métier et états (validation, stock, facture réglée, booking affecté…) peuvent refuser une requête pourtant correcte au format JSON.
Les enums du corps JSON sont des nombres. Les enums en paramètres peuvent utiliser le nom C# (exemple TrackingEntity : Client). Voir Enums.json pour les valeurs.

Les variables sont préremplies avec les données vérifiées : admin, S01, AG01, CLI001, P001, typeBooking=I, typeDossier=I, MAD, MA, FOB. Adapter le mot de passe si vous l'avez changé. Les références `A_REMPLACER` ne sont pas présentes dans le jeu de données initial. Le calcul de coût attend `moisCalcul` au format `MM/yyyy` (exemple : `10/2026`).

## 00 - Santé et documentation

### GET /health/live

Processus API en cours d'exécution.

URL : `{{baseUrl}}/health/live`

Authentification : aucune.

Corps : Aucun corps.

Codes documentés : 200.

### GET /health/ready

Connexion à la base attendue ; ne vérifie pas toutes les tables.

URL : `{{baseUrl}}/health/ready`

Authentification : aucune.

Corps : Aucun corps.

Codes documentés : 200.

### GET /swagger

Explorateur de documentation HTML.

URL : `{{baseUrl}}/swagger`

Authentification : aucune.

Corps : Aucun corps.

Codes documentés : 200.

### GET /openapi/v1.json

Document OpenAPI de l'API.

URL : `{{baseUrl}}/openapi/v1.json`

Authentification : aucune.

Corps : Aucun corps.

Codes documentés : 200.

## 01 - Authentification

### POST /api/v1/auth/token

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/auth/token`

Authentification : aucune.

Body → raw → JSON :

```json
{
  "identifiant": "{{identifiant}}",
  "motDePasse": "{{motDePasse}}",
  "societeKey": "{{societeKey}}",
  "agenceKey": "{{agenceKey}}"
}
```

Codes documentés : 200, 401.

### POST /api/v1/auth/mot-de-passe

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/auth/mot-de-passe`

Authentification : Bearer `{{token}}`.

Body → raw → JSON :

```json
{
  "ancienMotDePasse": "{{ancienMotDePasse}}",
  "nouveauMotDePasse": "{{nouveauMotDePasse}}"
}
```

Codes documentés : 204, 401, 422.

### GET /api/v1/auth/moi

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/auth/moi`

Authentification : Bearer `{{token}}`.

Corps : Aucun corps.

Codes documentés : 200, 401.

## 02 - Clients

### GET /api/v1/clients

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/clients?page=1&pageSize=20`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| search | query | Non | `Test Postman` |
| statut | query | Non | `Test Postman` |
| classe | query | Non | `Test Postman` |
| actif | query | Non | `false` |
| page | query | Non | `1` |
| pageSize | query | Non | `20` |

Corps : Aucun corps.

Codes documentés : 200.

### POST /api/v1/clients

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/clients`

Authentification : Bearer `{{token}}`.

Body → raw → JSON :

```json
{
  "raisonSociale": "Client Test Postman {{$timestamp}}",
  "refClient": null,
  "typeClient": null,
  "statutClient": null,
  "groupeClient": null,
  "classeClient": "{{classeClient}}",
  "risqueClient": null,
  "activiteClient": null,
  "detailActivite": null,
  "commercial": null,
  "agenceCommercial": null,
  "agenceExploitation": null,
  "typeAffectCommercial": null,
  "keyChargeClientele": null,
  "adresse1Client": "10 rue de test",
  "adresse2Client": null,
  "cpClient": "20000",
  "villeClient": "{{ville}}",
  "paysClient": "{{codePays}}",
  "departementClient": null,
  "regionClient": null,
  "langueClient": null,
  "telClient": "+212600000000",
  "faxClient": null,
  "siteWebClient": null,
  "anneeCreation": null,
  "observationClient": null,
  "rcClient": null,
  "centreRcClient": null,
  "patenteClient": null,
  "ifClient": null,
  "cnssClient": null,
  "iceClient": null,
  "deviseFacturation": "{{devise}}",
  "paiementClient": null,
  "echeance": null,
  "nbEcheance": null,
  "finMois": false,
  "plafond": null,
  "compteComptable": null,
  "journalVente": null,
  "journalDebours": null,
  "releveFacture": 0,
  "nbFactureReleve": null,
  "ras": false,
  "lieuDedouanement": null,
  "nbJrFranchise": null,
  "immobilisationMead": null,
  "immobilisationPort": null,
  "suiviAt": false
}
```

Codes documentés : 201, 409, 422.

### GET /api/v1/clients/{keyClient}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/clients/{{keyClient}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyClient | path | Oui | `{{keyClient}}` |

Corps : Aucun corps.

Codes documentés : 200, 404.

### PUT /api/v1/clients/{keyClient}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/clients/{{keyClient}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyClient | path | Oui | `{{keyClient}}` |

Body → raw → JSON :

```json
{
  "raisonSociale": "Client Test Postman {{$timestamp}}",
  "refClient": null,
  "typeClient": null,
  "statutClient": null,
  "groupeClient": null,
  "classeClient": "{{classeClient}}",
  "risqueClient": null,
  "activiteClient": null,
  "detailActivite": null,
  "commercial": null,
  "agenceCommercial": null,
  "agenceExploitation": null,
  "typeAffectCommercial": null,
  "keyChargeClientele": null,
  "adresse1Client": "10 rue de test",
  "adresse2Client": null,
  "cpClient": "20000",
  "villeClient": "{{ville}}",
  "paysClient": "{{codePays}}",
  "departementClient": null,
  "regionClient": null,
  "langueClient": null,
  "telClient": "+212600000000",
  "faxClient": null,
  "siteWebClient": null,
  "anneeCreation": null,
  "observationClient": null,
  "rcClient": null,
  "centreRcClient": null,
  "patenteClient": null,
  "ifClient": null,
  "cnssClient": null,
  "iceClient": null,
  "deviseFacturation": "{{devise}}",
  "paiementClient": null,
  "echeance": null,
  "nbEcheance": null,
  "finMois": false,
  "plafond": null,
  "compteComptable": null,
  "journalVente": null,
  "journalDebours": null,
  "releveFacture": 0,
  "nbFactureReleve": null,
  "ras": false,
  "lieuDedouanement": null,
  "nbJrFranchise": null,
  "immobilisationMead": null,
  "immobilisationPort": null,
  "suiviAt": false
}
```

Codes documentés : 200, 404.

### DELETE /api/v1/clients/{keyClient}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/clients/{{keyClient}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyClient | path | Oui | `{{keyClient}}` |

Corps : Aucun corps.

Codes documentés : 204, 422.

### POST /api/v1/clients/{keyClient}/validate

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/clients/{{keyClient}}/validate`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyClient | path | Oui | `{{keyClient}}` |

Corps : Aucun corps.

Codes documentés : 200, 422.

### POST /api/v1/clients/{keyClient}/adresses

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/clients/{{keyClient}}/adresses`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyClient | path | Oui | `{{keyClient}}` |

Body → raw → JSON :

```json
{
  "adresse1": "Test Postman",
  "adresse2": "Test Postman",
  "cp": "20000",
  "ville": "Casablanca",
  "pays": "{{codePays}}",
  "telAdresse": "Test Postman",
  "faxAdresse": "Test Postman",
  "contact": "Test Postman"
}
```

Codes documentés : 201, 422.

### DELETE /api/v1/clients/{keyClient}/adresses/{keyAdresse}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/clients/{{keyClient}}/adresses/{{keyAdresse}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyClient | path | Oui | `{{keyClient}}` |
| keyAdresse | path | Oui | `{{keyAdresse}}` |

Corps : Aucun corps.

Codes documentés : 204, 422.

### POST /api/v1/clients/{keyClient}/banques

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/clients/{{keyClient}}/banques`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyClient | path | Oui | `{{keyClient}}` |

Body → raw → JSON :

```json
{
  "banque": "Test Postman",
  "compteRib": "Test Postman",
  "codeBanque": "Test Postman",
  "adresseBanque": "Test Postman",
  "swift": "Test Postman",
  "ibanCode": "Test Postman"
}
```

Codes documentés : 201, 409.

### DELETE /api/v1/clients/{keyClient}/banques/{keyBanque}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/clients/{{keyClient}}/banques/{{keyBanque}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyClient | path | Oui | `{{keyClient}}` |
| keyBanque | path | Oui | `{{keyBanque}}` |

Corps : Aucun corps.

Codes documentés : 204.

## 03 - Référentiels

### GET /api/v1/reference/pays

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/reference/pays`

Authentification : Bearer `{{token}}`.

Corps : Aucun corps.

Codes documentés : 200.

### POST /api/v1/reference/pays

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/reference/pays`

Authentification : Bearer `{{token}}`.

Body → raw → JSON :

```json
{
  "codePays": "ZZ",
  "intitulePays": "Pays de test Postman",
  "alpha2": "ZZ",
  "accord": null,
  "zonePays": null
}
```

Codes documentés : 201, 409.

### GET /api/v1/reference/devises

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/reference/devises`

Authentification : Bearer `{{token}}`.

Corps : Aucun corps.

Codes documentés : 200.

### POST /api/v1/reference/devises

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/reference/devises`

Authentification : Bearer `{{token}}`.

Body → raw → JSON :

```json
{
  "codeDevise": "TST",
  "libelleDevise": "Devise test",
  "libelleFacture": "Unité test",
  "libelleCent": "Centime",
  "nbDecimal": 2
}
```

Codes documentés : 201, 409.

### GET /api/v1/reference/agences

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/reference/agences`

Authentification : Bearer `{{token}}`.

Corps : Aucun corps.

Codes documentés : 200.

### GET /api/v1/reference/comptes-tva

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/reference/comptes-tva`

Authentification : Bearer `{{token}}`.

Corps : Aucun corps.

Codes documentés : 200.

### GET /api/v1/reference/modes-paiement

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/reference/modes-paiement`

Authentification : Bearer `{{token}}`.

Corps : Aucun corps.

Codes documentés : 200.

### GET /api/v1/reference/tresoreries

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/reference/tresoreries`

Authentification : Bearer `{{token}}`.

Corps : Aucun corps.

Codes documentés : 200.

### GET /api/v1/reference/types-dossier

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/reference/types-dossier`

Authentification : Bearer `{{token}}`.

Corps : Aucun corps.

Codes documentés : 200.

### GET /api/v1/reference/incoterms

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/reference/incoterms`

Authentification : Bearer `{{token}}`.

Corps : Aucun corps.

Codes documentés : 200.

### GET /api/v1/reference/natures-frais-bancaire

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/reference/natures-frais-bancaire`

Authentification : Bearer `{{token}}`.

Corps : Aucun corps.

Codes documentés : 200.

### GET /api/v1/reference/classes-client

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/reference/classes-client`

Authentification : Bearer `{{token}}`.

Corps : Aucun corps.

Codes documentés : 200.

### GET /api/v1/reference/statuts-client

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/reference/statuts-client`

Authentification : Bearer `{{token}}`.

Corps : Aucun corps.

Codes documentés : 200.

### GET /api/v1/reference/risques

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/reference/risques`

Authentification : Bearer `{{token}}`.

Corps : Aucun corps.

Codes documentés : 200.

### GET /api/v1/reference/parametres

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/reference/parametres`

Authentification : Bearer `{{token}}`.

Corps : Aucun corps.

Codes documentés : 200.

### GET /api/v1/reference/societe

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/reference/societe`

Authentification : Bearer `{{token}}`.

Corps : Aucun corps.

Codes documentés : 200, 404.

### GET /api/v1/reference/{kind}/{key}/references

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/reference/{{kind}}/{{key}}/references`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| kind | path | Oui | `{{kind}}` |
| key | path | Oui | `{{key}}` |

Corps : Aucun corps.

Codes documentés : 200.

### DELETE /api/v1/reference/{kind}/{key}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/reference/{{kind}}/{{key}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| kind | path | Oui | `{{kind}}` |
| key | path | Oui | `{{key}}` |

Corps : Aucun corps.

Codes documentés : 204, 404, 422.

## 04 - Voyages

### GET /api/v1/voyages

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/voyages?page=1&pageSize=20`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| search | query | Non | `Test Postman` |
| categorie | query | Non | `0` |
| keyAgence | query | Non | `{{keyAgence}}` |
| from | query | Non | `{{dateTest}}` |
| to | query | Non | `{{dateFin}}` |
| validesSeulement | query | Non | `false` |
| page | query | Non | `1` |
| pageSize | query | Non | `20` |

Corps : Aucun corps.

Codes documentés : 200.

### POST /api/v1/voyages

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/voyages`

Authentification : Bearer `{{token}}`.

Body → raw → JSON :

```json
{
  "numVoyage": "VOY-{{$timestamp}}",
  "typeVoyage": null,
  "natureVoyage": null,
  "categorieVoyage": 0,
  "serviceVoyage": null,
  "provenance": "Casablanca",
  "destination": "Tanger",
  "commentaire": null,
  "keyAgenceSaisie": null,
  "intituleAgenceSaisie": null,
  "keyAgenceExploitation": null,
  "intituleAgenceExploitation": null,
  "dateDepartPrevu": "{{dateTest}}",
  "dateArriveePrevu": null,
  "dateDepart": null,
  "dateArrivee": null,
  "portDepart": null,
  "portArrivee": null,
  "navireDepart": null,
  "navireArrivee": null,
  "keyExploitant": null,
  "keyChauffeur1": null,
  "keyChauffeur2": null,
  "numAcquit": null,
  "numManifeste": null,
  "numDs": null,
  "numCnt": null,
  "numBooking": null,
  "numRemorque": null,
  "numTracteur": null,
  "numTc": null,
  "numCaisse": null,
  "magasinMead": null
}
```

Codes documentés : 201, 409.

### GET /api/v1/voyages/{keyVoyage}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/voyages/{{keyVoyage}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyVoyage | path | Oui | `{{keyVoyage}}` |

Corps : Aucun corps.

Codes documentés : 200, 404.

### PUT /api/v1/voyages/{keyVoyage}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/voyages/{{keyVoyage}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyVoyage | path | Oui | `{{keyVoyage}}` |

Body → raw → JSON :

```json
{
  "numVoyage": "VOY-{{$timestamp}}",
  "typeVoyage": null,
  "natureVoyage": null,
  "categorieVoyage": 0,
  "serviceVoyage": null,
  "provenance": "Casablanca",
  "destination": "Tanger",
  "commentaire": null,
  "keyAgenceSaisie": null,
  "intituleAgenceSaisie": null,
  "keyAgenceExploitation": null,
  "intituleAgenceExploitation": null,
  "dateDepartPrevu": "{{dateTest}}",
  "dateArriveePrevu": null,
  "dateDepart": null,
  "dateArrivee": null,
  "portDepart": null,
  "portArrivee": null,
  "navireDepart": null,
  "navireArrivee": null,
  "keyExploitant": null,
  "keyChauffeur1": null,
  "keyChauffeur2": null,
  "numAcquit": null,
  "numManifeste": null,
  "numDs": null,
  "numCnt": null,
  "numBooking": null,
  "numRemorque": null,
  "numTracteur": null,
  "numTc": null,
  "numCaisse": null,
  "magasinMead": null
}
```

Codes documentés : 200, 422.

### DELETE /api/v1/voyages/{keyVoyage}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/voyages/{{keyVoyage}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyVoyage | path | Oui | `{{keyVoyage}}` |

Corps : Aucun corps.

Codes documentés : 204, 422.

### POST /api/v1/voyages/{keyVoyage}/validate

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/voyages/{{keyVoyage}}/validate`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyVoyage | path | Oui | `{{keyVoyage}}` |
| dateVoyage | query | Non | `{{dateTest}}` |

Corps : Aucun corps.

Codes documentés : 200, 422.

### POST /api/v1/voyages/{keyVoyage}/close

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/voyages/{{keyVoyage}}/close`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyVoyage | path | Oui | `{{keyVoyage}}` |
| dateCloture | query | Non | `{{dateTest}}` |

Corps : Aucun corps.

Codes documentés : 200, 422.

### POST /api/v1/voyages/{keyVoyage}/reopen

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/voyages/{{keyVoyage}}/reopen`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyVoyage | path | Oui | `{{keyVoyage}}` |

Corps : Aucun corps.

Codes documentés : 200.

### POST /api/v1/voyages/{keyVoyage}/trajets

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/voyages/{{keyVoyage}}/trajets`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyVoyage | path | Oui | `{{keyVoyage}}` |

Body → raw → JSON :

```json
{
  "ordre": 1,
  "provenance": "Test Postman",
  "destination": "Test Postman",
  "dateDepart": "{{dateTest}}",
  "dateArrivee": "{{dateTest}}",
  "keyTracteur": "{{keyTracteur}}",
  "numTracteur": "PM-{{$timestamp}}",
  "keyRemorque": "{{keyRemorque}}",
  "numRemorque": "PM-{{$timestamp}}",
  "keyChauffeur1": "{{keyChauffeur1}}",
  "keyChauffeur2": "{{keyChauffeur2}}",
  "kilometrage": 1000,
  "fraisAutoroute": false,
  "numPlomb": "PM-{{$timestamp}}"
}
```

Codes documentés : 201.

### DELETE /api/v1/voyages/{keyVoyage}/trajets/{keyTrajet}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/voyages/{{keyVoyage}}/trajets/{{keyTrajet}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyVoyage | path | Oui | `{{keyVoyage}}` |
| keyTrajet | path | Oui | `{{keyTrajet}}` |

Corps : Aucun corps.

Codes documentés : 204.

### POST /api/v1/voyages/{keyVoyage}/equipements

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/voyages/{{keyVoyage}}/equipements?typeEquipement={{typeEquipement}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyVoyage | path | Oui | `{{keyVoyage}}` |
| typeEquipement | query | Oui | `{{typeEquipement}}` |
| natureEquipement | query | Non | `Test Postman` |

Corps : Aucun corps.

Codes documentés : 201.

### POST /api/v1/voyages/{keyVoyage}/dossiers/{keyDossier}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/voyages/{{keyVoyage}}/dossiers/{{keyDossier}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyVoyage | path | Oui | `{{keyVoyage}}` |
| keyDossier | path | Oui | `{{keyDossier}}` |
| position | query | Non | `1` |

Corps : Aucun corps.

Codes documentés : 204, 422.

### DELETE /api/v1/voyages/{keyVoyage}/dossiers/{keyDossier}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/voyages/{{keyVoyage}}/dossiers/{{keyDossier}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyVoyage | path | Oui | `{{keyVoyage}}` |
| keyDossier | path | Oui | `{{keyDossier}}` |

Corps : Aucun corps.

Codes documentés : 204.

### GET /api/v1/voyages/{keyVoyage}/operations-cout

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/voyages/{{keyVoyage}}/operations-cout`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyVoyage | path | Oui | `{{keyVoyage}}` |

Corps : Aucun corps.

Codes documentés : 200.

### POST /api/v1/voyages/{keyVoyage}/operations-cout

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/voyages/{{keyVoyage}}/operations-cout?operation=TRANSPORT`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyVoyage | path | Oui | `{{keyVoyage}}` |
| operation | query | Oui | `TRANSPORT` |

Corps : Aucun corps.

Codes documentés : 204, 422.

### DELETE /api/v1/voyages/{keyVoyage}/operations-cout

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/voyages/{{keyVoyage}}/operations-cout?operation=TRANSPORT`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyVoyage | path | Oui | `{{keyVoyage}}` |
| operation | query | Oui | `TRANSPORT` |

Corps : Aucun corps.

Codes documentés : 204.

### POST /api/v1/voyages/{keyVoyage}/cout/apercu

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/voyages/{{keyVoyage}}/cout/apercu`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyVoyage | path | Oui | `{{keyVoyage}}` |

Body → raw → JSON :

```json
{
  "moisCalcul": "{{moisCalcul}}",
  "coutFraisBancaire": 0,
  "legs": null,
  "equipements": null,
  "personnel": null
}
```

Codes documentés : 200.

### POST /api/v1/voyages/{keyVoyage}/cout/generer

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/voyages/{{keyVoyage}}/cout/generer`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyVoyage | path | Oui | `{{keyVoyage}}` |

Body → raw → JSON :

```json
{
  "moisCalcul": "{{moisCalcul}}",
  "coutFraisBancaire": 0,
  "legs": null,
  "equipements": null,
  "personnel": null
}
```

Codes documentés : 200, 422.

### GET /api/v1/voyages/{keyVoyage}/cout/taux

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/voyages/{{keyVoyage}}/cout/taux`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyVoyage | path | Oui | `{{keyVoyage}}` |
| nbTrajetsDiviseur | query | Non | `1` |

Corps : Aucun corps.

Codes documentés : 200, 404.

## 05 - Dossiers

### GET /api/v1/dossiers

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/dossiers?page=1&pageSize=20`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| search | query | Non | `Test Postman` |
| keyClient | query | Non | `{{keyClient}}` |
| keyVoyage | query | Non | `{{keyVoyage}}` |
| typeDossier | query | Non | `{{typeDossier}}` |
| statutFacturation | query | Non | `0` |
| meadSeulement | query | Non | `false` |
| page | query | Non | `1` |
| pageSize | query | Non | `20` |

Corps : Aucun corps.

Codes documentés : 200.

### POST /api/v1/dossiers

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/dossiers`

Authentification : Bearer `{{token}}`.

Body → raw → JSON :

```json
{
  "numDossier": "DOS-{{$timestamp}}",
  "typeDossier": "{{typeDossier}}",
  "keyClient": "{{keyClient}}",
  "intituleClient": null,
  "natureOperation": null,
  "serviceDossier": null,
  "position": null,
  "commentaireDossier": null,
  "keyAgenceExploitation": null,
  "intituleAgenceExploitation": null,
  "keyExpediteur": null,
  "intituleExpediteur": null,
  "keyDestinataire": null,
  "intituleDestinataire": null,
  "keyPayeur": null,
  "intitulePayeur": null,
  "keyAgent": null,
  "intituleAgent": null,
  "incoterm": null,
  "descriptionMarchandise": "Marchandise de test",
  "marchandiseGerbable": true,
  "marchandiseDangereuse": false,
  "classeMarchandiseDangereuse": null,
  "marchandiseFragile": false,
  "assurance": false,
  "nbColis": 10,
  "typeColis": null,
  "marque": null,
  "poidsBrut": 100,
  "volume": 2,
  "poidsTaxable": null,
  "volumeTaxable": null,
  "magasinDouane": null,
  "hsCode": null,
  "codeRegime": null,
  "codeBureau": null,
  "montantFret": null,
  "deviseFret": null,
  "temperatureDirigee": false,
  "temperatureMin": null,
  "temperatureMax": null
}
```

Codes documentés : 201, 409, 422.

### GET /api/v1/dossiers/{keyDossier}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/dossiers/{{keyDossier}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyDossier | path | Oui | `{{keyDossier}}` |

Corps : Aucun corps.

Codes documentés : 200, 404.

### PUT /api/v1/dossiers/{keyDossier}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/dossiers/{{keyDossier}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyDossier | path | Oui | `{{keyDossier}}` |

Body → raw → JSON :

```json
{
  "numDossier": "DOS-{{$timestamp}}",
  "typeDossier": "{{typeDossier}}",
  "keyClient": "{{keyClient}}",
  "intituleClient": null,
  "natureOperation": null,
  "serviceDossier": null,
  "position": null,
  "commentaireDossier": null,
  "keyAgenceExploitation": null,
  "intituleAgenceExploitation": null,
  "keyExpediteur": null,
  "intituleExpediteur": null,
  "keyDestinataire": null,
  "intituleDestinataire": null,
  "keyPayeur": null,
  "intitulePayeur": null,
  "keyAgent": null,
  "intituleAgent": null,
  "incoterm": null,
  "descriptionMarchandise": "Marchandise de test",
  "marchandiseGerbable": true,
  "marchandiseDangereuse": false,
  "classeMarchandiseDangereuse": null,
  "marchandiseFragile": false,
  "assurance": false,
  "nbColis": 10,
  "typeColis": null,
  "marque": null,
  "poidsBrut": 100,
  "volume": 2,
  "poidsTaxable": null,
  "volumeTaxable": null,
  "magasinDouane": null,
  "hsCode": null,
  "codeRegime": null,
  "codeBureau": null,
  "montantFret": null,
  "deviseFret": null,
  "temperatureDirigee": false,
  "temperatureMin": null,
  "temperatureMax": null
}
```

Codes documentés : 200, 422.

### DELETE /api/v1/dossiers/{keyDossier}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/dossiers/{{keyDossier}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyDossier | path | Oui | `{{keyDossier}}` |

Corps : Aucun corps.

Codes documentés : 204, 422.

### GET /api/v1/dossiers/by-voyage/{keyVoyage}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/dossiers/by-voyage/{{keyVoyage}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyVoyage | path | Oui | `{{keyVoyage}}` |

Corps : Aucun corps.

Codes documentés : 200.

### POST /api/v1/dossiers/{keyDossier}/depotage

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/dossiers/{{keyDossier}}/depotage`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyDossier | path | Oui | `{{keyDossier}}` |

Body → raw → JSON :

```json
{
  "dateDepotage": "{{dateTest}}",
  "lieuDepotage": 0,
  "constatDepotage": "Test Postman",
  "colisDepote": 10,
  "refDepotage": "Test Postman"
}
```

Codes documentés : 200, 422.

### POST /api/v1/dossiers/{keyDossier}/depotage/annulation

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/dossiers/{{keyDossier}}/depotage/annulation`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyDossier | path | Oui | `{{keyDossier}}` |

Corps : Aucun corps.

Codes documentés : 200.

### POST /api/v1/dossiers/{keyDossier}/statut-facturation

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/dossiers/{{keyDossier}}/statut-facturation?statut=0`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyDossier | path | Oui | `{{keyDossier}}` |
| statut | query | Oui | `0` |

Corps : Aucun corps.

Codes documentés : 200, 422.

### POST /api/v1/dossiers/{keyDossier}/colisages

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/dossiers/{{keyDossier}}/colisages`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyDossier | path | Oui | `{{keyDossier}}` |

Body → raw → JSON :

```json
{
  "typeColis": "Palette",
  "nbColis": 10,
  "natureMarchandise": "Test Postman",
  "marque": "Test Postman",
  "codeNgp": "Test Postman",
  "poidsBrut": 100,
  "volume": 1
}
```

Codes documentés : 201, 422.

### DELETE /api/v1/dossiers/{keyDossier}/colisages/{keyLigneColisage}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/dossiers/{{keyDossier}}/colisages/{{keyLigneColisage}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyDossier | path | Oui | `{{keyDossier}}` |
| keyLigneColisage | path | Oui | `{{keyLigneColisage}}` |

Corps : Aucun corps.

Codes documentés : 204.

### GET /api/v1/dossiers/by-voyage/{keyVoyage}/repartition

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/dossiers/by-voyage/{{keyVoyage}}/repartition?montantMad=1000&montantDevise=1000`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyVoyage | path | Oui | `{{keyVoyage}}` |
| montantMad | query | Oui | `1000` |
| montantDevise | query | Oui | `1000` |

Corps : Aucun corps.

Codes documentés : 200.

## 06 - Bookings

### GET /api/v1/bookings

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/bookings?Page=1&PageSize=20`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| KeyClient | query | Non | `{{keyClient}}` |
| KeyStatutBooking | query | Non | `{{keyStatutBooking}}` |
| TypeBooking | query | Non | `{{TypeBooking}}` |
| KeyAgence | query | Non | `{{keyAgence}}` |
| KeyVoyage | query | Non | `{{keyVoyage}}` |
| NumBooking | query | Non | `PM-{{$timestamp}}` |
| DateCreationDu | query | Non | `{{dateTest}}` |
| DateCreationAu | query | Non | `{{dateTest}}` |
| NonAffectes | query | Non | `false` |
| IncludeSupprimes | query | Non | `false` |
| Page | query | Non | `1` |
| PageSize | query | Non | `20` |

Corps : Aucun corps.

Codes documentés : 200, 400.

### POST /api/v1/bookings

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/bookings`

Authentification : Bearer `{{token}}`.

Body → raw → JSON :

```json
{
  "typeBooking": "{{typeBooking}}",
  "numBooking": "BKG-{{$timestamp}}",
  "typeChargement": null,
  "incoterm": "{{incoterm}}",
  "mode": null,
  "dateValidite": "{{dateFin}}",
  "keyAgent": null,
  "intituleAgent": null,
  "commercialBooking": null,
  "nomCommercialBooking": null,
  "keyCompagnie": null,
  "intituleCompagnie": null,
  "keyExpediteur": "{{keyClient}}",
  "intituleExpediteur": "ALPHA IMPORT SARL",
  "villeExpediteur": null,
  "paysExpediteur": null,
  "intitulePaysExpediteur": null,
  "keyDestinataire": null,
  "intituleDestinataire": null,
  "villeDestinataire": null,
  "paysDestinataire": null,
  "intitulePaysDestinataire": null,
  "keyPayeur": null,
  "intitulePayeur": null,
  "descriptionMarchandise": null,
  "marchandiseGerbable": true,
  "marchandiseDangereuse": false,
  "classeMarchandiseDangereuse": null,
  "marchandiseFragile": false,
  "assurance": false,
  "valeurMarchandise": null,
  "deviseAssurance": null,
  "temperatureDirigee": false,
  "temperatureMin": null,
  "temperatureMax": null,
  "keyCotation": null,
  "commentaire": null,
  "natureOperation": null,
  "keyFournisseurFret": null,
  "previsionAchatFret": null,
  "previsionVenteFret": null
}
```

Codes documentés : 201, 409, 422.

### GET /api/v1/bookings/{keyBooking}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/bookings/{{keyBooking}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyBooking | path | Oui | `{{keyBooking}}` |

Corps : Aucun corps.

Codes documentés : 200, 404.

### PUT /api/v1/bookings/{keyBooking}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/bookings/{{keyBooking}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyBooking | path | Oui | `{{keyBooking}}` |

Body → raw → JSON :

```json
{
  "numBooking": "PM-{{$timestamp}}",
  "typeChargement": "Test Postman",
  "incoterm": "Test Postman",
  "mode": "Test Postman",
  "dateValidite": "{{dateFin}}",
  "commentaire": "Test Postman",
  "natureOperation": "Test Postman"
}
```

Codes documentés : 200, 409.

### DELETE /api/v1/bookings/{keyBooking}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/bookings/{{keyBooking}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyBooking | path | Oui | `{{keyBooking}}` |

Corps : Aucun corps.

Codes documentés : 204, 422.

### POST /api/v1/bookings/{keyBooking}/valider

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/bookings/{{keyBooking}}/valider`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyBooking | path | Oui | `{{keyBooking}}` |

Corps : Aucun corps.

Codes documentés : 200, 422.

### POST /api/v1/bookings/{keyBooking}/dupliquer

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/bookings/{{keyBooking}}/dupliquer`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyBooking | path | Oui | `{{keyBooking}}` |

Body → raw → JSON :

```json
{
  "numBooking": "BKC-{{$timestamp}}",
  "dateValidite": "{{dateFin}}"
}
```

Codes documentés : 201, 409.

### POST /api/v1/bookings/{keyBooking}/annuler

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/bookings/{{keyBooking}}/annuler`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyBooking | path | Oui | `{{keyBooking}}` |

Body → raw → JSON :

```json
{
  "motif": "Test Postman"
}
```

Codes documentés : 200, 422.

### POST /api/v1/bookings/{keyBooking}/depotage

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/bookings/{{keyBooking}}/depotage`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyBooking | path | Oui | `{{keyBooking}}` |

Body → raw → JSON :

```json
{
  "dateDepotage": "{{dateTest}}",
  "constat": "Test Postman",
  "colisDepote": 10,
  "magasin": "Test Postman",
  "codeBureauDouane": "Test Postman"
}
```

Codes documentés : 200, 422.

### DELETE /api/v1/bookings/{keyBooking}/depotage

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/bookings/{{keyBooking}}/depotage`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyBooking | path | Oui | `{{keyBooking}}` |

Corps : Aucun corps.

Codes documentés : 200, 422.

### POST /api/v1/bookings/{keyBooking}/dossier

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/bookings/{{keyBooking}}/dossier?keyDossier={{keyDossier}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyBooking | path | Oui | `{{keyBooking}}` |
| keyDossier | query | Oui | `{{keyDossier}}` |

Corps : Aucun corps.

Codes documentés : 200, 422.

### DELETE /api/v1/bookings/{keyBooking}/dossier

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/bookings/{{keyBooking}}/dossier`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyBooking | path | Oui | `{{keyBooking}}` |

Corps : Aucun corps.

Codes documentés : 200.

### POST /api/v1/bookings/{keyBooking}/voyage

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/bookings/{{keyBooking}}/voyage`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyBooking | path | Oui | `{{keyBooking}}` |

Body → raw → JSON :

```json
{
  "keyVoyage": "{{keyVoyage}}"
}
```

Codes documentés : 200, 422.

### DELETE /api/v1/bookings/{keyBooking}/voyage/{keyVoyage}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/bookings/{{keyBooking}}/voyage/{{keyVoyage}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyBooking | path | Oui | `{{keyBooking}}` |
| keyVoyage | path | Oui | `{{keyVoyage}}` |

Corps : Aucun corps.

Codes documentés : 200, 422.

### GET /api/v1/bookings/capacite/{keyVoyage}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/bookings/capacite/{{keyVoyage}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyVoyage | path | Oui | `{{keyVoyage}}` |

Corps : Aucun corps.

Codes documentés : 200, 404.

### PUT /api/v1/bookings/{keyBooking}/equipements

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/bookings/{{keyBooking}}/equipements`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyBooking | path | Oui | `{{keyBooking}}` |

Body → raw → JSON :

```json
[
  {
    "typeEquipement": "A_REMPLACER",
    "natureEquipement": "Test Postman",
    "qte": 10
  }
]
```

Codes documentés : 200, 422.

### PUT /api/v1/bookings/{keyBooking}/colisages

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/bookings/{{keyBooking}}/colisages`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyBooking | path | Oui | `{{keyBooking}}` |

Body → raw → JSON :

```json
[
  {
    "typeColis": "Palette",
    "qte": 10,
    "poids": 100,
    "gerbable": true,
    "longueur": 1,
    "largeur": 1,
    "hauteur": 1,
    "volume": 1,
    "typeEquipement": null,
    "natureEquipement": null
  }
]
```

Codes documentés : 200, 422.

## 07 - Cotations

### GET /api/v1/baremes-achat

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/baremes-achat?Page=1&PageSize=20`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| Libelle | query | Non | `Test Postman` |
| Statut | query | Non | `0` |
| Fournisseur | query | Non | `{{keyFournisseur}}` |
| TypeBareme | query | Non | `0` |
| Flux | query | Non | `0` |
| Modifiables | query | Non | `false` |
| Page | query | Non | `1` |
| PageSize | query | Non | `20` |

Corps : Aucun corps.

Codes documentés : 200, 400.

### POST /api/v1/baremes-achat

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/baremes-achat`

Authentification : Bearer `{{token}}`.

Body → raw → JSON :

```json
{
  "libelleBareme": "Barème test Postman",
  "typeBareme": null,
  "baseCalcul": null,
  "fournisseur": null,
  "debutValidite": "{{dateTest}}",
  "finValidite": "{{dateFin}}",
  "arrondi1": null,
  "arrondi2": null,
  "devise": "{{devise}}",
  "flux": null,
  "incoterm": null
}
```

Codes documentés : 201, 409.

### GET /api/v1/baremes-achat/{keyBareme}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/baremes-achat/{{keyBareme}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyBareme | path | Oui | `{{keyBareme}}` |

Corps : Aucun corps.

Codes documentés : 200, 404.

### PUT /api/v1/baremes-achat/{keyBareme}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/baremes-achat/{{keyBareme}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyBareme | path | Oui | `{{keyBareme}}` |

Body → raw → JSON :

```json
{
  "libelleBareme": "Barème test Postman",
  "typeBareme": null,
  "baseCalcul": null,
  "fournisseur": null,
  "debutValidite": "{{dateTest}}",
  "finValidite": "{{dateFin}}",
  "arrondi1": null,
  "arrondi2": null,
  "devise": "{{devise}}",
  "flux": null,
  "incoterm": null
}
```

Codes documentés : 200, 409, 422.

### DELETE /api/v1/baremes-achat/{keyBareme}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/baremes-achat/{{keyBareme}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyBareme | path | Oui | `{{keyBareme}}` |

Corps : Aucun corps.

Codes documentés : 204.

### POST /api/v1/baremes-achat/{keyBareme}/valider

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/baremes-achat/{{keyBareme}}/valider`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyBareme | path | Oui | `{{keyBareme}}` |

Corps : Aucun corps.

Codes documentés : 200, 422.

### POST /api/v1/baremes-achat/{keyBareme}/dupliquer

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/baremes-achat/{{keyBareme}}/dupliquer`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyBareme | path | Oui | `{{keyBareme}}` |

Body → raw → JSON :

```json
{
  "libelleBareme": null
}
```

Codes documentés : 201, 409.

### PUT /api/v1/baremes-achat/{keyBareme}/colonnes

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/baremes-achat/{{keyBareme}}/colonnes`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyBareme | path | Oui | `{{keyBareme}}` |

Body → raw → JSON :

```json
[
  {
    "idColonne": 1,
    "libelleColonne": "Test Postman",
    "trancheDebut": 1,
    "trancheFin": 1,
    "formuleCalcul": null,
    "mtMin": null,
    "commentaire": null
  }
]
```

Codes documentés : 200, 422.

### PUT /api/v1/baremes-achat/{keyBareme}/details

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/baremes-achat/{{keyBareme}}/details`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyBareme | path | Oui | `{{keyBareme}}` |

Body → raw → JSON :

```json
[
  {
    "idColonne": 1,
    "departement": "75",
    "montant": 1000
  }
]
```

Codes documentés : 200, 422.

### PUT /api/v1/baremes-achat/{keyBareme}/prestations

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/baremes-achat/{{keyBareme}}/prestations`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyBareme | path | Oui | `{{keyBareme}}` |

Body → raw → JSON :

```json
[
  {
    "codeRubrique": "{{codeRubrique}}",
    "libelleRubrique": "Test Postman",
    "mtDevise": 1000,
    "devise": "{{devise}}",
    "baseCalcul": null
  }
]
```

Codes documentés : 200, 422.

### GET /api/v1/baremes-achat/{keyBareme}/tarif

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/baremes-achat/{{keyBareme}}/tarif?departement=75&valeur=1000`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyBareme | path | Oui | `{{keyBareme}}` |
| departement | query | Oui | `75` |
| valeur | query | Oui | `1000` |

Corps : Aucun corps.

Codes documentés : 200, 404.

### GET /api/v1/cotations

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/cotations?Page=1&PageSize=20`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| KeyClient | query | Non | `{{keyClient}}` |
| Statut | query | Non | `Test Postman` |
| ProduitCotation | query | Non | `Test Postman` |
| Commercial | query | Non | `Test Postman` |
| NumCotation | query | Non | `PM-{{$timestamp}}` |
| DateDu | query | Non | `{{dateTest}}` |
| DateAu | query | Non | `{{dateTest}}` |
| Convertibles | query | Non | `false` |
| IncludeSupprimes | query | Non | `false` |
| Page | query | Non | `1` |
| PageSize | query | Non | `20` |

Corps : Aucun corps.

Codes documentés : 200, 400.

### POST /api/v1/cotations

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/cotations`

Authentification : Bearer `{{token}}`.

Body → raw → JSON :

```json
{
  "produitCotation": "Test Postm",
  "keyClient": "{{keyClient}}",
  "numCotation": null,
  "typeTransport": null,
  "natureMarchandise": null,
  "mode": null,
  "modeTraitement": null,
  "typeTarif": null,
  "incoterm": "{{incoterm}}",
  "portChargement": null,
  "portDechargement": null,
  "dateValidite": null,
  "contact": null,
  "commercial": null,
  "keyCompagnie": null,
  "intituleCompagnie": null,
  "appliquerConditions": false
}
```

Codes documentés : 201, 409.

### GET /api/v1/cotations/{keyCotation}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/cotations/{{keyCotation}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyCotation | path | Oui | `{{keyCotation}}` |

Corps : Aucun corps.

Codes documentés : 200, 404.

### PUT /api/v1/cotations/{keyCotation}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/cotations/{{keyCotation}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyCotation | path | Oui | `{{keyCotation}}` |

Body → raw → JSON :

```json
{
  "numCotation": "PM-{{$timestamp}}",
  "typeTransport": "Test Postman",
  "natureMarchandise": "Test Postman",
  "mode": "Test Postman",
  "modeTraitement": "Test Postman",
  "typeTarif": "Test Postman",
  "frequenceDepart": "Test Postman",
  "transitTime": "Test Postman",
  "langueClient": "Test Postman",
  "commentaire": "Test Postman",
  "commentairePrix": "Test Postman",
  "dateValidite": null,
  "dateLimiteEnlevement": null
}
```

Codes documentés : 200, 409, 422.

### DELETE /api/v1/cotations/{keyCotation}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/cotations/{{keyCotation}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyCotation | path | Oui | `{{keyCotation}}` |

Corps : Aucun corps.

Codes documentés : 204, 422.

### POST /api/v1/cotations/{keyCotation}/envoyer

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/cotations/{{keyCotation}}/envoyer`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyCotation | path | Oui | `{{keyCotation}}` |

Corps : Aucun corps.

Codes documentés : 200, 422.

### POST /api/v1/cotations/{keyCotation}/cloturer

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/cotations/{{keyCotation}}/cloturer`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyCotation | path | Oui | `{{keyCotation}}` |

Body → raw → JSON :

```json
{
  "gagnee": true,
  "commentaire": null,
  "concurrent": null,
  "montantConcurrent": null,
  "deviseConcurrent": null,
  "motifPerdue": null
}
```

Codes documentés : 200, 422.

### POST /api/v1/cotations/{keyCotation}/annuler

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/cotations/{{keyCotation}}/annuler`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyCotation | path | Oui | `{{keyCotation}}` |

Body → raw → JSON :

```json
{
  "motif": "Test Postman"
}
```

Codes documentés : 200, 422.

### POST /api/v1/cotations/{keyCotation}/dupliquer

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/cotations/{{keyCotation}}/dupliquer`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyCotation | path | Oui | `{{keyCotation}}` |

Body → raw → JSON :

```json
{
  "numCotation": null,
  "contreOffre": false
}
```

Codes documentés : 201, 409.

### PUT /api/v1/cotations/{keyCotation}/colisages

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/cotations/{{keyCotation}}/colisages`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyCotation | path | Oui | `{{keyCotation}}` |

Body → raw → JSON :

```json
[
  {
    "typeColis": "Palette",
    "qte": 10,
    "poids": 100,
    "gerbable": true,
    "longueur": 1,
    "largeur": 1,
    "hauteur": 1,
    "volume": 1,
    "volumeNgerbable": null,
    "typeEquipement": null,
    "natureEquipement": null,
    "largeurEquipement": null,
    "mplUnitaire": null
  }
]
```

Codes documentés : 200, 422.

### PUT /api/v1/cotations/{keyCotation}/prix

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/cotations/{{keyCotation}}/prix`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyCotation | path | Oui | `{{keyCotation}}` |

Body → raw → JSON :

```json
[
  {
    "position": 1,
    "codeRubrique": "{{codeRubrique}}",
    "libelleRubrique": "Test Postman",
    "quantite": 10,
    "typeUtilisation": 0,
    "montantValeur": 1000,
    "devise": "{{devise}}",
    "baseCalcul": "Forfait",
    "arrondi": false,
    "montantAchat": null,
    "deviseAchat": null,
    "commentaire": null,
    "codeTva": null
  }
]
```

Codes documentés : 200, 422.

### PUT /api/v1/cotations/{keyCotation}/achats

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/cotations/{{keyCotation}}/achats`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyCotation | path | Oui | `{{keyCotation}}` |

Body → raw → JSON :

```json
[
  {
    "codeRubrique": "{{codeRubrique}}",
    "libelleRubrique": "Test Postman",
    "mtDevise": 1000,
    "devise": "{{devise}}",
    "keyFournisseur": "{{keyFournisseur}}",
    "libelleFournisseur": "Test Postman",
    "dateValidite": null,
    "cheminJustif": null,
    "libelleJustif": null
  }
]
```

Codes documentés : 200, 422.

### PUT /api/v1/cotations/{keyCotation}/taux-change

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/cotations/{{keyCotation}}/taux-change`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyCotation | path | Oui | `{{keyCotation}}` |

Body → raw → JSON :

```json
[
  {
    "devise": "{{devise}}",
    "tauxChange": 1
  }
]
```

Codes documentés : 200, 422.

### POST /api/v1/cotations/{keyCotation}/documents

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/cotations/{{keyCotation}}/documents`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyCotation | path | Oui | `{{keyCotation}}` |

Body → raw → JSON :

```json
{
  "libelleDocument": "Test Postman",
  "cheminDocument": "Test Postman",
  "typePieceDocument": null
}
```

Codes documentés : 200.

### DELETE /api/v1/cotations/{keyCotation}/documents/{keyDocument}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/cotations/{{keyCotation}}/documents/{{keyDocument}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyCotation | path | Oui | `{{keyCotation}}` |
| keyDocument | path | Oui | `{{keyDocument}}` |

Corps : Aucun corps.

Codes documentés : 200, 404.

### PUT /api/v1/cotations/{keyCotation}/regroupement

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/cotations/{{keyCotation}}/regroupement`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyCotation | path | Oui | `{{keyCotation}}` |
| keyRegroupement | query | Non | `{{keyRegroupement}}` |

Corps : Aucun corps.

Codes documentés : 200.

### GET /api/v1/cotations/{keyCotation}/totaux

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/cotations/{{keyCotation}}/totaux`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyCotation | path | Oui | `{{keyCotation}}` |

Corps : Aucun corps.

Codes documentés : 200.

## 08 - Factures de vente

### GET /api/v1/factures-vente

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/factures-vente?page=1&pageSize=20`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyClient | query | Non | `{{keyClient}}` |
| type | query | Non | `1` |
| statut | query | Non | `Test Postman` |
| factRegle | query | Non | `false` |
| from | query | Non | `{{dateTest}}` |
| to | query | Non | `{{dateFin}}` |
| page | query | Non | `1` |
| pageSize | query | Non | `20` |

Corps : Aucun corps.

Codes documentés : 200.

### POST /api/v1/factures-vente

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/factures-vente`

Authentification : Bearer `{{token}}`.

Body → raw → JSON :

```json
{
  "keyClient": "{{keyClient}}",
  "intituleClient": null,
  "keyDossier": null,
  "keyVoyageInt": null,
  "natureService": null,
  "typeOperation": null,
  "agFact": null,
  "langue": null,
  "deviseFacture": "{{devise}}",
  "tauxDeviseFacture": 1,
  "dateFacture": "{{dateTest}}",
  "dateEcheance": null,
  "commentaireFacture": null,
  "numFacture": null,
  "lignes": [
    {
      "codeRubrique": "{{codeRubrique}}",
      "socRubrique": "{{societeKey}}",
      "libelleRubrique": "Test Postman",
      "natureLigne": 0,
      "mtTotalDevise": 1000,
      "tauxRemise": 0,
      "tauxChange": 1,
      "tauxTva": 0.2,
      "codeTva": "{{codeTva}}"
    }
  ]
}
```

Codes documentés : 201, 409, 422.

### GET /api/v1/factures-vente/{keyFacture}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/factures-vente/{{keyFacture}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyFacture | path | Oui | `{{keyFacture}}` |

Corps : Aucun corps.

Codes documentés : 200, 404.

### POST /api/v1/factures-vente/{keyFacture}/recalculate

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/factures-vente/{{keyFacture}}/recalculate`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyFacture | path | Oui | `{{keyFacture}}` |

Corps : Aucun corps.

Codes documentés : 200.

### POST /api/v1/factures-vente/{keyFacture}/avoir-total

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/factures-vente/{{keyFacture}}/avoir-total`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyFacture | path | Oui | `{{keyFacture}}` |
| avoirTotalFactRegle | query | Non | `false` |

Corps : Aucun corps.

Codes documentés : 201, 422.

### POST /api/v1/factures-vente/{keyFacture}/exoneration

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/factures-vente/{{keyFacture}}/exoneration`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyFacture | path | Oui | `{{keyFacture}}` |
| commentaire | query | Non | `Test Postman` |

Corps : Aucun corps.

Codes documentés : 200, 422.

## 09 - Règlements

### GET /api/v1/reglements

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/reglements?page=1&pageSize=20`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| type | query | Non | `0` |
| keyTiers | query | Non | `{{keyClient}}` |
| statut | query | Non | `0` |
| from | query | Non | `{{dateTest}}` |
| to | query | Non | `{{dateFin}}` |
| page | query | Non | `1` |
| pageSize | query | Non | `20` |

Corps : Aucun corps.

Codes documentés : 200.

### POST /api/v1/reglements

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/reglements`

Authentification : Bearer `{{token}}`.

Body → raw → JSON :

```json
{
  "typeReglement": 0,
  "keyTiers": "{{keyClient}}",
  "devise": "{{devise}}",
  "montantDevise": 1000,
  "tauxDevise": 1,
  "modePaiement": "{{modePaiement}}",
  "modeChange": null,
  "agenceReglement": null,
  "keyTresorie": null,
  "referencePaiement": null,
  "observation": null,
  "dossier": null,
  "dateOperation": "{{dateTest}}",
  "dateEcheance": null,
  "isAvance": false,
  "fraisBancaire": null,
  "tvaFraisBancaire": null,
  "comissionBanque": null
}
```

Codes documentés : 201.

### GET /api/v1/reglements/{keyReglement}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/reglements/{{keyReglement}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyReglement | path | Oui | `{{keyReglement}}` |

Corps : Aucun corps.

Codes documentés : 200, 404.

### POST /api/v1/reglements/{keyReglement}/allocations

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/reglements/{{keyReglement}}/allocations`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyReglement | path | Oui | `{{keyReglement}}` |

Body → raw → JSON :

```json
{
  "keyFacture": "{{keyFacture}}",
  "montant": 1000,
  "sens": 0,
  "rasIsGlobal": 0,
  "netAPayerGlobal": 0
}
```

Codes documentés : 200, 422.

### DELETE /api/v1/reglements/{keyReglement}/allocations/{keyFacture}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/reglements/{{keyReglement}}/allocations/{{keyFacture}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyReglement | path | Oui | `{{keyReglement}}` |
| keyFacture | path | Oui | `{{keyFacture}}` |

Corps : Aucun corps.

Codes documentés : 200.

### POST /api/v1/reglements/{keyReglement}/remise

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/reglements/{{keyReglement}}/remise?keyTresorie={{keyTresorie}}&dateRemise={{dateTest}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyReglement | path | Oui | `{{keyReglement}}` |
| keyTresorie | query | Oui | `{{keyTresorie}}` |
| refCompta | query | Non | `Test Postman` |
| dateRemise | query | Oui | `{{dateTest}}` |

Corps : Aucun corps.

Codes documentés : 200.

### POST /api/v1/reglements/{keyReglement}/impaye

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/reglements/{{keyReglement}}/impaye?motif=Test Postman`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyReglement | path | Oui | `{{keyReglement}}` |
| motif | query | Oui | `Test Postman` |
| commentaire | query | Non | `Test Postman` |

Corps : Aucun corps.

Codes documentés : 200.

## 10 - Personnel et matériel

### GET /api/v1/resources/personnel

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/resources/personnel?Page=1&PageSize=20`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| Recherche | query | Non | `Test Postman` |
| KeyFonction | query | Non | `{{keyFonction}}` |
| Agence | query | Non | `Test Postman` |
| IncludeSortis | query | Non | `false` |
| Page | query | Non | `1` |
| PageSize | query | Non | `20` |

Corps : Aucun corps.

Codes documentés : 200, 400.

### POST /api/v1/resources/personnel

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/resources/personnel`

Authentification : Bearer `{{token}}`.

Body → raw → JSON :

```json
{
  "nom": "Test Postman",
  "prenom": null,
  "matricule": null,
  "keyFonction": null,
  "keyService": null,
  "agence": null,
  "ville": null,
  "gsm": null,
  "email": null,
  "dateEmbauche": null
}
```

Codes documentés : 201, 422.

### GET /api/v1/resources/personnel/{keyPersonnel}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/resources/personnel/{{keyPersonnel}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyPersonnel | path | Oui | `{{keyPersonnel}}` |

Corps : Aucun corps.

Codes documentés : 200, 404.

### PUT /api/v1/resources/personnel/{keyPersonnel}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/resources/personnel/{{keyPersonnel}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyPersonnel | path | Oui | `{{keyPersonnel}}` |

Body → raw → JSON :

```json
{
  "matricule": "Test Postman",
  "keyFonction": "{{keyFonction}}",
  "keyService": "{{keyService}}",
  "keyDepartement": "{{keyDepartement}}",
  "nom": "Test Postman",
  "prenom": "Youssef",
  "gsm": "Test Postman",
  "email": "test.postman@example.com",
  "dateEmbauche": "{{dateTest}}"
}
```

Codes documentés : 200, 404.

### POST /api/v1/resources/personnel/{keyPersonnel}/sortie

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/resources/personnel/{{keyPersonnel}}/sortie`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyPersonnel | path | Oui | `{{keyPersonnel}}` |

Body → raw → JSON :

```json
{
  "dateSortie": "{{dateTest}}",
  "motif": "Test Postman"
}
```

Codes documentés : 200, 422.

### PUT /api/v1/resources/personnel/{keyPersonnel}/cout

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/resources/personnel/{{keyPersonnel}}/cout`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyPersonnel | path | Oui | `{{keyPersonnel}}` |

Body → raw → JSON :

```json
{
  "cout": 1,
  "deviseCout": "{{devise}}"
}
```

Codes documentés : 200, 422.

### GET /api/v1/resources/materiel

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/resources/materiel?Page=1&PageSize=20`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| Recherche | query | Non | `Test Postman` |
| TypeMateriel | query | Non | `{{TypeMateriel}}` |
| Statut | query | Non | `0` |
| IncludeRetires | query | Non | `false` |
| Page | query | Non | `1` |
| PageSize | query | Non | `20` |

Corps : Aucun corps.

Codes documentés : 200, 400.

### POST /api/v1/resources/materiel

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/resources/materiel`

Authentification : Bearer `{{token}}`.

Body → raw → JSON :

```json
{
  "immatriculation": "TEST-{{$timestamp}}",
  "typeMateriel": "{{typeMateriel}}",
  "natureMateriel": null,
  "intitule": null,
  "fournisseurMateriel": null
}
```

Codes documentés : 201, 422.

### GET /api/v1/resources/materiel/{keyMateriel}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/resources/materiel/{{keyMateriel}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyMateriel | path | Oui | `{{keyMateriel}}` |

Corps : Aucun corps.

Codes documentés : 200, 404.

### PUT /api/v1/resources/materiel/{keyMateriel}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/resources/materiel/{{keyMateriel}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyMateriel | path | Oui | `{{keyMateriel}}` |

Body → raw → JSON :

```json
{
  "immatriculation": "Test Postman",
  "intitule": "Test Postman",
  "typeMateriel": "{{typeMateriel}}",
  "natureMateriel": "Test Postman",
  "moyenPropre": "Test Postman",
  "dateAcquisition": "{{dateTest}}",
  "dateMiseCirculation": "{{dateTest}}",
  "dateFinGarantie": "{{dateTest}}"
}
```

Codes documentés : 200, 404.

### POST /api/v1/resources/materiel/{keyMateriel}/sortie

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/resources/materiel/{{keyMateriel}}/sortie`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyMateriel | path | Oui | `{{keyMateriel}}` |

Body → raw → JSON :

```json
{
  "dateSortie": "{{dateTest}}",
  "natureSortie": "Test Postman",
  "montantVente": 1000,
  "kmSortie": 1
}
```

Codes documentés : 200, 422.

### POST /api/v1/resources/materiel/{keyMateriel}/reparation/entree

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/resources/materiel/{{keyMateriel}}/reparation/entree`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyMateriel | path | Oui | `{{keyMateriel}}` |

Corps : Aucun corps.

Codes documentés : 200, 422.

### POST /api/v1/resources/materiel/{keyMateriel}/reparation/sortie

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/resources/materiel/{{keyMateriel}}/reparation/sortie`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyMateriel | path | Oui | `{{keyMateriel}}` |

Corps : Aucun corps.

Codes documentés : 200, 422.

### PUT /api/v1/resources/materiel/{keyMateriel}/cout

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/resources/materiel/{{keyMateriel}}/cout`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyMateriel | path | Oui | `{{keyMateriel}}` |

Body → raw → JSON :

```json
{
  "cout": 1,
  "deviseCout": "{{devise}}"
}
```

Codes documentés : 200, 422.

## 11 - Carburant

### GET /api/v1/carburant/citernes

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/carburant/citernes?page=1&pageSize=20`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| recherche | query | Non | `Test Postman` |
| type | query | Non | `0` |
| page | query | Non | `1` |
| pageSize | query | Non | `20` |

Corps : Aucun corps.

Codes documentés : 200, 400.

### POST /api/v1/carburant/citernes

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/carburant/citernes`

Authentification : Bearer `{{token}}`.

Body → raw → JSON :

```json
{
  "intituleCiterne": "Test Postman",
  "typeCiterne": 0,
  "agence": null,
  "indexCompteur": null
}
```

Codes documentés : 201, 422.

### GET /api/v1/carburant/citernes/{keyCiterne}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/carburant/citernes/{{keyCiterne}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyCiterne | path | Oui | `{{keyCiterne}}` |

Corps : Aucun corps.

Codes documentés : 200, 404.

### POST /api/v1/carburant/citernes/{keyCiterne}/reception

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/carburant/citernes/{{keyCiterne}}/reception`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyCiterne | path | Oui | `{{keyCiterne}}` |

Body → raw → JSON :

```json
{
  "quantite": 10,
  "montant": 1000,
  "indexCompteur": null
}
```

Codes documentés : 200, 422.

### POST /api/v1/carburant/approvisionnements

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/carburant/approvisionnements`

Authentification : Bearer `{{token}}`.

Body → raw → JSON :

```json
{
  "keyFournisseur": "{{keyFournisseur}}",
  "dateApprovisionnement": "{{dateTest}}",
  "montant": 1000,
  "numBl": "BL-{{$timestamp}}",
  "lignes": [
    {
      "keyCiterne": "{{keyCiterne}}",
      "quantite": 100,
      "indexCompteur": null
    }
  ]
}
```

Codes documentés : 201, 422.

### GET /api/v1/carburant/approvisionnements

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/carburant/approvisionnements?page=1&pageSize=20`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyFournisseur | query | Non | `{{keyFournisseur}}` |
| du | query | Non | `{{dateTest}}` |
| au | query | Non | `{{dateFin}}` |
| page | query | Non | `1` |
| pageSize | query | Non | `20` |

Corps : Aucun corps.

Codes documentés : 200.

### GET /api/v1/carburant/approvisionnements/{keyApprovisionnement}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/carburant/approvisionnements/{{keyApprovisionnement}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyApprovisionnement | path | Oui | `{{keyApprovisionnement}}` |

Corps : Aucun corps.

Codes documentés : 200, 404.

### POST /api/v1/carburant/bons

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/carburant/bons`

Authentification : Bearer `{{token}}`.

Body → raw → JSON :

```json
{
  "keyFournisseur": "{{keyFournisseur}}",
  "dateBon": "{{dateTest}}",
  "quantite": 10,
  "numBon": "BON-{{$timestamp}}",
  "devise": "{{devise}}",
  "montant": 1000,
  "typeCiterne": null,
  "keyVoyage": null,
  "keyMateriel": null,
  "keyChauffeur": null,
  "keyTransporteur": null,
  "matricule": null,
  "compteur": null,
  "plein": false,
  "observation": null,
  "typeBon": null
}
```

Codes documentés : 201, 422.

### GET /api/v1/carburant/bons

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/carburant/bons?page=1&pageSize=20`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyFournisseur | query | Non | `{{keyFournisseur}}` |
| du | query | Non | `{{dateTest}}` |
| au | query | Non | `{{dateFin}}` |
| page | query | Non | `1` |
| pageSize | query | Non | `20` |

Corps : Aucun corps.

Codes documentés : 200.

### GET /api/v1/carburant/bons/{keyBon}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/carburant/bons/{{keyBon}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyBon | path | Oui | `{{keyBon}}` |

Corps : Aucun corps.

Codes documentés : 200, 404.

### POST /api/v1/carburant/ratios/{keyMateriel}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/carburant/ratios/{{keyMateriel}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyMateriel | path | Oui | `{{keyMateriel}}` |
| produit | query | Non | `Gasoil` |

Corps : Aucun corps.

Codes documentés : 200, 204, 422.

### GET /api/v1/carburant/ratios/{keyMateriel}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/carburant/ratios/{{keyMateriel}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyMateriel | path | Oui | `{{keyMateriel}}` |

Corps : Aucun corps.

Codes documentés : 200.

### POST /api/v1/carburant/citernes/{keyCiterne}/sorties

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/carburant/citernes/{{keyCiterne}}/sorties`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyCiterne | path | Oui | `{{keyCiterne}}` |

Body → raw → JSON :

```json
{
  "quantite": 10,
  "dateSortie": "{{dateTest}}",
  "keyVoyage": null,
  "keyMateriel": null,
  "keyChauffeur": null,
  "matricule": null,
  "compteur": null,
  "plein": false,
  "observation": null,
  "typeSortie": null,
  "keyTransporteur": null
}
```

Codes documentés : 201, 422.

### DELETE /api/v1/carburant/sorties/{keySortie}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/carburant/sorties/{{keySortie}}`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keySortie | path | Oui | `{{keySortie}}` |

Corps : Aucun corps.

Codes documentés : 200, 404.

### GET /api/v1/carburant/sorties

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/carburant/sorties?page=1&pageSize=20`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyCiterne | query | Non | `{{keyCiterne}}` |
| du | query | Non | `{{dateTest}}` |
| au | query | Non | `{{dateFin}}` |
| page | query | Non | `1` |
| pageSize | query | Non | `20` |

Corps : Aucun corps.

Codes documentés : 200.

### POST /api/v1/carburant/citernes/{keyCiterne}/jauge

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/carburant/citernes/{{keyCiterne}}/jauge`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| keyCiterne | path | Oui | `{{keyCiterne}}` |

Body → raw → JSON :

```json
{
  "quantiteReelle": 1,
  "dateJauge": "{{dateTest}}",
  "indexCompteur": null
}
```

Codes documentés : 201, 422.

## 12 - Tracking

### GET /api/v1/tracking/{entite}/{cle}

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/tracking/{{entite}}/{{cle}}?take=100`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| entite | path | Oui | `{{entite}}` |
| cle | path | Oui | `{{cle}}` |
| take | query | Non | `100` |

Corps : Aucun corps.

Codes documentés : 200, 400.

### GET /api/v1/tracking/{entite}/{cle}/statut

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/tracking/{{entite}}/{{cle}}/statut`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| entite | path | Oui | `{{entite}}` |
| cle | path | Oui | `{{cle}}` |

Corps : Aucun corps.

Codes documentés : 200.

### GET /api/v1/tracking/activite

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/tracking/activite?take=100`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| entites | query | Non | `Client` |
| from | query | Non | `{{dateTest}}` |
| to | query | Non | `{{dateFin}}` |
| take | query | Non | `100` |

Corps : Aucun corps.

Codes documentés : 200.

### GET /api/v1/tracking/statuts

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/tracking/statuts`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| utilisation | query | Non | `Test Postman` |

Corps : Aucun corps.

Codes documentés : 200.

### POST /api/v1/tracking/{entite}/{cle}/evenements

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/tracking/{{entite}}/{{cle}}/evenements`

Authentification : Bearer `{{token}}`.

| Paramètre | Emplacement | Obligatoire | Exemple |
|---|---|---|---|
| entite | path | Oui | `{{entite}}` |
| cle | path | Oui | `{{cle}}` |

Body → raw → JSON :

```json
{
  "keyStatut": "{{keyStatut}}",
  "commentaire": "Test Postman",
  "keyDocument": null,
  "valeur": 0,
  "dateEvenement": null
}
```

Codes documentés : 201, 422.

## 13 - EDI STC

### POST /api/v1/edi/stc/analyser

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/edi/stc/analyser`

Authentification : Bearer `{{token}}`.

Corps : Fichier STC : form-data, champ fichier.

Codes documentés : 200, 400.

### POST /api/v1/edi/stc/importer

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/edi/stc/importer`

Authentification : Bearer `{{token}}`.

Corps : Fichier STC : form-data, champ fichier.

Codes documentés : 200, 400.

### POST /api/v1/edi/stc/reconcilier

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/edi/stc/reconcilier`

Authentification : Bearer `{{token}}`.

Corps : Aucun corps.

Codes documentés : 200.

### POST /api/v1/edi/stc/promouvoir

Requête exposée par le contrôleur.

URL : `{{baseUrl}}/api/v1/edi/stc/promouvoir`

Authentification : Bearer `{{token}}`.

Corps : Aucun corps.

Codes documentés : 200, 422.

