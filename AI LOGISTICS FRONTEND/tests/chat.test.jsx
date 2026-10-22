import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import ChatPage from "../src/Chat/ChatPage";
import { mockChatTransport } from "../src/Chat/mockChatTransport";

afterEach(() => vi.useRealTimers());
const submit = (value) => {
  fireEvent.change(screen.getByRole("textbox", { name: "Votre message" }), { target: { value } });
  fireEvent.submit(screen.getByRole("form", { name: "Envoyer un message" }));
};

describe("mock chatbot", () => {
  it("identifies demo content and blocks empty submissions", () => {
    render(<ChatPage transport={mockChatTransport} initialMessages={[]} />);
    expect(screen.getByText("Mode démo")).toBeTruthy();
    expect(screen.getByText("Commencez une conversation")).toBeTruthy();
    submit("   ");
    expect(screen.getByRole("button", { name: "Envoyer" }).disabled).toBe(true);
    expect(screen.queryByRole("article")).toBeNull();
  });

  it("streams into one assistant message and releases the input controls on completion", async () => {
    vi.useFakeTimers();
    render(<ChatPage transport={mockChatTransport} initialMessages={[]} />);
    submit("Quelle référence dois-je préciser ?");
    expect(screen.getByRole("status", { name: "L’assistant prépare une réponse…" })).toBeTruthy();
    expect(screen.getByRole("textbox").value).toBe("");
    expect(screen.getByRole("button", { name: "Envoyer" }).disabled).toBe(true);
    await act(async () => { await vi.advanceTimersByTimeAsync(200); });
    const assistant = screen.getByRole("article", { name: "Message de Assistant" });
    const partial = assistant.textContent;
    expect(partial).toContain("Réponse simulée");
    expect(screen.getByRole("status", { name: "Réponse en cours…" })).toBeTruthy();
    await act(async () => { await vi.advanceTimersByTimeAsync(4000); });
    expect(screen.getAllByRole("article", { name: "Message de Assistant" })).toHaveLength(1);
    expect(assistant.textContent.length).toBeGreaterThan(partial.length);
    expect(assistant.textContent).toContain("prochaine étape.");
    expect(screen.getByRole("log").getAttribute("aria-busy")).toBe("false");
    expect(screen.queryByRole("button", { name: "Arrêter la réponse" })).toBeNull();
  });

  it("prevents concurrent submissions and ignores late chunks after cancellation", async () => {
    let release;
    const transport = { stream: vi.fn(async function* () {
      yield "Début de réponse ";
      await new Promise((resolve) => { release = resolve; });
      yield "fragment tardif";
    }) };
    render(<ChatPage transport={transport} initialMessages={[]} />);
    await act(async () => { submit("Question"); });
    submit("Autre question");
    expect(transport.stream).toHaveBeenCalledOnce();
    expect(screen.getAllByRole("article", { name: "Message de Vous" })).toHaveLength(1);
    fireEvent.click(screen.getByRole("button", { name: "Arrêter la réponse" }));
    expect(transport.stream.mock.calls[0][0].signal.aborted).toBe(true);
    await act(async () => { release(); });
    expect(screen.queryByText(/fragment tardif/)).toBeNull();
    expect(screen.getByText("Réponse arrêtée.")).toBeTruthy();
    expect(screen.getByRole("textbox").value).toBe("Autre question");
  });

  it("a new conversation aborts active work and clears draft and previous messages", async () => {
    vi.useFakeTimers();
    render(<ChatPage transport={mockChatTransport} />);
    submit("Question en cours");
    fireEvent.change(screen.getByRole("textbox"), { target: { value: "Brouillon" } });
    fireEvent.click(screen.getByRole("button", { name: "Nouvelle conversation" }));
    await act(async () => { await vi.advanceTimersByTimeAsync(4000); });
    expect(screen.getAllByRole("article")).toHaveLength(1);
    expect(screen.queryByText("Question en cours")).toBeNull();
    expect(screen.getByRole("textbox").value).toBe("");
    expect(screen.getByRole("log").getAttribute("aria-busy")).toBe("false");
  });

  it("shows a recoverable error without exposing technical details or duplicating the user prompt", async () => {
    const transport = { stream: vi.fn()
      .mockImplementationOnce(() => { throw new Error("Internal secret"); })
      .mockImplementation(async function* () { yield "Réponse retrouvée."; }) };
    render(<ChatPage transport={transport} initialMessages={[]} />);
    await act(async () => { submit("Question à réessayer"); });
    expect(screen.getByRole("alert")).toBeTruthy();
    expect(screen.queryByText(/Internal secret/)).toBeNull();
    await act(async () => { fireEvent.click(screen.getByRole("button", { name: "Réessayer" })); });
    expect(screen.getByText("Réponse retrouvée.")).toBeTruthy();
    expect(screen.queryByRole("alert")).toBeNull();
    expect(screen.getAllByRole("article", { name: "Message de Vous" })).toHaveLength(1);
  });

  it("renders messages as text rather than executable HTML", async () => {
    const transport = { async *stream() { yield "<strong>Réponse texte</strong>"; } };
    render(<ChatPage transport={transport} initialMessages={[]} />);
    await act(async () => { submit("<img src=x onerror=alert(1)>"); });
    expect(screen.getByText("<img src=x onerror=alert(1)>")).toBeTruthy();
    expect(screen.getByText("<strong>Réponse texte</strong>")).toBeTruthy();
    expect(document.querySelector("img,strong")).toBeNull();
  });

  it("aborts the transport on unmount", async () => {
    let signal;
    const transport = { async *stream(request) { signal = request.signal; yield "Fragment"; } };
    const view = render(<ChatPage transport={transport} initialMessages={[]} />);
    // Start before the async iterator can complete, then immediately unmount.
    act(() => { submit("Question"); view.unmount(); });
    await act(async () => {});
    expect(signal.aborted).toBe(true);
  });
});
