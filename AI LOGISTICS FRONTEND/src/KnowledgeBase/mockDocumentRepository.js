import { tokens } from "../design-system/tokens";
import { fileType } from "./documentTypes";

export const mockDocuments = [
  ["Guide des expéditions.pdf", "PDF", 245760, "indexed"],
  ["Procédure de réservation.docx", "DOCX", 86016, "indexed"],
  ["FAQ du suivi.md", "MD", 6144, "indexed"],
  ["Documents de facturation.pdf", "PDF", 512000, "processing"],
  ["Consignes de livraison.txt", "TXT", 4096, "indexed"],
  ["Référentiel des ports.csv", "CSV", 14336, "indexed"],
  ["Guide des réclamations.pdf", "PDF", 327680, "failed"],
  ["Procédures douanières.docx", "DOCX", 126976, "indexed"],
  ["Glossaire logistique.md", "MD", 9216, "indexed"],
  ["Manuel des bookings.pdf", "PDF", 1048576, "processing"],
  ["Politique d’archivage.txt", "TXT", 3072, "indexed"],
  ["Annexe documentaire.pdf", "PDF", 180224, "failed"],
].map(([name, type, size, status], index) => ({ id: `demo-${index + 1}`, name, type, size, status,
  date: `2026-10-${String(14 - index % 5).padStart(2, "0")}T12:00:00Z` }));

function delay(signal) {
  return new Promise((resolve, reject) => {
    if (signal?.aborted) { reject(new DOMException("Aborted", "AbortError")); return; }
    const abort = () => { clearTimeout(timer); reject(new DOMException("Aborted", "AbortError")); };
    const timer = setTimeout(() => { signal?.removeEventListener("abort", abort); resolve(); }, Number.parseInt(tokens.motion.layout, 10));
    signal?.addEventListener("abort", abort, { once: true });
  });
}

export function createMockDocumentRepository({ documents = mockDocuments, failListOnce = false } = {}) {
  let rows = documents.map((row) => ({ ...row }));
  let sequence = 0;
  return {
    async list({ signal } = {}) {
      await delay(signal);
      if (failListOnce) { failListOnce = false; throw new Error("Simulated load failure"); }
      return rows.map((row) => ({ ...row }));
    },
    async upload(files, { signal } = {}) {
      await delay(signal);
      // Only metadata is read. No FileReader, upload, parsing or real RAG indexing.
      const added = files.map((file) => ({ id: `local-${Date.now()}-${++sequence}`, name: file.name,
        type: fileType(file.name), size: file.size, date: new Date().toISOString(), status: "indexed" }));
      rows = [...added, ...rows];
      return added;
    },
    async remove(id, { signal } = {}) { await delay(signal); rows = rows.filter((row) => row.id !== id); },
  };
}
