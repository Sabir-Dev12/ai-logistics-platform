// @vitest-environment node
/* global process */
import net from "node:net";
import { readFile } from "node:fs/promises";
import { describe, expect, it } from "vitest";
import { createSignalRService } from "../src/services/signalRService";

const url = process.env.SIGNALR_TEST_URL;
const tokenFile = process.env.SIGNALR_TEST_TOKEN_FILE;
const ready = async () => JSON.parse(await readFile(tokenFile, "utf8"));
describe.skipIf(!url || !tokenFile)("actual ChatHub / JS SDK integration (controlled LLM fixture)", () => {
  it("streams deltas, completes, cancels and recovers from a safe hub error", async () => {
    const { token } = await ready();
    const service = createSignalRService({ url: url + "/hubs/chat", accessTokenFactory: () => token });
    try {
      await service.connect();
      const response = service.stream({ message: "gradual", signal: new AbortController().signal });
      expect((await response.next()).value).toBe("first:gradual");
      expect((await response.next()).value).toBe("second:gradual");
      expect((await response.next()).done).toBe(true);
      const cancel = new AbortController();
      const waiting = service.stream({ message: "wait", signal: cancel.signal });
      expect((await waiting.next()).value).toBe("first:wait");
      const pending = waiting.next();
      cancel.abort();
      await expect(pending).rejects.toMatchObject({ name: "AbortError" });
      const failed = service.stream({ message: "fail", signal: new AbortController().signal });
      expect((await failed.next()).value).toBe("first:fail");
      await expect(failed.next()).rejects.toMatchObject({ code: "chat_generation_failed" });
      const after = service.stream({ message: "normal", signal: new AbortController().signal });
      expect((await after.next()).value).toBe("first:normal");
      await after.return();
    } finally { await service.disconnect(); }
  }, 15000);

  it("reconnects after a real TCP interruption without replaying an interrupted prompt", async () => {
    const { token } = await ready();
    const target = new URL(url);
    const sockets = new Set();
    const proxy = net.createServer((client) => {
      const upstream = net.connect({ host: target.hostname, port: Number(target.port) });
      sockets.add(client); sockets.add(upstream);
      client.on("error", () => upstream.destroy()); upstream.on("error", () => client.destroy());
      client.on("close", () => { sockets.delete(client); upstream.destroy(); });
      upstream.on("close", () => { sockets.delete(upstream); client.destroy(); });
      client.pipe(upstream).pipe(client);
    });
    await new Promise((resolve) => proxy.listen(0, "127.0.0.1", resolve));
    const service = createSignalRService({ url: `http://127.0.0.1:${proxy.address().port}/hubs/chat`, accessTokenFactory: () => token });
    const transitions = [];
    let resolveReconnected;
    const reconnected = new Promise((resolve) => { resolveReconnected = resolve; });
    const unsubscribe = service.subscribe(({ status }) => {
      transitions.push(status);
      if (status === "connected" && transitions.includes("reconnecting")) resolveReconnected();
    });
    try {
      await service.connect();
      const interrupted = service.stream({ message: "wait", signal: new AbortController().signal });
      expect((await interrupted.next()).value).toBe("first:wait");
      const result = interrupted.next().catch((error) => error);
      for (const socket of sockets) socket.destroy();
      await reconnected;
      expect((await result).code).toBe("chat_connection_lost");
      expect(service.getSnapshot().status).toBe("connected");
      const after = service.stream({ message: "recovered", signal: new AbortController().signal });
      expect((await after.next()).value).toBe("first:recovered");
      expect((await after.next()).value).toBe("second:recovered");
      expect((await after.next()).done).toBe(true);
    } finally {
      unsubscribe(); await service.disconnect();
      for (const socket of sockets) socket.destroy();
      await new Promise((resolve) => proxy.close(resolve));
    }
  }, 20000);
});
