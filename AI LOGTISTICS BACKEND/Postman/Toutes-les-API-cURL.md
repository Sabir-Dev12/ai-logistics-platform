# cURL pour importer chaque requête dans Postman

Postman → Import → Raw text : coller un seul bloc cURL à la fois. Sélectionner l'environnement Logistics - Local HTTP.
Les variables sont conservées lors de l'import. Obtenir d'abord le jeton avec la collection ; le script du POST /auth/token remplit token automatiquement.

## GET /health/live

```bash
curl --request GET '{{baseUrl}}/health/live'
```

## GET /health/ready

```bash
curl --request GET '{{baseUrl}}/health/ready'
```

## GET /openapi/v1.json

```bash
curl --request GET '{{baseUrl}}/openapi/v1.json'
```

## GET /swagger

```bash
curl --request GET '{{baseUrl}}/swagger'
```

## GET /api/v1/auth/moi

```bash
curl --request GET '{{baseUrl}}/api/v1/auth/moi' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/auth/mot-de-passe

```bash
curl --request POST '{{baseUrl}}/api/v1/auth/mot-de-passe' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "ancienMotDePasse": "{{ancienMotDePasse}}",
  "nouveauMotDePasse": "{{nouveauMotDePasse}}"
}'
```

## POST /api/v1/auth/token

```bash
curl --request POST '{{baseUrl}}/api/v1/auth/token' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "identifiant": "{{identifiant}}",
  "motDePasse": "{{motDePasse}}",
  "societeKey": "{{societeKey}}",
  "agenceKey": "{{agenceKey}}"
}'
```

## GET /api/v1/clients

```bash
curl --request GET '{{baseUrl}}/api/v1/clients?page=1&pageSize=20' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/clients

```bash
curl --request POST '{{baseUrl}}/api/v1/clients' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
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
}'
```

## DELETE /api/v1/clients/{keyClient}

```bash
curl --request DELETE '{{baseUrl}}/api/v1/clients/{{keyClient}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/clients/{keyClient}

```bash
curl --request GET '{{baseUrl}}/api/v1/clients/{{keyClient}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## PUT /api/v1/clients/{keyClient}

```bash
curl --request PUT '{{baseUrl}}/api/v1/clients/{{keyClient}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
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
}'
```

## POST /api/v1/clients/{keyClient}/adresses

```bash
curl --request POST '{{baseUrl}}/api/v1/clients/{{keyClient}}/adresses' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "adresse1": "Test Postman",
  "adresse2": "Test Postman",
  "cp": "20000",
  "ville": "Casablanca",
  "pays": "{{codePays}}",
  "telAdresse": "Test Postman",
  "faxAdresse": "Test Postman",
  "contact": "Test Postman"
}'
```

## DELETE /api/v1/clients/{keyClient}/adresses/{keyAdresse}

```bash
curl --request DELETE '{{baseUrl}}/api/v1/clients/{{keyClient}}/adresses/{{keyAdresse}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/clients/{keyClient}/banques

```bash
curl --request POST '{{baseUrl}}/api/v1/clients/{{keyClient}}/banques' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "banque": "Test Postman",
  "compteRib": "Test Postman",
  "codeBanque": "Test Postman",
  "adresseBanque": "Test Postman",
  "swift": "Test Postman",
  "ibanCode": "Test Postman"
}'
```

## DELETE /api/v1/clients/{keyClient}/banques/{keyBanque}

```bash
curl --request DELETE '{{baseUrl}}/api/v1/clients/{{keyClient}}/banques/{{keyBanque}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/clients/{keyClient}/validate

```bash
curl --request POST '{{baseUrl}}/api/v1/clients/{{keyClient}}/validate' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/reference/agences

```bash
curl --request GET '{{baseUrl}}/api/v1/reference/agences' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/reference/classes-client

```bash
curl --request GET '{{baseUrl}}/api/v1/reference/classes-client' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/reference/comptes-tva

```bash
curl --request GET '{{baseUrl}}/api/v1/reference/comptes-tva' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/reference/devises

```bash
curl --request GET '{{baseUrl}}/api/v1/reference/devises' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/reference/devises

```bash
curl --request POST '{{baseUrl}}/api/v1/reference/devises' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "codeDevise": "TST",
  "libelleDevise": "Devise test",
  "libelleFacture": "Unité test",
  "libelleCent": "Centime",
  "nbDecimal": 2
}'
```

## GET /api/v1/reference/incoterms

```bash
curl --request GET '{{baseUrl}}/api/v1/reference/incoterms' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/reference/modes-paiement

```bash
curl --request GET '{{baseUrl}}/api/v1/reference/modes-paiement' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/reference/natures-frais-bancaire

```bash
curl --request GET '{{baseUrl}}/api/v1/reference/natures-frais-bancaire' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/reference/parametres

```bash
curl --request GET '{{baseUrl}}/api/v1/reference/parametres' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/reference/pays

```bash
curl --request GET '{{baseUrl}}/api/v1/reference/pays' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/reference/pays

```bash
curl --request POST '{{baseUrl}}/api/v1/reference/pays' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "codePays": "ZZ",
  "intitulePays": "Pays de test Postman",
  "alpha2": "ZZ",
  "accord": null,
  "zonePays": null
}'
```

## GET /api/v1/reference/risques

```bash
curl --request GET '{{baseUrl}}/api/v1/reference/risques' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/reference/societe

```bash
curl --request GET '{{baseUrl}}/api/v1/reference/societe' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/reference/statuts-client

```bash
curl --request GET '{{baseUrl}}/api/v1/reference/statuts-client' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/reference/tresoreries

```bash
curl --request GET '{{baseUrl}}/api/v1/reference/tresoreries' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/reference/types-dossier

```bash
curl --request GET '{{baseUrl}}/api/v1/reference/types-dossier' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## DELETE /api/v1/reference/{kind}/{key}

```bash
curl --request DELETE '{{baseUrl}}/api/v1/reference/{{kind}}/{{key}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/reference/{kind}/{key}/references

```bash
curl --request GET '{{baseUrl}}/api/v1/reference/{{kind}}/{{key}}/references' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/voyages

```bash
curl --request GET '{{baseUrl}}/api/v1/voyages?page=1&pageSize=20' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/voyages

```bash
curl --request POST '{{baseUrl}}/api/v1/voyages' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
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
}'
```

## DELETE /api/v1/voyages/{keyVoyage}

```bash
curl --request DELETE '{{baseUrl}}/api/v1/voyages/{{keyVoyage}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/voyages/{keyVoyage}

```bash
curl --request GET '{{baseUrl}}/api/v1/voyages/{{keyVoyage}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## PUT /api/v1/voyages/{keyVoyage}

```bash
curl --request PUT '{{baseUrl}}/api/v1/voyages/{{keyVoyage}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
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
}'
```

## POST /api/v1/voyages/{keyVoyage}/close

```bash
curl --request POST '{{baseUrl}}/api/v1/voyages/{{keyVoyage}}/close' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/voyages/{keyVoyage}/cout/apercu

```bash
curl --request POST '{{baseUrl}}/api/v1/voyages/{{keyVoyage}}/cout/apercu' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "moisCalcul": "{{moisCalcul}}",
  "coutFraisBancaire": 0,
  "legs": null,
  "equipements": null,
  "personnel": null
}'
```

## POST /api/v1/voyages/{keyVoyage}/cout/generer

```bash
curl --request POST '{{baseUrl}}/api/v1/voyages/{{keyVoyage}}/cout/generer' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "moisCalcul": "{{moisCalcul}}",
  "coutFraisBancaire": 0,
  "legs": null,
  "equipements": null,
  "personnel": null
}'
```

## GET /api/v1/voyages/{keyVoyage}/cout/taux

```bash
curl --request GET '{{baseUrl}}/api/v1/voyages/{{keyVoyage}}/cout/taux' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## DELETE /api/v1/voyages/{keyVoyage}/dossiers/{keyDossier}

```bash
curl --request DELETE '{{baseUrl}}/api/v1/voyages/{{keyVoyage}}/dossiers/{{keyDossier}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/voyages/{keyVoyage}/dossiers/{keyDossier}

```bash
curl --request POST '{{baseUrl}}/api/v1/voyages/{{keyVoyage}}/dossiers/{{keyDossier}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/voyages/{keyVoyage}/equipements

```bash
curl --request POST '{{baseUrl}}/api/v1/voyages/{{keyVoyage}}/equipements?typeEquipement={{typeEquipement}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## DELETE /api/v1/voyages/{keyVoyage}/operations-cout

```bash
curl --request DELETE '{{baseUrl}}/api/v1/voyages/{{keyVoyage}}/operations-cout?operation=TRANSPORT' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/voyages/{keyVoyage}/operations-cout

```bash
curl --request GET '{{baseUrl}}/api/v1/voyages/{{keyVoyage}}/operations-cout' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/voyages/{keyVoyage}/operations-cout

```bash
curl --request POST '{{baseUrl}}/api/v1/voyages/{{keyVoyage}}/operations-cout?operation=TRANSPORT' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/voyages/{keyVoyage}/reopen

```bash
curl --request POST '{{baseUrl}}/api/v1/voyages/{{keyVoyage}}/reopen' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/voyages/{keyVoyage}/trajets

```bash
curl --request POST '{{baseUrl}}/api/v1/voyages/{{keyVoyage}}/trajets' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
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
}'
```

## DELETE /api/v1/voyages/{keyVoyage}/trajets/{keyTrajet}

```bash
curl --request DELETE '{{baseUrl}}/api/v1/voyages/{{keyVoyage}}/trajets/{{keyTrajet}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/voyages/{keyVoyage}/validate

```bash
curl --request POST '{{baseUrl}}/api/v1/voyages/{{keyVoyage}}/validate' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/dossiers

```bash
curl --request GET '{{baseUrl}}/api/v1/dossiers?page=1&pageSize=20' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/dossiers

```bash
curl --request POST '{{baseUrl}}/api/v1/dossiers' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
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
}'
```

## GET /api/v1/dossiers/by-voyage/{keyVoyage}

```bash
curl --request GET '{{baseUrl}}/api/v1/dossiers/by-voyage/{{keyVoyage}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/dossiers/by-voyage/{keyVoyage}/repartition

```bash
curl --request GET '{{baseUrl}}/api/v1/dossiers/by-voyage/{{keyVoyage}}/repartition?montantMad=1000&montantDevise=1000' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## DELETE /api/v1/dossiers/{keyDossier}

```bash
curl --request DELETE '{{baseUrl}}/api/v1/dossiers/{{keyDossier}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/dossiers/{keyDossier}

```bash
curl --request GET '{{baseUrl}}/api/v1/dossiers/{{keyDossier}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## PUT /api/v1/dossiers/{keyDossier}

```bash
curl --request PUT '{{baseUrl}}/api/v1/dossiers/{{keyDossier}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
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
}'
```

## POST /api/v1/dossiers/{keyDossier}/colisages

```bash
curl --request POST '{{baseUrl}}/api/v1/dossiers/{{keyDossier}}/colisages' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "typeColis": "Palette",
  "nbColis": 10,
  "natureMarchandise": "Test Postman",
  "marque": "Test Postman",
  "codeNgp": "Test Postman",
  "poidsBrut": 100,
  "volume": 1
}'
```

## DELETE /api/v1/dossiers/{keyDossier}/colisages/{keyLigneColisage}

```bash
curl --request DELETE '{{baseUrl}}/api/v1/dossiers/{{keyDossier}}/colisages/{{keyLigneColisage}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/dossiers/{keyDossier}/depotage

```bash
curl --request POST '{{baseUrl}}/api/v1/dossiers/{{keyDossier}}/depotage' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "dateDepotage": "{{dateTest}}",
  "lieuDepotage": 0,
  "constatDepotage": "Test Postman",
  "colisDepote": 10,
  "refDepotage": "Test Postman"
}'
```

## POST /api/v1/dossiers/{keyDossier}/depotage/annulation

```bash
curl --request POST '{{baseUrl}}/api/v1/dossiers/{{keyDossier}}/depotage/annulation' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/dossiers/{keyDossier}/statut-facturation

```bash
curl --request POST '{{baseUrl}}/api/v1/dossiers/{{keyDossier}}/statut-facturation?statut=0' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/bookings

```bash
curl --request GET '{{baseUrl}}/api/v1/bookings?Page=1&PageSize=20' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/bookings

```bash
curl --request POST '{{baseUrl}}/api/v1/bookings' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
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
}'
```

## GET /api/v1/bookings/capacite/{keyVoyage}

```bash
curl --request GET '{{baseUrl}}/api/v1/bookings/capacite/{{keyVoyage}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## DELETE /api/v1/bookings/{keyBooking}

```bash
curl --request DELETE '{{baseUrl}}/api/v1/bookings/{{keyBooking}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/bookings/{keyBooking}

```bash
curl --request GET '{{baseUrl}}/api/v1/bookings/{{keyBooking}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## PUT /api/v1/bookings/{keyBooking}

```bash
curl --request PUT '{{baseUrl}}/api/v1/bookings/{{keyBooking}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "numBooking": "PM-{{$timestamp}}",
  "typeChargement": "Test Postman",
  "incoterm": "Test Postman",
  "mode": "Test Postman",
  "dateValidite": "{{dateFin}}",
  "commentaire": "Test Postman",
  "natureOperation": "Test Postman"
}'
```

## POST /api/v1/bookings/{keyBooking}/annuler

```bash
curl --request POST '{{baseUrl}}/api/v1/bookings/{{keyBooking}}/annuler' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "motif": "Test Postman"
}'
```

## PUT /api/v1/bookings/{keyBooking}/colisages

```bash
curl --request PUT '{{baseUrl}}/api/v1/bookings/{{keyBooking}}/colisages' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '[
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
]'
```

## DELETE /api/v1/bookings/{keyBooking}/depotage

```bash
curl --request DELETE '{{baseUrl}}/api/v1/bookings/{{keyBooking}}/depotage' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/bookings/{keyBooking}/depotage

```bash
curl --request POST '{{baseUrl}}/api/v1/bookings/{{keyBooking}}/depotage' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "dateDepotage": "{{dateTest}}",
  "constat": "Test Postman",
  "colisDepote": 10,
  "magasin": "Test Postman",
  "codeBureauDouane": "Test Postman"
}'
```

## DELETE /api/v1/bookings/{keyBooking}/dossier

```bash
curl --request DELETE '{{baseUrl}}/api/v1/bookings/{{keyBooking}}/dossier' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/bookings/{keyBooking}/dossier

```bash
curl --request POST '{{baseUrl}}/api/v1/bookings/{{keyBooking}}/dossier?keyDossier={{keyDossier}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/bookings/{keyBooking}/dupliquer

```bash
curl --request POST '{{baseUrl}}/api/v1/bookings/{{keyBooking}}/dupliquer' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "numBooking": "BKC-{{$timestamp}}",
  "dateValidite": "{{dateFin}}"
}'
```

## PUT /api/v1/bookings/{keyBooking}/equipements

```bash
curl --request PUT '{{baseUrl}}/api/v1/bookings/{{keyBooking}}/equipements' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '[
  {
    "typeEquipement": "A_REMPLACER",
    "natureEquipement": "Test Postman",
    "qte": 10
  }
]'
```

## POST /api/v1/bookings/{keyBooking}/valider

```bash
curl --request POST '{{baseUrl}}/api/v1/bookings/{{keyBooking}}/valider' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/bookings/{keyBooking}/voyage

```bash
curl --request POST '{{baseUrl}}/api/v1/bookings/{{keyBooking}}/voyage' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "keyVoyage": "{{keyVoyage}}"
}'
```

## DELETE /api/v1/bookings/{keyBooking}/voyage/{keyVoyage}

```bash
curl --request DELETE '{{baseUrl}}/api/v1/bookings/{{keyBooking}}/voyage/{{keyVoyage}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/baremes-achat

```bash
curl --request GET '{{baseUrl}}/api/v1/baremes-achat?Page=1&PageSize=20' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/baremes-achat

```bash
curl --request POST '{{baseUrl}}/api/v1/baremes-achat' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
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
}'
```

## DELETE /api/v1/baremes-achat/{keyBareme}

```bash
curl --request DELETE '{{baseUrl}}/api/v1/baremes-achat/{{keyBareme}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/baremes-achat/{keyBareme}

```bash
curl --request GET '{{baseUrl}}/api/v1/baremes-achat/{{keyBareme}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## PUT /api/v1/baremes-achat/{keyBareme}

```bash
curl --request PUT '{{baseUrl}}/api/v1/baremes-achat/{{keyBareme}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
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
}'
```

## PUT /api/v1/baremes-achat/{keyBareme}/colonnes

```bash
curl --request PUT '{{baseUrl}}/api/v1/baremes-achat/{{keyBareme}}/colonnes' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '[
  {
    "idColonne": 1,
    "libelleColonne": "Test Postman",
    "trancheDebut": 1,
    "trancheFin": 1,
    "formuleCalcul": null,
    "mtMin": null,
    "commentaire": null
  }
]'
```

## PUT /api/v1/baremes-achat/{keyBareme}/details

```bash
curl --request PUT '{{baseUrl}}/api/v1/baremes-achat/{{keyBareme}}/details' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '[
  {
    "idColonne": 1,
    "departement": "75",
    "montant": 1000
  }
]'
```

## POST /api/v1/baremes-achat/{keyBareme}/dupliquer

```bash
curl --request POST '{{baseUrl}}/api/v1/baremes-achat/{{keyBareme}}/dupliquer' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "libelleBareme": null
}'
```

## PUT /api/v1/baremes-achat/{keyBareme}/prestations

```bash
curl --request PUT '{{baseUrl}}/api/v1/baremes-achat/{{keyBareme}}/prestations' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '[
  {
    "codeRubrique": "{{codeRubrique}}",
    "libelleRubrique": "Test Postman",
    "mtDevise": 1000,
    "devise": "{{devise}}",
    "baseCalcul": null
  }
]'
```

## GET /api/v1/baremes-achat/{keyBareme}/tarif

```bash
curl --request GET '{{baseUrl}}/api/v1/baremes-achat/{{keyBareme}}/tarif?departement=75&valeur=1000' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/baremes-achat/{keyBareme}/valider

```bash
curl --request POST '{{baseUrl}}/api/v1/baremes-achat/{{keyBareme}}/valider' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/cotations

```bash
curl --request GET '{{baseUrl}}/api/v1/cotations?Page=1&PageSize=20' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/cotations

```bash
curl --request POST '{{baseUrl}}/api/v1/cotations' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
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
}'
```

## DELETE /api/v1/cotations/{keyCotation}

```bash
curl --request DELETE '{{baseUrl}}/api/v1/cotations/{{keyCotation}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/cotations/{keyCotation}

```bash
curl --request GET '{{baseUrl}}/api/v1/cotations/{{keyCotation}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## PUT /api/v1/cotations/{keyCotation}

```bash
curl --request PUT '{{baseUrl}}/api/v1/cotations/{{keyCotation}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
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
}'
```

## PUT /api/v1/cotations/{keyCotation}/achats

```bash
curl --request PUT '{{baseUrl}}/api/v1/cotations/{{keyCotation}}/achats' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '[
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
]'
```

## POST /api/v1/cotations/{keyCotation}/annuler

```bash
curl --request POST '{{baseUrl}}/api/v1/cotations/{{keyCotation}}/annuler' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "motif": "Test Postman"
}'
```

## POST /api/v1/cotations/{keyCotation}/cloturer

```bash
curl --request POST '{{baseUrl}}/api/v1/cotations/{{keyCotation}}/cloturer' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "gagnee": true,
  "commentaire": null,
  "concurrent": null,
  "montantConcurrent": null,
  "deviseConcurrent": null,
  "motifPerdue": null
}'
```

## PUT /api/v1/cotations/{keyCotation}/colisages

```bash
curl --request PUT '{{baseUrl}}/api/v1/cotations/{{keyCotation}}/colisages' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '[
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
]'
```

## POST /api/v1/cotations/{keyCotation}/documents

```bash
curl --request POST '{{baseUrl}}/api/v1/cotations/{{keyCotation}}/documents' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "libelleDocument": "Test Postman",
  "cheminDocument": "Test Postman",
  "typePieceDocument": null
}'
```

## DELETE /api/v1/cotations/{keyCotation}/documents/{keyDocument}

```bash
curl --request DELETE '{{baseUrl}}/api/v1/cotations/{{keyCotation}}/documents/{{keyDocument}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/cotations/{keyCotation}/dupliquer

```bash
curl --request POST '{{baseUrl}}/api/v1/cotations/{{keyCotation}}/dupliquer' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "numCotation": null,
  "contreOffre": false
}'
```

## POST /api/v1/cotations/{keyCotation}/envoyer

```bash
curl --request POST '{{baseUrl}}/api/v1/cotations/{{keyCotation}}/envoyer' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## PUT /api/v1/cotations/{keyCotation}/prix

```bash
curl --request PUT '{{baseUrl}}/api/v1/cotations/{{keyCotation}}/prix' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '[
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
]'
```

## PUT /api/v1/cotations/{keyCotation}/regroupement

```bash
curl --request PUT '{{baseUrl}}/api/v1/cotations/{{keyCotation}}/regroupement' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## PUT /api/v1/cotations/{keyCotation}/taux-change

```bash
curl --request PUT '{{baseUrl}}/api/v1/cotations/{{keyCotation}}/taux-change' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '[
  {
    "devise": "{{devise}}",
    "tauxChange": 1
  }
]'
```

## GET /api/v1/cotations/{keyCotation}/totaux

```bash
curl --request GET '{{baseUrl}}/api/v1/cotations/{{keyCotation}}/totaux' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/factures-vente

```bash
curl --request GET '{{baseUrl}}/api/v1/factures-vente?page=1&pageSize=20' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/factures-vente

```bash
curl --request POST '{{baseUrl}}/api/v1/factures-vente' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
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
}'
```

## GET /api/v1/factures-vente/{keyFacture}

```bash
curl --request GET '{{baseUrl}}/api/v1/factures-vente/{{keyFacture}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/factures-vente/{keyFacture}/avoir-total

```bash
curl --request POST '{{baseUrl}}/api/v1/factures-vente/{{keyFacture}}/avoir-total' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/factures-vente/{keyFacture}/exoneration

```bash
curl --request POST '{{baseUrl}}/api/v1/factures-vente/{{keyFacture}}/exoneration' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/factures-vente/{keyFacture}/recalculate

```bash
curl --request POST '{{baseUrl}}/api/v1/factures-vente/{{keyFacture}}/recalculate' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/reglements

```bash
curl --request GET '{{baseUrl}}/api/v1/reglements?page=1&pageSize=20' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/reglements

```bash
curl --request POST '{{baseUrl}}/api/v1/reglements' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
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
}'
```

## GET /api/v1/reglements/{keyReglement}

```bash
curl --request GET '{{baseUrl}}/api/v1/reglements/{{keyReglement}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/reglements/{keyReglement}/allocations

```bash
curl --request POST '{{baseUrl}}/api/v1/reglements/{{keyReglement}}/allocations' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "keyFacture": "{{keyFacture}}",
  "montant": 1000,
  "sens": 0,
  "rasIsGlobal": 0,
  "netAPayerGlobal": 0
}'
```

## DELETE /api/v1/reglements/{keyReglement}/allocations/{keyFacture}

```bash
curl --request DELETE '{{baseUrl}}/api/v1/reglements/{{keyReglement}}/allocations/{{keyFacture}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/reglements/{keyReglement}/impaye

```bash
curl --request POST '{{baseUrl}}/api/v1/reglements/{{keyReglement}}/impaye?motif=Test Postman' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/reglements/{keyReglement}/remise

```bash
curl --request POST '{{baseUrl}}/api/v1/reglements/{{keyReglement}}/remise?keyTresorie={{keyTresorie}}&dateRemise={{dateTest}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/resources/materiel

```bash
curl --request GET '{{baseUrl}}/api/v1/resources/materiel?Page=1&PageSize=20' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/resources/materiel

```bash
curl --request POST '{{baseUrl}}/api/v1/resources/materiel' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "immatriculation": "TEST-{{$timestamp}}",
  "typeMateriel": "{{typeMateriel}}",
  "natureMateriel": null,
  "intitule": null,
  "fournisseurMateriel": null
}'
```

## GET /api/v1/resources/materiel/{keyMateriel}

```bash
curl --request GET '{{baseUrl}}/api/v1/resources/materiel/{{keyMateriel}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## PUT /api/v1/resources/materiel/{keyMateriel}

```bash
curl --request PUT '{{baseUrl}}/api/v1/resources/materiel/{{keyMateriel}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "immatriculation": "Test Postman",
  "intitule": "Test Postman",
  "typeMateriel": "{{typeMateriel}}",
  "natureMateriel": "Test Postman",
  "moyenPropre": "Test Postman",
  "dateAcquisition": "{{dateTest}}",
  "dateMiseCirculation": "{{dateTest}}",
  "dateFinGarantie": "{{dateTest}}"
}'
```

## PUT /api/v1/resources/materiel/{keyMateriel}/cout

```bash
curl --request PUT '{{baseUrl}}/api/v1/resources/materiel/{{keyMateriel}}/cout' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "cout": 1,
  "deviseCout": "{{devise}}"
}'
```

## POST /api/v1/resources/materiel/{keyMateriel}/reparation/entree

```bash
curl --request POST '{{baseUrl}}/api/v1/resources/materiel/{{keyMateriel}}/reparation/entree' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/resources/materiel/{keyMateriel}/reparation/sortie

```bash
curl --request POST '{{baseUrl}}/api/v1/resources/materiel/{{keyMateriel}}/reparation/sortie' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/resources/materiel/{keyMateriel}/sortie

```bash
curl --request POST '{{baseUrl}}/api/v1/resources/materiel/{{keyMateriel}}/sortie' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "dateSortie": "{{dateTest}}",
  "natureSortie": "Test Postman",
  "montantVente": 1000,
  "kmSortie": 1
}'
```

## GET /api/v1/resources/personnel

```bash
curl --request GET '{{baseUrl}}/api/v1/resources/personnel?Page=1&PageSize=20' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/resources/personnel

```bash
curl --request POST '{{baseUrl}}/api/v1/resources/personnel' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
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
}'
```

## GET /api/v1/resources/personnel/{keyPersonnel}

```bash
curl --request GET '{{baseUrl}}/api/v1/resources/personnel/{{keyPersonnel}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## PUT /api/v1/resources/personnel/{keyPersonnel}

```bash
curl --request PUT '{{baseUrl}}/api/v1/resources/personnel/{{keyPersonnel}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "matricule": "Test Postman",
  "keyFonction": "{{keyFonction}}",
  "keyService": "{{keyService}}",
  "keyDepartement": "{{keyDepartement}}",
  "nom": "Test Postman",
  "prenom": "Youssef",
  "gsm": "Test Postman",
  "email": "test.postman@example.com",
  "dateEmbauche": "{{dateTest}}"
}'
```

## PUT /api/v1/resources/personnel/{keyPersonnel}/cout

```bash
curl --request PUT '{{baseUrl}}/api/v1/resources/personnel/{{keyPersonnel}}/cout' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "cout": 1,
  "deviseCout": "{{devise}}"
}'
```

## POST /api/v1/resources/personnel/{keyPersonnel}/sortie

```bash
curl --request POST '{{baseUrl}}/api/v1/resources/personnel/{{keyPersonnel}}/sortie' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "dateSortie": "{{dateTest}}",
  "motif": "Test Postman"
}'
```

## GET /api/v1/carburant/approvisionnements

```bash
curl --request GET '{{baseUrl}}/api/v1/carburant/approvisionnements?page=1&pageSize=20' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/carburant/approvisionnements

```bash
curl --request POST '{{baseUrl}}/api/v1/carburant/approvisionnements' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
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
}'
```

## GET /api/v1/carburant/approvisionnements/{keyApprovisionnement}

```bash
curl --request GET '{{baseUrl}}/api/v1/carburant/approvisionnements/{{keyApprovisionnement}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/carburant/bons

```bash
curl --request GET '{{baseUrl}}/api/v1/carburant/bons?page=1&pageSize=20' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/carburant/bons

```bash
curl --request POST '{{baseUrl}}/api/v1/carburant/bons' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
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
}'
```

## GET /api/v1/carburant/bons/{keyBon}

```bash
curl --request GET '{{baseUrl}}/api/v1/carburant/bons/{{keyBon}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/carburant/citernes

```bash
curl --request GET '{{baseUrl}}/api/v1/carburant/citernes?page=1&pageSize=20' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/carburant/citernes

```bash
curl --request POST '{{baseUrl}}/api/v1/carburant/citernes' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "intituleCiterne": "Test Postman",
  "typeCiterne": 0,
  "agence": null,
  "indexCompteur": null
}'
```

## GET /api/v1/carburant/citernes/{keyCiterne}

```bash
curl --request GET '{{baseUrl}}/api/v1/carburant/citernes/{{keyCiterne}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/carburant/citernes/{keyCiterne}/jauge

```bash
curl --request POST '{{baseUrl}}/api/v1/carburant/citernes/{{keyCiterne}}/jauge' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "quantiteReelle": 1,
  "dateJauge": "{{dateTest}}",
  "indexCompteur": null
}'
```

## POST /api/v1/carburant/citernes/{keyCiterne}/reception

```bash
curl --request POST '{{baseUrl}}/api/v1/carburant/citernes/{{keyCiterne}}/reception' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "quantite": 10,
  "montant": 1000,
  "indexCompteur": null
}'
```

## POST /api/v1/carburant/citernes/{keyCiterne}/sorties

```bash
curl --request POST '{{baseUrl}}/api/v1/carburant/citernes/{{keyCiterne}}/sorties' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
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
}'
```

## GET /api/v1/carburant/ratios/{keyMateriel}

```bash
curl --request GET '{{baseUrl}}/api/v1/carburant/ratios/{{keyMateriel}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/carburant/ratios/{keyMateriel}

```bash
curl --request POST '{{baseUrl}}/api/v1/carburant/ratios/{{keyMateriel}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/carburant/sorties

```bash
curl --request GET '{{baseUrl}}/api/v1/carburant/sorties?page=1&pageSize=20' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## DELETE /api/v1/carburant/sorties/{keySortie}

```bash
curl --request DELETE '{{baseUrl}}/api/v1/carburant/sorties/{{keySortie}}' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/tracking/activite

```bash
curl --request GET '{{baseUrl}}/api/v1/tracking/activite?take=100' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/tracking/statuts

```bash
curl --request GET '{{baseUrl}}/api/v1/tracking/statuts' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## GET /api/v1/tracking/{entite}/{cle}

```bash
curl --request GET '{{baseUrl}}/api/v1/tracking/{{entite}}/{{cle}}?take=100' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/tracking/{entite}/{cle}/evenements

```bash
curl --request POST '{{baseUrl}}/api/v1/tracking/{{entite}}/{{cle}}/evenements' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --header 'Content-Type: application/json' \
  --data-raw '{
  "keyStatut": "{{keyStatut}}",
  "commentaire": "Test Postman",
  "keyDocument": null,
  "valeur": 0,
  "dateEvenement": null
}'
```

## GET /api/v1/tracking/{entite}/{cle}/statut

```bash
curl --request GET '{{baseUrl}}/api/v1/tracking/{{entite}}/{{cle}}/statut' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/edi/stc/analyser

```bash
curl --request POST '{{baseUrl}}/api/v1/edi/stc/analyser' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --form 'fichier=@/chemin/vers/votre-fichier.stc'
```

## POST /api/v1/edi/stc/importer

```bash
curl --request POST '{{baseUrl}}/api/v1/edi/stc/importer' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json' \
  --form 'fichier=@/chemin/vers/votre-fichier.stc'
```

## POST /api/v1/edi/stc/promouvoir

```bash
curl --request POST '{{baseUrl}}/api/v1/edi/stc/promouvoir' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```

## POST /api/v1/edi/stc/reconcilier

```bash
curl --request POST '{{baseUrl}}/api/v1/edi/stc/reconcilier' \
  --header 'Authorization: Bearer {{token}}' \
  --header 'Accept: application/json'
```
