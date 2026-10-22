import PropTypes from "prop-types";
import { Badge } from "../design-system";
import { documentStatuses, documentErrorLabel } from "./documentTypes";

const variants = { indexed: "success", processing: "warning", failed: "error" };
export default function DocumentStatus({ status, remote = false, errorCode }) {
  return <Badge variant={variants[status]} title={remote ? status==="failed" ? documentErrorLabel(errorCode) : "Statut d’indexation renvoyé par le serveur" : "Statut d’indexation simulé"}>{documentStatuses[status]}</Badge>;
}
DocumentStatus.propTypes = { remote:PropTypes.bool, errorCode:PropTypes.string, status: PropTypes.oneOf(Object.keys(documentStatuses)).isRequired };
