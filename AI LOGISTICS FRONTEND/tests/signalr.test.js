import { afterEach, describe, expect, it, vi } from "vitest";
import { HubConnectionState } from "@microsoft/signalr";
import { createSignalRService } from "../src/services/signalRService";

afterEach(() => vi.useRealTimers());
function fixture(options = {}) {
  const events = {};
  const disposed = vi.fn();
  const hub = {
    state: HubConnectionState.Disconnected,
    start: vi.fn(async () => { hub.state = HubConnectionState.Connected; }),
    stop: vi.fn(async () => { hub.state = HubConnectionState.Disconnected; events.close?.(); }),
    onreconnecting: (callback) => { events.reconnecting = callback; },
    onreconnected: (callback) => { events.reconnected = callback; },
    onclose: (callback) => { events.close = callback; },
    stream: vi.fn(() => ({ subscribe(observer) { hub.observer = observer; return { dispose: disposed }; } })),
  };
  const tokenFactory = vi.fn(async () => "test-token");
  const service = createSignalRService({ connectionFactory: () => hub, accessTokenFactory: tokenFactory, initialRetryDelays: [0, 5], ...options });
  return { service, hub, events, disposed, tokenFactory };
}

describe("SignalR transport", () => {
  it("shares an in-flight connection and never starts anonymously", async () => {
    const { service, hub } = fixture();
    const first = service.connect();
    expect(service.connect()).toBe(first);
    await first;
    expect(hub.start).toHaveBeenCalledOnce();
    expect(service.getSnapshot().status).toBe("connected");
    await service.disconnect();
    const missing = fixture({ accessTokenFactory: () => null });
    await expect(missing.service.connect()).rejects.toMatchObject({ code: "chat_auth_required" });
    expect(missing.hub.start).not.toHaveBeenCalled();
    expect(missing.service.getSnapshot().status).toBe("unauthorized");
  });

  it("retries initial network failures but does not retry a rejected session", async () => {
    vi.useFakeTimers();
    const { service, hub } = fixture();
    hub.start.mockRejectedValueOnce(new Error("network"));
    const pending = service.connect();
    await vi.runAllTimersAsync();
    await pending;
    expect(hub.start).toHaveBeenCalledTimes(2);
    expect(service.getSnapshot().status).toBe("connected");
    await service.disconnect();
    const rejected = fixture();
    rejected.hub.start.mockRejectedValue(Object.assign(new Error("unauthorized"), { statusCode: 401 }));
    await expect(rejected.service.connect()).rejects.toMatchObject({ code: "chat_unauthorized" });
    expect(rejected.hub.start).toHaveBeenCalledOnce();
  });

  it("uses the actual hub contract and yields deltas before completion", async () => {
    const { service, hub, disposed } = fixture();
    await service.connect();
    const iterator = service.stream({ message: "Question", signal: new AbortController().signal });
    const first = iterator.next();
    expect(hub.stream).toHaveBeenCalledWith("StreamMessage", { message: "Question" });
    hub.observer.next({ message: "premier " });
    expect(await first).toEqual({ value: "premier ", done: false });
    const second = iterator.next();
    hub.observer.next({ message: "second" });
    expect(await second).toEqual({ value: "second", done: false });
    hub.observer.complete();
    expect((await iterator.next()).done).toBe(true);
    expect(disposed).toHaveBeenCalledOnce();
    await service.disconnect();
  });

  it("disposes a cancelled invocation and ignores late fragments", async () => {
    const { service, hub, disposed } = fixture();
    await service.connect();
    const controller = new AbortController();
    const iterator = service.stream({ message: "Question", signal: controller.signal });
    const pending = iterator.next();
    controller.abort();
    hub.observer.next({ message: "late" });
    await expect(pending).rejects.toMatchObject({ name: "AbortError" });
    expect(disposed).toHaveBeenCalledOnce();
    await service.disconnect();
  });

  it("fails an interrupted response without replaying it and permits a new stream after reconnection", async () => {
    const { service, hub, events } = fixture();
    await service.connect();
    const iterator = service.stream({ message: "Question", signal: new AbortController().signal });
    const first = iterator.next();
    hub.observer.next({ message: "partial" });
    await first;
    const waiting = iterator.next();
    hub.state = HubConnectionState.Reconnecting;
    events.reconnecting(new Error("network"));
    await expect(waiting).rejects.toMatchObject({ code: "chat_connection_lost" });
    expect(service.getSnapshot().status).toBe("reconnecting");
    hub.state = HubConnectionState.Connected;
    events.reconnected("new-id");
    expect(service.getSnapshot().status).toBe("connected");
    expect(hub.stream).toHaveBeenCalledOnce();
    const next = service.stream({ message: "New question", signal: new AbortController().signal });
    const answer = next.next();
    hub.observer.next({ message: "recovered" });
    expect((await answer).value).toBe("recovered");
    hub.observer.complete();
    await next.next();
    await service.disconnect();
  });

  it("preserves partial output and maps safe server errors", async () => {
    const { service, hub } = fixture();
    await service.connect();
    const iterator = service.stream({ message: "Question", signal: new AbortController().signal });
    const first = iterator.next();
    hub.observer.next({ message: "partial" });
    hub.observer.error(new Error("HubException: chat_llm_not_configured private-secret"));
    expect((await first).value).toBe("partial");
    await expect(iterator.next()).rejects.toMatchObject({ code: "chat_llm_not_configured", message: "Le service IA n’est pas configuré sur le serveur." });
    await service.disconnect();
  });

  it("rejects malformed items and propagates terminal disconnects", async () => {
    const { service, hub, events } = fixture();
    await service.connect();
    const malformed = service.stream({ message: "Question", signal: new AbortController().signal }).next();
    hub.observer.next({ text: "wrong" });
    await expect(malformed).rejects.toMatchObject({ code: "chat_invalid_response" });
    const waiting = service.stream({ message: "Question", signal: new AbortController().signal }).next();
    events.close(new Error("closed"));
    await expect(waiting).rejects.toMatchObject({ code: "chat_connection_lost" });
    expect(service.getSnapshot().status).toBe("failed");
    await service.disconnect();
  });

  it("cancels scheduled initial retries when disposed", async () => {
    vi.useFakeTimers();
    const { service, hub } = fixture();
    hub.start.mockRejectedValue(new Error("network"));
    const pending = service.connect().catch((error) => error);
    await vi.advanceTimersByTimeAsync(0);
    await service.disconnect();
    await vi.runAllTimersAsync();
    expect((await pending).name).toBe("AbortError");
    expect(hub.start).toHaveBeenCalledOnce();
    expect(hub.stop).toHaveBeenCalledOnce();
  });
});
