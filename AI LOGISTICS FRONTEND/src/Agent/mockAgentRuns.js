const question = "Où en est l’expédition SHP-DEMO-001 et quelle est la procédure si elle prend du retard ?";
const source = { id: "rag-demo-1", document: "Guide des expéditions.pdf", passage: "Procédure de retard — passage 4", excerpt: "En cas de retard, consulter les derniers événements de suivi puis contacter le service logistique pour confirmer la prochaine étape." };
const tool = { name: "GetShipment", input: { shipmentId: "SHP-DEMO-001" }, result: { shipmentId: "SHP-DEMO-001", status: "En transit", origin: "Casablanca", destination: "Tanger" }, error: null, status: "complete", durationMs: 320 };
const completedSteps = [
  { id: "user", label: "Question utilisateur", status: "complete", durationMs: null },
  { id: "agent", label: "Agent — analyse de la question", status: "complete", durationMs: 80 },
  { id: "rag", label: "RAG Search — recherche documentaire", status: "complete", durationMs: 240 },
  { id: "mcp", label: "MCP GetShipment — données métier", status: "complete", durationMs: 320 },
  { id: "llm", label: "LLM — rédaction", status: "complete", durationMs: 600 },
  { id: "response", label: "Réponse", status: "complete", durationMs: null },
];
const messages = (answer, status = "complete") => [
  { id: "question", role: "user", status: "complete", content: question },
  { id: "answer", role: "assistant", status, content: answer },
];
export const mockAgentRuns = [
  { id: "completed", label: "Réponse terminée", status: "complete", durationMs: 1240, steps: completedSteps, sources: [source], tools: [tool], error: null,
    messages: messages("Exemple fictif : SHP-DEMO-001 est en transit de Casablanca vers Tanger, selon GetShipment. En cas de retard, le Guide des expéditions recommande de consulter le suivi puis de contacter le service logistique. Aucune date de livraison n’est fournie par cet exemple.") },
  { id: "running", label: "Exécution en cours", status: "running", durationMs: null, sources: [source], tools: [{ ...tool, status: "running", result: null, durationMs: null }], error: null,
    steps: completedSteps.map((step, index) => ({ ...step, status: index < 3 ? "complete" : index === 3 ? "running" : "pending", durationMs: index < 3 ? step.durationMs : null })),
    messages: [{ id: "question", role: "user", status: "complete", content: question }] },
  { id: "failed", label: "Échec du tool MCP", status: "error", durationMs: 820, sources: [source],
    tools: [{ ...tool, status: "error", result: null, durationMs: 500, error: { code: "shipment_unavailable", message: "Le service d’expédition fictif est indisponible." } }],
    steps: completedSteps.map((step, index) => ({ ...step, status: index < 3 ? "complete" : index === 3 ? "error" : "skipped", durationMs: index < 3 ? step.durationMs : index === 3 ? 500 : null })),
    error: { code: "tool_call_failed", message: "GetShipment n’a pas répondu. Les données métier ne peuvent pas être confirmées dans ce scénario." },
    messages: messages("L’appel fictif à GetShipment a échoué. Je ne peux pas confirmer le statut de l’expédition. Le passage documentaire sur la procédure de retard reste consultable ci-dessous.", "error") },
];
export const executionLabels = { complete: "Terminé", running: "En cours", pending: "En attente", error: "Erreur", skipped: "Non exécuté" };
export const durationLabel = (milliseconds) => milliseconds === null ? "—" : `${new Intl.NumberFormat("fr-FR", { maximumFractionDigits: 2 }).format(milliseconds / 1000)} s`;
