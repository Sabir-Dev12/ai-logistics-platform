import { lazy, Suspense, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { LogOut, UserRound } from "lucide-react";
import PropTypes from "prop-types";
import "./index.css";
import Dashboard from "./Dashboard/DashboardPage";
import { createDashboardService } from "./services/dashboardService";
import { AppLayout } from "./App/Layout";
import { Button, LoadingState } from "./design-system";
import ChatPage from "./Chat/ChatPage";
import KnowledgeBasePage from "./KnowledgeBase/ConnectedKnowledgeBasePage";
import McpToolsPage from "./McpTools/ConnectedMcpToolsPage";
import AgentPage from "./Agent/ConnectedAgentPage";
import AccountPage from "./Account/ConnectedAccountPage";
import ClientsPage from "./Clients/ConnectedClientsPage";
import BookingsPage from "./Bookings/ConnectedBookingsPage";
import ShipmentsPage from "./Shipments/ConnectedShipmentsPage";
import VoyagesPage from "./Voyages/ConnectedVoyagesPage";
import TrackingPage from "./Tracking/ConnectedTrackingPage";
import QuotationsPage from "./Quotations/ConnectedQuotationsPage";
import PurchaseScalesPage from "./PurchaseScales/ConnectedPurchaseScalesPage";
import SalesInvoicesPage from "./SalesInvoices/ConnectedSalesInvoicesPage";
import PaymentsPage from "./Payments/ConnectedPaymentsPage";
import PersonnelPage from "./Personnel/ConnectedPersonnelPage";
import EquipmentPage from "./Equipment/ConnectedEquipmentPage";
import FuelPage from "./Fuel/ConnectedFuelPage";
import EdiImportPage from "./Edi/ConnectedEdiImportPage";
import ReferencesPage from "./ReferenceData/ConnectedReferencesPage";
import AuthProvider from "./Auth/AuthProvider";
import ProtectedRoute from "./Auth/ProtectedRoute";
import LoginPage from "./Auth/LoginPage";
import { useAuth } from "./Auth/useAuth";

const navigation = [{ href: "#main-content", label: "Dashboard" }, { href: "#clients", label: "Clients" }, { href: "#bookings", label: "Bookings / Réservations" }, { href: "#shipments", label: "Dossiers / Expéditions" }, { href: "#voyages", label: "Voyages" }, { href: "#tracking", label: "Tracking / Suivi" }, { href: "#quotations", label: "Cotations / Devis" }, { href: "#chat", label: "AI Chatbot" }, { href: "#knowledge-base", label: "Knowledge Base" }, { href: "#mcp-tools", label: "MCP Tools" }, { href: "#agent", label: "AI Agent" }, { href: "#account", label: "Mon compte" }];
navigation.splice(7, 0, { href: "#purchase-scales", label: "Barèmes d’achat" });
navigation.splice(8, 0, { href: "#sales-invoices", label: "Factures de vente" });
navigation.splice(9, 0, { href: "#payments", label: "Règlements" });
navigation.splice(10, 0, { href: "#personnel", label: "Personnel" });
navigation.splice(11, 0, { href: "#equipment", label: "Matériel" });
navigation.splice(12, 0, { href: "#fuel", label: "Carburant" });
navigation.splice(13, 0, { href: "#edi-import", label: "Import EDI / STC" });
navigation.splice(14, 0, { href: "#references", label: "Référentiels / Paramètres" });
const HomePage = lazy(() => import("./Home/HomePage"));
const validDestination = (hash) => ["#home", "#home-content"].includes(hash) ? "#home" : navigation.some((item) => item.href === hash) ? hash : "#main-content";
const currentPage = () => !window.location.hash || ["#home", "#home-content"].includes(window.location.hash) ? "home" : window.location.hash === "#login" ? "login" : validDestination(window.location.hash).slice(1);

function Application() {
  const auth = useAuth();
  const dashboardRepository = useMemo(() => createDashboardService({ request: auth.request }), [auth.request]);
  const [page, setPage] = useState(currentPage);
  const returnTo = useRef(validDestination(window.location.hash || "#home"));
  useEffect(() => {
    const update = () => setPage(currentPage());
    window.addEventListener("hashchange", update);
    return () => window.removeEventListener("hashchange", update);
  }, []);
  const redirectLogin = useCallback(() => {
    returnTo.current = validDestination(window.location.hash || "#home");
    window.history.replaceState(null, "", "#login"); setPage("login");
  }, []);
  const redirectAuthenticated = useCallback(() => {
    const target = validDestination(returnTo.current);
    window.history.replaceState(null, "", target); setPage(target.slice(1));
  }, []);
  if (page === "home") return <ProtectedRoute onRedirect={redirectLogin}><Suspense fallback={<main className="home-loading"><LoadingState label="Chargement…" /></main>}><HomePage repository={dashboardRepository} navigation={navigation} displayName={auth.user?.displayName} onLogout={auth.logout} /></Suspense></ProtectedRoute>;
  if (page === "login") return <LoginPage onAuthenticated={redirectAuthenticated} />;
  return <ProtectedRoute onRedirect={redirectLogin}><AppLayout navigation={navigation} activeHref={`#${page}`}
    headerActions={<><span className="app-user"><span className="app-user-icon"><UserRound size={16} aria-hidden="true" /></span>{auth.user?.displayName}</span><Button onClick={auth.logout}><LogOut size={14} aria-hidden="true" />Se déconnecter</Button></>}>
    {page === "references" ? <ReferencesPage /> : page === "edi-import" ? <EdiImportPage /> : page === "fuel" ? <FuelPage /> : page === "equipment" ? <EquipmentPage /> : page === "personnel" ? <PersonnelPage /> : page === "payments" ? <PaymentsPage /> : page === "sales-invoices" ? <SalesInvoicesPage /> : page === "purchase-scales" ? <PurchaseScalesPage /> : page === "quotations" ? <QuotationsPage /> : page === "tracking" ? <TrackingPage author={auth.user?.displayName} /> : page === "voyages" ? <VoyagesPage /> : page === "shipments" ? <ShipmentsPage /> : page === "bookings" ? <BookingsPage /> : page === "clients" ? <ClientsPage /> : page === "account" ? <AccountPage /> : page === "agent" ? <AgentPage /> : page === "mcp-tools" ? <McpToolsPage /> : page === "knowledge-base" ? <KnowledgeBasePage /> : page === "chat" ? <ChatPage /> : <Dashboard repository={dashboardRepository} />}
  </AppLayout></ProtectedRoute>;
}

export default function App({ authStore }) { return <AuthProvider store={authStore}><Application /></AuthProvider>; }
App.propTypes = { authStore: PropTypes.object };











