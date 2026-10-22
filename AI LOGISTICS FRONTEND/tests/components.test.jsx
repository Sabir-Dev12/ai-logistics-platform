import { useState } from "react";
import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { Alert, Badge, Button, Card, Dropdown, EmptyState, ErrorState, Input, LoadingState, Modal, Pagination, Select, Table } from "../src/design-system";

describe("controls and states", () => {
  it("buttons do not submit forms implicitly and busy buttons cannot run actions", () => {
    const click = vi.fn();
    render(<Button loading onClick={click}>Enregistrer</Button>);
    const button = screen.getByRole("button", { name: /Enregistrer/ });
    expect(button.type).toBe("button");
    expect(button.disabled).toBe(true);
    expect(button.getAttribute("aria-busy")).toBe("true");
    fireEvent.click(button);
    expect(click).not.toHaveBeenCalled();
  });

  it("input labels and validation messages are connected and existing descriptions are preserved", () => {
    render(<Input label="Client" error="Client requis" aria-describedby="external-help" defaultValue="CLI001" />);
    const input = screen.getByLabelText("Client");
    expect(input.value).toBe("CLI001");
    expect(input.getAttribute("aria-invalid")).toBe("true");
    const description = input.getAttribute("aria-describedby").split(" ");
    expect(description[0]).toBe("external-help");
    expect(document.getElementById(description[1]).textContent).toBe("Client requis");
    expect(screen.getByRole("alert").textContent).toBe("Client requis");
  });

  it("multiple fields have unique IDs and the select stays a native keyboard-accessible control", () => {
    render(<><Input label="Origine" /><Input label="Destination" /><Select label="Mode" defaultValue="road"><option value="road">Route</option><option value="sea">Mer</option></Select></>);
    expect(screen.getByLabelText("Origine").id).not.toBe(screen.getByLabelText("Destination").id);
    expect(screen.getByRole("combobox", { name: "Mode" }).value).toBe("road");
  });

  it("cards preserve content and sizing classes without forcing padding or rounded corners", () => {
    render(<Card className="h-full w-full" data-testid="card">Graphique</Card>);
    const card = screen.getByTestId("card");
    expect(card.className).toBe("ds-card h-full w-full");
    expect(card.textContent).toBe("Graphique");
    expect(card.style.padding).toBe("");
    expect(card.className).not.toContain("rounded");
  });

  it("alerts can be dismissed and error states offer a retry without exposing a stack", () => {
    const dismiss = vi.fn();
    const retry = vi.fn();
    render(<><Alert variant="warning" title="Attention" onDismiss={dismiss}>Vérifier le dossier.</Alert><ErrorState message="Lecture indisponible" onRetry={retry} /></>);
    fireEvent.click(screen.getByRole("button", { name: "Fermer l’alerte" }));
    fireEvent.click(screen.getByRole("button", { name: "Réessayer" }));
    expect(dismiss).toHaveBeenCalledOnce();
    expect(retry).toHaveBeenCalledOnce();
    expect(screen.getAllByRole("alert")).toHaveLength(2);
  });

  it("loading, empty and badge content are reusable without fake business data", () => {
    render(<><LoadingState label="Chargement des voyages" /><EmptyState description="Aucun voyage disponible." /><Badge variant="success">Terminé</Badge></>);
    expect(screen.getByRole("status", { name: "Chargement des voyages" })).toBeTruthy();
    expect(screen.getByText("Aucun voyage disponible.")).toBeTruthy();
    expect(screen.getByText("Terminé").dataset.variant).toBe("success");
  });
});

describe("table and pagination", () => {
  const columns = [{ key: "name", header: "Client" }];
  it("renders a semantic table with caption and custom cell content", () => {
    render(<Table caption="Clients" columns={[...columns, { key: "status", header: "Statut", render: (value) => <Badge>{value}</Badge> }]}
      rows={[{ id: "CLI001", name: "Client existant", status: "Actif" }]} />);
    expect(screen.getByRole("table", { name: "Clients" })).toBeTruthy();
    expect(screen.getAllByRole("columnheader")).toHaveLength(2);
    expect(screen.getByText("Actif").className).toContain("ds-badge");
  });

  it("uses a spanning empty or loading state instead of empty or fictional rows", () => {
    const view = render(<Table columns={columns} rows={[]} />);
    expect(screen.getByText("Aucune donnée").closest("td").colSpan).toBe(1);
    view.rerender(<Table columns={columns} rows={[]} loading />);
    expect(screen.getByRole("status", { name: "Chargement…" })).toBeTruthy();
  });

  it("prevents navigation beyond bounds and identifies the current page", () => {
    const change = vi.fn();
    const view = render(<Pagination page={1} totalPages={10} onPageChange={change} />);
    expect(screen.getByRole("button", { name: "Précédent" }).disabled).toBe(true);
    expect(screen.getByRole("button", { name: "1" }).getAttribute("aria-current")).toBe("page");
    fireEvent.click(screen.getByRole("button", { name: "Suivant" }));
    expect(change).toHaveBeenLastCalledWith(2);
    view.rerender(<Pagination page={10} totalPages={10} onPageChange={change} />);
    expect(screen.getByRole("button", { name: "Suivant" }).disabled).toBe(true);
    expect(screen.getByRole("button", { name: "10" }).getAttribute("aria-current")).toBe("page");
  });
});

describe("overlays", () => {
  it("keeps scrolling locked when a lower modal closes before the upper modal", () => {
    const previous = document.body.style.overflow;
    const view = render(<><Modal open onClose={() => {}} title="Premier">Parent</Modal><Modal open onClose={() => {}} title="Second">Enfant</Modal></>);
    view.rerender(<><Modal open={false} onClose={() => {}} title="Premier">Parent</Modal><Modal open onClose={() => {}} title="Second">Enfant</Modal></>);
    expect(document.body.style.overflow).toBe("hidden");
    expect(screen.getByRole("dialog", { name: "Second" }).contains(document.activeElement)).toBe(true);
    view.unmount();
    expect(document.body.style.overflow).toBe(previous);
  });

  function Dialog() {
    const [open, setOpen] = useState(false);
    return <><Button onClick={() => setOpen(true)}>Ouvrir</Button><Modal open={open} onClose={() => setOpen(false)} title="Dossier">
      <Input label="Référence" /><Button>Dernier contrôle</Button>
    </Modal></>;
  }

  it("opens a labeled dialog, traps focus, closes on Escape and restores focus and scroll", () => {
    const previous = document.body.style.overflow;
    render(<Dialog />);
    const trigger = screen.getByRole("button", { name: "Ouvrir" });
    trigger.focus();
    fireEvent.click(trigger);
    expect(screen.getByRole("dialog", { name: "Dossier" }).getAttribute("aria-modal")).toBe("true");
    expect(document.body.style.overflow).toBe("hidden");
    const close = screen.getByRole("button", { name: "Fermer la fenêtre" });
    const last = screen.getByRole("button", { name: "Dernier contrôle" });
    expect(document.activeElement).toBe(close);
    fireEvent.keyDown(close, { key: "Tab", shiftKey: true });
    expect(document.activeElement).toBe(last);
    fireEvent.keyDown(last, { key: "Tab" });
    expect(document.activeElement).toBe(close);
    fireEvent.keyDown(close, { key: "Escape" });
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(document.activeElement).toBe(trigger);
    expect(document.body.style.overflow).toBe(previous);
  });

  it("clicks inside a modal keep it open and backdrop clicks dismiss it", () => {
    render(<Dialog />);
    fireEvent.click(screen.getByRole("button", { name: "Ouvrir" }));
    fireEvent.pointerDown(screen.getByLabelText("Référence"));
    expect(screen.getByRole("dialog")).toBeTruthy();
    fireEvent.pointerDown(document.querySelector(".ds-modal-backdrop"));
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("dropdowns support arrow keys, skip disabled items and restore the trigger after selection", () => {
    const select = vi.fn();
    render(<Dropdown label="Actions" items={[{ id: "view", label: "Voir", onSelect: select }, { id: "disabled", label: "Interdit", disabled: true }, { id: "edit", label: "Modifier" }]} />);
    const trigger = screen.getByRole("button", { name: "Actions" });
    trigger.focus();
    fireEvent.keyDown(trigger, { key: "ArrowDown" });
    const first = screen.getByRole("menuitem", { name: "Voir" });
    expect(document.activeElement).toBe(first);
    fireEvent.keyDown(first, { key: "ArrowDown" });
    expect(document.activeElement).toBe(screen.getByRole("menuitem", { name: "Modifier" }));
    fireEvent.keyDown(document.activeElement, { key: "Home" });
    fireEvent.click(first);
    expect(select).toHaveBeenCalledOnce();
    expect(screen.queryByRole("menu")).toBeNull();
    expect(document.activeElement).toBe(trigger);
  });

  it("dropdowns dismiss on outside interaction and do not open when disabled", () => {
    const view = render(<Dropdown label="Actions" items={[]} />);
    fireEvent.click(screen.getByRole("button", { name: "Actions" }));
    fireEvent.pointerDown(document.body);
    expect(screen.queryByRole("menu")).toBeNull();
    view.rerender(<Dropdown label="Actions" items={[]} disabled />);
    fireEvent.click(screen.getByRole("button", { name: "Actions" }));
    expect(screen.queryByRole("menu")).toBeNull();
  });
});
