import { BookOpen, BookPlus, Lock, LogOut, Settings } from "lucide-react";
import { useState } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { SearchField } from "../../components/SearchField";
import { Breadcrumbs } from "../../components/ui/breadcrumbs";
import { Button } from "../../components/ui/button";
import { EmptyState } from "../../components/ui/empty-state";
import { FileUpload } from "../../components/ui/file-upload";
import { Menu } from "../../components/ui/menu";
import { Pagination } from "../../components/ui/pagination";
import { messages } from "../../i18n/messages";
import { createFormatter } from "../../lib/format";
import { guideMessages } from "./guideMessages";
import { GuideSection } from "./GuideSection";

const noop = () => undefined;
const format = createFormatter();

/** Phase 2 primitives: breadcrumbs, user menu, search, pagination, file upload, empty state, conflict alert. */
export function NavigationSection() {
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(2);
  return (
    <GuideSection id="guide-navigation" title={guideMessages.navigation}>
      <Breadcrumbs
        label={messages.school.nav.breadcrumbs}
        items={[{ label: guideMessages.breadcrumbRoot, to: "/design" }, { label: guideMessages.breadcrumbParent, to: "/design" }, { label: guideMessages.breadcrumbCurrent }]}
      />
      <div className="guide-row">
        <Menu
          label={guideMessages.menuLabel}
          trigger={<span>{guideMessages.menuLabel}</span>}
          items={[
            { key: "settings", label: messages.school.nav.settings, icon: <Settings aria-hidden="true" size={20} />, onSelect: noop },
            { key: "lock", label: messages.school.shell.lock, icon: <Lock aria-hidden="true" size={20} />, onSelect: noop },
            { key: "logout", label: messages.app.logout, icon: <LogOut aria-hidden="true" size={20} />, onSelect: noop },
          ]}
        />
        <FileUpload id="guide-upload" label={guideMessages.uploadLabel} accept="image/png" onSelect={noop} />
      </div>
      <SearchField id="guide-search" label={guideMessages.searchLabel} value={search} onChange={setSearch} />
      <Pagination page={page} pageSize={10} total={47} format={format} onPage={setPage} />
      <EmptyState
        icon={<BookOpen aria-hidden="true" size={24} />}
        message={guideMessages.emptyMessage}
        action={<Button icon={<BookPlus aria-hidden="true" size={20} />}>{guideMessages.addSubject}</Button>}
      />
      <ConflictAlert onReload={noop} />
    </GuideSection>
  );
}
