import PropTypes from "prop-types";
import { Badge, Button, Card } from "../design-system";

const labels = { demo: "Mode démo", connecting: "Connexion…", connected: "Connecté", reconnecting: "Reconnexion…",
  disconnected: "Déconnecté", failed: "Connexion indisponible", unauthorized: "Session requise" };

export default function ChatHeader({ onReset, connectionStatus = "demo" }) {
  return <Card className="chat-header">
    <div><h1>Assistant logistique</h1><p>{connectionStatus === "demo" ? "Conversation de démonstration" : "Conversation avec l’assistant"}</p></div>
    <Badge>{labels[connectionStatus]}</Badge>
    <Button onClick={onReset}>Nouvelle conversation</Button>
  </Card>;
}
ChatHeader.propTypes = { onReset: PropTypes.func.isRequired, connectionStatus: PropTypes.oneOf(Object.keys(labels)) };
