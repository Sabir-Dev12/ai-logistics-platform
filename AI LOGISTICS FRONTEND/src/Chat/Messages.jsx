import PropTypes from "prop-types";
import { Card } from "../design-system";
import { messageType } from "./chatTypes";

function Message({ message, sender }) {
  return <Card className="chat-message" role="article" aria-label={`Message de ${sender}`}>
    <div className="chat-message-sender">{sender}</div>
    {message.content && <p>{message.content}</p>}
    {message.status === "cancelled" && <p>Réponse arrêtée.</p>}
    {message.status === "error" && <p>Réponse interrompue.</p>}
  </Card>;
}
Message.propTypes = { message: messageType.isRequired, sender: PropTypes.string.isRequired };

export function UserMessage({ message }) { return <Message message={message} sender="Vous" />; }
export function AssistantMessage({ message }) { return <Message message={message} sender="Assistant" />; }
UserMessage.propTypes = AssistantMessage.propTypes = { message: messageType.isRequired };
