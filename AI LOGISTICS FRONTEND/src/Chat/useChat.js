import { useEffect, useRef, useState } from "react";
import { chatErrorMessage } from "../services/chatErrors";

export function useChat({ transport, initialMessages = [], resetMessages = initialMessages.slice(0, 1) } = {}) {
  const [messages, setMessages] = useState(initialMessages);
  const [phase, setPhase] = useState("idle");
  const [error, setError] = useState(null);
  const active = useRef(null);
  const lastPrompt = useRef("");
  const sequence = useRef(0);

  useEffect(() => () => { active.current?.controller.abort(); active.current = null; }, []);

  const stop = () => {
    const request = active.current;
    if (!request) return;
    active.current = null;
    request.controller.abort();
    setMessages((current) => current.map((item) => item.id === request.id ? { ...item, status: "cancelled" } : item));
    setPhase("idle");
  };

  const send = (value, appendUser = true) => {
    const text = value.trim();
    if (!text || active.current) return false;
    const key = `message-${Date.now()}-${++sequence.current}`;
    const request = { controller: new AbortController(), id: key + "-assistant" };
    active.current = request;
    lastPrompt.current = text;
    setError(null);
    setPhase("waiting");
    const additions = appendUser ? [{ id: key + "-user", role: "user", content: text, status: "complete" }] : [];
    additions.push({ id: request.id, role: "assistant", content: "", status: "streaming" });
    setMessages((current) => [...current, ...additions]);

    const run = async () => {
      try {
        let hasText = false;
        const history = messages.filter((item) => item.status === "complete");
        for await (const chunk of transport.stream({ message: text, history, signal: request.controller.signal })) {
          if (active.current !== request || request.controller.signal.aborted) return;
          if (typeof chunk !== "string") throw new Error("Invalid stream chunk");
          if (!chunk) continue;
          hasText = hasText || Boolean(chunk.trim());
          setPhase("streaming");
          setMessages((current) => current.map((item) => item.id === request.id ? { ...item, content: item.content + chunk } : item));
        }
        if (!hasText && active.current === request) throw new Error("Empty response");
        if (active.current === request) {
          setMessages((current) => current.map((item) => item.id === request.id ? { ...item, status: "complete" } : item));
        }
      } catch (failure) {
        if (active.current !== request) return;
        if (request.controller.signal.aborted) return;
        setError(chatErrorMessage(failure));
        setMessages((current) => current.map((item) => item.id === request.id ? { ...item, status: "error" } : item));
      } finally {
        if (active.current === request) { active.current = null; setPhase("idle"); }
      }
    };
    void run();
    return true;
  };

  const reset = () => {
    active.current?.controller.abort();
    active.current = null;
    lastPrompt.current = "";
    setMessages(resetMessages);
    setError(null);
    setPhase("idle");
  };

  return { messages, phase, error, send, stop, reset, retry: () => send(lastPrompt.current, false) };
}
