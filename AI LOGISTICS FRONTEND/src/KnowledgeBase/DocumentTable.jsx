import PropTypes from "prop-types";
import { Button, Table } from "../design-system";
import DocumentStatus from "./DocumentStatus";
import { documentShape, formatDate, formatSize } from "./documentTypes";

export default function DocumentTable({ documents, loading, busy, emptyState, onDelete, remote = false }) {
  const columns = [
    { key: "name", header: "Document", render: (name) => <span className="kb-document-name">{name}</span> },
    { key: "type", header: "Type" },
    { key: "size", header: "Taille", render: formatSize },
    { key: "date", header: "Date d’ajout", render: formatDate },
    { key: "status", header: "Statut", render: (status,row) => <DocumentStatus status={status} remote={remote} errorCode={row.errorCode || undefined} /> },
    { key: "actions", header: "Actions", render: (_, document) => <Button disabled={busy} onClick={() => onDelete(document)} aria-label={`Supprimer ${document.name}`}>Supprimer</Button> },
  ];
  return <Table className="kb-document-table" caption="Documents de la Knowledge Base" columns={columns}
    rows={documents} loading={loading} emptyState={emptyState} />;
}
DocumentTable.propTypes = { documents: PropTypes.arrayOf(documentShape).isRequired, loading: PropTypes.bool.isRequired,
  busy: PropTypes.bool.isRequired, remote:PropTypes.bool, emptyState: PropTypes.node, onDelete: PropTypes.func.isRequired };
