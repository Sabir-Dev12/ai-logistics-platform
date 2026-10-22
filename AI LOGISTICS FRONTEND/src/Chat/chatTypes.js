import PropTypes from "prop-types";

export const messageType = PropTypes.shape({
  id: PropTypes.string.isRequired,
  role: PropTypes.oneOf(["user", "assistant"]).isRequired,
  content: PropTypes.string.isRequired,
  status: PropTypes.oneOf(["complete", "streaming", "cancelled", "error"]).isRequired,
});
