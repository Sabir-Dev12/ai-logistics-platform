import { HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr";
import { getChatAccessToken } from "./chatAccessToken";
import { ChatTransportError, isAuthenticationError, streamError } from "./chatErrors";

const retryDelays = [0, 2000, 10000, 30000];
const aborted = () => new DOMException("Aborted", "AbortError");
function delay(ms, signal) {
  return new Promise((resolve, reject) => {
    if (signal.aborted) { reject(aborted()); return; }
    const cancel = () => { clearTimeout(timer); reject(aborted()); };
    const timer = setTimeout(() => { signal.removeEventListener("abort", cancel); resolve(); }, ms);
    signal.addEventListener("abort", cancel, { once: true });
  });
}

export function createSignalRService({ url = "/hubs/chat", accessTokenFactory = getChatAccessToken,
  connectionFactory, initialRetryDelays = retryDelays } = {}) {
  const hubUrl = typeof window === "undefined" ? url : new URL(url, window.location.href).href;
  const connection = connectionFactory ? connectionFactory() : new HubConnectionBuilder()
    .withUrl(hubUrl, { accessTokenFactory: async () => {
      const token = await accessTokenFactory();
      if (!token) throw new ChatTransportError("chat_auth_required");
      return token;
    }, withCredentials: false })
    .withAutomaticReconnect({ nextRetryDelayInMilliseconds: (context) =>
      isAuthenticationError(context.retryReason) ? null : retryDelays[context.previousRetryCount] ?? null })
    .configureLogging(LogLevel.None).build();
  let snapshot = { status: "disconnected", error: null };
  let startPromise = null;
  let stopped = false;
  const lifetime = new AbortController();
  const listeners = new Set();
  const streams = new Set();
  const update = (status, error = null) => {
    snapshot = { status, error };
    for (const listener of listeners) listener(snapshot);
  };
  const failStreams = (error) => { for (const stream of streams) stream.fail(error); };
  connection.onreconnecting(() => {
    if (stopped) return;
    update("reconnecting");
    failStreams(new ChatTransportError("chat_connection_lost"));
  });
  connection.onreconnected(() => { if (!stopped) update("connected"); });
  connection.onclose((error) => {
    if (stopped) return;
    const failure = new ChatTransportError(isAuthenticationError(error) ? "chat_unauthorized" : "chat_connection_lost");
    update(isAuthenticationError(error) ? "unauthorized" : "failed", failure);
    failStreams(failure);
  });

  const connect = () => {
    if (stopped) return Promise.reject(aborted());
    if (connection.state === HubConnectionState.Connected) return Promise.resolve();
    if (startPromise) return startPromise;
    if (connection.state === HubConnectionState.Reconnecting) return Promise.reject(new ChatTransportError("chat_connection_lost"));
    startPromise = (async () => {
      let lastError;
      update("connecting");
      for (const wait of initialRetryDelays) {
        if (wait) await delay(wait, lifetime.signal);
        try {
          const token = await accessTokenFactory();
          if (stopped) throw aborted();
          if (!token) throw new ChatTransportError("chat_auth_required");
          await connection.start();
          if (stopped) throw aborted();
          update("connected");
          return;
        } catch (error) {
          if (stopped) throw aborted();
          lastError = error;
          if (isAuthenticationError(error)) break;
        }
      }
      const failure = lastError?.code === "chat_auth_required" ? lastError
        : new ChatTransportError(isAuthenticationError(lastError) ? "chat_unauthorized" : "chat_connection_failed");
      update(isAuthenticationError(failure) ? "unauthorized" : "failed", failure);
      throw failure;
    })().finally(() => { startPromise = null; });
    return startPromise;
  };

  async function* stream({ message, signal }) {
    if (signal.aborted) throw aborted();
    if (snapshot.status !== "connected") throw new ChatTransportError("chat_connection_lost");
    if (!message?.trim()) throw new ChatTransportError("chat_invalid_request");
    const queue = [];
    let done = false;
    let failure = null;
    let wake = null;
    let subscription;
    const notify = () => { wake?.(); wake = null; };
    const pending = { fail(error) { if (done) return; failure = error; done = true; notify(); } };
    const cancel = () => { queue.length = 0; pending.fail(aborted()); };
    signal.addEventListener("abort", cancel, { once: true });
    streams.add(pending);
    try {
      subscription = connection.stream("StreamMessage", { message }).subscribe({
        next(item) {
          if (done || signal.aborted) return;
          if (typeof item?.message !== "string") { pending.fail(new ChatTransportError("chat_invalid_response")); return; }
          if (item.message) { queue.push(item.message); notify(); }
        },
        error(error) { pending.fail(streamError(error)); },
        complete() { if (!done) { done = true; notify(); } },
      });
      while (true) {
        if (signal.aborted) throw aborted();
        if (queue.length) { yield queue.shift(); continue; }
        if (failure) throw failure;
        if (done) return;
        await new Promise((resolve) => { wake = resolve; });
      }
    } finally {
      signal.removeEventListener("abort", cancel);
      streams.delete(pending);
      subscription?.dispose();
    }
  }

  return {
    connect, stream,
    getSnapshot: () => snapshot,
    subscribe(listener) { listeners.add(listener); listener(snapshot); return () => listeners.delete(listener); },
    async disconnect() {
      if (stopped) return;
      stopped = true;
      lifetime.abort();
      failStreams(new ChatTransportError("chat_connection_lost"));
      update("disconnected");
      await connection.stop();
    },
  };
}
