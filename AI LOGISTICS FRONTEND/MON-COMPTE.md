# Mon compte — démonstration frontend

Ouvrir **Mon compte** dans la navigation ou `/#account` après connexion à l’application. Seul le branchement de cette route est ajouté au routeur ; aucune autre page ni aucun token/style du Design System n’est modifié.

Composants réutilisés : Card, Input, Button, Badge, Modal, Alert, LoadingState, EmptyState et ErrorState. Les nouvelles classes définissent seulement la disposition et les espacements existants. Les champs d’identité sont en lecture seule ; ils affichent un utilisateur et une société fictifs, pas l’identité réelle de l’authentification.

Le mot de passe fictif initial est `DemoActuel123!`. Sa vérification est simulée dans mockAccountRepository. Le nouveau mot de passe et sa confirmation sont validés (8–200 caractères et concordance), puis le mot de passe fictif change uniquement dans la mémoire du repository. Les champs sont effacés à la soumission. Aucun secret n’est enregistré dans localStorage/sessionStorage, aucun appel réseau n’est réalisé et aucun compte réel n’est modifié.

La déconnexion de la page est simulée avec confirmation et état vide ; elle ne modifie pas la session applicative réelle ni le comportement des autres pages. « Revenir à la démonstration » restaure le profil et le mot de passe fictifs. Le bouton de déconnexion réel déjà présent dans le header conserve son comportement existant.

Les chargements/mutations simulés sont asynchrones et annulés lorsque la page est quittée. Les erreurs internes ne sont pas exposées. Pour vérifier les états, injecter un repository :

```jsx
createMockAccountRepository({ profile: null }) // empty
createMockAccountRepository({ failLoadOnce: true }) // error + retry
createMockAccountRepository({ failChangeOnce: true }) // mutation failure
```

Tests : chargement, identité, profil vide, erreur récupérable, ancien mot de passe incorrect, confirmation, changement réussi, erreur de mutation et déconnexion simulée. Contrôle visuel desktop/mobile et build/lint.
