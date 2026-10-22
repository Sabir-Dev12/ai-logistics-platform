import { useEffect, useState } from "react";
import PropTypes from "prop-types";
import { Alert, Badge, Button, Card, EmptyState, ErrorState, Input, LoadingState, Modal, Pagination, Select, Table } from "../design-system";
import BookingLinesEditor from "../Bookings/BookingLinesEditor";
import VoyageDetails from "./VoyageDetails";
import VoyageForm from "./VoyageForm";
import VoyageEquipment from "./VoyageEquipment";
import VoyageTrajets from "./VoyageTrajets";
import { categories, categoryLabel, natureLabel, normalize, status } from "./voyageRules";
import { useVoyages } from "./useVoyages";
import "../Bookings/bookings.css";
const titles = { detail: "Fiche voyage", create: "Créer un voyage", edit: "Modifier le voyage", validate: "Valider le voyage", close: "Clôturer le voyage", reopen: "Rouvrir le voyage", trajets: "Gérer les trajets", equipment: "Gérer les équipements", generate: "Générer les coûts du voyage" };
export default function VoyagesPage({ repository }) {
  const [query, setQuery] = useState(""), [category, setCategory] = useState(""), [state, setState] = useState(""), [page, setPage] = useState(1), [pageSize, setPageSize] = useState(5), [dialog, setDialog] = useState(null), [notice, setNotice] = useState("");
  const data=useVoyages(repository,{query,category,state,page,pageSize});
  const row=data.serverPaging?(data.detail.id===dialog?.id?data.detail.row:null):data.voyages.find(item=>item.id===dialog?.id);
  const change = (setter) => (event) => { setter(event.target.value); setPage(1); };
  const reset = () => { setQuery(""); setCategory(""); setState(""); setPage(1); };
  const open = (kind, id, tab = "summary", month) => { data.clearError(); setNotice(""); setDialog({ kind, id, tab, month });if(id)void data.openDetail(id); };
  const close = () => { if (!data.busy) { setDialog(null); data.clearError(); } };
  const back = () => dialog.id ? open("detail", dialog.id, dialog.tab) : close();
  const filtered = data.serverPaging?data.voyages:data.voyages.filter((item) => [item.numVoyage, item.provenance, item.destination, item.agence].some((value) => normalize(value).includes(normalize(query))) && (!category || item.categorie === category) && (!state || status(item) === state));
  const total=data.serverPaging?data.totalCount:filtered.length,totalPages=Math.max(1,Math.ceil(total/pageSize));
  useEffect(()=>{if(data.serverPaging&&data.phase==="ready"&&page>totalPages)setPage(totalPages);},[data.serverPaging,data.phase,page,totalPages]);
  const current = Math.min(page, totalPages), start = (current - 1) * pageSize;
  const perform = async (payload) => {
    const kind = dialog.kind, method = { edit: "update", trajets: "setTrajets", equipment: "setEquipment" }[kind] || kind;
    const args = kind === "create" ? [payload] : kind === "reopen" ? [dialog.id] : [dialog.id, kind === "generate" ? (data.serverPaging?dialog.month:{ month: dialog.month }) : payload];
    const result = await data.execute(method, ...args); if (!result) return false;
    if (kind === "create") reset(); open("detail",result.updatedId,dialog.tab); setNotice("Opération enregistrée."); return true;
  };
  const related = async (method, payload) => { const result = await data.execute(method, dialog.id, payload); if(result){setNotice("Opération enregistrée.");if(data.serverPaging)void data.openDetail(dialog.id);} return Boolean(result); };
  const columns = [{ key: "numVoyage", header: "Voyage", render: (value, item) => <Button variant="ghost" onClick={() => open("detail", item.id)}>{value}</Button> }, { key: "categorie", header: "Catégorie", render: categoryLabel }, { key: "nature", header: "Nature", render: natureLabel }, { key: "provenance", header: "Provenance" }, { key: "destination", header: "Destination" }, { key: "dateDepartPrevu", header: "Départ prévu", render:value=>value?.slice(0,10)||"—" }, { key: "status", header: "Statut", render: (_, item) => <Badge variant={item.dateCloture ? "success" : item.dateVoyage ? "info" : "warning"}>{status(item)}</Badge> }, { key: "dossiers", header: "Dossiers", render: (_, item) => data.serverPaging?"Voir la fiche":data.dossiers.filter((dossier) => dossier.keyVoyageInter === item.id).length }, { key: "actions", header: "Actions", render: (_, item) => <Button disabled={Boolean(item.dateCloture)} onClick={() => open("edit", item.id)} aria-label={`Modifier ${item.numVoyage}`}>Modifier</Button> }];
  const empty = data.voyages.length || query.trim() || category || state ? <EmptyState title="Aucun résultat" description="Modifiez vos filtres pour retrouver les voyages." action={<Button disabled={data.busy} onClick={reset}>Réinitialiser les filtres</Button>} /> : <EmptyState title="Aucun voyage" description="Créez votre premier voyage." action={<Button onClick={() => open("create")}>Créer un voyage</Button>} />;
  return <section className="bookings-page" aria-label="Voyages"><Card className="bookings-header"><div><h1>Voyages</h1><p>Trajets, équipements, dossiers et coûts du voyage.</p></div><Badge>Voyages</Badge><div className="bookings-header-actions"><Button disabled={data.busy || data.phase === "loading"} onClick={data.reload}>Actualiser</Button><Button variant="primary" disabled={data.busy || data.phase !== "ready"} onClick={() => open("create")}>Créer un voyage</Button></div></Card>
    <Card className="bookings-toolbar"><Input label="Rechercher un voyage" placeholder={data.serverPaging?"Numéro, référence ou navire":"Numéro, provenance, destination ou agence"} disabled={data.busy} fieldClassName="bookings-search" value={query} onChange={change(setQuery)} /><Select disabled={data.busy} label="Catégorie de voyage" fieldClassName="bookings-filter" value={category} onChange={change(setCategory)}><option value="">Toutes les catégories</option>{categories.map((item) => <option key={item} value={item}>{categoryLabel(item)}</option>)}</Select><Select disabled={data.busy} label="Statut du voyage" fieldClassName="bookings-filter" value={state} onChange={change(setState)}><option value="">Tous les statuts</option>{["Brouillon", "Validé", "Clôturé"].map((item) => <option key={item}>{item}</option>)}</Select><Button disabled={data.busy} onClick={reset}>Réinitialiser les filtres</Button></Card>
    {notice && <Alert title="Opération effectuée" variant="success" onDismiss={() => setNotice("")}>{notice}</Alert>}
    <Card className="bookings-list">{data.phase === "loading" ? <LoadingState label="Chargement des voyages…" /> : data.phase === "error" ? <ErrorState message={data.listError||"Impossible de charger les voyages. Réessayez."} onRetry={data.reload} /> : <Table caption="Liste des voyages" rows={data.serverPaging?filtered:filtered.slice(start,start+pageSize)} columns={columns} className="bookings-table" emptyState={empty} />}</Card>
    {data.phase === "ready" && <div className="bookings-footer"><p aria-live="polite">{total ? `${start + 1}–${Math.min(start + pageSize, total)} sur ${total} voyages` : "0 voyage"}</p><Select disabled={data.busy} label="Voyages par page" value={pageSize} onChange={(event) => { setPageSize(Number(event.target.value)); setPage(1); }}><option value={5}>5</option><option value={10}>10</option><option value={20}>20</option></Select>{total > 0 && <Pagination page={current} totalPages={totalPages} onPageChange={setPage} disabled={data.busy} />}</div>}
    <Modal open={Boolean(dialog)} title={titles[dialog?.kind] || "Voyage"} onClose={close} className="bookings-dialog" actions={["reopen", "generate"].includes(dialog?.kind) ? <><Button disabled={data.busy} onClick={back}>Annuler</Button><Button variant="primary" loading={data.busy} disabled={data.uncertain||!row} onClick={() => perform()}>Confirmer</Button></> : dialog?.kind === "detail" ? <Button onClick={close} disabled={data.busy}>Fermer</Button> : undefined}>
      {data.serverPaging&&dialog?.id&&data.detail.phase==="loading"&&<LoadingState label="Chargement du voyage…"/>}
      {data.serverPaging&&dialog?.id&&data.detail.phase==="error"&&<ErrorState message={data.detail.error} onRetry={()=>data.openDetail(dialog.id)}/>}
      {data.uncertain&&<Button onClick={()=>{data.clearError();if(dialog?.id)open("detail",dialog.id,dialog.tab);else close();void data.reload();}}>Vérifier le résultat</Button>}
      {dialog?.kind === "detail" && row && <VoyageDetails key={`${row.id}:${dialog.tab}`} row={row} data={data} initialTab={dialog.tab} onAction={(kind, tab) => open(kind, row.id, tab)} onExecute={related} onGenerate={(month) => open("generate", row.id, "costs", month)} />}
      {["create", "edit", "validate", "close"].includes(dialog?.kind) && (!dialog.id||row) && <VoyageForm key={`${dialog.kind}:${dialog.id || "new"}`} kind={dialog.kind} row={row} repository={data.serverPaging?repository:undefined} blocked={data.uncertain} busy={data.busy} error={data.error} onSubmit={perform} onCancel={back} />}
      {dialog?.kind === "trajets" && row && <VoyageTrajets row={row} remote={data.serverPaging} blocked={data.uncertain} onAdd={value=>related("addTrajet",value)} onRemove={id=>related("removeTrajet",id)} busy={data.busy} error={data.error} onSubmit={perform} onCancel={back} />}
      {dialog?.kind === "equipment" && row && (data.serverPaging?<VoyageEquipment row={row} busy={data.busy} blocked={data.uncertain} error={data.error} onAdd={value=>related("addEquipment",value)} onCancel={back}/>:<BookingLinesEditor kind="equipment" booking={row} busy={data.busy} error={data.error} onSubmit={perform} onCancel={back} />)}
      {["reopen", "generate"].includes(dialog?.kind) && <div className="bookings-confirmation"><p>{row?.numVoyage}</p><p>{dialog.kind === "reopen" ? "Rouvrir ce voyage ?" : `Générer les coûts pour ${data.serverPaging?dialog.month?.moisCalcul:dialog.month} avec les taux de l’aperçu confirmé ? Les opérations déjà générées seront ignorées.`}</p>{data.error && <ErrorState message={data.error} />}</div>}
    </Modal>
  </section>;
}
VoyagesPage.propTypes = { repository: PropTypes.object.isRequired };

