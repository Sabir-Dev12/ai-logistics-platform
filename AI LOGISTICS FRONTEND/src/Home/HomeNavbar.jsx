import { useState } from "react";
import PropTypes from "prop-types";
import { LogOut, Menu } from "lucide-react";
import { Button, Modal } from "../design-system";
import BrandMark from "../components/BrandMark";

export default function HomeNavbar({ navigation, displayName, onLogout }) {
  const [mobileOpen, setMobileOpen] = useState(false);
  const links = () => navigation.map((item) => <a key={item.href} href={item.href === "#main-content" ? "#home" : item.href}
    aria-current={item.href === "#main-content" ? "page" : undefined} onClick={() => setMobileOpen(false)}>{item.label}</a>);
  return <><header className="home-navbar">
    <a className="home-brand" href="#home" aria-label="Accueil AI LOGISTICS"><BrandMark /><span>AI LOGISTICS</span></a>
    <nav className="home-nav-links" aria-label="Pages de l’application">{links()}</nav>
    <div className="home-nav-actions">
      {displayName && <span className="home-user" title={displayName}>{displayName}</span>}
      <Button className="home-logout" onClick={onLogout} aria-label="Se déconnecter"><LogOut size={18} aria-hidden="true" /></Button>
      <button className="home-mobile-toggle" aria-label="Ouvrir les pages" aria-haspopup="dialog" aria-expanded={mobileOpen} onClick={() => setMobileOpen(true)}><Menu size={20} aria-hidden="true" /><span>Pages</span></button>
    </div>
  </header>
    <Modal open={mobileOpen} title="Pages de l’application" onClose={() => setMobileOpen(false)}><nav className="home-mobile-links" aria-label="Pages sur mobile">{links()}</nav></Modal>
  </>;
}
HomeNavbar.propTypes = {
  navigation: PropTypes.arrayOf(PropTypes.shape({ href: PropTypes.string.isRequired, label: PropTypes.string.isRequired })).isRequired,
  displayName: PropTypes.string,
  onLogout: PropTypes.func.isRequired,
};
