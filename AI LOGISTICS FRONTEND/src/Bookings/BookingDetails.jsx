import { useState } from "react";
import PropTypes from "prop-types";
import { Alert, Badge, Button, EmptyState, Table } from "../design-system";
import { bookingStatuses, bookingTypes, linkedVoyage } from "./bookingRules";
import VoyageCapacity from "./VoyageCapacity";

export default function BookingDetails({ booking, data, initialTab = "summary", onAction }) {
  const [tab, setTab] = useState(initialTab), status = bookingStatuses[booking.keyStatutBooking];
  const action = (kind) => onAction(kind, tab);
  const equipmentColumns = [{ key: "typeEquipement", header: "Type" }, { key: "natureEquipement", header: "Nature" }, { key: "qte", header: "Quantité" }];
  const packingColumns = [{ key: "typeColis", header: "Colis" }, { key: "qte", header: "Quantité" }, { key: "poids", header: "Poids" },
    { key: "gerbable", header: "Gerbable", render: (value) => value ? "Oui" : "Non" }, { key: "longueur", header: "Longueur" }, { key: "largeur", header: "Largeur" }, { key: "hauteur", header: "Hauteur" }, { key: "volume", header: "Volume" }];
  const summary = [["Numéro", booking.numBooking], ["Type", bookingTypes.find((type) => type.value === booking.typeBooking)?.label || booking.typeBooking],
    ["Chargement", booking.typeChargement], ["Client / payeur", booking.intituleClient], ["Expéditeur", booking.intituleExpediteur], ["Pays expéditeur", booking.paysExpediteur],
    ["Destinataire", booking.intituleDestinataire], ["Pays destinataire", booking.paysDestinataire], ["Création", booking.dateCreation?.slice(0, 10) || "—"], ["Validité", booking.dateValidite],
    ["Incoterm", booking.incoterm], ["Mode", booking.mode], ["Marchandise", booking.descriptionMarchandise], ["Port de départ", booking.portDepart], ["Départ prévu", booking.dateDepartPrevue],
    ["Disponibilité marchandise", booking.dateDisponibiliteMarchandise], ["Volume prévisionnel (m³)", booking.volumePrevision], ["Total colis", booking.nbTotalColis], ["Commentaire", booking.commentaire], ["Motif d’annulation", booking.commentaireStatutBooking]];
  return <div className="bookings-detail"><div className="bookings-actions"><Badge variant={status?.variant}>{status?.label || booking.keyStatutBooking}</Badge><Badge>{booking.libelleStatutMead || "Sans statut dépotage"}</Badge></div>
    <nav className="bookings-actions" aria-label="Sections de la réservation">{[["summary", "Résumé"], ["lines", "Équipements et colisages"], ["links", "Voyages et dossiers"], ["unloading", "Dépotage"]].map(([key, label]) =>
      <Button key={key} variant={tab === key ? "primary" : "secondary"} aria-pressed={tab === key} onClick={() => setTab(key)}>{label}</Button>)}</nav>
    {tab === "summary" && <><dl className="bookings-fields">{summary.map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value ?? "—"}</dd></div>)}</dl>
      <div className="bookings-actions"><Button onClick={() => action("edit")}>Modifier la réservation</Button><Button onClick={() => action("validate")}>Valider la réservation</Button><Button onClick={() => action("duplicate")}>Dupliquer la réservation</Button>
        <Button disabled={booking.keyStatutBooking === "$BOKAN"} onClick={() => action("cancel")}>Annuler la réservation</Button><Button onClick={() => action("remove")}>Supprimer la réservation</Button></div></>}
    {tab === "lines" && <>{booking.depote && <Alert title="Lignes verrouillées">Annulez le dépotage avant de modifier les équipements ou colisages.</Alert>}
      <section><h3>Équipements</h3><p>Total : {booking.equipements.reduce((sum, line) => sum + Number(line.qte || 0), 0)}</p>
        <Table columns={equipmentColumns} rows={booking.equipements} caption="Équipements réservés" emptyState={<EmptyState title="Aucun équipement" />} />
        <Button disabled={booking.depote} onClick={() => action("equipment")}>Gérer les équipements</Button></section>
      <section><h3>Colisages</h3><Table columns={packingColumns} rows={booking.colisages} caption="Colisages réservés" className="bookings-detail-table" emptyState={<EmptyState title="Aucun colisage" />} />
        <Button disabled={booking.depote} onClick={() => action("packing")}>Gérer les colisages</Button></section>
    </>}
    {tab === "links" && <><section><h3>Voyage associé</h3><p>{data.voyages.find((trip) => trip.keyVoyage === linkedVoyage(booking))?.label || linkedVoyage(booking) || "Aucun voyage associé"}</p>
      <div className="bookings-actions"><Button disabled={Boolean(linkedVoyage(booking)) || booking.keyStatutBooking === "$BOKAN"} onClick={() => action("linkVoyage")}>Associer un voyage</Button>
        <Button disabled={!linkedVoyage(booking)} onClick={() => action("unlinkVoyage")}>Désaffecter le voyage</Button></div>
      {linkedVoyage(booking) && <VoyageCapacity voyages={data.voyages} bookings={data.bookings} selectedId={linkedVoyage(booking)} repository={data.serverPaging ? data.repository : undefined} readOnly />}
    </section><section><h3>Dossier associé</h3><p>{data.dossiers.find((file) => file.keyDossier === booking.keyDossier)?.label || booking.keyDossier || "Aucun dossier associé"}</p>
      <div className="bookings-actions"><Button disabled={Boolean(booking.keyDossier) || booking.keyStatutBooking === "$BOKAN"} onClick={() => action("linkDossier")}>Associer un dossier</Button>
        <Button disabled={!booking.keyDossier} onClick={() => action("unlinkDossier")}>Détacher le dossier</Button></div></section></>}
    {tab === "unloading" && <><dl className="bookings-fields">{[["État", booking.depote ? "Dépoté" : "Non dépoté"], ["Date", booking.dateDebutDepotage], ["Colis dépotés", booking.colisDepote],
      ["Colis sortis", booking.colisSortie], ["Magasin", booking.magasin], ["Bureau de douane", booking.codeBureauDouane], ["Constat", booking.constatDepotageBooking]].map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value ?? "—"}</dd></div>)}</dl>
      <div className="bookings-actions"><Button disabled={booking.depote} onClick={() => action("unload")}>Enregistrer le dépotage</Button><Button disabled={!booking.depote} onClick={() => action("cancelUnload")}>Annuler le dépotage</Button></div></>}
  </div>;
}
BookingDetails.propTypes = { booking: PropTypes.object.isRequired, data: PropTypes.object.isRequired, initialTab: PropTypes.string, onAction: PropTypes.func.isRequired };
