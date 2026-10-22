import { useState } from "react";
import PropTypes from "prop-types";
import { Alert, Badge, Button, Card, EmptyState, ErrorState, Input, LoadingState, Modal } from "../design-system";
import { useAccount } from "./useAccount";
import "./account.css";

export default function AccountPage({ repository }) {
  const account = useAccount(repository);
  const [oldPassword, setOldPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmation, setConfirmation] = useState("");
  const [validation, setValidation] = useState({});
  const [success, setSuccess] = useState(false);
  const [logoutOpen, setLogoutOpen] = useState(false);
  const clearFields = () => { setOldPassword(""); setNewPassword(""); setConfirmation(""); setValidation({}); };
  const submit = async (event) => {
    event.preventDefault(); if (account.busy) return;
    const errors = {};
    if (!oldPassword) errors.oldPassword = "L’ancien mot de passe est obligatoire.";
    if (!newPassword.trim() || newPassword.length < 8 || newPassword.length > 200) errors.newPassword = "Utilisez entre 8 et 200 caractères.";
    if (confirmation !== newPassword) errors.confirmation = "Les mots de passe ne correspondent pas.";
    setValidation(errors); setSuccess(false); account.clearError();
    if (Object.keys(errors).length) return;
    const payload = { oldPassword, newPassword }; clearFields();
    if (await account.changePassword(payload)) setSuccess(true);
  };
  const logout = async () => {
    clearFields(); setSuccess(false);
    if (await account.logout()) setLogoutOpen(false);
  };
  const edit = (setter) => (event) => { setter(event.target.value); setValidation({}); setSuccess(false); account.clearError(); };
  const busy = Boolean(account.busy);
  return <section className="account-page" aria-label="Mon compte">
    <Card className="account-header"><div><h1>Mon compte</h1><p>Votre identité, votre société et votre session.</p></div><Badge>Compte</Badge></Card>
    {account.phase === "loading" ? <Card><LoadingState label="Chargement du compte…" /></Card>
      : account.phase === "error" ? <ErrorState message={account.error?.message || "Impossible de charger le compte. Réessayez."} onRetry={account.reload} />
        : account.phase === "disconnected" ? <Card><EmptyState title="Déconnecté" description="Votre session est fermée. Reconnectez-vous pour continuer."
          action={<a href="#login">Revenir à la connexion</a>} /></Card>
          : !account.profile ? <Card><EmptyState title="Aucune identité disponible" description="Le backend n’a renvoyé aucune identité." /></Card>
            : <>
              <Card className="account-panel"><div className="account-panel-header"><h2>Identité connectée</h2><Badge variant="success">Connecté</Badge></div>
                <div className="account-identity">
                  <Input label="Nom affiché" readOnly value={account.profile.displayName} />
                  <Input label="Identifiant" readOnly value={account.profile.identifier} />
                  <Input label="Société" readOnly value={account.profile.companyName} />
                </div>
              </Card>
              {account.profile.companyWarning && <ErrorState message={account.profile.companyWarning} onRetry={account.reload} />}
              <Card className="account-panel"><h2>Changer le mot de passe</h2><p>Votre ancien mot de passe sera vérifié par le serveur.</p>
                <form onSubmit={submit} className="account-form">
                  <Input label="Ancien mot de passe" type="password" autoComplete="current-password" required disabled={busy} value={oldPassword} onChange={edit(setOldPassword)}
                    error={validation.oldPassword || (account.error?.code === "old_password_invalid" ? account.error.message : undefined)} />
                  <Input label="Nouveau mot de passe" type="password" autoComplete="new-password" required disabled={busy} value={newPassword} onChange={edit(setNewPassword)} hint="Entre 8 et 200 caractères." error={validation.newPassword} />
                  <Input label="Confirmer le nouveau mot de passe" type="password" autoComplete="new-password" required disabled={busy} value={confirmation} onChange={edit(setConfirmation)} error={validation.confirmation} />
                  {account.error && account.error.code !== "old_password_invalid" && !logoutOpen && <ErrorState message={account.error.message} />}
                  {success && <Alert variant="success" title="Mot de passe modifié">Votre nouveau mot de passe a été enregistré.</Alert>}
                  <div className="account-actions"><Button type="submit" variant="primary" loading={account.busy === "password"} disabled={busy || !oldPassword || !newPassword || !confirmation}>Modifier le mot de passe</Button></div>
                </form>
              </Card>
              <Card className="account-panel"><h2>Session</h2><p>Fermez votre session sur cet appareil.</p>
                <div className="account-actions"><Button disabled={busy} onClick={() => { account.clearError(); setLogoutOpen(true); }}>Se déconnecter</Button></div>
              </Card>
            </>}
    <Modal open={logoutOpen} title="Déconnexion" onClose={() => { if (!busy) { setLogoutOpen(false); account.clearError(); } }}
      actions={<><Button disabled={busy} onClick={() => { setLogoutOpen(false); account.clearError(); }}>Annuler</Button><Button variant="primary" loading={account.busy === "logout"} disabled={busy} onClick={logout}>Confirmer la déconnexion</Button></>}>
      <p>Voulez-vous fermer votre session sur cet appareil ?</p>
      {account.error && <ErrorState message={account.error.message} />}
    </Modal>
  </section>;
}
AccountPage.propTypes = { repository: PropTypes.shape({ getProfile: PropTypes.func.isRequired, changePassword: PropTypes.func.isRequired, logout: PropTypes.func.isRequired }).isRequired };
