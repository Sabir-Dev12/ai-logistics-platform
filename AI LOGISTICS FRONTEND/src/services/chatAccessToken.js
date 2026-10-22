let token = null;
const listeners = new Set();
// Supplied only by the auth state. No persistence or development token fallback.
export function setChatAccessToken(value) {
  token = typeof value === "string" && value.trim() ? value.trim() : null;
  for (const listener of listeners) listener();
}
export function subscribeChatAccessToken(listener) { listeners.add(listener); return () => listeners.delete(listener); }
export async function getChatAccessToken() { return token; }
