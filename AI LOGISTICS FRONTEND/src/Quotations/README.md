# Cotations / Devis — frontend mocké

Route protégée : `#quotations`. Seuls le lien de navigation et les nouveaux fichiers de la page sont ajoutés. Les composants métier des autres pages et le backend restent inchangés.

## Réutilisation du design

Composants du Design System et styles existants de Réservations. `BookingLinesEditor` sert aux colisages ; `DocumentUpload` et les styles Knowledge Base servent au choix de documents. Le formulaire de lignes Prix/Achats/Taux est mutualisé au sein de cette page. Aucun nouveau CSS ni package.

## Fonctionnalités

Liste, recherche, filtres client/statut, pagination, fiche et totaux ; création, modification d’en-tête, duplication, envoi simulé, clôture gagnée/perdue, annulation, suppression confirmée ; ajout/modification/retrait des colisages, prix et achats ; taux de change ; association/retrait de documents ; création/affectation/retrait de regroupements et consultation des cotations associées.

Les règles vérifiées dans `QuotationFeature.cs` et `Cotation.cs` sont reprises dans le mock : en-tête modifiable aux statuts Saisie/Envoyée, motif requis pour une perte, au moins un prix pour envoyer et colisage obligatoire en groupage lorsque le paramètre fictif `requirePacking` est actif ; suppression refusée avec documents ou frais annexes. La duplication copie les colisages, prix, taux et documents, mais exclut achats et regroupement. Les achats exigent un fournisseur et un montant strictement positif ; leur validité ne dépasse pas la date de création, conformément à la règle actuelle du backend. Les lignes restent gérables sur les devis clôturés, comme avec `EnsureModifiable` dans le backend actuel.

## Montants fictifs

La simulation calcule les montants fixes et les pourcentages (base des lignes au montant de la même devise), convertit vente/achats en MAD et affiche la marge. Le taux MAD est toujours 1 ; les devises étrangères doivent avoir un taux positif unique. Une devise sans taux bloque l’affichage des totaux/marge plutôt qu’utiliser un taux inventé. Les valeurs sont arrondies à deux décimales.

Il s’agit d’un calcul frontend de démonstration, pas d’une reproduction comptable du moteur serveur. Lors d’une future intégration, confirmer l’unité du DTO `TotalVente` : le code actuel additionne les lignes de vente avant conversion et le mélange de devises ne doit pas être présenté comme un total MAD fiable. Aucun calcul backend n’est modifié ici.

## Données et documents

Repository isolé en mémoire ; « Actualiser » conserve les changements dans la page, le remontage/rechargement les réinitialise. Les autres modules ne sont pas synchronisés. Aucun appel API, courriel, facture réelle ni stockage navigateur. Les fichiers choisis ne sont pas lus ni envoyés : seuls nom/type/taille/date sont associés au devis fictif. Le retrait ne touche pas les fichiers du PC.

## Vérification

`npm run build`, `npm test` et `tests/quotations.test.jsx` couvrent le cycle de vie, les lignes et totaux, les taux absents, documents, regroupements, états loading/empty/error, erreurs de mutation et protection de route. Le repository est injectable ; les requêtes sont annulées au démontage et les mutations concurrentes bloquées.
