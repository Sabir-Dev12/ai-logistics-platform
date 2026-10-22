import { useCallback, useEffect, useState } from "react";
import PropTypes from "prop-types";
import { Alert, Badge, Button, Card, EmptyState, ErrorState, Input, LoadingState, Modal, Pagination, Select, Table } from "../design-system";
import BookingDetails from "./BookingDetails";
import BookingForm from "./BookingForm";
import BookingLinesEditor from "./BookingLinesEditor";
import VoyageCapacity from "./VoyageCapacity";
import { bookingStatuses, bookingTypes, normalizeBookingSearch, linkedVoyage } from "./bookingRules";
import { useBookingResource } from "./useBookingResource";
import { useBookings } from "./useBookings";
import "./bookings.css";

const titles = { detail: "Fiche réservation", create: "Créer une réservation", edit: "Modifier la réservation", duplicate: "Dupliquer la réservation", validate: "Valider la réservation", cancel: "Annuler la réservation", remove: "Supprimer la réservation",
  equipment: "Gérer les équipements", packing: "Gérer les colisages", linkVoyage: "Associer un voyage", unlinkVoyage: "Désaffecter le voyage", linkDossier: "Associer un dossier", unlinkDossier: "Détacher le dossier", unload: "Enregistrer le dépotage", cancelUnload: "Annuler le dépotage", capacity: "Capacité d’un voyage" };
const confirmations = { validate: "Vérifier la cohérence de cette réservation ? Ce contrôle ne crée pas de nouveau statut.", remove: "Supprimer cette réservation ?",
  unlinkVoyage: "Désaffecter cette réservation du voyage ?", unlinkDossier: "Détacher le dossier de cette réservation ?", cancelUnload: "Annuler le dépotage et réinitialiser les colis dépotés et sortis ?" };
const methods = { edit: "update", equipment: "setEquipment", packing: "setPacking" };
export default function BookingsPage({ repository }) {
  const [query, setQuery] = useState(""), [status, setStatus] = useState(""), [type, setType] = useState("");
  const [page, setPage] = useState(1), [pageSize, setPageSize] = useState(5), [dialog, setDialog] = useState(null), [notice, setNotice] = useState(""), [capacityVoyage, setCapacityVoyage] = useState("");
  const data = useBookings(repository, {query,status,type,page,pageSize});
  const loadTypes=useCallback(signal=>data.serverPaging?repository.types({signal}):Promise.resolve(bookingTypes),[repository,data.serverPaging]);
  const availableTypes=useBookingResource(loadTypes);
  const booking = data.serverPaging ? (data.detail.id === dialog?.id ? data.detail.booking : null) : data.bookings.find((row) => row.keyBooking === dialog?.id);
  const reset = () => { setQuery(""); setStatus(""); setType(""); setPage(1); };
  const change = (setter) => (event) => { setter(event.target.value); setPage(1); };
  const open = (kind, id, tab = "summary") => { data.clearError(); setNotice(""); setDialog({ kind, id, tab }); if(id) void data.openDetail(id); };
  const close = () => { if (!data.busy) { setDialog(null); data.clearError(); } };
  const back = () => dialog.id ? open("detail", dialog.id, dialog.tab) : close();
  const filtered = data.serverPaging ? data.bookings : data.bookings.filter((row) => [row.numBooking, row.intituleClient, row.keyDossier, linkedVoyage(row)].some((value) => normalizeBookingSearch(value).includes(normalizeBookingSearch(query)))
    && (!status || row.keyStatutBooking === status) && (!type || row.typeBooking === type));
  const total = data.serverPaging ? data.totalCount : filtered.length;
  const totalPages = Math.max(1, Math.ceil(total / pageSize)), currentPage = Math.min(page, totalPages), start = (currentPage - 1) * pageSize;
  useEffect(() => { if(data.serverPaging && data.phase === "ready" && page > totalPages) setPage(totalPages); }, [data.serverPaging,data.phase,page,totalPages]);
  const perform = async (...payload) => {
    const kind = dialog.kind, result = await data.execute(methods[kind] || kind, ...(kind === "create" ? payload : [dialog.id, ...payload]));
    if (!result) return;
    if (["create", "duplicate"].includes(kind)) reset();
    if (kind === "remove") setDialog(null); else open("detail", result.updatedId, dialog.tab);
    setNotice(kind === "validate" ? "Contrôles de validation réussis. Le statut de la réservation est inchangé." : "Opération enregistrée.");
  };
  const columns = [{ key: "numBooking", header: "Réservation", render: (value, row) => <Button variant="ghost" onClick={() => open("detail", row.keyBooking)}>{value}</Button> },
    { key: "typeBooking", header: "Type", render: (value) => bookingTypes.find((item) => item.value === value)?.label || value }, { key: "intituleClient", header: "Client" },
    { key: "keyStatutBooking", header: "Statut", render: (value) => <Badge variant={bookingStatuses[value]?.variant}>{bookingStatuses[value]?.label || value}</Badge> },
    { key: "voyage", header: "Voyage", render: (_, row) => linkedVoyage(row) || "—" }, { key: "keyDossier", header: "Dossier" },
    { key: "volumePrevision", header: "Volume prévu (m³)" }, { key: "depote", header: "Dépotage", render: (value) => <Badge variant={value ? "success" : "info"}>{value == null ? "Voir la fiche" : value ? "Dépoté" : "Non dépoté"}</Badge> },
    { key: "actions", header: "Actions", render: (_, row) => <div className="bookings-row-actions"><Button onClick={() => open("edit", row.keyBooking)} aria-label={`Modifier ${row.numBooking}`}>Modifier</Button><Button onClick={() => open("duplicate", row.keyBooking)} aria-label={`Dupliquer ${row.numBooking}`}>Dupliquer</Button></div> }];
  const empty = data.bookings.length ? <EmptyState title="Aucun résultat" description="Modifiez la recherche ou les filtres." action={<Button disabled={data.busy} onClick={reset}>Réinitialiser les filtres</Button>} />
    : <EmptyState title="Aucune réservation" description="Créez une première réservation pour commencer." action={<Button onClick={() => open("create")} disabled={data.busy || data.phase !== "ready" || (data.serverPaging && availableTypes.phase !== "ready")}>Créer une réservation</Button>} />;
  const form = ["create", "edit", "duplicate", "cancel", "linkVoyage", "linkDossier", "unload"].includes(dialog?.kind);
  return <section className="bookings-page" aria-label="Bookings / Réservations">
    <Card className="bookings-header"><div><h1>Bookings / Réservations</h1><p>Réservations, voyages, dossiers et opérations associées.</p></div><Badge>Réservations</Badge>
      <div className="bookings-header-actions"><Button disabled={data.phase !== "ready" || data.busy} onClick={() => open("capacity")}>Capacité d’un voyage</Button>
        <Button disabled={data.phase === "loading" || data.busy} onClick={data.reload}>Actualiser</Button><Button variant="primary" onClick={() => open("create")} disabled={data.busy || data.phase !== "ready" || (data.serverPaging && availableTypes.phase !== "ready")}>Créer une réservation</Button></div>
    </Card>
    <Card className="bookings-toolbar"><Input label="Rechercher une réservation" placeholder={data.serverPaging ? "Numéro de réservation" : "Numéro, client, voyage ou dossier"} disabled={data.busy} fieldClassName="bookings-search" value={query} onChange={change(setQuery)} />
      <Select label="Statut de réservation" fieldClassName="bookings-filter" disabled={data.busy} value={status} onChange={change(setStatus)}><option value="">Tous</option>{Object.entries(bookingStatuses).map(([key, value]) => <option key={key} value={key}>{value.label}</option>)}</Select>
      <Select label="Type de réservation" fieldClassName="bookings-filter" disabled={data.busy || (data.serverPaging && availableTypes.phase!=="ready")} value={type} onChange={change(setType)}><option value="">Tous</option>{(data.serverPaging ? availableTypes.value || [] : bookingTypes).map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}</Select>
      <Button disabled={data.busy} onClick={reset}>Réinitialiser les filtres</Button>
      {data.serverPaging && availableTypes.phase === "error" && <ErrorState title="Types de réservation indisponibles" message={availableTypes.error} onRetry={availableTypes.retry} />}
    </Card>
    {notice && <Alert title="Opération effectuée" variant="success" onDismiss={() => setNotice("")}>{notice}</Alert>}
    <Card className="bookings-list">{data.phase === "loading" ? <LoadingState label="Chargement des réservations…" /> : data.phase === "error" ? <ErrorState message={data.listError || "Impossible de charger les réservations. Réessayez."} onRetry={data.reload} />
      : <Table columns={columns} rows={data.serverPaging ? filtered : filtered.slice(start, start + pageSize)} rowKey="keyBooking" caption="Liste des réservations" className="bookings-table" emptyState={empty} />}</Card>
    {data.phase === "ready" && <div className="bookings-footer"><p aria-live="polite">{total ? `${start + 1}–${Math.min(start + pageSize, total)} sur ${total} réservations` : "0 réservation"}</p>
      <Select label="Réservations par page" disabled={data.busy} value={pageSize} onChange={(event) => { setPageSize(Number(event.target.value)); setPage(1); }}><option value={5}>5</option><option value={10}>10</option><option value={20}>20</option></Select>
      {total > 0 && <Pagination page={currentPage} totalPages={totalPages} onPageChange={setPage} disabled={data.busy} />}</div>}
    <Modal open={Boolean(dialog)} title={titles[dialog?.kind] || "Réservation"} onClose={close} className="bookings-dialog"
      actions={confirmations[dialog?.kind] ? <><Button disabled={data.busy} onClick={back}>Annuler</Button><Button variant="primary" loading={data.busy} disabled={data.uncertain || !booking} onClick={() => perform()}>Confirmer</Button></>
        : ["detail", "capacity"].includes(dialog?.kind) ? <Button onClick={close}>Fermer</Button> : undefined}>
      {dialog?.kind === "detail" && booking && <BookingDetails key={`${booking.keyBooking}:${dialog.tab}`} booking={booking} data={data} initialTab={dialog.tab} onAction={(kind, tab) => open(kind, booking.keyBooking, tab)} />}
      {data.serverPaging && dialog?.id && data.detail.phase === "loading" && <LoadingState label="Chargement de la réservation…" />}
      {data.serverPaging && dialog?.id && data.detail.phase === "error" && <ErrorState message={data.detail.error} onRetry={()=>data.openDetail(dialog.id)} />}
      {data.uncertain && <Button onClick={()=>{data.clearError();if(dialog?.id)open("detail",dialog.id,dialog.tab);else close();void data.reload();}}>Vérifier le résultat</Button>}
      {form && (!dialog.id || booking) && <BookingForm key={`${dialog.kind}:${dialog.id || "new"}`} kind={dialog.kind} booking={booking} data={data} onSubmit={perform} onCancel={back} />}
      {["equipment", "packing"].includes(dialog?.kind) && booking && <BookingLinesEditor key={`${dialog.kind}:${booking.keyBooking}`} kind={dialog.kind} booking={booking} busy={data.busy} blocked={data.uncertain} error={data.error} onSubmit={perform} onCancel={back} />}
      {dialog?.kind === "capacity" && <VoyageCapacity voyages={data.voyages} bookings={data.bookings} selectedId={capacityVoyage} onSelect={setCapacityVoyage} repository={data.serverPaging ? repository : undefined} />}
      {confirmations[dialog?.kind] && <div className="bookings-confirmation"><p>{booking?.numBooking}</p><p>{confirmations[dialog.kind]}</p>{data.error && <ErrorState message={data.error} />}</div>}
    </Modal>
  </section>;
}
BookingsPage.propTypes = { repository: PropTypes.object.isRequired };
