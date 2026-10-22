import { act, cleanup, fireEvent, render, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import BackgroundVideo from "../src/Home/BackgroundVideo";
import { heroVideoSource } from "../src/Home/mediaAssets";

const state = vi.hoisted(() => ({ supported: true, instances: [] }));
vi.mock("hls.js", () => {
  class Hls {
    static isSupported() { return state.supported; }
    static Events = { MANIFEST_PARSED: "manifest", ERROR: "error" };
    static ErrorTypes = { MEDIA_ERROR: "media" };
    constructor() {
      this.handlers = {}; this.loadSource = vi.fn(); this.attachMedia = vi.fn(); this.destroy = vi.fn();
      this.recoverMediaError = vi.fn(); this.stopLoad = vi.fn(); this.startLoad = vi.fn(); state.instances.push(this);
    }
    on(event, handler) { this.handlers[event] = handler; }
  }
  return { default: Hls };
});
beforeEach(() => {
  state.supported = true; state.instances.length = 0;
  vi.spyOn(HTMLMediaElement.prototype, "play").mockResolvedValue(); vi.spyOn(HTMLMediaElement.prototype, "pause").mockImplementation(() => {});
  vi.spyOn(HTMLMediaElement.prototype, "load").mockImplementation(() => {}); vi.spyOn(HTMLMediaElement.prototype, "canPlayType").mockReturnValue("");
  vi.spyOn(document, "hidden", "get").mockReturnValue(false);
});
afterEach(() => { cleanup(); vi.restoreAllMocks(); });
const engine = async () => { await waitFor(() => expect(state.instances).toHaveLength(1)); return state.instances[0]; };
describe("hero HLS video", () => {
  it("attaches the requested stream, starts after the manifest and destroys HLS on unmount", async () => {
    const view = render(<BackgroundVideo />), hls = await engine(), video = view.container.querySelector("video");
    expect(hls.loadSource).toHaveBeenCalledWith(heroVideoSource); expect(hls.attachMedia).toHaveBeenCalledWith(video);
    act(() => hls.handlers.manifest()); expect(video.play).toHaveBeenCalled(); expect(video.muted).toBe(true);
    expect(video.loop).toBe(true); expect(video.playsInline).toBe(true);
    fireEvent.playing(video); expect(view.container.firstChild.dataset.ready).toBe("true");
    view.unmount(); expect(hls.destroy).toHaveBeenCalledOnce(); expect(video.pause).toHaveBeenCalled();
  });
  it("uses native Safari HLS and removes its metadata listener and source on cleanup", async () => {
    state.supported = false; HTMLMediaElement.prototype.canPlayType.mockReturnValue("probably");
    const view = render(<BackgroundVideo />), video = view.container.querySelector("video");
    await waitFor(() => expect(video.getAttribute("src")).toBe(heroVideoSource)); expect(state.instances).toHaveLength(0);
    fireEvent.loadedMetadata(video); expect(video.play).toHaveBeenCalledOnce(); view.unmount(); expect(video.getAttribute("src")).toBeNull();
    fireEvent.loadedMetadata(video); expect(video.play).toHaveBeenCalledOnce();
  });
  it("retains the poster for unsupported browsers and never requests an HLS stream while paused", async () => {
    state.supported = false; const view = render(<BackgroundVideo playing={false} />);
    await act(async () => {}); expect(state.instances).toHaveLength(0); expect(view.container.querySelector("video").getAttribute("src")).toBeNull();
    view.rerender(<BackgroundVideo playing />); await act(async () => {});
    expect(view.container.firstChild.dataset.ready).toBe("false"); expect(view.container.querySelector("img").getAttribute("src")).toContain("images.unsplash.com");
  });
  it("cleans up streaming when paused and creates a fresh engine when resumed", async () => {
    const view = render(<BackgroundVideo />), first = await engine(); view.rerender(<BackgroundVideo playing={false} />);
    expect(first.destroy).toHaveBeenCalledOnce(); view.rerender(<BackgroundVideo playing />);
    await waitFor(() => expect(state.instances).toHaveLength(2));
  });
  it("recovers a media error once then falls back to the poster without a retry loop", async () => {
    const view = render(<BackgroundVideo />), hls = await engine();
    act(() => hls.handlers.error(null, { fatal: true, type: "media" })); expect(hls.recoverMediaError).toHaveBeenCalledOnce();
    act(() => hls.handlers.error(null, { fatal: true, type: "media" })); expect(hls.destroy).toHaveBeenCalledOnce(); expect(view.container.firstChild.dataset.ready).toBe("false");
  });
  it("keeps the poster when autoplay is blocked and pauses downloads while the page is hidden", async () => {
    HTMLMediaElement.prototype.play.mockRejectedValue(new Error("Autoplay blocked"));
    const view = render(<BackgroundVideo />), hls = await engine();
    await act(async () => hls.handlers.manifest()); expect(view.container.firstChild.dataset.ready).toBe("false");
    vi.spyOn(document, "hidden", "get").mockReturnValue(true); fireEvent(document, new Event("visibilitychange")); expect(hls.stopLoad).toHaveBeenCalledOnce();
    vi.spyOn(document, "hidden", "get").mockReturnValue(false); fireEvent(document, new Event("visibilitychange")); expect(hls.startLoad).toHaveBeenCalledOnce();
  });
  it("does not create an engine if the page unmounts before the lazy import resolves", async () => {
    const view = render(<BackgroundVideo />); view.unmount(); await act(async () => {}); expect(state.instances).toHaveLength(0);
  });
});
