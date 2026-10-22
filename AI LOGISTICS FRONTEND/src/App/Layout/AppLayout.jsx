import { useEffect, useState } from "react";
import PropTypes from "prop-types";
import { Modal, tokens } from "../../design-system";
import Sidebar from "./Sidebar";
import Header from "./Header";
import MainContent from "./MainContent";
import "./layout.css";

const desktopQuery = `(min-width: ${tokens.grid.breakpoints.sm}px)`;

function useCompactNavigation() {
  const [compact, setCompact] = useState(() => !window.matchMedia(desktopQuery).matches);
  useEffect(() => {
    const media = window.matchMedia(desktopQuery);
    const update = () => setCompact(!media.matches);
    update();
    media.addEventListener("change", update);
    return () => media.removeEventListener("change", update);
  }, []);
  return compact;
}

export default function AppLayout({ children, navigation, activeHref, title = "AI LOGISTICS", headerActions }) {
  const compact = useCompactNavigation();
  const [navigationOpen, setNavigationOpen] = useState(false);
  useEffect(() => { setNavigationOpen(false); }, [compact]);
  return <div className="app-layout" data-navigation={compact ? "compact" : "expanded"}>
    <a className="ds-button app-skip-link" href="#main-content" onClick={(event) => {
      event.preventDefault();
      document.getElementById("main-content")?.focus();
    }}>Aller au contenu principal</a>
    <Header title={title} pageTitle={navigation.find((item) => item.href === activeHref)?.label} compact={compact} navigationOpen={compact && navigationOpen}
      onOpenNavigation={() => setNavigationOpen(true)} actions={headerActions} />
    {!compact && <Sidebar items={navigation} activeHref={activeHref} />}
    <MainContent>{children}</MainContent>
    <Modal open={compact && navigationOpen} onClose={() => setNavigationOpen(false)} title="Navigation">
      <Sidebar items={navigation} activeHref={activeHref} onNavigate={() => setNavigationOpen(false)} />
    </Modal>
  </div>;
}

AppLayout.propTypes = {
  children: PropTypes.node,
  navigation: Sidebar.propTypes.items,
  activeHref: PropTypes.string,
  title: PropTypes.node,
  headerActions: PropTypes.node,
};
