import { useState } from "react";
import PropTypes from "prop-types";
import { Card, ErrorState } from "../design-system";
import ChatHeader from "./ChatHeader";
import MessageList from "./MessageList";
import MessageInput from "./MessageInput";
import StreamingIndicator from "./StreamingIndicator";
import { useChat } from "./useChat";
import { messageType } from "./chatTypes";
import { useChatSignalR } from "../hooks/useChatSignalR";
import { mockMessages, welcomeMessage as mockWelcome } from "./mockChatTransport";
import "./chat.css";

const welcome = { id: "welcome", role: "assistant", status: "complete", content: "Bonjour. Posez votre question à l’assistant logistique." };

export default function ChatPage({ transport, initialMessages }) {
  const connection = useChatSignalR({ enabled: !transport });
  const chat = useChat({ transport: transport || connection.transport,
    initialMessages: initialMessages ?? (transport ? mockMessages : [welcome]),
    resetMessages: [transport ? mockWelcome : welcome] });
  const [conversation, setConversation] = useState(0);
  const busy = chat.phase !== "idle";
  const reset = () => { chat.reset(); setConversation((value) => value + 1); };
  return <section className="chat-page" aria-label="AI Chatbot">
    <ChatHeader onReset={reset} connectionStatus={connection.status} />
    <Card className="chat-thread">
      <MessageList key={conversation} messages={chat.messages} busy={busy} />
      <StreamingIndicator phase={chat.phase} />
    </Card>
    {(connection.error || chat.error) && <ErrorState message={connection.error?.message || chat.error}
      onRetry={connection.ready ? chat.retry : connection.reconnect} />}
    <MessageInput key={conversation} busy={busy} canSend={connection.ready} onSend={chat.send} onStop={chat.stop} />
  </section>;
}
ChatPage.propTypes = {
  transport: PropTypes.shape({ stream: PropTypes.func.isRequired }),
  initialMessages: PropTypes.arrayOf(messageType),
};
