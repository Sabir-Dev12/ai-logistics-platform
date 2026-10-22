import { useEffect, useState } from "react";
import PropTypes from "prop-types";
import { Alert, Badge, Button, Card, EmptyState, ErrorState, Modal, Pagination, Select } from "../design-system";
import DocumentToolbar from "./DocumentToolbar";
import DocumentUpload from "./DocumentUpload";
import DocumentTable from "./DocumentTable";
import { documentStatuses, documentErrorLabel, normalizeSearch } from "./documentTypes";
import { useDocuments } from "./useDocuments";
import "./knowledge-base.css";

export default function KnowledgeBasePage({ repository }) {
  const [query, setQuery] = useState("");
  const [type, setType] = useState("");
  const [status, setStatus] = useState("");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(5);
  const [uploadOpen, setUploadOpen] = useState(false);
  const [selected, setSelected] = useState(null);
  const data = useDocuments(repository,{query,type,status,page,pageSize});
  useEffect(()=>{if(data.serverPaging && data.phase==="ready" && page>Math.max(1,Math.ceil(data.totalCount/pageSize)))setPage(Math.max(1,Math.ceil(data.totalCount/pageSize)));},[data.serverPaging,data.phase,data.totalCount,page,pageSize]);
  const change = (setter) => (value) => { setter(value); setPage(1); };
  const resetFilters = () => { setQuery(""); setType(""); setStatus(""); setPage(1); };
  const filtered = data.serverPaging ? data.documents : data.documents.filter((document) => normalizeSearch(document.name).includes(normalizeSearch(query))
    && (!type || document.type === type) && (!status || document.status === status));
  const total = data.serverPaging ? data.totalCount : filtered.length;
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  const currentPage = data.serverPaging ? page : Math.min(page, totalPages);
  const start = (currentPage - 1) * pageSize;
  const rows = data.serverPaging ? filtered : filtered.slice(start, start + pageSize);
  const closeUpload = () => { setUploadOpen(false); data.clearActionError(); };
  const closeDelete = () => { setSelected(null); data.clearActionError(); };
  const upload = async (files) => { const success = await data.upload(files); if (success) resetFilters(); return success; };
  const remove = async () => { if (selected && await data.remove(selected.id)) { closeDelete(); setPage(1); } };
  const empty = (data.documents.length || data.serverPaging && (query || type || status)) ? <EmptyState title="Aucun résultat" description="Modifiez la recherche ou les filtres."
    action={<Button onClick={resetFilters}>Réinitialiser les filtres</Button>} />
    : <EmptyState title="Aucun document" description="Ajoutez un premier document pour commencer." />;

  return <section className="kb-page" aria-label="Knowledge Base">
    <Card className="kb-header"><div><h1>Knowledge Base</h1><p>{data.serverPaging ? "Documents et statuts d’indexation du serveur RAG." : "Documents et indexation simulés ; aucun fichier envoyé."}</p></div><Badge>{data.serverPaging ? "API backend" : "Mode démo"}</Badge>
      <div className="kb-header-actions"><Button disabled={data.phase === "loading" || data.busy} onClick={data.reload}>Actualiser</Button>
        <Button variant="primary" disabled={data.phase !== "ready" || data.busy || data.uncertain} onClick={() => { data.clearActionError(); setUploadOpen(true); }}>Ajouter des documents</Button></div>
    </Card>
    {data.uncertain && <Alert variant="warning" title="Confirmation à vérifier">La confirmation d’une écriture a été perdue. Fermez la fenêtre, actualisez puis vérifiez les documents avant toute nouvelle opération.</Alert>}
    {data.uploadOutcome.length>0 && <Alert variant={data.uploadOutcome.some(row=>row.status==="failed")?"warning":"info"} title="Résultat de l’envoi" onDismiss={data.clearUploadOutcome}><ul>{data.uploadOutcome.map(row=><li key={row.id}>{row.name} : {documentStatuses[row.status]}{row.status==="failed"?` — ${documentErrorLabel(row.errorCode)}`:""}</li>)}</ul></Alert>}
    <DocumentToolbar disabled={data.busy} query={query} type={type} status={status} onQuery={change(setQuery)} onType={change(setType)} onStatus={change(setStatus)} onReset={resetFilters} />
    <Card className="kb-documents">
      {data.phase === "error" ? <ErrorState message={data.listError || "Impossible de charger les documents. Réessayez."} onRetry={data.reload} />
        : <DocumentTable documents={rows} loading={data.phase === "loading"} busy={data.busy || data.uncertain} remote={data.serverPaging} emptyState={empty} onDelete={(document) => { data.clearActionError(); setSelected(document); }} />}
    </Card>
    {data.phase === "ready" && <div className="kb-footer">
      <p aria-live="polite">{total ? `${start + 1}–${Math.min(start + rows.length, total)} sur ${total} ${total === 1 ? "document" : "documents"}` : "0 document"}</p>
      <Select disabled={data.busy} label="Documents par page" value={pageSize} onChange={(event) => change(setPageSize)(Number(event.target.value))}>
        <option value={5}>5</option><option value={10}>10</option><option value={20}>20</option>
      </Select>
      {total > 0 && <Pagination page={currentPage} totalPages={totalPages} onPageChange={setPage} disabled={data.busy} />}
    </div>}
    <DocumentUpload open={uploadOpen} onClose={closeUpload} onUpload={upload} busy={data.busy} remote={data.serverPaging} blocked={data.uncertain || data.phase!=="ready"} error={data.actionError} />
    <Modal open={Boolean(selected)} onClose={() => { if (!data.busy) closeDelete(); }} title="Supprimer le document"
      actions={<><Button disabled={data.busy} onClick={closeDelete}>Annuler</Button><Button variant="primary" disabled={data.busy || data.uncertain || data.phase!=="ready"} loading={data.busy} onClick={remove}>Confirmer la suppression</Button></>}>
      <p className="kb-file-list">Supprimer « {selected?.name} » {data.serverPaging ? "du serveur et de l’index RAG ? Cette suppression est définitive." : "de cette démonstration ?"}</p>
      {data.actionError && <ErrorState message={data.actionError} />}
    </Modal>
  </section>;
}
KnowledgeBasePage.propTypes = { repository: PropTypes.shape({ list: PropTypes.func.isRequired, upload: PropTypes.func.isRequired, remove: PropTypes.func.isRequired }) };
