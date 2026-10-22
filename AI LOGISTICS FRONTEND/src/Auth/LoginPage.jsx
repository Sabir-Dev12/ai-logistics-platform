import { useEffect, useState } from "react";
import PropTypes from "prop-types";
import { Button, Card, ErrorState, Input, LoadingState } from "../design-system";
import { useAuth } from "./useAuth";
import "./auth.css";
import { ArrowLeft, ArrowRight, Boxes, CalendarDays, Sparkles } from "lucide-react";
import BrandMark from "../components/BrandMark";
export default function LoginPage({ onAuthenticated }) {
  const auth = useAuth();
  const [identifiant, setIdentifiant] = useState("");
  const [motDePasse, setMotDePasse] = useState("");
  const busy = auth.status === "loading";
  useEffect(() => { if (auth.status === "authenticated") onAuthenticated(); }, [auth.status, onAuthenticated]);
  const submit = async (event) => {
    event.preventDefault(); if (busy) return;
    const password = motDePasse; setMotDePasse("");
    await auth.login({ identifiant, motDePasse: password });
  };
  return <main className="auth-page" id="main-content"><aside className="auth-intro" aria-label="Présentation"><a className="auth-intro-brand" href="#home"><BrandMark />AI LOGISTICS</a>
    <div><span className="auth-eyebrow">UN ESPACE. TOUTES VOS OPÉRATIONS.</span><p className="auth-intro-title">Vos opérations,<br /><em>une longueur d’avance.</em></p><p className="auth-intro-description">Clients, réservations et intelligence artificielle, réunis dans votre espace de travail.</p>
      <div className="auth-workflows"><span><Boxes size={18} aria-hidden="true" />Clients</span><ArrowRight size={14} aria-hidden="true" /><span><CalendarDays size={18} aria-hidden="true" />Réservations</span><ArrowRight size={14} aria-hidden="true" /><span><Sparkles size={18} aria-hidden="true" />AI</span></div>
    </div><a href="#home" className="auth-back"><ArrowLeft size={14} aria-hidden="true" />Retour à l’accueil</a>
  </aside><div className="auth-form-area"><Card className="auth-card"><div className="auth-card-brand"><BrandMark /><h1>AI LOGISTICS</h1></div><h2>Connexion</h2><p>Connectez-vous pour accéder à l’application.</p>
    <form onSubmit={submit} className="auth-form">
      <Input label="Identifiant" autoComplete="username" required error={auth.fieldErrors?.identifiant} value={identifiant} disabled={busy} onChange={(event) => setIdentifiant(event.target.value)} />
      <Input label="Mot de passe" type="password" autoComplete="current-password" required error={auth.fieldErrors?.motDePasse} value={motDePasse} disabled={busy} onChange={(event) => setMotDePasse(event.target.value)} />
      {auth.error && <ErrorState title={auth.status === "unauthorized" ? "Accès non autorisé" : "Connexion impossible"} message={auth.error} />}
      {busy && <LoadingState label="Connexion en cours…" />}
      <Button type="submit" variant="primary" loading={busy} disabled={!identifiant.trim() || !motDePasse}>Se connecter<ArrowRight size={16} aria-hidden="true" /></Button>
      {auth.retryable && <p role="status">Ressaisissez votre mot de passe puis cliquez sur « Se connecter » pour réessayer.</p>}
    </form>
    <a className="auth-mobile-back" href="#home"><ArrowLeft size={14} aria-hidden="true" />Retour à l’accueil</a>
  </Card></div></main>;
}
LoginPage.propTypes = { onAuthenticated: PropTypes.func.isRequired };

