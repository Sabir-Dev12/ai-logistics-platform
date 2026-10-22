import PropTypes from "prop-types";
import { Button, Card } from "../../design-system";
import { Menu } from "lucide-react";
import BrandMark from "../../components/BrandMark";

export default function Header({ title, pageTitle, compact, navigationOpen, onOpenNavigation, actions }) {
  return <Card className="app-header">
    <header>
      {compact && <Button onClick={onOpenNavigation} aria-haspopup="dialog"
        aria-expanded={navigationOpen} aria-label="Ouvrir la navigation"><Menu size={18} aria-hidden="true" /></Button>}
      <a className="app-header-brand" href="#home" aria-label="AI LOGISTICS — accueil"><BrandMark /><span className="app-header-title">{title}</span></a>
      {pageTitle && <span className="app-header-context">{pageTitle}</span>}
      {actions && <div className="app-header-actions">{actions}</div>}
    </header>
  </Card>;
}

Header.propTypes = {
  title: PropTypes.node.isRequired,
  pageTitle: PropTypes.node,
  compact: PropTypes.bool.isRequired,
  navigationOpen: PropTypes.bool.isRequired,
  onOpenNavigation: PropTypes.func.isRequired,
  actions: PropTypes.node,
};
