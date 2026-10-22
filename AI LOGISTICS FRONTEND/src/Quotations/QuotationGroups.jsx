import { useEffect, useState } from "react";
import PropTypes from "prop-types";
import { Badge, Button, EmptyState, Input, Select, Table } from "../design-system";
import { quotationStatuses } from "./quotationRules";
export default function QuotationGroups({ row, data, onSubmit, onOpen }) {
  const [groupId, setGroup] = useState(row.groupId || ""), [newGroupName, setName] = useState("");
  useEffect(() => { setGroup(row.groupId || ""); }, [row.groupId]);
  if (data.remote) return <div className="bookings-form"><Input label="Référence du regroupement" value={groupId} disabled={data.busy || data.uncertain} onChange={event=>setGroup(event.target.value)} hint="Référence existante ; laisser vide pour détacher. La création et la liste des regroupements ne sont pas exposées par cette API." /><Button variant="primary" disabled={data.busy || data.uncertain} onClick={()=>onSubmit({groupId})}>Enregistrer le regroupement</Button>{data.error && <p role="alert">{data.error}</p>}</div>;
  return <div className="bookings-form"><Select label="Regroupement du devis" disabled={data.busy} value={groupId} onChange={(event) => { setGroup(event.target.value); setName(""); }}><option value="">Aucun regroupement</option>{data.groups.map((item) => <option key={item.id} value={item.id}>{item.label}</option>)}</Select><Input label="Nouveau regroupement" disabled={data.busy} value={newGroupName} onChange={(event) => setName(event.target.value)} hint="Laissez vide pour conserver le regroupement sélectionné." />
    <Button loading={data.busy} variant="primary" onClick={async () => { if (await onSubmit({ groupId, newGroupName })) setName(""); }}>Enregistrer le regroupement</Button>
    <Table caption="Cotations du regroupement enregistré" rows={row.groupId ? data.quotations.filter((item) => item.groupId === row.groupId) : []} columns={[{ key: "numCotation", header: "Cotation", render: (value, item) => <Button variant="ghost" onClick={() => onOpen(item.id)}>{value}</Button> }, { key: "keyClient", header: "Client", render: (value) => data.clients.find((item) => item.id === value)?.label || "—" }, { key: "statut", header: "Statut", render: (value) => <Badge variant={quotationStatuses[value]}>{value}</Badge> }]} emptyState={<EmptyState title="Aucun regroupement associé" />} />
  </div>;
}
QuotationGroups.propTypes = { row: PropTypes.object.isRequired, data: PropTypes.object.isRequired, onSubmit: PropTypes.func.isRequired, onOpen: PropTypes.func.isRequired };
