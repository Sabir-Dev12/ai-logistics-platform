import PropTypes from "prop-types";
import { Badge, Button, EmptyState, Table } from "../design-system";
import { billingStatuses, shipmentTypes } from "./shipmentRules";
export default function ShipmentDetails({ row, voyages, busy, onAction }) {
  const values = [["Client", row.intituleClient], ["Type", shipmentTypes.find((item) => item.value === row.typeDossier)?.label || row.typeDossier], ["Voyage", voyages.find((item) => item.id === row.keyVoyageInter)?.label || row.keyVoyageInter], ["Création", row.dateCreation], ["Marchandise", row.descriptionMarchandise], ["Incoterm", row.incoterm], ["Nombre de colis", row.nbColis], ["Poids brut (kg)", row.poidsBrut], ["Poids taxable (kg)", row.poidsTaxable], ["Volume (m³)", row.volume], ["Magasin de douane", row.magasinDouane]];
  return <div className="bookings-detail"><h3>{row.numDossier}</h3><div className="bookings-actions"><Badge variant={billingStatuses[row.statutFacturation]?.variant}>{billingStatuses[row.statutFacturation]?.label}</Badge><Badge variant={row.dateDepotage ? "success" : "info"}>{row.dateDepotage ? "Dépoté" : "Non dépoté"}</Badge></div>
    <dl className="bookings-fields">{values.map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value === "" || value == null ? "—" : value}</dd></div>)}</dl>
    <div className="bookings-actions"><Button disabled={busy} onClick={() => onAction("edit")}>Modifier le dossier</Button><Button disabled={busy} onClick={() => onAction("packing")}>Gérer les colisages</Button>
      <Button disabled={busy || !row.magasinDouane?.trim() || Boolean(row.dateDepotage)} onClick={() => onAction("unload")}>Enregistrer le dépotage</Button><Button disabled={busy || !row.dateDepotage} onClick={() => onAction("cancelUnload")}>Annuler le dépotage</Button>
      <Button disabled={busy || row.statutFacturation === "Facture"} onClick={() => onAction("remove")}>Supprimer le dossier</Button></div>
    {row.statutFacturation === "Facture" && <p>Un dossier facturé ne peut pas être supprimé.</p>}{!row.magasinDouane?.trim() && <p>Le dépotage est réservé aux dossiers MEAD.</p>}
    <h3>Dépotage</h3><p>{row.dateDepotage ? `${row.dateDepotage} · ${row.lieuDepotage === "Tc" ? "Conteneur (TC)" : "Remorque"} · ${row.colisDepote ?? 0} colis dépotés · ${row.colisSortie ?? 0} colis sortis` : "Aucun dépotage enregistré."}</p>
    {row.refDepotage && <p>Référence : {row.refDepotage}</p>}{row.constatDepotage && <p>Constat : {row.constatDepotage}</p>}
    <Table caption="Colisages du dossier" rows={row.colisages} rowKey="id" className="bookings-detail-table" columns={[{ key: "typeColis", header: "Type de colis" }, { key: "nbColis", header: "Colis" }, { key: "natureMarchandise", header: "Marchandise" }, { key: "poidsBrut", header: "Poids (kg)" }, { key: "volume", header: "Volume (m³)" }, { key: "marque", header: "Marque" }, { key: "codeNgp", header: "Code NGP" }]} emptyState={<EmptyState title="Aucun colisage" />} />
  </div>;
}
ShipmentDetails.propTypes = { row: PropTypes.object.isRequired, voyages: PropTypes.array.isRequired, busy: PropTypes.bool, onAction: PropTypes.func.isRequired };

