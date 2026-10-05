import { ChevronDown, Lock, LogOut, Settings, UserRound } from "lucide-react";
import { useNavigate } from "react-router-dom";
import { LtrText } from "../components/ui/ltr-text";
import { Menu } from "../components/ui/menu";
import { useLock, useLogout } from "../features/auth/useLogout";
import { messages } from "../i18n/messages";

/** User menu in the top bar: settings, lock screen, logout. */
export function UserMenu({ username, onError }: { username: string | null; onError: (reason: unknown) => void }) {
  const navigate = useNavigate();
  const logout = useLogout(onError);
  const lock = useLock(username, onError);
  return (
    <Menu
      label={messages.school.shell.userMenu}
      trigger={(
        <>
          <UserRound aria-hidden="true" size={18} strokeWidth={2} />
          <span className="user-menu-role">{messages.school.shell.ownerRole}</span>
          {username && <LtrText className="user-menu-name">{username}</LtrText>}
          <ChevronDown aria-hidden="true" size={16} strokeWidth={2} />
        </>
      )}
      items={[
        { key: "settings", label: messages.school.nav.settings, icon: <Settings aria-hidden="true" size={20} />, onSelect: () => navigate("/settings") },
        { key: "lock", label: messages.school.shell.lock, icon: <Lock aria-hidden="true" size={20} />, onSelect: () => lock.mutate() },
        { key: "logout", label: messages.app.logout, icon: <LogOut aria-hidden="true" size={20} />, onSelect: () => logout.mutate() },
      ]}
    />
  );
}
