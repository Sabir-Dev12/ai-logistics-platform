# MCP Tools — interface de consultation

La page **MCP Tools**, accessible via le menu ou `/#mcp-tools`, réutilise le Design System existant : Card, Table, Badge, Button, Input, Select, Modal, LoadingState, EmptyState et ErrorState. Le CSS ajouté définit seulement la disposition, les espaces via les tokens, le retour à la ligne et le défilement local du tableau.

Les cinq tools fictifs GetClient, GetInvoice, GetShipment, GetTracking et GetBooking exposent leur nom, description, catégorie, disponibilité et dernière utilisation. « Voir les détails » affiche les paramètres attendus, les paramètres du dernier appel fictif, le résultat structuré ou l’erreur et le cas jamais utilisé. Recherche et filtres catégorie/statut sont combinables.

Cette interface est en consultation uniquement : aucun appel API, exécution de tool, éditeur SQL ou modification backend. Les identifiants, dates, statuts et résultats sont des exemples. Le statut Erreur illustre un dernier appel en échec ; Disponible/Indisponible illustrent la disponibilité simulée.

`createMockToolRepository({ tools: [] })` permet de vérifier le catalogue vide ; `createMockToolRepository({ failListOnce: true })` simule une erreur de chargement récupérable. Injecter ce repository via `<McpToolsPage repository={repository} />`. L’actualisation recharge seulement les fixtures locales ; quitter la page annule le chargement.

Validation : `npm run build`, `npm test`, `npm run lint` et contrôle visuel desktop/mobile du tableau, des filtres et du modal.
