import { useState } from "react";
import PropTypes from "prop-types";
import { Badge, Button, EmptyState, ErrorState, Table } from "../design-system";
import VoyageDossiers from "./VoyageDossiers";
import VoyageCosts from "./VoyageCosts";
import { categoryLabel, natureLabel, status } from "./voyageRules";
export default function VoyageDetails({ row, data, initialTab, onAction, onExecute, onGenerate }) {
  const [tab, setTab] = useState(initialTab || "summary");
  const tabs = [["summary", "Résumé"], ["trajets", "Trajets"], ["equipment", "Équipements"], ["dossiers", "Dossiers associés"], ["costs", "Taux et coûts"]];
  const closed = Boolean(row.dateCloture);
  return <div className="bookings-detail"><div className="bookings-actions"><h3>{row.numVoyage}</h3><Badge variant={closed ? "success" : row.dateVoyage ? "info" : "warning"}>{status(row)}</Badge>{row.isMead && <Badge>Parcours MEAD</Badge>}</div>
    <div className="bookings-actions" aria-label="Sections du voyage">{tabs.map(([key, label]) => <Button key={key} aria-pressed={tab === key} variant={tab === key ? "primary" : "secondary"} onClick={() => { data.clearError(); setTab(key); }}>{label}</Button>)}</div>
    {tab === "summary" && <><dl className="bookings-fields">{[["Catégorie", categoryLabel(row.categorie)], ["Nature", natureLabel(row.nature)], ["Type", row.typeVoyage], ["Provenance", row.provenance], ["Destination", row.destination], ["Départ prévu", row.dateDepartPrevu?.slice(0,10)], ["Arrivée prévue", row.dateArriveePrevu?.slice(0,10)], ["Agence", row.agence], ["Date de validation", row.dateVoyage?.slice(0,10)], ["Date de clôture", row.dateCloture?.slice(0,10)], ["Commentaire", row.commentaire]].map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value || "—"}</dd></div>)}</dl>
      <div className="bookings-actions"><Button disabled={closed || data.busy} onClick={() => onAction("edit", tab)}>Modifier le voyage</Button><Button disabled={closed || data.busy} onClick={() => onAction("validate", tab)}>Valider le voyage</Button><Button disabled={closed || !row.dateVoyage || data.busy} onClick={() => onAction("close", tab)}>Clôturer le voyage</Button><Button disabled={!closed || data.busy} onClick={() => onAction("reopen", tab)}>Rouvrir le voyage</Button></div>{closed && <p>Rouvrez le voyage pour modifier son contenu.</p>}</>}
    {tab === "trajets" && <><Button disabled={closed || data.busy} onClick={() => onAction("trajets", tab)}>Gérer les trajets</Button><Table caption="Trajets du voyage" className="bookings-detail-table" rows={row.trajets} columns={[{ key: "provenance", header: "Provenance" }, { key: "destination", header: "Destination" }, { key: "dateDepart", header: "Départ", render:value=>value?.slice(0,10)||"—" }, { key: "dateArrivee", header: "Arrivée", render:value=>value?.slice(0,10)||"—" }, { key: "tracteur", header: "Tracteur" }, { key: "remorque", header: "Remorque" }, { key: "chauffeur", header: "Chauffeur" }, { key: "kilometrage", header: "Km" }]} emptyState={<EmptyState title="Aucun trajet" />} /></>}
    {tab === "equipment" && <><Button disabled={closed || data.busy} onClick={() => onAction("equipment", tab)}>Gérer les équipements</Button><Table caption="Équipements du voyage" rows={row.equipements} columns={[{ key: "typeEquipement", header: "Type" }, { key: "natureEquipement", header: "Nature" }, { key: "qte", header: "Quantité" }]} emptyState={<EmptyState title="Aucun équipement" />} /></>}
    {tab === "dossiers" && <VoyageDossiers row={row} dossiers={data.dossiers} repository={data.serverPaging?data.repository:undefined} blocked={data.uncertain} busy={data.busy} error={data.error} onExecute={onExecute} />}
    {tab === "costs" && <VoyageCosts row={row} dossiers={data.dossiers} repository={data.serverPaging?data.repository:undefined} generated={data.generated} blocked={data.uncertain} busy={data.busy} onGenerate={onGenerate} />}
    {data.error && tab !== "dossiers" && <ErrorState message={data.error} />}
  </div>;
}
VoyageDetails.propTypes = { row: PropTypes.object.isRequired, data: PropTypes.object.isRequired, initialTab: PropTypes.string, onAction: PropTypes.func.isRequired, onExecute: PropTypes.func.isRequired, onGenerate: PropTypes.func.isRequired };

