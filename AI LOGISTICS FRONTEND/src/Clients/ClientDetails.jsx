import { useState } from "react";
import PropTypes from "prop-types";
import { Badge, Button, EmptyState, Table } from "../design-system";
import { clientGroups } from "./clientFields";
import { deletionReason } from "./clientRules";

export default function ClientDetails({ client, busy, onAction, initialTab = "identity" }) {
  const [tab, setTab] = useState(initialTab);
  const addressColumns = [{ key: "type", header: "Type", render: (value) => <Badge>{value}</Badge> },
    { key: "adresse1", header: "Adresse", render: (_, row) => [row.adresse1, row.adresse2, row.cp, row.ville, row.pays].filter(Boolean).join(", ") },
    { key: "contact", header: "Contact" }, { key: "telAdresse", header: "Téléphone" },
    { key: "actions", header: "Actions", render: (_, row) => row.type?.toLowerCase() === "principal" ? <span>Adresse protégée</span>
      : <Button disabled={busy} onClick={() => onAction("removeAddress", row)}>Retirer l’adresse</Button> }];
  const bankColumns = [{ key: "banque", header: "Banque" }, { key: "compteRib", header: "RIB" }, { key: "codeBanque", header: "Agence" },
    { key: "adresseBanque", header: "Ville" }, { key: "swift", header: "SWIFT" }, { key: "ibanCode", header: "IBAN" },
    { key: "actions", header: "Actions", render: (_, row) => <Button disabled={busy} onClick={() => onAction("removeBank", row)}>Retirer la banque</Button> }];
  return <div className="clients-detail">
    <div className="clients-actions"><Badge variant={client.valide ? "success" : "warning"}>{client.valide ? "Validé" : "À valider"}</Badge><Badge>{client.actif ? "Actif" : "Inactif"}</Badge></div>
    <nav className="clients-actions" aria-label="Sections de la fiche client">{[["identity", "Identité"], ["addresses", "Adresses"], ["banks", "Banques"]].map(([value, label]) =>
      <Button key={value} variant={tab === value ? "primary" : "secondary"} aria-pressed={tab === value} onClick={() => setTab(value)}>{label}</Button>)}</nav>
    {tab === "identity" && <>
      <p>Référence interne : {client.keyClient} · Créé le {client.dateCreation && Number.isFinite(Date.parse(client.dateCreation)) ? new Intl.DateTimeFormat("fr-FR").format(new Date(client.dateCreation)) : "—"}</p>
      {clientGroups.map((group) => <section key={group.title}><h3>{group.title}</h3><dl className="clients-fields">{group.fields.map(([field, label]) => <div key={field}><dt>{label}</dt><dd>{client[field] || "—"}</dd></div>)}</dl></section>)}
      {deletionReason(client) && <p>{deletionReason(client)}{client.references?.length > 0 && ` Références : ${client.references.join(", ")}.`}</p>}
      <div className="clients-actions"><Button onClick={() => onAction("edit")}>Modifier le client</Button><Button disabled={client.valide} onClick={() => onAction("validate")}>Valider le client</Button><Button disabled={busy || Boolean(deletionReason(client))} title={deletionReason(client) || undefined} onClick={() => onAction("remove")}>Supprimer le client</Button></div>
    </>}
    {tab === "addresses" && <><p>L’adresse principale de la fiche se modifie avec le client. Les adresses de livraison peuvent être ajoutées ou retirées.</p>
      <div className="clients-actions"><Button variant="primary" onClick={() => onAction("address")}>Ajouter une adresse</Button></div>
      <Table columns={addressColumns} rows={client.adresses} rowKey="keyAdresse" caption="Adresses du client" className="clients-detail-table" emptyState={<EmptyState title="Aucune adresse complémentaire" />} />
    </>}
    {tab === "banks" && <><p>Coordonnées bancaires du client. Une banque liée bloque la suppression du client.</p>
      <div className="clients-actions"><Button variant="primary" onClick={() => onAction("bank")}>Ajouter une banque</Button></div>
      <Table columns={bankColumns} rows={client.banques} rowKey="keyBanque" caption="Banques du client" className="clients-detail-table" emptyState={<EmptyState title="Aucune coordonnée bancaire" />} />
    </>}
  </div>;
}
ClientDetails.propTypes = { client: PropTypes.object.isRequired, busy: PropTypes.bool, onAction: PropTypes.func.isRequired, initialTab: PropTypes.string };
