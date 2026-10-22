import { equipmentSeed } from "../Equipment/mockEquipmentRepository";
import { personnelSeed } from "../Personnel/mockPersonnelRepository";
export class FuelError extends Error {}
export const issueLocked = (data, issue) => issue.calculated || data.ratios.some((ratio) => ratio.equipmentId === issue.equipmentId && ratio.product === issue.product && issue.date >= ratio.from && issue.date <= ratio.to);
const guard = (condition, message) => { if (!condition) throw new FuelError(message); };
const rounded = (value, digits = 4) => Math.round((value + Number.EPSILON) * 10 ** digits) / 10 ** digits;
const numeric = (value, label, positive = false) => { const n = Number(value); guard(value !== "" && value != null && Number.isFinite(n) && (positive ? n > 0 : n >= 0) && n < 1e9, `${label} : renseignez un nombre ${positive ? "strictement positif" : "positif ou nul"} valide.`); return n; };
export function fuelSeed() {
  const equipment = equipmentSeed().equipment.filter((row) => row.status !== "Sorti").map((row) => ({ id: row.id, label: row.registration }));
  const drivers = personnelSeed().employees.filter((row) => row.functionId === "$CHAUFFEUR" && !row.departureDate).map((row) => ({ id: row.id, label: `${row.lastName} ${row.firstName}` }));
  const tanks = [{ id: "CIT-1", label: "Gasoil Casablanca Démo", product: "Gasoil", agency: "Casablanca", stock: 1200, pmp: 10 }, { id: "CIT-2", label: "Gasoil Tanger Démo", product: "Gasoil", agency: "Tanger", stock: 500, pmp: 11 }, { id: "CIT-3", label: "AdBlue Casablanca Démo", product: "AdBlue", agency: "Casablanca", stock: 300, pmp: 4 }];
  const receptions = tanks.map((tank, index) => ({ id: `REC-${index}`, tankId: tank.id, product: tank.product, quantity: index === 0 ? 1500 : tank.stock, amount: (index === 0 ? 1500 : tank.stock) * tank.pmp, date: "2026-10-01T08:00", currency: "MAD" }));
  const issues = [{ id: "SOR-1", date: "2026-10-02T08:00", quantity: 100, meter: 10000, full: true }, { id: "SOR-2", date: "2026-10-03T08:00", quantity: 50, meter: 10500, full: false }, { id: "SOR-3", date: "2026-10-04T08:00", quantity: 150, meter: 11000, full: true }].map((row) => ({ ...row, tankId: "CIT-1", equipmentId: equipment[0].id, driverId: drivers[0].id, product: "Gasoil", unitPrice: 10, amount: row.quantity * 10, currency: "MAD", cancelled: false, calculated: false, note: "Sortie interne Démo", voyage: "" }));
  return { tanks, receptions, supplies: [], vouchers: [], issues, gauges: [], ratios: [], equipment, drivers, suppliers: [{ id: "FOU-1", label: "Fournisseur carburant Démo" }] };
}
const wait = (delay, signal) => new Promise((resolve, reject) => { const abort = () => { clearTimeout(timer); reject(new DOMException("Annulé", "AbortError")); }; const timer = setTimeout(() => { signal?.removeEventListener("abort", abort); resolve(); }, delay); if (signal?.aborted) abort(); else signal?.addEventListener("abort", abort, { once: true }); });
export function fuelMovements(data) {
  return [...data.receptions.map((row) => ({ ...row, kind: "Réception", delta: row.quantity })), ...data.supplies.flatMap((row) => row.lines.map((line, index) => ({ ...row, ...line, id: `${row.id}:${index}`, kind: "Approvisionnement", delta: line.quantity }))), ...data.issues.map((row) => ({ ...row, kind: row.cancelled ? "Sortie annulée" : "Sortie", delta: row.cancelled ? 0 : -row.quantity })), ...data.vouchers.map((row) => ({ ...row, kind: "Bon fournisseur", delta: 0 }))].sort((a, b) => b.date.localeCompare(a.date));
}
export function createMockFuelRepository({ seed = fuelSeed(), delay = 200 } = {}) {
  let state = structuredClone(seed), sequence = 0;
  const snapshot = (notice) => ({ ...structuredClone(state), notice });
  return {
    async list({ signal } = {}) { await wait(delay, signal); return snapshot(); },
    async save(kind, value, { signal } = {}) {
      await wait(delay, signal); const next = structuredClone(state), id = `FUEL-NEW-${++sequence}`;
      const tank = (key) => { const row = next.tanks.find((item) => item.id === key); guard(row, "Sélectionnez une citerne valide."); return row; };
      const date = () => { guard(value.date && !Number.isNaN(Date.parse(value.date)), "La date est obligatoire."); return value.date; };
      const supplier = () => guard(next.suppliers.some((row) => row.id === value.supplierId), "Sélectionnez un fournisseur.");
      const product = () => guard(["Gasoil", "AdBlue"].includes(value.product), "Sélectionnez un produit valide.");
      const vehicle = () => { if (value.equipmentId) guard(next.equipment.some((row) => row.id === value.equipmentId), "Sélectionnez un matériel valide."); if (value.driverId) guard(next.drivers.some((row) => row.id === value.driverId), "Sélectionnez un chauffeur valide."); };
      const receive = (row, quantity, amount) => { row.pmp = rounded((row.stock * row.pmp + amount) / (row.stock + quantity)); row.stock = rounded(row.stock + quantity); guard(Number.isFinite(row.pmp) && row.stock < 1e9 && row.stock * row.pmp < 1e12, "Les montants dépassent la plage de démonstration."); };
      let notice = "Action enregistrée dans les données de démonstration.";
      if (kind === "tank") { product(); guard(value.label.trim(), "Le libellé de citerne est obligatoire."); next.tanks.unshift({ id, label: value.label.trim(), agency: value.agency.trim(), product: value.product, stock: 0, pmp: 0 }); }
      else if (kind === "reception") { const row = tank(value.tankId), quantity = numeric(value.quantity, "Quantité", true), amount = numeric(value.amount, "Montant"); date(); receive(row, quantity, amount); next.receptions.unshift({ id, tankId: row.id, product: row.product, quantity, amount, date: value.date, currency: "MAD" }); }
      else if (kind === "supply") {
        supplier(); date(); const amount = numeric(value.amount, "Montant du BL"), lines = value.lines.map((line) => ({ tankId: tank(line.tankId).id, quantity: numeric(line.quantity, "Quantité de ligne", true) })); guard(lines.length, "Ajoutez au moins une ligne de citerne."); guard(new Set(lines.map((line) => line.tankId)).size === lines.length, "Une citerne ne peut apparaître qu’une fois dans le BL."); const total = lines.reduce((sum, line) => sum + line.quantity, 0);
        const allocated = lines.map((line) => ({ ...line, product: tank(line.tankId).product, amount: amount * line.quantity / total })); allocated.forEach((line) => receive(tank(line.tankId), line.quantity, line.amount)); next.supplies.unshift({ id, supplierId: value.supplierId, reference: value.reference.trim(), date: value.date, quantity: total, amount, currency: "MAD", lines: allocated });
      } else if (kind === "issue" || kind === "voucher") {
        date(); vehicle(); const quantity = numeric(value.quantity, "Quantité", true), meter = value.meter === "" ? null : numeric(value.meter, "Compteur"); guard(!value.full || (value.equipmentId && meter != null), "Un plein nécessite un matériel et un compteur.");
        const actualProduct = kind === "issue" ? tank(value.tankId).product : value.product;
        guard(!next.ratios.some((ratio) => ratio.equipmentId === value.equipmentId && ratio.product === actualProduct && value.date <= ratio.to), "La date appartient à une période déjà calculée ; utilisez une date postérieure au dernier ratio.");
        const base = { id, date: value.date, quantity, equipmentId: value.equipmentId, driverId: value.driverId, voyage: value.voyage.trim(), full: value.full, meter, note: value.note.trim(), calculated: false, cancelled: false };
        if (kind === "issue") { const row = tank(value.tankId); guard(quantity <= row.stock, "Stock insuffisant dans la citerne."); row.stock = rounded(row.stock - quantity); next.issues.unshift({ ...base, tankId: row.id, product: row.product, unitPrice: row.pmp, amount: rounded(quantity * row.pmp, 2), currency: "MAD" }); }
        else { supplier(); product(); guard(["MAD", "EUR", "USD", "GBP"].includes(value.currency), "Sélectionnez une devise valide."); const amount = numeric(value.amount, "Montant du bon"); next.vouchers.unshift({ ...base, supplierId: value.supplierId, reference: value.reference.trim(), product: value.product, currency: value.currency, amount, unitPrice: rounded(amount / quantity) }); }
      } else if (kind === "gauge") { const row = tank(value.tankId), actual = numeric(value.quantity, "Quantité réelle"); date(); next.gauges.unshift({ id, tankId: row.id, product: row.product, date: value.date, actual, theoretical: row.stock, gap: rounded(row.stock - actual) }); }
      else if (kind === "cancel") { const row = next.issues.find((item) => item.id === value.id); guard(row, "Cette sortie n’existe plus."); guard(!issueLocked(next, row), "Cette sortie appartient à un ratio calculé ; son annulation n’est pas proposée dans cette démonstration."); if (!row.cancelled) { tank(row.tankId).stock = rounded(tank(row.tankId).stock + row.quantity); row.cancelled = true; } }
      else if (kind === "ratio") {
        product(); guard(next.equipment.some((row) => row.id === value.equipmentId), "Sélectionnez un matériel."); const rows = [...next.issues, ...next.vouchers].filter((row) => !row.cancelled && !row.calculated && row.equipmentId === value.equipmentId && row.product === value.product).sort((a, b) => a.date.localeCompare(b.date)); const fulls = rows.filter((row) => row.full && row.meter != null);
        if (fulls.length < 2) return snapshot("Ratio indisponible : deux pleins avec compteur sont nécessaires."); const [opening, closing] = fulls, window = rows.filter((row) => row.date > opening.date && row.date <= closing.date); guard(new Set(window.map((row) => row.currency)).size <= 1, "La fenêtre mélange plusieurs devises ; le ratio ne peut pas être calculé."); const litres = window.reduce((sum, row) => sum + row.quantity, 0), amount = window.reduce((sum, row) => sum + row.amount, 0), distance = closing.meter - opening.meter;
        if (distance <= 0 || litres <= 0 || amount <= 0) return snapshot("Ratio indisponible : distance, litres et montant doivent être strictement positifs."); next.ratios.unshift({ id, equipmentId: value.equipmentId, product: value.product, from: opening.date, to: closing.date, distance, litres, amount, currency: window[0].currency, consumption: rounded(litres / distance * 100), costKm: rounded(amount / distance) }); rows.filter((row) => row.date >= opening.date && row.date < closing.date).forEach((row) => { row.calculated = true; });
      } else guard(false, "Opération inconnue.");
      state = next; return snapshot(notice);
    },
  };
}
