import { StrictMode } from "react";
import { act, renderHook } from "@testing-library/react";
import { expect, it, vi } from "vitest";
import { useChatSignalR } from "../src/hooks/useChatSignalR";

it("opens independent lifetimes in StrictMode and disposes connections and listeners on unmount", async () => {
  const services = [];
  const factory = vi.fn(() => {
    let listener;
    const current = {
      connect: vi.fn(async () => { listener({ status: "connected", error: null }); }),
      disconnect: vi.fn(async () => {}),
      subscribe: vi.fn((callback) => { listener = callback; return current.unsubscribe; }),
      unsubscribe: vi.fn(), stream: vi.fn(),
    };
    services.push(current);
    return current;
  });
  const view = renderHook(() => useChatSignalR({ serviceFactory: factory }), { wrapper: StrictMode });
  await act(async () => {});
  expect(services).toHaveLength(2);
  expect(services[0].disconnect).toHaveBeenCalledOnce();
  expect(services[0].unsubscribe).toHaveBeenCalledOnce();
  expect(view.result.current.ready).toBe(true);
  view.unmount();
  expect(services[1].disconnect).toHaveBeenCalledOnce();
  expect(services[1].unsubscribe).toHaveBeenCalledOnce();
});
