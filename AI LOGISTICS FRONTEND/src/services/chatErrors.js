const messages = {
  chat_cancelled: "La réponse a été arrêtée.",
  chat_auth_required: "Connectez-vous pour utiliser le chatbot.",
  chat_unauthorized: "Votre session a expiré ou n’est pas autorisée. Connectez-vous à nouveau.",
  chat_connection_failed: "La connexion au chatbot est indisponible. Vous pouvez réessayer.",
  chat_connection_lost: "La connexion a été interrompue. La réponse partielle est conservée ; réessayez après la reconnexion.",
  chat_invalid_request: "Le message ne peut pas être envoyé. Vérifiez votre saisie.",
  chat_llm_not_configured: "Le service IA n’est pas configuré sur le serveur.",
  chat_generation_failed: "La réponse n’a pas pu être terminée. Vous pouvez réessayer.",
  chat_invalid_response: "Le serveur a renvoyé une réponse non reconnue. Vous pouvez réessayer.",
};

export class ChatTransportError extends Error {
  constructor(code) { super(messages[code] || messages.chat_generation_failed); this.name = "ChatTransportError"; this.code = code; }
}

export const chatErrorMessage = (error) => messages[error?.code] || messages.chat_generation_failed;
export const isAuthenticationError = (error) => [401, 403].includes(error?.statusCode)
  || ["chat_auth_required", "chat_unauthorized"].includes(error?.code)
  || /status code\s*['"]?(401|403)\b/i.test(error?.message || "");

export function streamError(error) {
  const code = Object.keys(messages).find((key) => error?.message?.includes(key));
  if (!code && /underlying connection|connection.*closed|WebSocket.*closed|Server timeout/i.test(error?.message || ""))
    return new ChatTransportError("chat_connection_lost");
  return new ChatTransportError(code || (isAuthenticationError(error) ? "chat_unauthorized" : "chat_generation_failed"));
}
