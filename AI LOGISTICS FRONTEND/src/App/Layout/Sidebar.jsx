import PropTypes from "prop-types";
import { Card } from "../../design-system";
import { ArrowUpRight, CalendarDays, CircleUserRound, FolderClosed, LayoutDashboard, MessagesSquare, Orbit, PlugZap, UsersRound, Layers3 } from "lucide-react";

const icons = { "#main-content": LayoutDashboard, "#clients": UsersRound, "#bookings": CalendarDays, "#shipments": FolderClosed, "#voyages": CalendarDays, "#tracking": Layers3, "#quotations": Layers3, "#chat": MessagesSquare, "#knowledge-base": FolderClosed, "#mcp-tools": PlugZap, "#agent": Orbit, "#account": CircleUserRound };

icons["#purchase-scales"] = Layers3;
icons["#sales-invoices"] = Layers3;
icons["#payments"] = Layers3;
icons["#personnel"] = UsersRound;
icons["#equipment"] = Layers3;
icons["#fuel"] = Layers3;
icons["#edi-import"] = Layers3;

export default function Sidebar({ items, activeHref, onNavigate, className = "" }) {
  return <Card className={`app-sidebar ${className}`}>
    <p className="app-sidebar-heading">WORKSPACE</p>
    <nav aria-label="Navigation principale">
      {items.map((item) => { const Icon = icons[item.href] || Layers3; return <a key={item.href} href={item.href}
        className="ds-button app-sidebar-link"
        data-variant={item.href === activeHref ? "primary" : "secondary"}
        aria-current={item.href === activeHref ? "page" : undefined}
        onClick={onNavigate}><Icon size={18} aria-hidden="true" /><span>{item.label}</span></a>; })}
    </nav>
    <div className="app-sidebar-footer"><span>Votre espace intelligent.</span><a href="#home" onClick={onNavigate}>Retour à l’accueil<ArrowUpRight size={14} aria-hidden="true" /></a></div>
  </Card>;
}

Sidebar.propTypes = {
  items: PropTypes.arrayOf(PropTypes.shape({ href: PropTypes.string.isRequired, label: PropTypes.node.isRequired })).isRequired,
  activeHref: PropTypes.string,
  onNavigate: PropTypes.func,
  className: PropTypes.string,
};









icons["#references"] = Layers3;
