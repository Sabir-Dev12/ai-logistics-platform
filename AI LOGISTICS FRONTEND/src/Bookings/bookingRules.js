export const bookingStatuses = {
  $ATRM: { label: "En attente de rattachement", variant: "info" },
  $BOCV: { label: "Convertie en dossier", variant: "success" },
  $BOKC: { label: "Dupliquée", variant: "info" },
  $BOKAN: { label: "Annulée", variant: "warning" },
};
export const bookingTypes = [{ value: "RI", label: "Import routier" }, { value: "RE", label: "Export routier" }];
export const normalizeBookingSearch = (value) => String(value ?? "").normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLowerCase().trim();
export const bookingMessages = {
  missing: "La réservation ou la référence sélectionnée n’est plus disponible.", duplicate: "Ce numéro de réservation est déjà utilisé.",
  required: "Le numéro et le type de réservation sont obligatoires.", validity: "La validité ne peut pas être antérieure à la création.",
  port: "Le port de départ est obligatoire lorsqu’une date de départ est prévue.",
  origin: "Une réservation à l’import ne peut pas avoir MAROC comme pays expéditeur.",
  destination: "Une réservation à l’export ne peut pas avoir MAROC comme pays destinataire.",
  availability: "La disponibilité de la marchandise ne peut pas dépasser le départ prévu.",
  equipment: "Une réservation complète doit comporter au moins une ligne d’équipement.",
  packages: "Une réservation de groupage doit comporter un nombre de colis supérieur à zéro.",
  linked: "Désaffectez la réservation de son voyage avant de l’annuler ou de la supprimer.",
  cancelled: "Cette réservation est annulée.", voyage: "Cette réservation est déjà affectée à un voyage.",
  capacity: "La capacité restante du voyage est insuffisante.", dossier: "La réservation ou le dossier est déjà rattaché à un autre élément.",
  unloaded: "Les lignes d’une réservation dépotée ne peuvent plus être modifiées.",
  already_unloaded: "Cette réservation a déjà été dépotée.", not_unloaded: "Cette réservation n’est pas dépotée.",
  number: "Renseignez des nombres finis et non négatifs.", date: "Renseignez une date valide.",
  unavailable: "L’opération n’a pas pu aboutir. Réessayez.",
};
export class BookingRuleError extends Error { constructor(code) { super(bookingMessages[code]); this.code = code; } }
export const bookingGuard = (test, code) => { if (!test) throw new BookingRuleError(code); };
export const linkedVoyage = (booking) => booking.keyVoyagePrevisionnel || booking.keyVoyageEnlevement;
export function validateBooking(booking) {
  bookingGuard(!booking.dateValidite || booking.dateValidite.slice(0, 10) >= booking.dateCreation.slice(0, 10), "validity");
  bookingGuard(!booking.dateDepartPrevue || Boolean(booking.portDepart?.trim()), "port");
  bookingGuard(booking.typeBooking?.[1] !== "I" || booking.paysExpediteur?.trim().toUpperCase() !== "MAROC", "origin");
  bookingGuard(booking.typeBooking?.[1] !== "E" || booking.paysDestinataire?.trim().toUpperCase() !== "MAROC", "destination");
  bookingGuard(!booking.dateDisponibiliteMarchandise || !booking.dateDepartPrevue || booking.dateDisponibiliteMarchandise <= booking.dateDepartPrevue, "availability");
  bookingGuard(booking.typeChargement !== "Complet" || booking.equipements.length > 0, "equipment");
  bookingGuard(!booking.typeChargement?.startsWith("Groupage") || Number(booking.nbTotalColis) > 0, "packages");
}
export function voyageCapacity(voyage, bookings) {
  const rows = bookings.filter((row) => !row.supprime && linkedVoyage(row) === voyage.keyVoyage);
  const volumeReserve = rows.reduce((sum, row) => sum + Number(row.volumePrevision || 0), 0);
  return { ...voyage, volumeReserve, libre: voyage.capaciteRemorque - volumeReserve, nbBookings: rows.length,
    tauxOccupation: voyage.capaciteRemorque > 0 ? Math.round(volumeReserve / voyage.capaciteRemorque * 10000) / 100 : 0 };
}
export const checkNumbers = (values) => {
  for (const value of values) bookingGuard(value == null || value === "" || (Number.isFinite(Number(value)) && Number(value) >= 0), "number");
};
