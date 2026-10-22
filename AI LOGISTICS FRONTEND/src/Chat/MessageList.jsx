import { useEffect, useRef } from "react";
import PropTypes from "prop-types";
import { EmptyState, tokens } from "../design-system";
import { AssistantMessage, UserMessage } from "./Messages";
import { messageType } from "./chatTypes";

export default function MessageList({ messages, busy }) {
  const list = useRef(null);
  const follow = useRef(true);
  useEffect(() => {
    if (follow.current && list.current) list.current.scrollTop = list.current.scrollHeight;
  }, [messages]);
  return <div ref={list} className="chat-message-list" role="log" aria-label="Conversation"
    aria-live="polite" aria-relevant="additions" aria-busy={busy} tabIndex={0}
    onScroll={() => {
      const element = list.current;
      follow.current = element.scrollHeight - element.scrollTop - element.clientHeight <= Number.parseInt(tokens.spacing.lg, 10);
    }}>
    {messages.length ? messages.map((message) => message.role === "user"
      ? <UserMessage key={message.id} message={message} />
      : <AssistantMessage key={message.id} message={message} />)
      : <EmptyState title="Commencez une conversation" description="Écrivez votre première question ci-dessous." />}
  </div>;
}
MessageList.propTypes = { messages: PropTypes.arrayOf(messageType).isRequired, busy: PropTypes.bool.isRequired };
