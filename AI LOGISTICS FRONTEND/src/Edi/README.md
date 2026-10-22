# Import EDI / STC — API

La route protégée utilise le transport authentifié existant. Aucun nouveau stockage de fichier, token ou préparation dans localStorage.

- POST /api/v1/edi/stc/analyser : FormData, champ fichier, analyse serveur sans écriture.
- POST /importer : renvoie le même objet File analysé, remplace la préparation société/agence/utilisateur après confirmation systématique.
- POST /reconcilier : sans body, validation côté serveur des références de la préparation ; anomalies affichées telles que retournées.
- POST /promouvoir : sans body, création réelle uniquement après réconciliation valide et confirmation. Les compteurs et références retournés sont affichés ; aucune clé voyage/dossier n’est inventée.

La limite frontend reste 2 Mio, sous la limite HTTP backend de 20 Mio. Aucun Content-Type multipart défini manuellement : le navigateur fournit la boundary. Pas de parsing local en production, exemples et alias locaux réservés aux tests mock explicites.

Limites du contrat : pas de GET de préparation, pas d’identifiant/version de lot, pas d’API de correspondance manuelle. L’état montré suit la session courante ; Actualiser ne prétend pas relire la base. Une autre session du même utilisateur peut remplacer la préparation serveur. Corriger les référentiels ou le fichier avant de réimporter. Les comptages sont vérifiés entre étapes, mais ne remplacent pas un verrou/version backend.

Lectures annulables et délai de 30 s. Aucune relance automatique des écritures. Après perte de confirmation ou 5xx sur une écriture, blocage pour cette session, conservé après Actualiser : il faut vérifier l’état backend avant de recharger la page. Le dry run d’analyse peut être repris.

Tests HTTP isolés et aperçu visuel local ne prouvent pas l’accès authentifié à la base réelle.
