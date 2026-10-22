import { act, fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AppLayout } from "../src/App/Layout";

let change;
let media;
const navigation = [{ href: "#main-content", label: "Dashboard" }, { href: "/clients", label: "Clients" }];
beforeEach(() => {
  media = { matches: true, addEventListener: vi.fn((event, listener) => { change = listener; }), removeEventListener: vi.fn() };
  vi.stubGlobal("matchMedia", vi.fn(() => media));
});
const resize = (matches) => act(() => { media.matches = matches; change(); });

describe("global layout", () => {
  it("provides landmarks, the active link and reusable children without inventing optional menus", () => {
    render(<AppLayout navigation={navigation} activeHref="/clients"><p>Page future</p></AppLayout>);
    expect(screen.getByRole("banner")).toBeTruthy();
    expect(screen.getByRole("main").textContent).toBe("Page future");
    expect(screen.getByRole("link", { name: "Clients" }).getAttribute("aria-current")).toBe("page");
    expect(screen.queryByRole("button", { name: "Ouvrir la navigation" })).toBeNull();
    expect(screen.queryByRole("menu")).toBeNull();
    expect(window.matchMedia).toHaveBeenCalledWith("(min-width: 768px)");
  });

  it("uses an accessible mobile navigation that closes on selection and restores focus", () => {
    media.matches = false;
    render(<AppLayout navigation={navigation}><p>Dashboard</p></AppLayout>);
    expect(screen.queryByRole("navigation")).toBeNull();
    const trigger = screen.getByRole("button", { name: "Ouvrir la navigation" });
    trigger.focus();
    fireEvent.click(trigger);
    expect(screen.getByRole("dialog", { name: "Navigation" })).toBeTruthy();
    expect(trigger.getAttribute("aria-expanded")).toBe("true");
    fireEvent.click(screen.getByRole("link", { name: "Dashboard" }));
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(document.activeElement).toBe(trigger);
    expect(document.body.style.overflow).not.toBe("hidden");
  });

  it("closes the mobile overlay on desktop resize and does not reopen it on returning to mobile", () => {
    media.matches = false;
    const view = render(<AppLayout navigation={navigation}>Graphiques</AppLayout>);
    fireEvent.click(screen.getByRole("button", { name: "Ouvrir la navigation" }));
    resize(true);
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(screen.getAllByRole("navigation")).toHaveLength(1);
    expect(document.body.style.overflow).not.toBe("hidden");
    resize(false);
    expect(screen.queryByRole("dialog")).toBeNull();
    view.unmount();
    expect(media.removeEventListener).toHaveBeenCalledWith("change", change);
  });

  it("mobile Escape dismisses navigation and the skip link targets main content", () => {
    media.matches = false;
    render(<AppLayout navigation={navigation}>Graphiques</AppLayout>);
    expect(screen.getByRole("link", { name: "Aller au contenu principal" }).hash).toBe("#main-content");
    expect(screen.getByRole("main").id).toBe("main-content");
    fireEvent.click(screen.getByRole("button", { name: "Ouvrir la navigation" }));
    fireEvent.keyDown(document.activeElement, { key: "Escape" });
    expect(screen.queryByRole("dialog")).toBeNull();
  });
});
