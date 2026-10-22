# Référentiels / Paramètres — API

La route protégée injecte le transport authentifié existant. Chaque rubrique charge sa collection complète ; recherche et pagination sont locales sur cette collection, sans multiplier les requêtes des autres catalogues.

GET /api/v1/reference : pays, devises, agences, comptes-tva, modes-paiement, tresoreries, types-dossier, incoterms, classes-client, statuts-client, risques, parametres, societe. Secteurs et groupes n’ont pas de GET : état explicite sans données fictives.

POST pays et devises : propriétés autorisées seulement et limites du schéma SQL (code pays 3, code devise 5, libellés 60/50). Les doublons sont décidés par le serveur.

GET /{ReferentialKind}/{key}/references : utilisations retournées sans inventer de nombres. Vérification liée à la rubrique et à la clé ; nouveau contrôle avant DELETE /{ReferentialKind}/{key}, puis garde métier renouvelée par le backend. DELETE attend 204. Le rôle administrateur est lu via GET /api/v1/auth/moi ; l’autorisation reste imposée par le backend. Agence et Incoterm sont masqués logiquement, les autres suppressions sont physiques.

Paramètres et société en lecture seule ; pas de référence société affichée. Valeurs de paramètres affichées comme texte, jamais interprétées. Les champs absents du contrat (délai de paiement, devise de trésorerie, forme juridique…) restent non renseignés.

Lectures annulables, délai de 15 s, reprise manuelle. Écritures sans répétition automatique. Confirmation perdue/5xx : blocage jusqu’à actualisation explicite et vérification. Une écriture confirmée reste un succès même si la lecture suivante échoue.

Tests HTTP isolés et aperçu visuel local ne prouvent pas l’accès authentifié à la base réelle. Aucune suppression réelle utilisée pour valider cette intégration.
