# Module ML.NET : audit préalable et guide de réalisation

## Statut

Audit de lecture terminé le 13 octobre 2026. Aucun code ML, package, modèle,
endpoint ou donnée d'entraînement ajouté. La tâche jointe exige une validation
avant de démarrer la première phase d'implémentation.

La connexion Windows à `(localdb)\MSSQLLocalDB`, base `LogisticsDb`, a été
vérifiée avec des requêtes SELECT agrégées. Résultat réel :

| Table | Nombre de lignes |
| --- | ---: |
| `t_voyages` | 0 |
| `t_dossiers` | 0 |
| `t_booking` | 0 |
| `t_trajet_voyages` | 0 |

Les sommes conditionnelles sur les dates retournent NULL car aucune ligne
n'existe. Elles ne permettent de calculer ni taux de complétude, ni retard,
ni performance. Le jeu d'entraînement et le jeu d'évaluation sont indisponibles.

## Architecture existante

- `Logistics.Domain` : entités et règles métier.
- `Logistics.Application` : contrats, DTO, services métier, chat, RAG et agent.
- `Logistics.Infrastructure` : EF Core SQL Server, repositories et adaptateurs.
- `Logistics.Api` : endpoints `/api/v1/...`, authentification, DI, erreurs,
  SignalR et MCP.
- Tests xUnit unitaires et d'intégration ; runner local de secours.

Le backend cible .NET 10. EF Core SQL Server est référencé en version 8.0.22.
Le projet contient déjà `IVoyageService`, `IVoyageRepository`, `IDossierService`,
`IDossierRepository`, les services de booking et de suivi. L'agent sélectionne
actuellement cinq tools MCP en lecture seule. Aucune implémentation ML.NET ni
dépendance `Microsoft.ML` n'a été trouvée dans les projets ou le cache NuGet.

## Données du schéma et limites

| Entité | Champs intéressants | Limite à vérifier |
| --- | --- | --- |
| Voyage | création, départ/arrivée prévus et réels, origine/destination, ports, catégorie, nature, transporteur | arrivée de voyage différente d'une livraison finale ; champs modifiables |
| TrajetVoyage | origine/destination, pays, départ/arrivée prévus et réels, kilométrage | distance peut être saisie après le transport |
| Dossier | création, voyage associé, poids, volume, colis, statuts, dates de dépotage | pas de date explicite de livraison finale trouvée dans l'entité inspectée |
| Booking | mode, origine/destination, poids/volume prévisionnels, dates prévues | relier précisément au voyage et conserver la valeur connue au moment de prédire |
| Tracking | événements horodatés et statuts | les codes de livraison effective restent à identifier et confirmer |

La présence de colonnes ne signifie pas que des observations historiques sont
disponibles. Le schéma prévoit les champs ci-dessus ; les quatre tables
opérationnelles vérifiées sont actuellement vides.

## Cas d'usage proposés

1. **Analyse descriptive** des durées et retards par route : premier résultat
   exploitable après import de données réelles, sans entraînement.
2. **ETA d'arrivée de voyage**, après définition du moment de prédiction et du
   sens des dates : régression candidate sur une durée en heures.
3. **Retard d'arrivée de voyage**, si la date prévue originale est disponible
   et qu'il existe des exemples retardés et non retardés : classification binaire.

Ne pas présenter ces résultats comme une prédiction de livraison finale tant
que sa date/son événement réel ne sont pas identifiés. Les algorithmes candidats
sont FastTree pour la régression et SDCA/logistic regression pour la
classification ; le choix définitif dépendra du dataset et de l'évaluation.

## Structure proposée

```text
src/Logistics.Application/AI/ML/
    DTOs/               demandes et résultats sans dépendance ML.NET
    Services/           contrats de prédiction et d'audit des données
    Evaluation/         contrats de rapports et métriques

src/Logistics.Infrastructure/AI/ML/
    Models/             classes d'entrée/sortie ML.NET
    Data/               lecteur historique via EF/repositories, avec scope société
    Training/           pipelines, split temporel et baselines
    Evaluation/         évaluation sur des observations tenues à part
    Persistence/        chargement et artefacts versionnés
    Services/           adaptateur de prédiction et enregistrement DI

tools/Logistics.ML.Trainer/  exécutable d'entraînement hors de l'API
src/Logistics.Api/Controllers/MlController.cs
tests/.../                 qualité, chargement, service, API et orchestration
```

Les fichiers ci-dessus sont proposés, pas encore créés. Garder les contrats
dans Application et ML.NET dans Infrastructure évite de coupler les règles
métier au framework ML. Le lecteur historique ne réimplémente pas les règles
des services ; il prépare des observations autorisées pour l'entraînement.

## Phases et fichiers concernés

| Phase | Travail | Vérification |
| --- | --- | --- |
| 1 | contrats `AI/ML`, lecteur historique, `DataReadinessService`, rapport de qualité et tests | build, comptages, dates invalides, doublons, isolation société, refus du dataset vide |
| 2 | import/export d'historiques réels, features et labels, séparation chronologique | provenance des champs, absence d'informations futures, séparation des voyages entre partitions |
| 3 | dépendances ML.NET, trainer hors API, baselines, évaluation, artefacts | MAE/RMSE pour ETA ; précision/rappel/F1/AUC pour retard ; comparaison au baseline |
| 4 | service de chargement/prédiction, DI et `MlController` | fichiers absents/incompatibles, validation DTO, concurrence, erreurs API |
| 5 | tool MCP de prédiction et extension contrôlée de l'agent | valeurs ML inchangées, provenance et version visibles, séparation faits/prédictions/recommandations |

Les packages envisagés sont `Microsoft.ML`, éventuellement
`Microsoft.ML.FastTree` et `Microsoft.Extensions.ML`, avec versions compatibles
vérifiées au moment de l'ajout. Le `NuGet.config` du dépôt désactive les feeds ;
la restauration de nouveaux packages devra nommer explicitement la source.

Routes proposées :

```text
POST /api/v1/ml/eta/predict
POST /api/v1/ml/delay/predict
GET  /api/v1/ml/model/status
GET  /api/v1/ml/model/metrics
```

Le statut doit annoncer honnêtement `insufficient_data` ou `model_unavailable`
tant qu'aucun modèle n'est disponible. Aucune métrique fictive ou prédiction
par défaut ne doit masquer cet état. L'entraînement n'est jamais déclenché
par une requête de prédiction. Les endpoints ne divulguent pas les chemins
internes des artefacts.

## Comment refaire la démarche toi-même

1. **Définis la cible.** Par exemple : durée entre départ et arrivée du voyage.
   Précise quand tu demandes la prédiction. Une donnée connue à l'arrivée ne
   peut pas servir à une prédiction faite au départ.
2. **Inspecte les entités et leurs mappings.** Cherche les dates, routes et
   valeurs prévisionnelles. Confirme leur sens avec les opérations métier.
3. **Vérifie les vraies données.** Compte les observations, les valeurs
   manquantes, les dates incohérentes et les doublons. Une base contenant
   uniquement des référentiels ou des données de démonstration ne suffit pas.
4. **Prépare le dataset.** Utilise les informations effectivement connues à
   l'instant de prédiction. Exclue arrivée réelle, statut final et résultats
   futurs des inputs. La date d'arrivée peut servir au label, pas à la feature.
5. **Sépare passé et futur.** Entraîne sur les périodes anciennes et évalue
   sur une période plus récente. Les segments d'un même voyage doivent rester
   dans la même partition. Ajuste les transformations sur le train uniquement.
6. **Établis un baseline.** Pour l'ETA, une durée médiane apprise sur le train ;
   pour le retard, une règle naïve fondée sur le train. Compare sur le même test.
7. **Entraîne hors API.** Fixe la seed et conserve paramètres, période,
   schéma des features et version du modèle. N'invente pas d'historique pour
   remplir le dataset ; les fixtures de tests restent séparées du train réel.
8. **Évalue puis sauvegarde.** Pour l'ETA, MAE/RMSE exprimées en heures. Pour
   le retard, précision, rappel, F1 et AUC si les classes le permettent.
   N'annonce aucune précision réelle tant que cette étape n'a pas été faite.
9. **Sers le modèle déjà entraîné.** Charge un artefact versionné depuis la
   configuration. Utilise un pool ou un autre mécanisme adapté à la concurrence,
   pas un `PredictionEngine` partagé sans protection.
10. **Teste les erreurs et l'intégration.** Inputs incomplets, modèle absent,
    historique vide, incompatibilité de schéma et sortie du tool MCP. Le LLM
    ne calcule ni ne modifie la valeur prédite. Une probabilité n'est pas
    automatiquement un intervalle de confiance.

Commandes de vérification depuis la racine :

```powershell
dotnet build Logistics.slnx --no-restore -m:1
dotnet test tests/Logistics.UnitTests/Logistics.UnitTests.csproj --no-build --no-restore
```

Si le testhost VSTest est bloqué dans cet environnement, le runner existant
exécute les mêmes méthodes unitaires sans entrer dans les scénarios de base :

```powershell
dotnet build tests/Logistics.Verify/Logistics.Verify.csproj --no-restore -m:1
dotnet tests/Logistics.Verify/bin/Debug/net10.0/Logistics.Verify.dll --unit-tests-only
```

Références primaires :

- [Évaluation des métriques ML.NET](https://learn.microsoft.com/en-us/dotnet/machine-learning/resources/metrics)
- [Servir un modèle dans ASP.NET Core et gérer les PredictionEngine](https://learn.microsoft.com/en-us/dotnet/machine-learning/how-to-guides/serve-model-web-api-ml-net)

## Prochaine décision

Valider la phase 1 : architecture et audit de qualité, sans entraînement sur la
base vide. Le sens métier de « livraison » et l'origine des historiques réels
devront être confirmés avant d'entraîner un modèle opérationnel.
