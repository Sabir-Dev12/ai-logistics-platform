import PropTypes from "prop-types";

export default function MainContent({ children, id = "main-content", className = "" }) {
  return <main id={id} tabIndex={-1} aria-label="Contenu principal" className={`app-main-content ${className}`}>
    {children}
  </main>;
}

MainContent.propTypes = { children: PropTypes.node, id: PropTypes.string, className: PropTypes.string };
