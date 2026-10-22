# Catalogue MCP connecté

La route de production injecte createMcpToolsService avec le request de la session existante via ConnectedMcpToolsPage. Aucun token ni résultat métier n’est stocké ou journalisé dans le navigateur. La présentation reste celle du Design System existant ; aucun CSS ajouté.

Le backend existant expose /mcp en Streamable HTTP **stateless**. La découverte effectue initialize (protocole 2025-11-25), notifications/initialized (202), puis tools/list. Les réponses JSON et SSE, identifiants JSON-RPC et curseurs sont vérifiés. Les autres versions et sessions stateful sont explicitement refusées ; ce module n’est pas un client MCP généraliste. Timeout global 15 secondes, annulation au démontage, réessai manuel. Limites défensives : 2 Mio par réponse décodée, 500 tools et 100 curseurs.

Le catalogue fournit les noms, descriptions et inputSchema. La table de paramètres et le schéma complet proviennent de ces données (keyClient, keyFacture, keyDossier, keyBooking, entity/entityKey/take). Les catégories sont des libellés de classement UI associés aux cinq noms existants. « Publié » signifie découvert dans tools/list, pas un contrôle de santé SQL ni une garantie d’accès métier.

Aucun endpoint d’historique n’existe actuellement : dernière utilisation, derniers arguments/résultats et erreurs métier restent indiqués comme non exposés. Aucun historique fictif n’est substitué. Aucun tools/call, exécution SQL, formulaire d’exécution, modification backend ou accès aux données métier n’est ajouté.

L’autorisation HTTP existante accepte désormais uniquement /mcp en plus de /api/*, toujours sur la même origine. Vite relaie /mcp vers le même backend que /api. En production, le reverse proxy doit également relayer /mcp ; la configuration Development du backend autorise http://localhost:5173 et http://127.0.0.1:5173. Pour une autre adresse, renseigner explicitement Cors:AllowedOrigins. La validation Origin reste active, sans contournement.

McpToolsPage sans repository garde le mock explicite pour les démonstrations/tests isolés. Les tests connectés emploient les vrais services frontend et des réponses contrôlées ; ils ne prouvent pas une connexion authentifiée au backend réel.

Référence du transport : https://modelcontextprotocol.io/specification/2025-11-25/basic/transports
