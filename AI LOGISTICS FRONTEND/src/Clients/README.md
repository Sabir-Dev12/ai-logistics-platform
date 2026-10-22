# Clients — démonstration frontend

Page protégée accessible par la navigation **Clients** (`#clients`). Réutilise le layout et les Card, Button, Input, Select, Table, Badge, Alert, Modal, Pagination et états du Design System. Aucun token ni style global n’est modifié.

Fonctions : recherche par nom/référence/ville/pays, filtres activité/validation, pagination 5/10/20, fiche détaillée, création, modification, validation, suppression confirmée, adresses et banques. L’adresse principale de la fiche est modifiable dans le formulaire du client ; les adresses complémentaires et banques sont ajoutées/retirées, conformément aux opérations actuellement disponibles côté backend.

Les règles de la maquette proviennent de `Logistics.Domain/Entities/Clients/Client.cs` et `Logistics.Application/Features/Clients/ClientFeature.cs` : nom unique sans espaces, compte comptable unique, compte requis à la validation, aucun identifiant marocain pour un pays différent de MAROC, client validé/référencé non supprimable, banque liée bloquante, adresse Principal protégée, RIB unique. La validation ne rend pas le client actif. Un compte existant n’est pas effacé par une valeur vide.

`mockClientRepository` simule ces opérations en mémoire et retourne des copies isolées. Actualiser conserve les modifications de l’instance ; quitter/remonter la page ou recharger le navigateur recrée les données initiales. Aucun appel réseau, stockage local ou envoi de coordonnées bancaires. Les banques et RIB d’exemple sont fictifs.

`useClients` gère chargement, mutation unique, annulation au démontage et erreurs publiques. Les formulaires conservent leur saisie après une erreur. Les refus métier sont expliqués et les suppressions demandent une confirmation. Les messages internes d’un fournisseur ne sont pas affichés.

Une future intégration devra remplacer le repository par un adaptateur API avec recherche/pagination serveur, chargement de la fiche et opérations enfants. Les règles backend resteront la source de vérité. Aucun endpoint n’est connecté dans cette étape.

Vérifications : build de production, lint ciblé, tests de règles et de parcours UI. Responsive vérifié en navigateur à 320, 390, 768 et 1440 px, tables à défilement interne, formulaires/modales défilants, états loading/empty/error et reprise après erreur.
