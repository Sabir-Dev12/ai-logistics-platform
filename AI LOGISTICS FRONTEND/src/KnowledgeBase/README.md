# Knowledge Base connectée

La route applicative utilise ConnectedKnowledgeBasePage et le request authentifié existant. Le service knowledgeBaseService effectue les appels ; les composants de présentation ne font pas de HTTP. Aucun token ni contenu documentaire n’est enregistré dans localStorage.

- GET /api/v1/ai/documents : recherche par nom, type, statut et pagination serveur.
- POST /api/v1/ai/documents : multipart, champs files répétés, sans Content-Type manuel. Les résultats 201 peuvent contenir des documents failed ; le statut et les erreurs d’indexation sont affichés sans inventer une réussite.
- DELETE /api/v1/ai/documents/{id} : confirmation explicite, réponse 204, suppression du document et de ses vecteurs côté serveur.

La collection est choisie côté backend selon la société authentifiée ; c’est celle utilisée par le RAG du chatbot. La page n’expose pas de SQL ni de sélection de société. Les API de réindexation et de recherche sémantique existent côté serveur mais n’ont pas de commande dans cette interface.

Limites UI alignées sur les valeurs serveur par défaut : 10 fichiers, 10 Mio par fichier. Limite de lot conservatrice de 49 Mio pour réserver la marge multipart sous 50 Mio. Le serveur reste autoritaire (quota et configuration). Formats PDF, DOCX, TXT, MD, CSV. Les PDF scannés nécessitent une couche texte ; pas d’OCR ajouté.

Timeout lecture/suppression 15 s, upload/indexation 120 s. Aucun renvoi automatique des écritures. Une confirmation perdue bloque les opérations jusqu’à une actualisation manuelle réussie ; vérifier la liste avant de renvoyer. Annulation des appels obsolètes et au démontage. Une erreur de rafraîchissement après écriture confirmée ne transforme pas l’écriture en échec.

KnowledgeBasePage sans repository conserve le mode mock explicite pour les tests et démonstrations isolées. La route de production injecte toujours le service API.

Tests locaux avec réponses HTTP contrôlées : contrat multipart, métadonnées, pagination, statuts, erreurs, cancellation et états UI. Aucune connexion authentifiée réelle ni indexation Ollama réelle n’est prouvée par ces tests.
