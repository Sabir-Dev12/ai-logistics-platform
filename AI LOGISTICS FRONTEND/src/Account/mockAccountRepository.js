import { tokens } from "../design-system/tokens";

export const mockIdentity = { displayName: "Utilisateur de démonstration", identifier: "demo.utilisateur", companyName: "Logistics Démonstration", companyKey: "S01" };
// Public fictional fixture, never used by authentication or sent to an API.
export const demoCurrentPassword = "DemoActuel123!";
export class AccountDemoError extends Error { constructor(code) { super(code); this.code = code; } }
function wait(signal) {
  return new Promise((resolve, reject) => {
    if (signal?.aborted) { reject(new DOMException("Aborted", "AbortError")); return; }
    const abort = () => { clearTimeout(timer); reject(new DOMException("Aborted", "AbortError")); };
    const timer = setTimeout(() => { signal?.removeEventListener("abort", abort); resolve(); }, Number.parseInt(tokens.motion.layout, 10));
    signal?.addEventListener("abort", abort, { once: true });
  });
}
export function createMockAccountRepository({ profile = mockIdentity, failLoadOnce = false, failChangeOnce = false } = {}) {
  let connected = true; let currentPassword = demoCurrentPassword;
  return {
    async getProfile({ signal } = {}) {
      await wait(signal);
      if (failLoadOnce) { failLoadOnce = false; throw new AccountDemoError("unavailable"); }
      return connected && profile ? { ...profile } : null;
    },
    async changePassword({ oldPassword, newPassword }, { signal } = {}) {
      await wait(signal);
      if (!connected) throw new AccountDemoError("disconnected");
      if (failChangeOnce) { failChangeOnce = false; throw new AccountDemoError("unavailable"); }
      if (oldPassword !== currentPassword) throw new AccountDemoError("old_password_invalid");
      currentPassword = newPassword;
    },
    async logout({ signal } = {}) { await wait(signal); connected = false; currentPassword = null; },
    resetDemo() { connected = true; currentPassword = demoCurrentPassword; },
  };
}
