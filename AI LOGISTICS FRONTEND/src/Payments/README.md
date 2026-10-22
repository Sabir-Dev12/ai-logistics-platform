# Règlements — intégration API

ConnectedPaymentsPage injecte createPaymentsService via la requête authentifiée existante. Aucune requête HTTP dans les composants de présentation ; aucun token ou donnée métier stocké dans localStorage. Le Design System et les CSS restent inchangés.

## Lecture

GET /api/v1/reglements : pagination serveur 5/10/20, filtres type, keyTiers et statut. Les sept statuts backend sont mappés selon leurs valeurs réelles (Impaye=6, Regle=5). Les types couvrent Client, Fournisseur, Etrangers et Personnel.

Le champ de recherche utilise GET /api/v1/reglements/{clé exacte}, car le backend ne propose pas de recherche par référence de paiement ou nom. Un 404 est un résultat vide. Les autres filtres s’appliquent à ce seul règlement. Le filtre tiers attend une clé exacte et ne présente pas une liste fictive de tiers polymorphes.

## Écritures

- POST /api/v1/reglements : les données du formulaire sont sélectionnées explicitement. Un montant saisi positif devient négatif pour un décaissement. Le backend détermine le statut et force le taux de la devise société ; aucun statut, identité société, auteur ou montant converti n’est envoyé par le formulaire.
- POST /{key}/allocations : keyFacture, montant, sens (Vente=0/Achat=1), rasIsGlobal et netAPayerGlobal optionnels. Une lecture préalable refuse les doublons évidents. La réponse AllocateResult confirme l’écriture et présente l’écart, la décision et une éventuelle OD. Le rechargement suivant est distinct de cette confirmation.
- DELETE /{key}/allocations/{keyFacture} : retrait d’affectation.
- POST /{key}/remise?keyTresorie=...&dateRemise=...&refCompta=... . Le formulaire charge les trésoreries autorisées et exige une sélection disponible.
- POST /{key}/impaye?motif=...&commentaire=... . Les affectations restent conservées ; leur retrait est une action séparée.

Modes de paiement, devises et devise société chargés depuis /api/v1/reference/modes-paiement, /devises et /societe. La remise charge uniquement /tresoreries. Aucun calcul d’écart, seuil, conversion ou décision de lettrage n’est reproduit côté frontend.

Les références de tiers et de factures sont saisies comme clés exactes. L’ancien catalogue de factures compatibles du mock n’est pas utilisé : il imposait des règles de devise et de tiers qui ne correspondent pas au contrat backend. Le serveur garde la décision métier. Le solde d’une facture liée reste « Indisponible » lorsque le DTO ne le fournit pas. Le disponible d’un règlement ordinaire n’est pas inventé ; le reste d’une avance vient de resteAvance. Les montants affectés sont ceux retournés par le serveur.

## Correctifs backend nécessaires

ReglementDetail expose désormais referencePaiement, keyRemiseSys et refCompta pour conserver les références après réouverture de la fiche. Une affectation déjà présente est refusée par ReglementService avant toute modification de facture : auparavant le domaine renvoyait l’affectation existante et le service pouvait incrémenter à nouveau le total réglé. Ce garde protège les répétitions séquentielles ; il ne constitue pas une nouvelle garantie d’idempotence concurrente.

Recompiler et redémarrer Logistics.Api après déploiement. Le contrat OpenAPI local a été actualisé. Aucun SQL fourni ou exécuté par l’interface.

## États et validation

Chargement, vide, validation, erreurs métier/droits/serveur/réseau, délai de 15 secondes et lectures annulables. Aucune répétition automatique d’écriture. Une perte de confirmation bloque le renvoi et demande de vérifier le règlement. Un succès confirmé reste distinct d’un échec de rechargement.

Tests HTTP en mémoire, interface connectée et régressions mock. Tests backend ciblés sur le refus du doublon et la projection des références. Prévisualisation avec données fictives sur desktop/tablette/mobile. Aucune écriture sur une base réelle ; les échanges authentifiés réels restent à vérifier.
