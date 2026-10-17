import { Download, KeyRound, QrCode as QrIcon, Smartphone } from "lucide-react";
import { useLayoutEffect } from "react";
import { createPortal } from "react-dom";
import { messages } from "../../i18n/messages";
import { isolate } from "../../i18n/isolate";
import { useFormatter } from "../../lib/schoolContext";
import { localDateTime } from "../history/historyPresentation";
import { QrCode } from "./QrCode";
import type { TeacherCards } from "./sharingApi";

const text = messages.school.sharing.cards;
const qrText = messages.school.sharing;

/** Four cards on each A4 page (two by two); a card is never split across pages. */
export const cardsPerPage = 4;

/** A4 portrait without a browser margin (the page pads itself), as the timetable printing does (ADR 0054). */
function usePageRule() {
  useLayoutEffect(() => {
    if (typeof CSSStyleSheet === "undefined" || !("adoptedStyleSheets" in document)) return undefined;
    const pageStyle = new CSSStyleSheet();
    pageStyle.replaceSync("@page { size: A4 portrait; margin: 0; }");
    document.adoptedStyleSheets = [...document.adoptedStyleSheets, pageStyle];
    return () => { document.adoptedStyleSheets = document.adoptedStyleSheets.filter((item) => item !== pageStyle); };
  }, []);
}

/**
 * «بطاقات المعلمين»: what `window.print()` prints while it is mounted (the rest of the page is hidden in print). Each card: the
 * teacher's name, the three steps with their codes (download the app, pair with the school, scan your timetable at 6 cm), the
 * version and date, and the key fingerprint the phone shows when pairing.
 */
export function TeacherCardsPrint({ cards }: { cards: TeacherCards }) {
  usePageRule();
  const format = useFormatter();
  const approved = localDateTime(cards.approvedAt, format.preferences.timeZone);
  const version = text.version(format.number(cards.versionNumber), format.date(approved.date));
  const pages: TeacherCards["cards"][] = [];
  for (let index = 0; index < cards.cards.length; index += cardsPerPage) pages.push(cards.cards.slice(index, index + cardsPerPage));
  return createPortal(
    <div className="print-document cards-document" data-pages={pages.length}>
      {pages.map((page, pageIndex) => (
        <section key={page[0].teacherId} className="cards-page">
          {page.map((card) => (
            <article key={card.teacherId} className="teacher-card">
              <header className="teacher-card-header">
                <span className="teacher-card-school">{cards.schoolName}</span>
                <strong className="teacher-card-name">{card.name}</strong>
                <span className="teacher-card-title"><Smartphone aria-hidden="true" size={14} />{text.cardTitle}</span>
              </header>
              {/* DECISIONS_LOG #111: two numbered codes; «جدولي» pairs the phone on the first scan, the pairing code is optional. */}
              <div className="teacher-card-main">
                <span className="teacher-card-step"><QrIcon aria-hidden="true" size={14} />{text.step(format.number(2), text.step2)}</span>
                {card.code.qr
                  ? <QrCode code={card.code.qr} label={qrText.teacher.alt(isolate(card.name))} size="6cm" />
                  : <p className="teacher-card-file">{text.useFile}</p>}
                {card.code.qr && <span className="teacher-card-note">{text.step2Hint}</span>}
              </div>
              <div className="teacher-card-side">
                <figure className="teacher-card-small">
                  <figcaption><Download aria-hidden="true" size={12} />{text.step(format.number(1), text.step1)}</figcaption>
                  {cards.appQr ? <QrCode code={cards.appQr} label={text.step1} size="2.2cm" /> : <p className="teacher-card-note">{text.noApp}</p>}
                </figure>
                {cards.pairing.qr && (
                  <figure className="teacher-card-small is-optional">
                    <figcaption><KeyRound aria-hidden="true" size={12} />{text.optionalPairing}</figcaption>
                    <QrCode code={cards.pairing.qr} label={text.optionalPairing} size="1.8cm" />
                  </figure>
                )}
              </div>
              <footer className="teacher-card-footer">
                <span>{text.yearAndVersion(isolate(cards.academicYear), version)}</span>
                <span>{text.fingerprint("")}<bdi dir="ltr">{cards.fingerprint}</bdi></span>
              </footer>
            </article>
          ))}
          <p className="cards-page-number">{text.page(format.number(pageIndex + 1), format.number(pages.length))}</p>
        </section>
      ))}
    </div>,
    document.body,
  );
}
