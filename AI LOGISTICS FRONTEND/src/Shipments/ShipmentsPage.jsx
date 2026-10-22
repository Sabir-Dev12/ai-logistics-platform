import { useCallback, useEffect, useState } from "react";
import PropTypes from "prop-types";
import { Alert, Badge, Button, Card, EmptyState, ErrorState, Input, LoadingState, Modal, Pagination, Select, Table } from "../design-system";
import ShipmentDetails from "./ShipmentDetails";
import ShipmentForm from "./ShipmentForm";
import ShipmentPacking from "./ShipmentPacking";
import ShipmentVoyage from "./ShipmentVoyage";
import { billingStatuses, normalizeSearch, shipmentTypes } from "./shipmentRules";
import BookingReferencePicker from "../Bookings/BookingReferencePicker";
import { useBookingResource } from "../Bookings/useBookingResource";
import { useShipments } from "./useShipments";
import "../Bookings/bookings.css";
const titles = { detail: "Fiche dossier", create: "Créer un dossier", edit: "Modifier le dossier", packing: "Gérer les colisages", unload: "Enregistrer le dépotage", cancelUnload: "Annuler le dépotage", remove: "Supprimer le dossier", voyage: "Dossiers d’un voyage et répartition" };
export default function ShipmentsPage({ repository }) {
  const [query, setQuery] = useState(""), [client, setClient] = useState(""), [voyage, setVoyage] = useState(""), [type, setType] = useState(""), [billing, setBilling] = useState("");
  const [page, setPage] = useState(1), [pageSize, setPageSize] = useState(5), [dialog, setDialog] = useState(null), [notice, setNotice] = useState("");
  const data = useShipments(repository,{query,client,voyage,type,billing,page,pageSize});
  const loadTypes=useCallback(signal=>data.serverPaging?repository.types({signal}):Promise.resolve(shipmentTypes),[repository,data.serverPaging]);
  const types=useBookingResource(loadTypes);
  const row = data.serverPaging ? (data.detail.id===dialog?.id ? data.detail.row : null) : data.shipments.find(item=>item.keyDossier===dialog?.id);
  const change = (setter) => (event) => { setter(event.target.value); setPage(1); };
  const reset = () => { setQuery(""); setClient(""); setVoyage(""); setType(""); setBilling(""); setPage(1); };
  const open = (kind, id) => { data.clearError(); setNotice(""); setDialog({ kind, id }); if(id)void data.openDetail(id); };
  const close = () => { if (!data.busy) { setDialog(null); data.clearError(); } };
  const back = () => dialog.id ? open("detail", dialog.id) : close();
  const filtered = data.serverPaging ? data.shipments : data.shipments.filter((item) => [item.numDossier, item.intituleClient, item.keyVoyageInter].some((value) => normalizeSearch(value).includes(normalizeSearch(query))) && (!client || item.keyClient === client) && (!voyage || item.keyVoyageInter === voyage) && (!type || item.typeDossier === type) && (!billing || item.statutFacturation === billing));
  const total=data.serverPaging?data.totalCount:filtered.length,totalPages=Math.max(1,Math.ceil(total/pageSize));
  useEffect(()=>{if(data.serverPaging && data.phase==="ready" && page>totalPages)setPage(totalPages);},[data.serverPaging,data.phase,page,totalPages]);
  const current = Math.min(page, totalPages), start = (current - 1) * pageSize;
  const perform = async (payload) => {
    const kind = dialog.kind, method = { edit: "update", packing: "setPacking" }[kind] || kind;
    const result = await data.execute(method, ...(kind === "create" ? [payload] : [dialog.id, ...(["remove", "cancelUnload"].includes(kind) ? [] : [payload])]));
    if (!result) return;
    if (kind === "create") reset();
    if (kind === "remove") setDialog(null); else open("detail",result.updatedId);
    setNotice("Opération enregistrée.");
  };
  const packingAction=async(method,payload)=>{const result=await data.execute(method,dialog.id,payload);if(!result)return false;void data.openDetail(dialog.id);setNotice("Colisage enregistré.");return true;};
  const columns = [{ key: "numDossier", header: "Dossier", render: (value, item) => <Button variant="ghost" onClick={() => open("detail", item.keyDossier)}>{value}</Button> }, { key: "intituleClient", header: "Client" }, { key: "typeDossier", header: "Type", render: (value) => shipmentTypes.find((item) => item.value === value)?.label || value }, { key: "keyVoyageInter", header: "Voyage" }, { key: "statutFacturation", header: "Facturation", render: (value) => <Badge variant={billingStatuses[value]?.variant}>{billingStatuses[value]?.label || value}</Badge> }, { key: "nbColis", header: "Colis" }, { key: "dateDepotage", header: "Dépotage", render: (value) => <Badge variant={value ? "success" : "info"}>{data.serverPaging && value === undefined ? "Voir la fiche" : value ? "Dépoté" : "Non dépoté"}</Badge> },
    { key: "actions", header: "Actions", render: (_, item) => <div className="bookings-row-actions"><Button onClick={() => open("edit", item.keyDossier)} aria-label={`Modifier ${item.numDossier}`}>Modifier</Button><Button disabled={item.statutFacturation === "Facture"} onClick={() => open("remove", item.keyDossier)} aria-label={`Supprimer ${item.numDossier}`}>Supprimer</Button></div> }];
  const empty = data.shipments.length ? <EmptyState title="Aucun résultat" description="Modifiez les filtres pour retrouver vos dossiers." action={<Button disabled={data.busy} onClick={reset}>Réinitialiser les filtres</Button>} /> : <EmptyState title="Aucun dossier" description="Créez votre premier dossier." action={<Button onClick={() => open("create")} disabled={data.busy || data.phase !== "ready" || (data.serverPaging && types.phase !== "ready")}>Créer un dossier</Button>} />;
  return <section className="bookings-page" aria-label="Dossiers / Expéditions">
    <Card className="bookings-header"><div><h1>Dossiers / Expéditions</h1><p>Dossiers, colisages et opérations de dépotage.</p></div><Badge>Dossiers</Badge><div className="bookings-header-actions"><Button disabled={data.phase !== "ready" || data.busy} onClick={() => open("voyage")}>Dossiers d’un voyage</Button><Button disabled={data.phase === "loading" || data.busy} onClick={data.reload}>Actualiser</Button><Button variant="primary" onClick={() => open("create")} disabled={data.busy || data.phase !== "ready" || (data.serverPaging && types.phase !== "ready")}>Créer un dossier</Button></div></Card>
    <Card className="bookings-toolbar"><Input label="Rechercher un dossier" placeholder={data.serverPaging ? "Numéro ou référence" : "Numéro, client ou voyage"} disabled={data.busy} value={query} onChange={change(setQuery)} fieldClassName="bookings-search" />
      {data.serverPaging ? <BookingReferencePicker repository={repository} disabled={data.busy} kind="clients" label="Client" placeholder="Tous les clients" value={client} onChange={id=>{setClient(id);setPage(1);}} /> : <Select label="Client" value={client} onChange={change(setClient)} fieldClassName="bookings-filter"><option value="">Tous les clients</option>{data.clients.map((item) => <option key={item.id} value={item.id}>{item.label}</option>)}</Select>}
      {data.serverPaging ? <BookingReferencePicker repository={repository} disabled={data.busy} kind="voyages" label="Voyage" placeholder="Tous les voyages" value={voyage} onChange={id=>{setVoyage(id);setPage(1);}} /> : <Select label="Voyage" value={voyage} onChange={change(setVoyage)} fieldClassName="bookings-filter"><option value="">Tous les voyages</option>{data.voyages.map((item) => <option key={item.id} value={item.id}>{item.label}</option>)}</Select>}
      <Select label="Type" disabled={data.busy || (data.serverPaging && types.phase!=="ready")} value={type} onChange={change(setType)} fieldClassName="bookings-filter"><option value="">Tous les types</option>{(data.serverPaging ? types.value || [] : shipmentTypes).map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}</Select>
      <Select label="Statut de facturation" disabled={data.busy} value={billing} onChange={change(setBilling)} fieldClassName="bookings-filter"><option value="">Tous les statuts</option>{Object.entries(billingStatuses).map(([value, item]) => <option key={value} value={value}>{item.label}</option>)}</Select><Button disabled={data.busy} onClick={reset}>Réinitialiser les filtres</Button>
      {data.serverPaging && types.phase==="error" && <ErrorState title="Types de dossier indisponibles" message={types.error} onRetry={types.retry} />}
    </Card>
    {notice && <Alert title="Opération effectuée" variant="success" onDismiss={() => setNotice("")}>{notice}</Alert>}
    <Card className="bookings-list">{data.phase === "loading" ? <LoadingState label="Chargement des dossiers…" /> : data.phase === "error" ? <ErrorState message={data.listError || "Impossible de charger les dossiers. Réessayez."} onRetry={data.reload} /> : <Table caption="Liste des dossiers" rows={data.serverPaging?filtered:filtered.slice(start, start + pageSize)} rowKey="keyDossier" columns={columns} className="bookings-table" emptyState={empty} />}</Card>
    {data.phase === "ready" && <div className="bookings-footer"><p aria-live="polite">{total ? `${start + 1}–${Math.min(start + pageSize, total)} sur ${total} dossiers` : "0 dossier"}</p><Select label="Dossiers par page" disabled={data.busy} value={pageSize} onChange={(event) => { setPageSize(Number(event.target.value)); setPage(1); }}><option value={5}>5</option><option value={10}>10</option><option value={20}>20</option></Select>{total > 0 && <Pagination page={current} totalPages={totalPages} onPageChange={setPage} disabled={data.busy} />}</div>}
    <Modal open={Boolean(dialog)} title={titles[dialog?.kind] || "Dossier"} onClose={close} className="bookings-dialog" actions={["remove", "cancelUnload"].includes(dialog?.kind) ? <><Button disabled={data.busy} onClick={back}>Annuler</Button><Button variant="primary" loading={data.busy} disabled={data.uncertain || !row} onClick={() => perform()}>Confirmer</Button></> : ["detail", "voyage"].includes(dialog?.kind) ? <Button onClick={close}>Fermer</Button> : undefined}>
      {data.serverPaging && dialog?.id && data.detail.phase==="loading" && <LoadingState label="Chargement du dossier…" />}
      {data.serverPaging && dialog?.id && data.detail.phase==="error" && <ErrorState message={data.detail.error} onRetry={()=>data.openDetail(dialog.id)} />}
      {data.uncertain && <Button onClick={()=>{data.clearError();if(dialog?.id)open("detail",dialog.id);else close();void data.reload();}}>Vérifier le résultat</Button>}
      {dialog?.kind === "detail" && row && <ShipmentDetails row={row} voyages={data.voyages} busy={data.busy} onAction={(kind) => open(kind, row.keyDossier)} />}
      {["create", "edit", "unload"].includes(dialog?.kind) && (!dialog.id || row) && <ShipmentForm key={`${dialog.kind}:${dialog.id || "new"}`} row={row} clients={data.clients} repository={data.serverPaging?repository:undefined} blocked={data.uncertain} busy={data.busy} error={data.error} unload={dialog.kind === "unload"} onSubmit={perform} onCancel={back} />}
      {dialog?.kind === "packing" && row && <ShipmentPacking key={row.keyDossier} row={row} remote={data.serverPaging} blocked={data.uncertain} onAdd={value=>packingAction("addPacking",value)} onRemove={id=>packingAction("removePacking",id)} busy={data.busy} error={data.error} onSubmit={perform} onCancel={back} />}
      {dialog?.kind === "voyage" && <ShipmentVoyage repository={data.serverPaging?repository:undefined} shipments={data.shipments} voyages={data.voyages} onOpen={(id) => open("detail", id)} />}
      {["remove", "cancelUnload"].includes(dialog?.kind) && <div className="bookings-confirmation"><p>{row?.numDossier}</p><p>{dialog.kind === "remove" ? "Supprimer ce dossier ? Il sera retiré de la liste et du voyage." : "Annuler le dépotage ? Les colis dépotés et sortis seront réinitialisés."}</p>{data.error && <ErrorState message={data.error} />}</div>}
    </Modal>
  </section>;
}
ShipmentsPage.propTypes = { repository: PropTypes.object.isRequired };

