import { useAbout } from "../features/about/aboutApi";
import { messages } from "../i18n/messages";
import { isolate } from "../i18n/isolate";

/** The program name and version at the foot of every screen (the full details are under «حول البرنامج»). */
export function AppFooter() {
  const about = useAbout();
  const text = messages.school.about;
  return <footer className="app-footer">{about.data ? text.footer(isolate(about.data.version)) : text.appName}</footer>;
}
