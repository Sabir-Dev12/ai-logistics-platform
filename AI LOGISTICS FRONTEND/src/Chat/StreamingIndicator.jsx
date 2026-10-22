import PropTypes from "prop-types";
import { Spinner } from "../design-system";

export default function StreamingIndicator({ phase }) {
  if (phase === "idle") return null;
  const label = phase === "waiting" ? "L’assistant prépare une réponse…" : "Réponse en cours…";
  return <div className="chat-streaming-indicator"><Spinner label={label} /><span>{label}</span></div>;
}
StreamingIndicator.propTypes = { phase: PropTypes.oneOf(["idle", "waiting", "streaming"]).isRequired };
