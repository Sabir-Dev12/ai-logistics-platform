const field = (line, start, width) => line.slice(start - 1, start - 1 + width).trim();
const number = (value) => value && /^[-+]?\d+(?:[.,]\d+)?$/.test(value) && Number.isFinite(Number(value.replace(",", "."))) ? Number(value.replace(",", ".")) : null;
const date = (value) => { if (!/^\d{8}$/.test(value) || value === "00000000") return null; const text = `${value.slice(0, 4)}-${value.slice(4, 6)}-${value.slice(6, 8)}`; const parsed = new Date(`${text}T00:00:00Z`); return !Number.isNaN(parsed.valueOf()) && parsed.toISOString().slice(0, 10) === text ? text : null; };
export function parseStc(text) {
  const lines = text.replace(/^\uFEFF/, "").split(/\r\n|\n|\r/); if (lines.at(-1) === "") lines.pop();
  const errors = [], voyages = [], dossiers = [], anomalies = [];
  if (!lines.length || lines.every((line) => !line.trim())) errors.push({ id: "empty", line: 0, message: "Le fichier est vide." });
  if (lines.length > 2000) errors.push({ id: "limit", line: 0, message: "La démonstration accepte au maximum 2 000 lignes." });
  lines.forEach((line, index) => { if (line.length !== 1000) errors.push({ id: `length-${index}`, line: index + 1, message: `${line.length} caractères au lieu de 1 000.` }); if (Array.from(line).some((char) => char.charCodeAt(0) < 32 || char.charCodeAt(0) === 65533)) errors.push({ id: `encoding-${index}`, line: index + 1, message: "Caractères non reconnus : vérifiez l’encodage UTF-8." }); });
  if (errors.length) return { accepted: false, errors, voyages, dossiers, anomalies, lineCount: lines.length };
  const add = (row, message) => anomalies.push({ id: `A-${anomalies.length}`, line: row.line, type: row.type, reference: row.reference, message });
  lines.forEach((line, index) => {
    const reference = field(line, 1, 8), position = field(line, 9, 2);
    if (position === "00") {
      const row = { id: `V-${index}`, line: index + 1, type: "Voyage", reference, departurePort: field(line, 11, 20), departureDate: date(field(line, 31, 8)), arrivalPort: field(line, 39, 20), arrivalDate: date(field(line, 59, 8)), ship: field(line, 67, 20), vehicle: field(line, 87, 20), container: field(line, 107, 20), count: number(field(line, 127, 2)), nature: field(line, 129, 1).toUpperCase() };
      voyages.push(row); if (!reference) add(row, "N° voyage invalide."); if (!row.departurePort) add(row, "Port départ invalide."); if (!row.arrivalPort) add(row, "Port arrivée invalide."); if (!row.ship) add(row, "Navire invalide."); if (!row.departureDate || !row.arrivalDate) add(row, "Date de départ ou d’arrivée invalide."); if (row.departureDate && row.arrivalDate && row.arrivalDate < row.departureDate) add(row, "Date d’arrivée inférieure au départ."); if (!["C", "G"].includes(row.nature)) add(row, "Nature voyage invalide (C ou G)."); if (!row.vehicle && !row.container) add(row, "Remorque ou conteneur obligatoire."); if (!Number.isInteger(row.count) || row.count < 1) add(row, "Nombre de dossiers invalide.");
    } else {
      const row = { id: `D-${index}`, line: index + 1, type: "Dossier", reference: `${reference}-${position}`, voyage: reference, position: number(position), client: field(line, 179, 8), clientName: field(line, 187, 30), goods: field(line, 437, 40), packaging: field(line, 477, 40) || "COLIS", packages: number(field(line, 517, 8)), weight: number(field(line, 525, 8)), volume: number(field(line, 533, 6)), goodsValue: number(field(line, 549, 8)), incoterm: field(line, 557, 2), cod: field(line, 559, 2), assigned: number(field(line, 561, 8)), currency: field(line, 569, 2) === "00" ? "EUR" : field(line, 569, 2), invoices: [] };
      dossiers.push(row); if (!reference) add(row, "Voyage invalide."); if (!Number.isInteger(row.position) || row.position < 1) add(row, "Position invalide.");
      for (const [key, label] of [["packages", "Colis"], ["weight", "Poids"], ["volume", "Volume"], ["goodsValue", "Valeur marchandise"], ["assigned", "Montant assigné"]]) if (row[key] == null || row[key] < 0) add(row, `${label} invalide.`);
      row.weight = row.weight == null ? null : row.weight / 1000; row.volume = row.volume == null ? null : row.volume / 1000; row.goodsValue = row.goodsValue == null ? null : row.goodsValue / 100; row.assigned = row.assigned == null ? null : row.assigned / 100;
      for (let i = 0; i < 5; i++) { const start = 573 + i * 26, num = field(line, start, 8), rawDate = field(line, start + 8, 8), rawAmount = field(line, start + 16, 10); if (num || rawDate || rawAmount) { const d = date(rawDate), amount = number(rawAmount); if (!d || amount == null) add(row, `Date ou montant de facture ${i + 1} invalide.`); row.invoices.push({ number: num, date: d, amount: amount == null ? null : amount / 100 }); } }
      const nums = row.invoices.filter((invoice) => invoice.number).map((invoice) => invoice.number.toLowerCase()); if (new Set(nums).size !== nums.length) add(row, "Facture en double.");
    }
  });
  if (!voyages.length) anomalies.push({ id: "no-header", line: 0, type: "Fichier", reference: "", message: "Aucune entête de voyage." });
  for (const row of voyages) { if (voyages.filter((v) => v.reference.toLowerCase() === row.reference.toLowerCase()).length > 1) add(row, "N° voyage en double."); if (dossiers.filter((d) => d.voyage === row.reference).length !== row.count) add(row, "Le nombre de dossiers ne correspond pas à l’entête."); }
  for (const row of dossiers) { if (!voyages.some((v) => v.reference === row.voyage)) add(row, "Voyage parent absent du fichier."); if (dossiers.filter((d) => d.voyage === row.voyage && d.position === row.position).length > 1) add(row, "Position de dossier en double."); }
  return { accepted: true, lineCount: lines.length, errors, voyages, dossiers, anomalies };
}
export function stcExample({ unknownClient = false } = {}) {
  const record = (fields) => { const chars = Array(1000).fill(" "); fields.forEach(([start, width, value]) => String(value).slice(0, width).split("").forEach((char, index) => { chars[start - 1 + index] = char; })); return chars.join(""); };
  return [record([[1, 8, "DEMO0001"], [9, 2, "00"], [11, 20, "CASABLANCA"], [31, 8, "20261017"], [39, 20, "TANGER"], [59, 8, "20261018"], [67, 20, "NAVIRE DEMO"], [87, 20, "REMORQUE DEMO"], [127, 2, "01"], [129, 1, "G"]]), record([[1, 8, "DEMO0001"], [9, 2, "01"], [179, 8, unknownClient ? "INCONNU" : "CLI001"], [187, 30, "Client de demonstration"], [437, 40, "Marchandise demo HS 1234"], [517, 8, "10"], [525, 8, "1000000"], [533, 6, "10000"], [549, 8, "500000"], [557, 2, "DA"], [559, 2, "10"], [561, 8, "0"], [569, 2, "00"]])].join("\r\n");
}
