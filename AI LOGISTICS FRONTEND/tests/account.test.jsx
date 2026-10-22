import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import AccountPage from "../src/Account/AccountPage";
import ConnectedAccountPage from "../src/Account/ConnectedAccountPage";
import AuthProvider from "../src/Auth/AuthProvider";
import { createAuthStore } from "../src/Auth/authState";
import { AccountError } from "../src/services/accountService";
import { createAuthenticationService } from "../src/services/authenticationService";
const profile = { displayName: "Compte Test", identifier: "P001", companyName: "Logistics Test" };
const repository = (extra = {}) => ({ getProfile: vi.fn().mockResolvedValue(profile), changePassword: vi.fn().mockResolvedValue(), logout: vi.fn().mockResolvedValue(), ...extra });
const fill = (oldPassword = "AncienTest123!", newPassword = "NouveauTest123!", confirmation = newPassword) => {
  fireEvent.change(screen.getByLabelText("Ancien mot de passe", { exact: true }), { target: { value: oldPassword } });
  fireEvent.change(screen.getByLabelText("Nouveau mot de passe", { exact: true }), { target: { value: newPassword } });
  fireEvent.change(screen.getByLabelText("Confirmer le nouveau mot de passe", { exact: true }), { target: { value: confirmation } });
};
const submit = () => fireEvent.click(screen.getByRole("button", { name: "Modifier le mot de passe", exact: true }));
const ready = async (repo = repository()) => { render(<AccountPage repository={repo} />); await screen.findByLabelText("Nom affiché"); return repo; };
afterEach(() => vi.restoreAllMocks());
describe("connected account presentation", () => {
  it("loads read-only identity without displaying demo passwords or a company key", async () => {
    const storage = vi.spyOn(Storage.prototype, "setItem"); render(<AccountPage repository={repository()} />); expect(screen.getByRole("status", { name: "Chargement du compte…" })).toBeTruthy(); await screen.findByLabelText("Nom affiché"); expect(screen.getByLabelText("Nom affiché").value).toBe("Compte Test"); expect(screen.getByLabelText("Société", { exact: true }).readOnly).toBe(true); expect(screen.queryByText(/Valeur initiale/)).toBeNull(); expect(screen.queryByText("Référence société")).toBeNull(); expect(storage).not.toHaveBeenCalled();
  });
  it("shows empty identity and retries a loading failure", async () => {
    const view = render(<AccountPage repository={repository({ getProfile: vi.fn().mockResolvedValue(null) })} />); await screen.findByText("Aucune identité disponible"); expect(screen.queryByLabelText("Ancien mot de passe")).toBeNull(); view.unmount(); const repo = repository({ getProfile: vi.fn().mockRejectedValueOnce(new AccountError("network", "Connexion indisponible.")).mockResolvedValue(profile) }); render(<AccountPage repository={repo} />); await screen.findByText("Connexion indisponible."); fireEvent.click(screen.getByRole("button", { name: "Réessayer" })); await screen.findByLabelText("Nom affiché"); expect(repo.getProfile).toHaveBeenCalledTimes(2);
  });
  it("preserves identity and offers retry if company details are unavailable", async () => {
    const repo = await ready(repository({ getProfile: vi.fn().mockResolvedValueOnce({ ...profile, companyName: "Non disponible", companyWarning: "Société indisponible." }).mockResolvedValue(profile) })); expect(screen.getByLabelText("Nom affiché").value).toBe("Compte Test"); fireEvent.click(screen.getByRole("button", { name: "Réessayer" })); await waitFor(() => expect(screen.getByLabelText("Société", { exact: true }).value).toBe("Logistics Test")); expect(repo.getProfile).toHaveBeenCalledTimes(2);
  });
  it("validates length and confirmation before sending any password", async () => {
    const repo = await ready(); fill("AncienTest123!", "court", "différent"); submit(); expect(screen.getByText("Utilisez entre 8 et 200 caractères.")).toBeTruthy(); expect(screen.getByText("Les mots de passe ne correspondent pas.")).toBeTruthy(); expect(repo.changePassword).not.toHaveBeenCalled();
  });
  it("disables duplicate changes, clears password fields and confirms a successful 204", async () => {
    let done; const changePassword = vi.fn(() => new Promise((resolve) => { done = resolve; })); await ready(repository({ changePassword })); fill(); submit(); expect(screen.getByLabelText("Ancien mot de passe").value).toBe(""); expect(screen.getByLabelText("Nouveau mot de passe", { exact: true }).value).toBe(""); expect(screen.getByRole("button", { name: /Modifier le mot de passe/ }).disabled).toBe(true); expect(changePassword.mock.calls[0][0]).toEqual({ oldPassword: "AncienTest123!", newPassword: "NouveauTest123!" }); await act(async () => done()); await screen.findByText("Mot de passe modifié"); expect(changePassword).toHaveBeenCalledTimes(1);
  });
  it("shows a wrong old password inline and permits a fresh attempt", async () => {
    const repo = await ready(repository({ changePassword: vi.fn().mockRejectedValueOnce(new AccountError("old_password_invalid", "L’ancien mot de passe est incorrect.")).mockResolvedValue() })); fill(); submit(); await screen.findByText("L’ancien mot de passe est incorrect."); expect(screen.getByLabelText("Ancien mot de passe").getAttribute("aria-invalid")).toBe("true"); fill(); submit(); await screen.findByText("Mot de passe modifié"); expect(repo.changePassword).toHaveBeenCalledTimes(2);
  });
  it.each([["validation", "Mot de passe refusé."], ["network", "Réseau indisponible."], ["not_found", "Compte introuvable."], ["timeout", "Délai dépassé."]])("displays %s without losing identity or claiming success", async (code, message) => {
    await ready(repository({ changePassword: vi.fn().mockRejectedValue(new AccountError(code, message)) })); fill(); submit(); await screen.findByText(message); expect(screen.queryByText("Mot de passe modifié")).toBeNull(); expect(screen.getByLabelText("Nom affiché").value).toBe("Compte Test");
  });
  it("cancels then confirms real-session logout through the supplied service", async () => {
    const repo = await ready(); fireEvent.click(screen.getByRole("button", { name: "Se déconnecter", exact: true })); fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Annuler" })); expect(repo.logout).not.toHaveBeenCalled(); fireEvent.click(screen.getByRole("button", { name: "Se déconnecter", exact: true })); fireEvent.click(screen.getByRole("button", { name: "Confirmer la déconnexion" })); await screen.findByText("Déconnecté"); expect(repo.logout).toHaveBeenCalledTimes(1); expect(screen.getByRole("link", { name: "Revenir à la connexion" }).getAttribute("href")).toBe("#login");
  });
  it("aborts loading and password requests on unmount", async () => {
    let signal; const view = render(<AccountPage repository={repository({ getProfile: ({ signal: value }) => { signal = value; return new Promise(() => {}); } })} />); await act(async () => {}); view.unmount(); expect(signal.aborted).toBe(true); const repo = repository({ changePassword: (_, options) => { signal = options.signal; return new Promise(() => {}); } }); const mutation = render(<AccountPage repository={repo} />); await screen.findByLabelText("Nom affiché"); fill(); submit(); mutation.unmount(); expect(signal.aborted).toBe(true);
  });
  it("uses the authentication provider for bearer calls and clears its real token on logout", async () => {
    const fetcher = vi.fn().mockResolvedValueOnce(new Response(JSON.stringify({ societeKey: "S01", userKey: "P001", userDisplayName: "Compte Test" }))).mockResolvedValueOnce(new Response(JSON.stringify({ keySoc: "S01", intituleSoc: "Logistics Test" })));
    const http = createAuthenticationService({ fetcher }); const store = createAuthStore({ service: { signIn: async () => ({ token: "unit-test-bearer", expiresAt: Date.now() + 60000, user: { displayName: "Compte Test" } }), authorizedRequest: http.authorizedRequest }, tokenSink: vi.fn() }); await store.login({}); const view = render(<AuthProvider store={store}><ConnectedAccountPage /></AuthProvider>);
    try { await screen.findByLabelText("Nom affiché"); expect(fetcher.mock.calls[0][1].headers.get("Authorization")).toBe("Bearer unit-test-bearer"); fireEvent.click(screen.getByRole("button", { name: "Se déconnecter", exact: true })); fireEvent.click(screen.getByRole("button", { name: "Confirmer la déconnexion" })); await waitFor(() => expect(store.getAccessToken()).toBeNull()); } finally { view.unmount(); store.logout(); }
  });
});

