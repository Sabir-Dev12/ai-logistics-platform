import PropTypes from "prop-types";
import AgentPage from "./AgentPage";
import {useAgentConversation} from "./useAgentConversation";
export default function ConnectedAgentPage({transport,signalROptions}){
 const session=useAgentConversation({transport,signalROptions});
 return <AgentPage session={session}/>;
}
ConnectedAgentPage.propTypes={transport:PropTypes.shape({stream:PropTypes.func.isRequired}),signalROptions:PropTypes.object};
