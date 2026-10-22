export const entityTypes = [
  { value: "shipment", label: "Expédition" }, { value: "booking", label: "Réservation" },
  { value: "voyage", label: "Voyage" }, { value: "client", label: "Client" },
  { value: "payment", label: "Règlement" }, { value: "invoice", label: "Facture" },
  { value: "quotation", label: "Cotation" },
];
export const activityStatuses = {
  pending: { label: "En attente", variant: "info" },
  active: { label: "En cours", variant: "warning" },
  complete: { label: "Terminée", variant: "success" },
  attention: { label: "À vérifier", variant: "error" },
  unknown: { label: "Non catégorisé", variant: "info" },
};
export const periods = [{ value: "7", label: "7 derniers jours" }, { value: "30", label: "30 derniers jours" },
  { value: "90", label: "90 derniers jours" }, { value: "all", label: "Toutes les périodes" }];

const calendarDay = (date) => new Date(date.getFullYear(), date.getMonth(), date.getDate());
const dayKey = (date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;

export function createMockActivities(now = new Date()) {
  const labels = ["Dossier pris en charge", "Réservation confirmée", "Voyage préparé", "Fiche client mise à jour", "Règlement actualisé", "Facture contrôlée", "Cotation créée"];
  const offsets = [0, 2, 6, 8, 12, 14, 18, 22, 29, 35, 48, 65];
  return offsets.flatMap((offset, index) => {
    const date = calendarDay(now); date.setDate(date.getDate() - offset);
    const entityType = entityTypes[index % entityTypes.length].value;
    const entityId = `${["EXP", "BKG", "VOY", "CLI", "REG", "FAC", "COT"][index % entityTypes.length]}-${String(index + 1).padStart(3, "0")}`;
    const status = ["active", "complete", "pending", "complete", "attention", "complete"][index % 6];
    const previous = new Date(date); previous.setDate(previous.getDate() - 1);
    return [
      { id: `event-${index}-new`, entityType, entityId, date: date.toISOString(), status, description: labels[index % labels.length] },
      { id: `event-${index}-old`, entityType, entityId, date: previous.toISOString(), status: "pending", description: "Dossier enregistré" },
    ];
  });
}

export function selectDashboard(activities, { period, entityType, now }) {
  const cutoff = calendarDay(now);
  if (period !== "all") cutoff.setDate(cutoff.getDate() - (Number(period) - 1));
  const filtered = activities.filter((event) => (!entityType || event.entityType === entityType)
    && new Date(event.date) <= now && (period === "all" || new Date(event.date) >= cutoff))
    .sort((left, right) => new Date(right.date) - new Date(left.date) || left.id.localeCompare(right.id));
  const latest = new Map();
  const days = new Map();
  for (const event of filtered) {
    const key = `${event.entityType}:${event.entityId}`;
    if (!latest.has(key)) latest.set(key, event);
    const date = new Date(event.date), day = dayKey(date);
    if (!days.has(day)) days.set(day, { name: new Intl.DateTimeFormat("fr-FR", { day: "2-digit", month: "2-digit", year: "2-digit" }).format(date), uv: 0, pv: 0, amt: 0 });
    const series = event.entityType === "shipment" ? "uv" : event.entityType === "booking" ? "pv" : "amt";
    days.get(day)[series]++;
  }
  const entities = [...latest.values()];
  const summaries = entityTypes.filter((type) => !entityType || type.value === entityType).map((type) => {
    const rows = entities.filter((event) => event.entityType === type.value);
    return { ...type, total: rows.length, ...Object.fromEntries(Object.keys(activityStatuses).map((status) => [status, rows.filter((event) => event.status === status).length])) };
  });
  const hasUnknownStatus = entities.some((event) => !activityStatuses[event.status] || event.status === "unknown");
  return { activities: filtered, entities: entities.length, active: hasUnknownStatus ? null : entities.filter((event) => event.status === "active").length,
    attention: hasUnknownStatus ? null : entities.filter((event) => event.status === "attention").length, summaries,
    chartData: [...days].sort(([left], [right]) => left.localeCompare(right)).map(([, value]) => value) };
}

export function createMockDashboardRepository({ now = () => new Date(), delay = 350 } = {}) {
  return { list: ({ signal } = {}) => new Promise((resolve, reject) => {
    const abort = () => { clearTimeout(timer); reject(new DOMException("Chargement annulé", "AbortError")); };
    const timer = setTimeout(() => { signal?.removeEventListener("abort", abort); resolve(createMockActivities(now())); }, delay);
    if (signal?.aborted) abort(); else signal?.addEventListener("abort", abort, { once: true });
  }) };
}
