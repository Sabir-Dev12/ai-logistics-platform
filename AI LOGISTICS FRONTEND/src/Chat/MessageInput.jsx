import { useState } from "react";
import PropTypes from "prop-types";
import { Button, Card, Input } from "../design-system";

export default function MessageInput({ busy, canSend = true, onSend, onStop }) {
  const [draft, setDraft] = useState("");
  const submit = (event) => {
    event.preventDefault();
    if (!busy && canSend && draft.trim() && onSend(draft)) setDraft("");
  };
  return <Card className="chat-composer">
    <form onSubmit={submit} aria-label="Envoyer un message">
      <Input label="Votre message" placeholder="Posez votre question logistique…" value={draft}
        onChange={(event) => setDraft(event.target.value)} fieldClassName="chat-composer-field"
        onKeyDown={(event) => { if (event.key === "Enter" && event.nativeEvent.isComposing) event.preventDefault(); }} />
      <div className="chat-composer-actions">
        <Button type="submit" variant="primary" disabled={busy || !canSend || !draft.trim()}>Envoyer</Button>
        {busy && <Button onClick={onStop}>Arrêter la réponse</Button>}
      </div>
    </form>
  </Card>;
}
MessageInput.propTypes = { busy: PropTypes.bool.isRequired, canSend: PropTypes.bool, onSend: PropTypes.func.isRequired, onStop: PropTypes.func.isRequired };
