import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import App from "../src/App";
import { createAuthStore } from "../src/Auth/authState";
import TrackingPage from "../src/Tracking/TrackingPage";
import { createMockTrackingRepository, trackingSeed } from "../src/Tracking/mockTrackingRepository";
import { trackingEntities } from "../src/Tracking/trackingRules";
async function ready(repository = createMockTrackingRepository({ delay: 0 })) { render(<TrackingPage repository={repository} />); await screen.findByRole("table", { name: "Chronologie des événements" }); return repository; }
const modal = () => screen.getByRole("dialog");
const field = (label, value) => fireEvent.change(within(modal()).getByLabelText(label, { exact: true }), { target: { value } });
const save = () => fireEvent.click(within(modal()).getByRole("button", { name: "Enregistrer l’événement", exact: true }));
const open = () => fireEvent.click(screen.getByRole("button", { name: "Ajouter un événement", exact: true }));
describe("Tracking / Suivi", () => {
  it("shows a distinct timeline and latest status for all seven entity types", async () => {
    await ready();
    for (const entity of trackingEntities) {
      fireEvent.change(screen.getByLabelText("Type d’entité", { exact: true }), { target: { value: entity.value } });
      expect(screen.getByRole("table", { name: "Chronologie des événements" }).textContent).toContain(`Suivi ${entity.label.toLowerCase()} confirmé`);
      expect(screen.getByLabelText("Auteur du dernier événement").value).toBe("Équipe logistique Démo");
      expect(screen.getByLabelText("Date du dernier événement").value).not.toBe(screen.getByLabelText("Dernier enregistrement").value);
      expect(screen.getByText("1–5 sur 7 événements")).toBeTruthy();
    }
  });
  it("paginates, changes order and displays historical system events", async () => {
    await ready(); fireEvent.click(screen.getByRole("button", { name: "Suivant", exact: true })); expect(screen.getByText("6–7 sur 7 événements")).toBeTruthy();
    expect(within(screen.getByRole("table")).getByText("Système")).toBeTruthy();
    fireEvent.change(screen.getByLabelText("Ordre des événements"), { target: { value: "old" } });
    const rows = within(screen.getByRole("table")).getAllByRole("row"); expect(rows[1].textContent).toContain("Création");
  });
  it("filters business dates without changing the latest-status summary", async () => {
    await ready(); const latest = screen.getByLabelText("Dernier enregistrement").value;
    fireEvent.change(screen.getByLabelText("Du (date de l’événement)"), { target: { value: "2026-09-01" } }); fireEvent.change(screen.getByLabelText("Au (date de l’événement)"), { target: { value: "2026-09-30" } });
    expect(screen.getByText("Aucun résultat")).toBeTruthy(); expect(screen.getByLabelText("Dernier enregistrement").value).toBe(latest);
    fireEvent.change(screen.getByLabelText("Au (date de l’événement)"), { target: { value: "2026-08-01" } }); expect(screen.getByText("La date de fin doit être postérieure ou égale à la date de début.")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Réinitialiser la période", exact: true })); expect(screen.getByText("1–5 sur 7 événements")).toBeTruthy();
  });
  it("shows an empty history and resets when switching entity type", async () => {
    await ready(); fireEvent.change(screen.getByLabelText("Référence à suivre"), { target: { value: "CLI-DEMO-2" } }); expect(screen.getByText("Aucun événement")).toBeTruthy(); expect(screen.getByLabelText("Auteur du dernier événement").value).toBe("—");
    fireEvent.change(screen.getByLabelText("Type d’entité"), { target: { value: "Dossier" } }); expect(screen.getByLabelText("Référence à suivre").value).toBe("DOS-DEMO-1"); expect(screen.getByText("1–5 sur 7 événements")).toBeTruthy();
  });
  it("only offers authorized non-system statuses and validates input", async () => {
    await ready(); open();
    const options = within(modal()).getByLabelText("Statut autorisé").options;
    expect([...options].map((option) => option.value)).toEqual(["", "NOTE", "SUIVI_CLIENT"]);
    save(); expect(within(modal()).getByText("Sélectionnez un statut autorisé.")).toBeTruthy(); field("Statut autorisé", "NOTE"); save(); expect(within(modal()).getByText("Le commentaire de suivi est obligatoire.")).toBeTruthy();
  });
  it("adds a backdated event with automatic actor and recording date, persisting on refresh", async () => {
    const repository = createMockTrackingRepository({ delay: 0, author: "Opérateur Démo", now: () => new Date("2026-10-16T10:00:00Z") }); await ready(repository); open();
    field("Statut autorisé", "NOTE"); field("Date de l’événement", "2026-10-01T08:00"); field("Commentaire de suivi", "Contact effectué"); save();
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull()); expect(screen.getByLabelText("Auteur du dernier événement").value).toBe("Opérateur Démo"); expect(screen.getByText("1–5 sur 8 événements")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Actualiser", exact: true })); await screen.findByRole("table", { name: "Chronologie des événements" });
    const event = (await repository.list()).events.at(-1); expect(event.utilisateur).toBe("Opérateur Démo"); expect(event.dateTraceSystem).toBe("2026-10-16T10:00:00.000Z"); expect(new Date(event.dateTraceUser).getTime()).toBe(new Date("2026-10-01T08:00").getTime());
  });
  it("rejects system, retired, wrong-scope statuses and forged actors at repository level", async () => {
    const repo = createMockTrackingRepository({ delay: 0, author: "Auteur automatique" }); const payload = { commentaire: "Note", dateEvenement: "2026-10-16T10:00:00Z" };
    for (const keyStatut of ["$CREATION", "ARCHIVE", "SUIVI_BOOKING", "INCONNU"]) await expect(repo.record("Client", "CLI-DEMO-1", { ...payload, keyStatut })).rejects.toThrow("statut n’est pas autorisé");
    await expect(repo.record("Client", "DOS-DEMO-1", { ...payload, keyStatut: "NOTE" })).rejects.toThrow("référence valide");
    const state = await repo.record("Client", "CLI-DEMO-1", { ...payload, keyStatut: "NOTE", utilisateur: "Auteur forgé", dateTraceSystem: "2000-01-01" }); expect(state.events.at(-1).utilisateur).toBe("Auteur automatique"); expect(state.events.at(-1).dateTraceSystem).not.toBe("2000-01-01");
  });
  it("supports read-only permissions without permitting record calls", async () => {
    const seed = trackingSeed(); seed.canRecord = false; const repo = createMockTrackingRepository({ seed, delay: 0 }); await ready(repo);
    expect(screen.getByRole("button", { name: "Ajouter un événement", exact: true }).disabled).toBe(true); expect(screen.getByText("Consultation uniquement")).toBeTruthy();
    await expect(repo.record("Client", "CLI-DEMO-1", {})).rejects.toThrow("pas autorisé");
  });
  it("shows loading and load errors with retry without exposing technical messages", async () => {
    const repo = createMockTrackingRepository({ delay: 0 }); const list = vi.fn().mockRejectedValueOnce(new Error("private")).mockImplementation(repo.list); render(<TrackingPage repository={{ ...repo, list }} />);
    expect(screen.getByRole("status", { name: "Chargement du suivi…" })).toBeTruthy(); await screen.findByText("Impossible de charger le suivi. Réessayez."); expect(screen.queryByText("private")).toBeNull(); fireEvent.click(screen.getByRole("button", { name: "Réessayer" })); await screen.findByRole("table", { name: "Chronologie des événements" });
  });
  it("retains the form and its values after a failed mutation", async () => {
    const repo = createMockTrackingRepository({ delay: 0 }); await ready({ ...repo, record: vi.fn().mockRejectedValue(new Error("private")) }); open(); field("Statut autorisé", "NOTE"); field("Commentaire de suivi", "À conserver"); save(); await within(modal()).findByText("L’opération n’a pas pu aboutir. Réessayez."); expect(within(modal()).getByLabelText("Commentaire de suivi").value).toBe("À conserver");
  });
  it("protects the tracking route and returns to it after login", async () => {
    vi.stubGlobal("matchMedia", vi.fn(() => ({ matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() }))); window.history.replaceState(null, "", "#tracking");
    const store = createAuthStore({ tokenSink: vi.fn(), service: { signIn: async () => ({ token: "test-only", expiresAt: Date.now() + 60000, user: { displayName: "Compte Démo" } }) } }); const view = render(<App authStore={store} />);
    try { await screen.findByRole("heading", { name: "Connexion" }); await act(async () => { await store.login({}); }); await screen.findByRole("heading", { name: "Tracking / Suivi", exact: true }); expect(window.location.hash).toBe("#tracking"); }
    finally { view.unmount(); store.logout(); vi.unstubAllGlobals(); window.history.replaceState(null, "", "/"); }
  });
});
