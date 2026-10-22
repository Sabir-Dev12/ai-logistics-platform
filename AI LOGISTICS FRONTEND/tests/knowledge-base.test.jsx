import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import KnowledgeBasePage from "../src/KnowledgeBase/KnowledgeBasePage";
import { createMockDocumentRepository } from "../src/KnowledgeBase/mockDocumentRepository";

beforeEach(() => vi.useFakeTimers());
afterEach(() => { vi.useRealTimers(); vi.unstubAllGlobals(); vi.restoreAllMocks(); });
const settle = async () => { await act(async () => { await vi.advanceTimersByTimeAsync(250); }); };
const openUpload = () => fireEvent.click(screen.getByRole("button", { name: "Ajouter des documents" }));

describe("mock knowledge base", () => {
  it("loads document metadata with the existing table and paginates the fixtures", async () => {
    render(<KnowledgeBasePage />);
    expect(screen.getByRole("status", { name: "Chargement…" })).toBeTruthy();
    await settle();
    expect(screen.getByRole("table", { name: "Documents de la Knowledge Base" })).toBeTruthy();
    expect(screen.getAllByRole("columnheader")).toHaveLength(6);
    expect(screen.getByText("Guide des expéditions.pdf")).toBeTruthy();
    expect(screen.getByText("240 Ko")).toBeTruthy();
    expect(screen.getAllByText("14/10/2026").length).toBeGreaterThan(0);
    expect(screen.getByText("1–5 sur 12 documents")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Suivant" }));
    expect(screen.getByText("Référentiel des ports.csv")).toBeTruthy();
    expect(screen.queryByText("Guide des expéditions.pdf")).toBeNull();
    fireEvent.change(screen.getByRole("combobox", { name: "Documents par page" }), { target: { value: "20" } });
    expect(screen.getByText("1–12 sur 12 documents")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Suivant" }).disabled).toBe(true);
  });

  it("combines search and filters, ignores accents and resets pagination when filtering", async () => {
    render(<KnowledgeBasePage />); await settle();
    fireEvent.click(screen.getByRole("button", { name: "Suivant" }));
    fireEvent.change(screen.getByRole("textbox", { name: "Rechercher un document" }), { target: { value: "EXPEDITIONS" } });
    expect(screen.getByText("Guide des expéditions.pdf")).toBeTruthy();
    expect(screen.getByText("1–1 sur 1 document")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Réinitialiser", exact: true }));
    fireEvent.change(screen.getByRole("combobox", { name: "Type", exact: true }), { target: { value: "PDF" } });
    fireEvent.change(screen.getByRole("combobox", { name: "Statut", exact: true }), { target: { value: "failed" } });
    expect(screen.getByText("Guide des réclamations.pdf")).toBeTruthy();
    expect(screen.getByText("Annexe documentaire.pdf")).toBeTruthy();
    expect(screen.getByText("1–2 sur 2 documents")).toBeTruthy();
    expect(screen.queryByText("Guide des expéditions.pdf")).toBeNull();
  });

  it("distinguishes no results from an empty collection", async () => {
    const view = render(<KnowledgeBasePage />); await settle();
    fireEvent.change(screen.getByRole("textbox"), { target: { value: "aucun-nom" } });
    expect(screen.getByText("Aucun résultat")).toBeTruthy();
    expect(screen.queryByRole("navigation", { name: "Pagination" })).toBeNull();
    view.unmount();
    render(<KnowledgeBasePage repository={createMockDocumentRepository({ documents: [] })} />); await settle();
    expect(screen.getByText("Aucun document")).toBeTruthy();
    expect(screen.queryByText("Guide des expéditions.pdf")).toBeNull();
  });

  it("adds multiple local files as metadata without reading content or calling fetch", async () => {
    const fetch = vi.fn(); vi.stubGlobal("fetch", fetch);
    const read = vi.spyOn(FileReader.prototype, "readAsText");
    render(<KnowledgeBasePage />); await settle(); openUpload();
    fireEvent.change(screen.getByLabelText("Fichiers à ajouter"), { target: { files: [new File(["demo"], "Plan.pdf"), new File(["note"], "Note.md")] } });
    fireEvent.click(screen.getByRole("button", { name: "Ajouter", exact: true }));
    await settle();
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(screen.getByText("Plan.pdf")).toBeTruthy();
    expect(screen.getByText("Note.md")).toBeTruthy();
    expect(screen.getByText("1–5 sur 14 documents")).toBeTruthy();
    expect(read).not.toHaveBeenCalled(); expect(fetch).not.toHaveBeenCalled();
    read.mockRestore(); vi.unstubAllGlobals();
  });

  it("rejects unsupported or empty files without submitting the upload", async () => {
    const repository = createMockDocumentRepository();
    const upload = vi.spyOn(repository, "upload");
    render(<KnowledgeBasePage repository={repository} />); await settle(); openUpload();
    fireEvent.change(screen.getByLabelText("Fichiers à ajouter"), { target: { files: [new File(["x"], "Program.exe")] } });
    expect(screen.getByRole("alert")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Ajouter", exact: true }).disabled).toBe(true);
    fireEvent.change(screen.getByLabelText("Fichiers à ajouter"), { target: { files: [new File([], "Empty.txt")] } });
    expect(screen.getByRole("button", { name: "Ajouter", exact: true }).disabled).toBe(true);
    expect(upload).not.toHaveBeenCalled();
  });

  it("deletes only after confirmation and leaves the document intact after cancellation", async () => {
    render(<KnowledgeBasePage />); await settle();
    const open = () => fireEvent.click(screen.getByRole("button", { name: "Supprimer Guide des expéditions.pdf" }));
    open(); fireEvent.click(screen.getByRole("button", { name: "Annuler" }));
    expect(screen.getByText("Guide des expéditions.pdf")).toBeTruthy();
    open(); fireEvent.click(screen.getByRole("button", { name: "Confirmer la suppression" })); await settle();
    expect(screen.queryByText("Guide des expéditions.pdf")).toBeNull();
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(screen.getByText("1–5 sur 11 documents")).toBeTruthy();
  });

  it("recovers from a load failure through the existing error state", async () => {
    render(<KnowledgeBasePage repository={createMockDocumentRepository({ failListOnce: true })} />); await settle();
    expect(screen.getByRole("alert")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Réessayer" })); await settle();
    expect(screen.queryByRole("alert")).toBeNull();
    expect(screen.getByText("Guide des expéditions.pdf")).toBeTruthy();
  });

  it("keeps a failed deletion available for retry without exposing exception details", async () => {
    const repository = createMockDocumentRepository();
    const original = repository.remove;
    repository.remove = vi.fn().mockRejectedValueOnce(new Error("private-detail")).mockImplementation(original);
    render(<KnowledgeBasePage repository={repository} />); await settle();
    fireEvent.click(screen.getByRole("button", { name: "Supprimer Guide des expéditions.pdf" }));
    fireEvent.click(screen.getByRole("button", { name: "Confirmer la suppression" })); await settle();
    expect(screen.getByRole("dialog")).toBeTruthy();
    expect(screen.getByRole("alert")).toBeTruthy();
    expect(screen.queryByText("private-detail")).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Confirmer la suppression" })); await settle();
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(screen.queryByText("Guide des expéditions.pdf")).toBeNull();
  });
});
