# Factures de vente — intégration API

ConnectedSalesInvoicesPage injecte createInvoicesService via la requête authentifiée existante. Les composants de présentation ne font pas d’appels HTTP. Aucun token ni donnée métier n’est stocké dans localStorage. Aucun changement CSS, palette ou package. Backend inchangé pour cette intégration.

## Lecture et filtres

GET /api/v1/factures-vente : pagination serveur 5/10/20 et filtres keyClient, type, statut, factRegle, from, to. La borne de fin inclut toute la journée. Aucun filtrage local d’une page serveur.

L’API ne propose pas de recherche par numéro ou nom. Le champ de recherche effectue donc GET /api/v1/factures-vente/{clé exacte}. Un 404 devient un résultat vide. Les autres critères s’appliquent à cet unique document ; cela ne simule pas une recherche globale par numéro.

Le statut est un texte libre côté backend, sans catalogue : saisie du statut exact. Les six types sont pris en charge. Attention, TypeFacture est un enum à valeurs de caractères : Facture=70, Avoir=65, DebitNote=68, CreditNote=67, Od=79, Impaye=73 ; pas 0/1.

Sélection clients : GET /api/v1/clients, recherche et pagination par 20 ; catalogue devises : GET /api/v1/reference/devises. Les libellés clients du tableau sont les intitulés enregistrés sur les factures. Le service relit la fiche du client avant création pour transmettre son identité officielle.

## Écritures

- POST /api/v1/factures-vente : en-tête et lignes explicites, sans total calculé par le navigateur ni identité société/auteur fournie par le formulaire. La TVA saisie 20 % devient tauxTva=0.2 ; la remise reste 20 pour 20 %. Les références dossier/voyage sont des clés.
- Le numéro est facultatif ; contrairement au mock, cet endpoint ne génère pas de numéro automatiquement. S’il reste absent, la clé serveur est affichée comme référence.
- POST /{key}/recalculate.
- POST /{key}/avoir-total?avoirTotalFactRegle=true|false : réception d’un nouvel avoir avec statut de règlement demandé.
- POST /{key}/exoneration?commentaire=... : ne recalcule pas automatiquement les totaux d’en-tête. Après confirmation, le message demande un recalcul. Une fiche dont toutes les TVA de lignes sont à zéro et dont la TVA d’en-tête est encore non nulle est signalée à recalculer, sans modifier les montants retournés.

Aucun ajout d’endpoint de suppression, modification de facture ou annulation d’exonération.

## Montants et limites du DTO

Totaux, retenues, soldes et récapitulatif TVA viennent du backend. Le solde est affiché comme « serveur », sans réinterpréter l’unité du champ encoursMad. Le formulaire affiche « Calculé par le serveur » à la place des estimations du mock : arrondis et retenues dépendent de la société.

Le DTO ne fournit pas le montant TVA individuel d’une ligne : « Non fourni » est affiché, sans calcul local. Le récapitulatif enregistré, y compris ses codes TVA, est reproduit tel quel. Les liens source/avoir, le motif d’exonération et un indicateur persistant de recalcul ne figurent pas dans ce DTO : aucune valeur n’est inventée pour ces champs.

## États et vérification

Chargement, vide, dates et saisies invalides, conflit, droits, erreurs serveur/réseau, délai de 15 secondes et lectures annulables. Aucun renvoi automatique d’une écriture. Une perte de confirmation (réseau/5xx/réponse invalide) bloque la répétition et invite à vérifier la facture. Une écriture confirmée reste un succès si le rechargement suivant échoue.

Les mocks restent utilisables par injection explicite dans les tests. Tests de contrat HTTP en mémoire, tests de présentation connectée et régressions existantes. Prévisualisation sur données fictives à 390/768/1440 pixels. Ces vérifications ne constituent pas un test authentifié sur une base réelle ; aucune facture réelle n’est créée ou modifiée.
