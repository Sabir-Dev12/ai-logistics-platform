import { useState } from "react";
import PropTypes from "prop-types";
import { Alert, Badge, Button, Card, EmptyState, ErrorState, Input, LoadingState, Modal, Pagination, Select, Table } from "../design-system";
import TrackingReferencePicker from "./TrackingReferencePicker";
import TrackingEventForm from "./TrackingEventForm";
import { allowedStatuses, displayDate, localDateTime, timelineFor, trackingEntities } from "./trackingRules";
import { useTracking } from "./useTracking";
import "../Bookings/bookings.css";
export default function TrackingPage({ repository, author }) {
  const [entity, setEntity] = useState("Client"), [reference, setReference] = useState(""), [from, setFrom] = useState(""), [to, setTo] = useState(""), [order, setOrder] = useState("recent"), [page, setPage] = useState(1), [pageSize, setPageSize] = useState(5), [formOpen, setFormOpen] = useState(false), [notice, setNotice] = useState("");
  const [referenceLabel,setReferenceLabel]=useState("");
  const data=useTracking(repository,author,{entity,reference});
  const references = data.records.filter((record) => record.entite === entity);
  const selected = data.remote ? (reference?{key:reference,label:referenceLabel||reference}:null) : references.find((record) => record.key === reference) || references[0];
  const timeline = selected ? timelineFor(data.events, entity, selected.key) : [];
  const latest = data.remote ? data.latest : timeline[0], statuses = allowedStatuses(data.statuses, entity);
  const invalidRange = Boolean(from && to && from > to);
  const filtered = timeline.filter((event) => { const date = localDateTime(event.dateTraceUser).slice(0, 10); return (!from || date >= from) && (!to || date <= to); });
  const rows = order === "recent" ? filtered : [...filtered].reverse();
  const current = Math.min(page, Math.max(1, Math.ceil(rows.length / pageSize))), start = (current - 1) * pageSize;
  const change = (setter) => (event) => { setter(event.target.value); setPage(1); setNotice(""); };
  const resetFilters = () => { setFrom(""); setTo(""); setOrder("recent"); setPage(1); };
  const changeEntity = (event) => { setEntity(event.target.value); setReference("");setReferenceLabel(""); resetFilters(); setNotice(""); data.clearError(); };
  const close = () => { if (!data.busy) { setFormOpen(false); data.clearError(); } };
  const submit = async (value) => {
    if (!selected) return;
    const result = await data.execute("record", entity, selected.key, value); if (!result) return;
    setFormOpen(false); resetFilters(); setNotice("Événement ajouté à la chronologie.");
  };
  const columns = [{ key: "dateTraceUser", header: "Date de l’événement", render: (value) => <time dateTime={value}>{displayDate(value)}</time> }, { key: "dateTraceSystem", header: "Enregistré le", render: (value) => <time dateTime={value}>{displayDate(value)}</time> }, { key: "libelleStatut", header: "Statut", render: (value, event) => <div className="bookings-actions"><Badge>{value || event.keyStatut || "Sans statut"}</Badge>{event.isSysteme && <Badge variant="warning">Système</Badge>}</div> }, { key: "utilisateur", header: "Auteur" }, { key: "commentaire", header: "Commentaire" }];
  const empty = !selected ? <EmptyState title="Aucune référence sélectionnée" description="Choisissez une référence dans la liste ci-dessus pour consulter son suivi." /> : !timeline.length ? <EmptyState title="Aucun événement" description="Aucun historique n’est enregistré pour cette référence." /> : <EmptyState title="Aucun résultat" description="Aucun événement ne correspond à la période choisie." action={<Button onClick={resetFilters}>Réinitialiser la période</Button>} />;
  return <section className="bookings-page" aria-label="Tracking / Suivi"><Card className="bookings-header"><div><h1>Tracking / Suivi</h1><p>Chronologie et derniers statuts des entités métier.</p></div><Badge>Suivi</Badge><div className="bookings-header-actions"><Button disabled={data.busy || data.phase === "loading"} onClick={data.reload}>Actualiser</Button><Button variant="primary" disabled={data.phase !== "ready" || data.busy || !selected || !data.canRecord || !statuses.length} onClick={() => { data.clearError(); setNotice(""); setFormOpen(true); }}>Ajouter un événement</Button></div></Card>
    <Card className="bookings-toolbar"><Select label="Type d’entité" value={entity} onChange={changeEntity} fieldClassName="bookings-filter" disabled={data.busy||formOpen}>{trackingEntities.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}</Select>
      {data.remote?<TrackingReferencePicker key={entity} repository={repository} entity={entity} value={reference} label={referenceLabel} disabled={data.busy||formOpen} onChange={(key,label)=>{if(key===reference)return;setReference(key);setReferenceLabel(label);resetFilters();setNotice("");data.clearError();}}/>:<Select label="Référence à suivre" value={selected?.key || ""} onChange={change(setReference)} fieldClassName="bookings-search" disabled={data.phase !== "ready" || data.busy}>{!references.length && <option value="">Aucune référence</option>}{references.map((item) => <option key={item.key} value={item.key}>{item.label}</option>)}</Select>}
    </Card>
    {notice && <Alert title="Événement enregistré" variant="success" onDismiss={() => setNotice("")}>{notice}</Alert>}
    {data.phase === "loading" ? <Card><LoadingState label="Chargement du suivi…" /></Card> : data.phase === "error" ? <ErrorState message={data.loadError||"Impossible de charger le suivi. Réessayez."} onRetry={data.reload} /> : <>
      {!data.canRecord && <Alert variant="warning" title="Consultation uniquement">Aucun ajout d’événement n’est disponible pour la référence et les statuts actuels.</Alert>}
      {data.remote&&data.limited&&<Alert title="Historique limité" variant="warning">Les 500 derniers événements enregistrés sont chargés. La période, l’ordre et la pagination s’appliquent à cet ensemble.</Alert>}
      <Card className="account-panel"><div className="bookings-actions"><h2>Dernier statut</h2><Badge>{latest ? latest.libelleStatut || latest.keyStatut || "Sans statut" : "Aucun statut disponible"}</Badge>{latest?.isSysteme && <Badge variant="warning">Système</Badge>}</div><p>Dernier événement enregistré pour {selected?.label || "la référence sélectionnée"}, indépendamment des filtres de chronologie.</p>
        <div className="bookings-fields"><Input label="Auteur du dernier événement" readOnly value={latest?.utilisateur || "—"} /><Input label="Date du dernier événement" readOnly value={displayDate(latest?.dateTraceUser)} /><Input label="Dernier enregistrement" readOnly value={displayDate(latest?.dateTraceSystem)} /></div>{latest?.commentaire && <p>{latest.commentaire}</p>}
      </Card>
      <Card className="bookings-toolbar"><Input label="Du (date de l’événement)" type="date" value={from} onChange={change(setFrom)} fieldClassName="bookings-filter" /><Input label="Au (date de l’événement)" type="date" value={to} onChange={change(setTo)} fieldClassName="bookings-filter" /><Select label="Ordre des événements" value={order} onChange={change(setOrder)} fieldClassName="bookings-filter"><option value="recent">Enregistrements les plus récents</option><option value="old">{data.remote?"Les plus anciens parmi les événements chargés":"Enregistrements les plus anciens"}</option></Select><Button onClick={resetFilters}>Réinitialiser la période</Button></Card>
      <Card className="bookings-list">{invalidRange ? <ErrorState message="La date de fin doit être postérieure ou égale à la date de début." /> : <Table caption="Chronologie des événements" rows={rows.slice(start, start + pageSize)} columns={columns} className="bookings-table" emptyState={empty} />}</Card>
      {!invalidRange && <div className="bookings-footer"><p aria-live="polite">{rows.length ? `${start + 1}–${Math.min(start + pageSize, rows.length)} sur ${rows.length} événements${data.remote?" chargés":""}` : "0 événement"}</p><Select label="Événements par page" value={pageSize} onChange={(event) => { setPageSize(Number(event.target.value)); setPage(1); }}><option value={5}>5</option><option value={10}>10</option><option value={20}>20</option></Select>{rows.length > 0 && <Pagination page={current} totalPages={Math.ceil(rows.length / pageSize)} onPageChange={setPage} disabled={data.busy} />}</div>}
    </>}
    <Modal open={formOpen} title="Ajouter un événement de suivi" onClose={close} className="bookings-dialog">{data.uncertain&&<Button onClick={()=>{close();void data.reload();}}>Vérifier la chronologie</Button>}<TrackingEventForm key={`${entity}:${selected?.key||""}:${formOpen}`} statuses={statuses} blocked={data.uncertain} busy={data.busy} error={data.error} reference={selected?.label || ""} onSubmit={submit} onCancel={close} /></Modal>
  </section>;
}
TrackingPage.propTypes = { repository: PropTypes.object.isRequired, author: PropTypes.string };
