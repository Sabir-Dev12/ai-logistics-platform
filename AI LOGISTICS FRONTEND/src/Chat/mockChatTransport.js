import { tokens } from "../design-system/tokens";

export const welcomeMessage = {
  id: "welcome", role: "assistant", status: "complete",
  content: "Bonjour. Je suis l’assistant logistique en mode démo. Les réponses sont simulées et aucune donnée métier n’est consultée.",
};

export const mockMessages = [welcomeMessage,
  { id: "example-user", role: "user", status: "complete", content: "Comment préparer une question sur une expédition ?" },
  { id: "example-assistant", role: "assistant", status: "complete", content: "Exemple de réponse simulée : indiquez la référence de l’expédition et précisez ce que vous souhaitez vérifier : documents, réservation ou suivi. Cette interface ne lit pas encore les données de votre plateforme." },
];

function delay(signal) {
  return new Promise((resolve, reject) => {
    if (signal.aborted) { reject(new DOMException("Aborted", "AbortError")); return; }
    const abort = () => { clearTimeout(timer); reject(new DOMException("Aborted", "AbortError")); };
    const timer = setTimeout(() => { signal.removeEventListener("abort", abort); resolve(); }, Number.parseInt(tokens.motion.layout, 10));
    signal.addEventListener("abort", abort, { once: true });
  });
}

// Transport contract: stream({ message, history, signal }) => AsyncIterable<string>.
// A future SignalR adapter can implement the same contract without changing UI.
export const mockChatTransport = {
  async *stream({ signal }) {
    const response = "Réponse simulée : votre question a bien été reçue. Pour une demande logistique, précisez la référence et les informations à vérifier. En mode démo, aucun client, booking, facture ou suivi réel n’est consulté. La connexion au chatbot sera ajoutée dans une prochaine étape.";
    const chunks = response.match(/(?:\S+\s*){1,5}/g) || [];
    for (const chunk of chunks) { await delay(signal); yield chunk; }
  },
};
