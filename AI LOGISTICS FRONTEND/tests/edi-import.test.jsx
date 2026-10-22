import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import EdiImportPage from "../src/Edi/EdiImportPage";
import { createMockEdiRepository, readEdiFile } from "../src/Edi/mockEdiRepository";
import { parseStc, stcExample } from "../src/Edi/stcParser";
const click = (name) => fireEvent.click(screen.getByRole("button", { name, exact: true }));
const patch = (text, line, start, width, value) => { const rows = text.split("\r\n"); rows[line] = rows[line].slice(0, start - 1) + String(value).padEnd(width).slice(0, width) + rows[line].slice(start - 1 + width); return rows.join("\r\n"); };
async function prepared(repo, text = stcExample()) { await repo.analyze({ text, name: "test.stc", selectionId: 1 }); await repo.stage(); }
async function ready(repository = createMockEdiRepository({ delay: 0 })) { render(<EdiImportPage repository={repository} />); await screen.findByText("Aucun fichier sélectionné"); return repository; }
async function analyze(unknown = false) { click(unknown ? "Charger un exemple à réconcilier" : "Charger un exemple valide"); click("Analyser le fichier"); await screen.findByText("Format accepté"); }
async function stage() { click("Importer en préparation"); await screen.findByText("Données en préparation"); }
async function reconcile() { click("Réconcilier les références"); await waitFor(() => expect(screen.getByRole("button", { name: "Réconcilier les références" }).disabled).toBe(false)); }
describe("Import EDI / STC", () => {
  it("parses fixed width fields, scales and special codes with BOM and trailing newline", () => {
    const result = parseStc("\uFEFF" + stcExample() + "\r\n"); expect(result.accepted).toBe(true); expect(result.anomalies).toEqual([]); expect(result.voyages[0]).toMatchObject({ reference: "DEMO0001", count: 1, departureDate: "2026-10-17" }); expect(result.dossiers[0]).toMatchObject({ packages: 10, weight: 1000, volume: 10, goodsValue: 5000, currency: "EUR", cod: "10" });
  });
  it("rejects empty, misaligned and invalidly encoded files before preparing rows", () => {
    for (const value of ["", stcExample().slice(1), patch(stcExample(), 0, 200, 1, String.fromCharCode(0))]) { const result = parseStc(value); expect(result.accepted).toBe(false); expect(result.voyages).toEqual([]); expect(result.errors.length).toBeGreaterThan(0); }
  });
  it("finds invalid dates, numbers, orphan dossiers, counts and duplicate positions", () => {
    let value = patch(stcExample(), 0, 31, 8, "20260230"); value = patch(value, 1, 525, 8, "bad"); value = patch(value, 1, 1, 8, "OTHER"); const messages = parseStc(value).anomalies.map((row) => row.message).join(" "); expect(messages).toContain("Date"); expect(messages).toContain("Poids"); expect(messages).toContain("parent absent"); expect(messages).toContain("nombre de dossiers");
    const duplicate = parseStc(stcExample() + "\r\n" + stcExample().split("\r\n")[1]); expect(duplicate.anomalies.some((row) => row.message.includes("Position de dossier en double"))).toBe(true);
  });
  it("parses invoice blocks and detects duplicate invoice numbers", () => {
    let text = stcExample(); for (const offset of [573, 599]) { text = patch(text, 1, offset, 8, "INV001"); text = patch(text, 1, offset + 8, 8, "20261017"); text = patch(text, 1, offset + 16, 10, "12345"); } const result = parseStc(text); expect(result.dossiers[0].invoices[0].amount).toBe(123.45); expect(result.anomalies.some((row) => row.message === "Facture en double.")).toBe(true);
  });
  it("reads an actual local file and rejects inappropriate extensions or sizes", async () => {
    expect(await readEdiFile(new File([stcExample()], "test.STC"))).toBe(stcExample()); expect(() => readEdiFile(new File(["x"], "test.csv"))).toThrow("STC"); expect(() => readEdiFile(new File([], "test.stc"))).toThrow("non vide"); expect(() => readEdiFile({ name: "large.stc", size: 2097153 })).toThrow("2 Mo"); const controller = new AbortController(); controller.abort(); await expect(readEdiFile(new File([stcExample()], "test.stc"), controller.signal)).rejects.toMatchObject({ name: "AbortError" });
  });
  it("requires preparation and reconciliation and commits linked records only once", async () => {
    const repo = createMockEdiRepository({ delay: 0 }); await expect(repo.promote()).rejects.toThrow("préparation"); await prepared(repo); await expect(repo.promote()).rejects.toThrow("Réconciliez"); expect((await repo.reconcile()).report.valid).toBe(true); const state = await repo.promote(); expect(state.createdVoyages).toHaveLength(1); expect(state.createdDossiers[0].voyageId).toBe(state.createdVoyages[0].id); expect(state.createdDossiers[0].clientId).toBe("CLI-DEMO-1"); await expect(repo.promote()).rejects.toThrow("déjà été intégrée"); await prepared(repo); expect((await repo.reconcile()).report.valid).toBe(false); await expect(repo.promote()).rejects.toThrow("anomalies"); expect((await repo.list()).createdVoyages).toHaveLength(1);
  });
  it("maps unknown references locally and invalidates reconciliation until rerun", async () => {
    const repo = createMockEdiRepository({ delay: 0 }); await prepared(repo, stcExample({ unknownClient: true })); expect((await repo.reconcile()).report.anomalies[0].code).toBe("INCONNU"); await expect(repo.map({ kind: "clients", code: "INCONNU", targetId: "invalid" })).rejects.toThrow("valide"); expect((await repo.map({ kind: "clients", code: "INCONNU", targetId: "CLI-DEMO-1" })).report).toBeNull(); await expect(repo.promote()).rejects.toThrow("Réconciliez"); expect((await repo.reconcile()).report.valid).toBe(true); expect((await repo.promote()).createdDossiers).toHaveLength(1);
  });
  it("never promotes structural anomalies and preserves previous preparation on rejected input", async () => {
    const repo = createMockEdiRepository({ delay: 0 }); await prepared(repo, patch(stcExample(), 0, 31, 8, "20260230")); expect((await repo.reconcile()).report.valid).toBe(false); await expect(repo.promote()).rejects.toThrow("anomalies"); const previous = (await repo.list()).stage; await repo.analyze({ text: "invalid", name: "bad.stc" }); await expect(repo.stage()).rejects.toThrow("format accepté"); expect((await repo.list()).stage).toEqual(previous); expect((await repo.list()).createdVoyages).toEqual([]);
  });
  it("cancels processing before changing repository state", async () => {
    const repo = createMockEdiRepository({ delay: 10 }), controller = new AbortController(); const pending = repo.analyze({ text: stcExample() }, { signal: controller.signal }); controller.abort(); await expect(pending).rejects.toMatchObject({ name: "AbortError" }); expect((await repo.list()).analysis).toBeNull();
  });
  it("completes the UI workflow with explicit creation confirmation", async () => {
    await ready(); expect(screen.getByRole("button", { name: "Analyser le fichier" }).disabled).toBe(true); await analyze(); expect(screen.getByRole("button", { name: "Créer les voyages et dossiers" }).disabled).toBe(true); await stage(); await reconcile(); await screen.findByText("Réconciliation valide"); click("Créer les voyages et dossiers"); fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Confirmer" })); await screen.findByRole("table", { name: "Voyages créés (démo)" }); expect(screen.getByRole("table", { name: "Dossiers créés (démo)" }).textContent).toContain("CLI-DEMO-1"); expect(screen.getByRole("button", { name: "Créer les voyages et dossiers" }).disabled).toBe(true);
  });
  it("resolves an unknown client with the existing modal and select components", async () => {
    await ready(); await analyze(true); await stage(); await reconcile(); click("Associer Client · INCONNU"); fireEvent.change(screen.getByLabelText("Référence cible"), { target: { value: "CLI-DEMO-1" } }); click("Enregistrer la correspondance"); await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull()); expect(screen.getByRole("button", { name: "Créer les voyages et dossiers" }).disabled).toBe(true); await reconcile(); await screen.findByText("Réconciliation valide");
  });
  it("clears stale selection results and confirms replacement without losing prior staging on cancel", async () => {
    const repo = await ready(); await analyze(); await stage(); click("Charger un exemple à réconcilier"); expect(screen.queryByRole("button", { name: "Créer les voyages et dossiers" })).toBeNull(); click("Analyser le fichier"); await screen.findByText("Format accepté"); click("Importer en préparation"); expect(screen.getByRole("dialog").textContent).toContain("Remplacer la préparation"); click("Annuler"); expect((await repo.list()).stage.name).toBe("exemple-valide.stc"); click("Importer en préparation"); click("Confirmer"); await screen.findByText("Données en préparation"); expect((await repo.list()).stage.name).toBe("exemple-reference-inconnue.stc");
  });
  it("analyzes an uploaded file and displays rejected-file anomalies", async () => {
    await ready(); fireEvent.change(screen.getByLabelText("Fichier EDI / STC"), { target: { files: [new File(["bad"], "test.stc")] } }); click("Analyser le fichier"); await screen.findByText("Fichier rejeté"); expect(screen.getByRole("table", { name: "Anomalies de l’import" }).textContent).toContain("3 caractères au lieu de 1 000"); expect(screen.getByRole("button", { name: "Importer en préparation" }).disabled).toBe(true);
  });
  it("handles loading, failure, retry and failed analysis without exposing internal errors", async () => {
    const repo = createMockEdiRepository({ delay: 0 }); render(<EdiImportPage repository={{ ...repo, list: vi.fn().mockRejectedValueOnce(new Error("private")).mockImplementation(repo.list), analyze: vi.fn().mockRejectedValue(new Error("private")) }} />); expect(screen.getByRole("status", { name: "Chargement de l’import EDI…" })).toBeTruthy(); await screen.findByText("Impossible de charger l’import EDI. Réessayez."); click("Réessayer"); await screen.findByText("Aucun fichier sélectionné"); click("Charger un exemple valide"); click("Analyser le fichier"); await screen.findByText("L’opération n’a pas pu aboutir. Réessayez."); expect(screen.queryByText("private")).toBeNull(); expect(screen.getByText(/exemple-valide.stc/)).toBeTruthy();
  });
});
