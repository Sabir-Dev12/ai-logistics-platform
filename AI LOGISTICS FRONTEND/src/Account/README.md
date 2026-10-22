# Mon compte — intégration backend

`ConnectedAccountPage` branche la session AuthProvider sur `accountService`. `AccountPage` reste une présentation, `useAccount` gère chargement/erreurs/annulation ; aucun HTTP dans les composants UI, aucun CSS modifié. La route protégée existante reste `#account`.

- GET `/api/v1/auth/moi` : nom affiché et identifiant utilisateur (`userKey`, le backend ne renvoie pas le login textuel).
- GET `/api/v1/reference/societe` : `intituleSoc` de la société courante. Sa clé technique n’est pas affichée. Une société absente/inaccessible laisse l’identité consultable avec un message et une reprise du chargement.
- POST `/api/v1/auth/mot-de-passe` : `{ ancienMotDePasse, nouveauMotDePasse }`, sans clé utilisateur ni société. Seul HTTP 204 confirme la réussite. Validation 8–200 caractères, ancien mot de passe requis, confirmation locale. Les champs sensibles sont effacés à la soumission, rien n’est persisté ni journalisé.
- Déconnexion : `auth.logout()` supprime le token en mémoire et déclenche la protection des routes existante. Le backend n’expose pas de route logout ; aucun endpoint fictif n’est appelé. Cela ne révoque pas un bearer déjà émis côté serveur.

Le 401 du POST peut désigner un ancien mot de passe incorrect. L’option interne `acceptPasswordRejection` ne s’applique qu’à ce POST exact ; elle n’est pas envoyée sur le réseau. Après ce 401, un GET `/auth/moi` vérifie la session : si elle reste valide, erreur de saisie ; sinon le traitement normal 401 invalide la session. Aucune répétition automatique du POST. Les autres endpoints conservent l’invalidation habituelle.

Erreurs : validation 400/422, accès interdit 403, compte absent/service account sans personnel 404, limitation 429, serveur, réseau, délai de 15 secondes et réponse invalide. Aucune fuite de détails techniques. Retry manuel après nouvelle saisie du mot de passe ; chargement relançable via les états existants. Les appels sont annulés au démontage.

Configuration : même proxy `/api` et même `AUTH_BACKEND_URL` que Connexion. La page n’utilise plus le repository de démonstration. Celui-ci est conservé seulement pour les anciennes fixtures ; il n’est plus importé par le code de production de Mon compte.

Tests : `tests/account.test.jsx` couvre la présentation, le chargement, l’absence d’identité, la reprise, les validations, les erreurs, la confirmation de déconnexion et l’intégration avec la session. `tests/account-service.test.js` vérifie payloads, statuts, conservation/invalidation de session, annulation et délais avec des réponses HTTP de test. Un changement réel de mot de passe doit être vérifié par l’utilisateur sur un compte de test ; ces tests ne modifient aucun compte réel.
