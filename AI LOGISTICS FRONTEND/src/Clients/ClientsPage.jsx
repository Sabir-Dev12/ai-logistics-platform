import { useState } from "react";
import PropTypes from "prop-types";
import { Alert, Badge, Button, Card, EmptyState, ErrorState, Input, LoadingState, Modal, Pagination, Select, Table } from "../design-system";
import ClientDetails from "./ClientDetails";
import ClientForm from "./ClientForm";
import { deletionReason, normalizeSearch } from "./clientRules";
import { useClients } from "./useClients";
import "./clients.css";

const titles = { detail: "Fiche client", create: "Créer un client", edit: "Modifier le client", validate: "Valider le client", remove: "Supprimer le client", address: "Ajouter une adresse", bank: "Ajouter une banque", removeAddress: "Retirer l’adresse", removeBank: "Retirer la banque" };
export default function ClientsPage({ repository }) {
  const [query, setQuery] = useState(""), [active, setActive] = useState(""), [validated, setValidated] = useState("");
  const [page, setPage] = useState(1), [pageSize, setPageSize] = useState(5), [dialog, setDialog] = useState(null), [notice, setNotice] = useState("");
  const data = useClients(repository, {query,active,validated,page,pageSize});
  const selected = data.remote ? (data.detail.id === dialog?.id ? data.detail.row : null) : data.clients.find((row) => row.keyClient === dialog?.id);
  const reset = () => { setQuery(""); setActive(""); setValidated(""); setPage(1); };
  const change = (setter) => (event) => { setter(event.target.value); setPage(1); };
  const open = (type, id, item, tab = "identity") => { data.clearActionError(); setNotice(""); setDialog({ type, id, item, tab }); if (id && data.remote) void data.openDetail(id); };
  const close = () => { if (!data.busy) { setDialog(null); data.clearActionError(); } };
  const back = () => dialog.id ? open("detail", dialog.id, null, dialog.tab) : close();
  const filtered = data.remote ? data.clients : data.clients.filter((row) => [row.raisonSociale, row.refClient, row.keyClient, row.villeClient, row.paysClient].some((value) => normalizeSearch(value).includes(normalizeSearch(query)))
    && (!active || String(row.actif) === active) && (!validated || String(row.valide) === validated));
  const total = data.remote ? data.totalCount : filtered.length;
  const totalPages = Math.max(1, Math.ceil(total / pageSize)), currentPage = Math.min(page, totalPages), start = (currentPage - 1) * pageSize;
  const submit = async (values) => {
    const method = { create: "create", edit: "update", address: "addAddress", bank: "addBank" }[dialog.type];
    const result = await data.execute(method, ...(dialog.type === "create" ? [values] : [dialog.id, values]));
    if (!result) return;
    if (dialog.type === "create") reset();
    const tab = dialog.type === "address" ? "addresses" : dialog.type === "bank" ? "banks" : "identity";
    open("detail", result.keyClient, null, tab); setNotice(data.remote ? "Modification enregistrée." : "Modification enregistrée dans la démonstration.");
  };
  const confirm = async () => {
    const args = [dialog.id];
    if (dialog.type === "removeAddress") args.push(dialog.item.keyAdresse);
    if (dialog.type === "removeBank") args.push(dialog.item.keyBanque);
    const result = await data.execute(dialog.type, ...args);
    if (!result) return;
    if (dialog.type === "remove") { setDialog(null); setNotice(data.remote ? "Client supprimé." : "Client supprimé de la démonstration."); }
    else { open("detail", dialog.id, null, dialog.type === "removeAddress" ? "addresses" : dialog.type === "removeBank" ? "banks" : "identity"); setNotice(data.remote ? "Opération enregistrée." : dialog.type === "validate" ? "Client validé dans la démonstration." : "Élément retiré de la démonstration."); }
  };
  const columns = [{ key: "raisonSociale", header: "Client", render: (value, row) => <Button variant="ghost" onClick={() => open("detail", row.keyClient)}>{value}</Button> },
    { key: "refClient", header: "Référence" }, { key: "villeClient", header: "Ville" }, { key: "paysClient", header: "Pays" },
    { key: "valide", header: "Validation", render: (value) => <Badge variant={value ? "success" : "warning"}>{value ? "Validé" : "À valider"}</Badge> },
    { key: "actif", header: "Activité", render: (value) => <Badge>{value ? "Actif" : "Inactif"}</Badge> },
    { key: "actions", header: "Actions", render: (_, row) => <div className="clients-row-actions">
      <Button onClick={() => open("edit", row.keyClient)} aria-label={`Modifier ${row.raisonSociale}`}>Modifier</Button>
      <Button disabled={row.valide} onClick={() => open("validate", row.keyClient)} aria-label={`Valider ${row.raisonSociale}`}>Valider</Button>
      <Button disabled={Boolean(deletionReason(row))} title={deletionReason(row) || undefined} onClick={() => open("remove", row.keyClient)} aria-label={`Supprimer ${row.raisonSociale}`}>Supprimer</Button>
    </div> }];
  const empty = data.clients.length || query.trim() || active || validated ? <EmptyState title="Aucun résultat" description="Modifiez la recherche ou les filtres." action={<Button onClick={reset}>Réinitialiser les filtres</Button>} />
    : <EmptyState title="Aucun client" description="Créez un premier client pour commencer." action={<Button onClick={() => open("create")}>Créer un client</Button>} />;
  const isForm = ["create", "edit", "address", "bank"].includes(dialog?.type);
  const isConfirmation = ["validate", "remove", "removeAddress", "removeBank"].includes(dialog?.type);
  const blocked = dialog?.type === "remove" && selected ? deletionReason(selected) : null;
  return <section className="clients-page" aria-label="Clients">
    <Card className="clients-header"><div><h1>Clients</h1><p>{data.remote ? "Fiches, adresses et coordonnées bancaires du serveur." : "Fiches, adresses et banques fictives, conservées en mémoire pour cette démonstration."}</p></div><Badge>{data.remote ? "Données serveur" : "Mode démo"}</Badge>
      <div className="clients-header-actions"><Button disabled={data.phase === "loading" || data.busy} onClick={data.reload}>Actualiser</Button>
        <Button variant="primary" disabled={data.phase !== "ready" || data.busy} onClick={() => open("create")}>Créer un client</Button></div>
    </Card>
    <Card className="clients-toolbar"><Input label="Rechercher un client" placeholder={data.remote ? "Nom, référence ou identifiant" : "Nom, référence, ville ou pays"} fieldClassName="clients-search" value={query} onChange={change(setQuery)} />
      <Select label="Activité du client" fieldClassName="clients-filter" value={active} onChange={change(setActive)}><option value="">Tous</option><option value="true">Actifs</option><option value="false">Inactifs</option></Select>
      <Select label="Validation du client" fieldClassName="clients-filter" value={validated} onChange={change(setValidated)}><option value="">Tous</option><option value="true">Validés</option><option value="false">À valider</option></Select>
      <Button onClick={reset}>Réinitialiser les filtres</Button>
    </Card>
    {notice && <Alert title="Opération effectuée" variant="success" onDismiss={() => setNotice("")}>{notice}</Alert>}
    <Card className="clients-list">{data.phase === "loading" ? <LoadingState label="Chargement des clients…" /> : data.phase === "error" ? <ErrorState message={data.listError || "Impossible de charger les clients. Réessayez."} onRetry={data.reload} />
      : <Table columns={columns} rows={data.remote ? filtered : filtered.slice(start, start + pageSize)} rowKey="keyClient" caption="Liste des clients" className="clients-table" emptyState={empty} />}</Card>
    {data.phase === "ready" && <div className="clients-footer"><p aria-live="polite">{total ? `${start + 1}–${Math.min(start + pageSize, total)} sur ${total} client${total > 1 ? "s" : ""}` : "0 client"}</p>
      <Select label="Clients par page" value={pageSize} onChange={(event) => { setPageSize(Number(event.target.value)); setPage(1); }}><option value={5}>5</option><option value={10}>10</option><option value={20}>20</option></Select>
      {total > 0 && <Pagination page={currentPage} totalPages={totalPages} onPageChange={setPage} disabled={data.busy} />}
    </div>}
    <Modal open={Boolean(dialog)} title={titles[dialog?.type] || "Fiche client"} onClose={close} className="clients-dialog"
      actions={isConfirmation ? <><Button disabled={data.busy} onClick={back}>Annuler</Button><Button variant="primary" loading={data.busy} disabled={Boolean(blocked) || data.uncertain || (data.remote && !selected)} onClick={confirm}>Confirmer</Button></>
        : dialog?.type === "detail" ? <Button onClick={close}>Fermer</Button> : undefined}>
      {dialog?.type === "detail" && selected && <ClientDetails key={`${selected.keyClient}:${dialog.tab}`} client={selected} initialTab={dialog.tab} busy={data.busy}
        onAction={(type, item) => open(type, selected.keyClient, item, ["address", "removeAddress"].includes(type) ? "addresses" : ["bank", "removeBank"].includes(type) ? "banks" : "identity")} />}
      {data.remote && dialog?.id && data.detail.phase === "loading" && <LoadingState label="Chargement du client…" />}
      {data.remote && dialog?.id && data.detail.phase === "error" && <ErrorState message={data.detail.error} onRetry={() => data.openDetail(dialog.id)} />}
      {data.uncertain && <ErrorState message={data.actionError} onRetry={data.reload} />}
      {isForm && (dialog.type === "create" || selected) && <ClientForm key={`${dialog.type}:${dialog.id || "new"}`} kind={dialog.type === "address" ? "address" : dialog.type === "bank" ? "bank" : "client"}
        client={dialog.type === "edit" ? selected : null} remote={data.remote} busy={data.busy || data.uncertain} error={data.actionError} onSubmit={submit} onCancel={back} />}
      {isConfirmation && <div className="clients-confirmation"><p>{selected?.raisonSociale}</p>
        {blocked ? <ErrorState title="Suppression impossible" message={blocked} />
          : <p>{dialog.type === "validate" ? "Valider cette fiche ? Un compte comptable est obligatoire. La validation ne modifie pas son activité."
            : dialog.type === "remove" ? "Supprimer ce client ?" : dialog.type === "removeAddress" ? "Retirer cette adresse de livraison ?" : "Retirer ces coordonnées bancaires ?"}</p>}
        {dialog.type === "remove" && selected?.references?.length > 0 && <p>Références : {selected.references.join(", ")}.</p>}
        {data.actionError && <ErrorState message={data.actionError} />}
        {dialog.type === "validate" && data.actionError && <Button disabled={data.busy} onClick={() => open("edit", selected.keyClient)}>Compléter la fiche</Button>}
      </div>}
    </Modal>
  </section>;
}
ClientsPage.propTypes = { repository: PropTypes.object };
