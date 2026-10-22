import { useState } from "react";
import PropTypes from "prop-types";
import { Alert, Badge, Button, Card, EmptyState, ErrorState, Input, LoadingState, Modal, Pagination, Select, Table } from "../design-system";
import BookingLinesEditor from "../Bookings/BookingLinesEditor";
import DocumentUpload from "../KnowledgeBase/DocumentUpload";
import { fileType } from "../KnowledgeBase/documentTypes";
import InvoiceClientPicker from "../SalesInvoices/InvoiceClientPicker";
import DocumentReferenceForm from "./DocumentReferenceForm";
import QuotationDetails from "./QuotationDetails";
import QuotationForm from "./QuotationForm";
import QuotationLinesEditor from "./QuotationLinesEditor";
import { money, normalize, products, quotationStatuses, quotationTotals } from "./quotationRules";
import { useQuotations } from "./useQuotations";
import "../Bookings/bookings.css";
import "../KnowledgeBase/knowledge-base.css";
const titles = { detail: "Fiche cotation", create: "Créer un devis", edit: "Modifier le devis", duplicate: "Dupliquer le devis", send: "Envoyer le devis", close: "Clôturer le devis", cancel: "Annuler le devis", remove: "Supprimer le devis", colisages: "Gérer les colisages", prix: "Gérer les prix", achats: "Gérer les achats", taux: "Gérer les taux de change", removeDocument: "Retirer le document associé" };
export default function QuotationsPage({ repository }) {
  const [query, setQuery] = useState(""), [status, setStatus] = useState(""), [client, setClient] = useState(""), [page, setPage] = useState(1), [pageSize, setPageSize] = useState(5), [dialog, setDialog] = useState(null), [notice, setNotice] = useState("");
  const data = useQuotations(repository, {query,status,client,page,pageSize});
  const row = data.remote ? (data.detail.id === dialog?.id ? data.detail.row : null) : data.quotations.find((item) => item.id === dialog?.id);
  const change = (setter) => (event) => { setter(event.target.value); setPage(1); };
  const reset = () => { setQuery(""); setStatus(""); setClient(""); setPage(1); };
  const open = (kind, id, tab = "summary", documentId) => { data.clearError(); setNotice(""); setDialog({ kind, id, tab, documentId }); if (id && data.remote) void data.openDetail(id); };
  const close = () => { if (!data.busy) { setDialog(null); data.clearError(); } };
  const back = () => { if (!data.busy) { data.clearError(); setDialog(dialog.id ? { kind: "detail", id: dialog.id, tab: dialog.tab } : null); } };
  const filtered = data.remote ? data.quotations : data.quotations.filter((item) => [item.numCotation, data.clients.find((value) => value.id === item.keyClient)?.label, item.commercial].some((value) => normalize(value).includes(normalize(query))) && (!status || item.statut === status) && (!client || item.keyClient === client));
  const total = data.remote ? data.totalCount : filtered.length;
  const current = Math.min(page, Math.max(1, Math.ceil(total / pageSize))), start = (current - 1) * pageSize;
  const perform = async (payload) => {
    const kind = dialog.kind;
    const method = kind === "edit" ? "update" : ["prix", "achats", "taux", "colisages"].includes(kind) ? "setLines" : kind;
    const value = method === "setLines" ? { kind, lines: payload } : kind === "removeDocument" ? { documentId: dialog.documentId } : payload;
    const args = kind === "create" ? [value] : ["send", "remove"].includes(kind) ? [dialog.id] : [dialog.id, value];
    const result = await data.execute(method, ...args); if (!result) return false;
    if (["create", "duplicate"].includes(kind)) reset();
    setDialog(kind === "remove" ? null : { kind: "detail", id: result.updatedId, tab: dialog.tab }); setNotice(data.remote ? "Opération enregistrée par le serveur." : kind === "send" ? "Envoi simulé : le devis est marqué envoyé. Aucun courriel n’a été transmis." : "Opération enregistrée dans la démonstration."); return true;
  };
  const group = async (value) => { const result = await data.execute("setGroup", row.id, value); return Boolean(result); };
  const upload = async (files) => Boolean(await data.execute("addDocuments", row.id, files.map((file) => ({ name: file.name, type: fileType(file.name), size: file.size }))));
  const empty = data.quotations.length ? <EmptyState title="Aucun résultat" description="Modifiez vos filtres pour retrouver les devis." action={<Button onClick={reset}>Réinitialiser les filtres</Button>} /> : <EmptyState title="Aucune cotation" description="Créez votre premier devis." action={<Button onClick={() => open("create")}>Créer un devis</Button>} />;
  const columns = [{ key: "numCotation", header: "Devis", render: (value, item) => <Button variant="ghost" onClick={() => open("detail", item.id)}>{value}</Button> }, { key: "keyClient", header: "Client", render: (value) => data.clients.find((item) => item.id === value)?.label || value || "—" }, { key: "produit", header: "Produit", render: (value) => products.find((item) => item.id === value)?.label || value }, { key: "statut", header: "Statut", render: (value) => <Badge variant={quotationStatuses[value]}>{value}</Badge> }, { key: "dateValidite", header: "Validité" }, { key: "vente", header: "Vente (MAD)", render: (_, item) => { if(data.remote && !item.serverTotals) return "Voir la fiche"; const totals = quotationTotals(item); return totals.complete ? money(totals.vente) : "Taux manquant"; } }, { key: "documents", header: "Documents", render: (value, item) => data.remote ? (item.attachement ? "Oui" : "Non") : value.length }, { key: "actions", header: "Actions", render: (_, item) => <Button onClick={() => open("duplicate", item.id)} aria-label={`Dupliquer ${item.numCotation}`}>Dupliquer</Button> }];
  return <section className="bookings-page" aria-label="Cotations / Devis"><Card className="bookings-header"><div><h1>Cotations / Devis</h1><p>{data.remote ? "Offres, prix, achats et documents du serveur." : "Offres, prix, achats et documents fictifs, conservés en mémoire."}</p></div><Badge>{data.remote ? "Données serveur" : "Mode démo"}</Badge><div className="bookings-header-actions"><Button disabled={data.busy || data.phase === "loading"} onClick={data.reload}>Actualiser</Button><Button variant="primary" disabled={data.busy || data.phase !== "ready"} onClick={() => open("create")}>Créer un devis</Button></div></Card>
    <Card className="bookings-toolbar"><Input label="Rechercher un devis" placeholder={data.remote ? "Numéro de cotation" : "Numéro, client ou commercial"} fieldClassName="bookings-search" value={query} onChange={change(setQuery)} />{data.remote ? <InvoiceClientPicker source={repository} value={client} onChange={value => {setClient(value);setPage(1);}} label="Client" /> : <Select label="Client" value={client} onChange={change(setClient)} fieldClassName="bookings-filter"><option value="">Tous les clients</option>{data.clients.map((item) => <option key={item.id} value={item.id}>{item.label}</option>)}</Select>}<Select label="Statut du devis" value={status} onChange={change(setStatus)} fieldClassName="bookings-filter"><option value="">Tous les statuts</option>{Object.keys(quotationStatuses).map((value) => <option key={value}>{value}</option>)}</Select><Button onClick={reset}>Réinitialiser les filtres</Button></Card>
    {notice && <Alert title="Opération effectuée" variant="success" onDismiss={() => setNotice("")}>{notice}</Alert>}
    <Card className="bookings-list">{data.phase === "loading" ? <LoadingState label="Chargement des cotations…" /> : data.phase === "error" ? <ErrorState message={data.listError || "Impossible de charger les cotations. Réessayez."} onRetry={data.reload} /> : <Table caption="Liste des cotations" rows={data.remote ? filtered : filtered.slice(start, start + pageSize)} columns={columns} className="bookings-table" emptyState={empty} />}</Card>
    {data.phase === "ready" && <div className="bookings-footer"><p aria-live="polite">{total ? `${start + 1}–${Math.min(start + pageSize, total)} sur ${total} cotation${total > 1 ? "s" : ""}` : "0 cotation"}</p><Select label="Cotations par page" value={pageSize} onChange={(event) => { setPageSize(Number(event.target.value)); setPage(1); }}><option value={5}>5</option><option value={10}>10</option><option value={20}>20</option></Select>{total > 0 && <Pagination page={current} totalPages={Math.ceil(total / pageSize)} onPageChange={setPage} disabled={data.busy} />}</div>}
    <Modal open={Boolean(dialog) && (dialog.kind !== "upload" || data.remote)} title={titles[dialog?.kind] || "Cotation"} onClose={close} className="bookings-dialog" actions={["send", "remove", "removeDocument"].includes(dialog?.kind) ? <><Button disabled={data.busy} onClick={back}>Annuler</Button><Button variant="primary" loading={data.busy} disabled={data.uncertain || (data.remote && !row)} onClick={() => perform()}>Confirmer</Button></> : dialog?.kind === "detail" ? <Button disabled={data.busy} onClick={close}>Fermer</Button> : undefined}>
      {data.remote && dialog?.kind === "upload" && row && <DocumentReferenceForm busy={data.busy || data.uncertain} error={data.error} onCancel={back} onSubmit={async value=>{ const result=await data.execute("addDocument",row.id,value); if(result)setDialog({kind:"detail",id:result.updatedId,tab:"documents"}); }} />}
      {data.remote && dialog?.id && data.detail.phase === "loading" && <LoadingState label="Chargement de la cotation…" />}
      {data.remote && dialog?.id && data.detail.phase === "error" && <ErrorState message={data.detail.error} onRetry={() => data.openDetail(dialog.id)} />}
      {data.uncertain && <ErrorState message={data.error} onRetry={data.reload} />}
      {dialog?.kind === "detail" && row && <QuotationDetails key={`${row.id}:${dialog.tab}`} row={row} data={{...data,busy:data.busy || data.uncertain}} initialTab={dialog.tab} onAction={(kind, tab, documentId) => open(kind, row.id, tab, documentId)} onGroup={group} onOpen={(id) => open("detail", id, "groups")} />}
      {["create", "edit", "duplicate", "close", "cancel"].includes(dialog?.kind) && (dialog.kind === "create" || row) && <QuotationForm key={`${dialog.kind}:${dialog.id || "new"}`} kind={dialog.kind} row={row} source={data.remote ? repository : undefined} clients={data.clients} busy={data.busy || data.uncertain} error={data.error} onSubmit={perform} onCancel={back} />}
      {["prix", "achats", "taux"].includes(dialog?.kind) && row && <QuotationLinesEditor key={`${dialog.kind}:${row.id}`} kind={dialog.kind} row={row} busy={data.busy || data.uncertain} error={data.error} onSubmit={perform} onCancel={back} />}
      {dialog?.kind === "colisages" && row && <BookingLinesEditor kind="packing" booking={row} busy={data.busy || data.uncertain} error={data.error} onSubmit={perform} onCancel={back} />}
      {["send", "remove", "removeDocument"].includes(dialog?.kind) && <div className="bookings-confirmation"><p>{row?.numCotation}</p><p>{dialog.kind === "send" ? "Enregistrer l’envoi de ce devis ? Cette action ne transmet pas de courriel." : dialog.kind === "remove" ? "Supprimer ce devis ?" : `Retirer ${row?.documents.find((item) => item.id === dialog.documentId)?.name || "ce document"} du devis ?`}</p>{data.error && <ErrorState message={data.error} />}</div>}
    </Modal>
    <DocumentUpload open={!data.remote && dialog?.kind === "upload"} onClose={back} onUpload={upload} busy={data.busy} error={data.error} />
  </section>;
}
QuotationsPage.propTypes = { repository: PropTypes.object };
