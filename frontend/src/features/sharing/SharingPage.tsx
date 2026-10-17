import {
  CalendarCheck, Download, FileArchive, FileKey, KeyRound, Link2, MonitorPlay, MonitorStop, Printer, QrCode as QrIcon, RefreshCw, Save,
  ShieldCheck, Smartphone, UserRound, UsersRound, X,
} from "lucide-react";
import { useEffect, useRef, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { PasswordField } from "../../components/PasswordField";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Checkbox } from "../../components/ui/checkbox";
import { ConfirmDialog } from "../../components/ui/confirm-dialog";
import { Dialog } from "../../components/ui/dialog";
import { EmptyState } from "../../components/ui/empty-state";
import { Field } from "../../components/ui/field";
import { FileUpload } from "../../components/ui/file-upload";
import { Input } from "../../components/ui/input";
import { Select } from "../../components/ui/select";
import { Spinner } from "../../components/ui/spinner";
import { ApiRequestError } from "../../i18n/errors";
import { isolate } from "../../i18n/isolate";
import { messages } from "../../i18n/messages";
import { passwordMinLength } from "../../lib/credentialRules";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback, type FieldErrors } from "../../lib/useFormFeedback";
import { PageHeader } from "../../layout/PageHeader";
import { localDateTime } from "../history/historyPresentation";
import { SettingsSection } from "../settings/SettingsSection";
import { QrCode } from "./QrCode";
import {
  fileToBase64, useDownload, useImportKey, useKiosk, usePairingQr, useRegenerateKey, useSaveSharingSettings, useSharingSettings, useSharingStatus,
  useTeacherCards, useTeacherQr, type SchoolKey, type SharingStatus, type TeacherCards,
} from "./sharingApi";
import { TeacherCardsPrint } from "./TeacherCardsPrint";

const text = messages.school.sharing;

/** «تاريخ، وقت» of an API instant in the school's time zone. */
function useInstant() {
  const format = useFormatter();
  return (iso: string) => {
    const { date, time } = localDateTime(iso, format.preferences.timeZone);
    return messages.school.audit.at(format.date(date), format.time(time));
  };
}

/**
 * «مشاركة الجدول مع الهاتف» (Phase 9a-2, ADR 0056): the approved version as signed packages for phones (QR codes and .sst files),
 * the school key and its password-protected copy, the A4 teacher cards, and «شاشة العرض» for the staff room.
 */
export function SharingPage() {
  const status = useSharingStatus();
  const [backupPrompt, setBackupPrompt] = useState<"none" | "first" | "manual">("none");
  const hadKey = useRef<boolean | null>(null);

  // DECISIONS_LOG #106: right after the key is first made (by a code or an export on this page), the owner is asked to save a copy.
  useEffect(() => {
    if (!status.data) return;
    const hasKey = status.data.key !== null;
    if (hadKey.current === false && hasKey && status.data.key?.backedUpAt === null) setBackupPrompt("first");
    hadKey.current = hasKey;
  }, [status.data]);

  return (
    <div className="page page-full">
      <PageHeader icon={Smartphone} title={text.title} description={text.description} />
      {status.isPending && <Spinner />}
      {status.isError && <Alert tone="error" message={text.loadFailed} />}
      {status.data && (
        <div className="settings-grid">
          <ApprovedSection status={status.data} />
          <KeySection status={status.data} onBackup={() => setBackupPrompt("manual")} />
          <PairingSection status={status.data} />
          <TeacherSection status={status.data} />
          <PrincipalSection status={status.data} />
          <CardsSection status={status.data} />
          <KioskSection />
        </div>
      )}
      <BackupKeyDialog open={backupPrompt !== "none"} firstTime={backupPrompt === "first"} onClose={() => setBackupPrompt("none")} />
    </div>
  );
}

/** Exports need an approved version that still fits today's data, in a year that has not ended. */
function canExport(status: SharingStatus): boolean {
  return status.approved !== null && !status.approved.conflicts && !status.approved.yearEnded && status.key?.readable !== false;
}

function ApprovedSection({ status }: { status: SharingStatus }) {
  const format = useFormatter();
  const approved = status.approved;
  return (
    <SettingsSection id="share-approved" icon={CalendarCheck} title={text.approved.title} description={text.approved.description}>
      {!approved && (
        <EmptyState icon={<CalendarCheck aria-hidden="true" size={28} />} message={text.approved.none}
          action={<Link className="link-button" to="/timetable/view"><CalendarCheck aria-hidden="true" size={18} /><span>{text.approved.openTimetables}</span></Link>} />
      )}
      {approved && (
        <div className="form-stack">
          <p className="share-version">{text.approved.version(format.number(approved.number), format.date(localDateTime(approved.approvedAt, format.preferences.timeZone).date))}</p>
          <p className="card-note">{text.approved.expires(format.date(approved.expiresOn))}</p>
          {approved.conflicts && <Alert tone="error" message={text.approved.conflicts} />}
          {approved.yearEnded && <Alert tone="error" message={text.approved.yearEnded} />}
        </div>
      )}
    </SettingsSection>
  );
}

function KeySection({ status, onBackup }: { status: SharingStatus; onBackup: () => void }) {
  const format = useFormatter();
  const instant = useInstant();
  const [importing, setImporting] = useState(false);
  const [regenerating, setRegenerating] = useState(false);
  const feedback = useFormFeedback();
  const key: SchoolKey | null = status.key;
  return (
    <SettingsSection id="share-key" icon={KeyRound} title={text.key.title} description={text.key.intro}>
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      {!key && <p className="card-note">{text.key.none}</p>}
      {key && (
        <div className="form-stack">
          <Field id="share-fingerprint" label={text.key.fingerprint} hint={text.key.fingerprintHint}>
            <output id="share-fingerprint" className="share-fingerprint font-mono" aria-describedby="share-fingerprint-hint"><bdi dir="ltr">{key.fingerprint}</bdi></output>
          </Field>
          <p className="card-note">{`${text.key.generation(format.number(key.generation))} — ${text.key.created(instant(key.createdAt))}`}</p>
          {key.readable && key.backedUpAt && <Badge tone="success" icon={<ShieldCheck aria-hidden="true" size={16} />}>{text.key.backedUp(instant(key.backedUpAt))}</Badge>}
          {key.readable && !key.backedUpAt && <Alert tone="warning" message={text.key.notBackedUp} />}
          {!key.readable && <Alert tone="error" message={text.key.unreadable} />}
        </div>
      )}
      <div className="form-actions">
        {/* With an unreadable key the import is offered first; regeneration comes last (DECISIONS_LOG #106). */}
        {key && !key.readable && <Button icon={<FileKey aria-hidden="true" size={18} />} onClick={() => { feedback.reset(); setImporting(true); }}>{text.key.import}</Button>}
        {(!key || key.readable) && <Button variant={key && !key.backedUpAt ? "primary" : "secondary"} icon={<Save aria-hidden="true" size={18} />} onClick={onBackup}>{text.key.backup}</Button>}
        {(!key || key.readable) && <Button variant="secondary" icon={<FileKey aria-hidden="true" size={18} />} onClick={() => { feedback.reset(); setImporting(true); }}>{text.key.import}</Button>}
        {key && <Button variant="ghost" icon={<RefreshCw aria-hidden="true" size={18} />} onClick={() => setRegenerating(true)}>{text.key.regenerate}</Button>}
      </div>
      <ImportKeyDialog open={importing} onClose={() => setImporting(false)} onDone={(message) => { setImporting(false); feedback.showSuccess(message); }} />
      <RegenerateKeyDialog open={regenerating} onClose={() => setRegenerating(false)} onDone={() => { setRegenerating(false); feedback.showSuccess(text.regenerateDialog.done); }} />
    </SettingsSection>
  );
}

function BackupKeyDialog({ open, firstTime, onClose }: { open: boolean; firstTime: boolean; onClose: () => void }) {
  const format = useFormatter();
  const download = useDownload();
  const feedback = useFormFeedback();
  const [done, setDone] = useState(false);
  useEffect(() => {
    if (open) {
      feedback.reset();
      setDone(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    feedback.reset();
    const form = new FormData(event.currentTarget);
    const password = String(form.get("keyPassword") ?? "");
    const confirmPassword = String(form.get("keyPasswordConfirm") ?? "");
    const errors: FieldErrors = {};
    if (!password) errors.Password = messages.errors.REQUIRED;
    else if (password !== confirmPassword) errors.ConfirmPassword = messages.app.validationConfirm;
    if (Object.keys(errors).length > 0) {
      feedback.showFieldErrors(errors);
      return;
    }
    download.mutate({ path: "/api/v1/sharing/key/backup.sstkey", body: { password, confirmPassword }, fallback: "school-key.sstkey" }, {
      onSuccess: () => { setDone(true); feedback.showSuccess(text.backupDialog.saved); },
      onError: feedback.showError,
    });
  }

  return (
    <Dialog open={open} title={text.backupDialog.title} description={firstTime ? text.backupDialog.firstTime : undefined} onClose={onClose}
      footer={done
        ? <Button icon={<ShieldCheck aria-hidden="true" size={18} />} onClick={onClose}>{text.backupDialog.done}</Button>
        : (
          <>
            <Button type="submit" form="share-backup-form" icon={<Save aria-hidden="true" size={18} />} loading={download.isPending}>{text.backupDialog.save}</Button>
            <Button variant="secondary" icon={<X aria-hidden="true" size={18} />} onClick={onClose}>{text.backupDialog.later}</Button>
          </>
        )}>
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      {!done && (
        <form id="share-backup-form" ref={feedback.formRef} className="form-stack" noValidate onSubmit={submit} onInput={feedback.clearFieldFromEvent}>
          <Alert tone="warning" message={text.backupDialog.warning} />
          <PasswordField id="keyPassword" label={text.backupDialog.password} hint={text.backupDialog.passwordHint(format.number(passwordMinLength))} autoComplete="new-password" field="Password" errors={feedback.fieldErrors} />
          <PasswordField id="keyPasswordConfirm" label={text.backupDialog.confirm} autoComplete="new-password" field="ConfirmPassword" errors={feedback.fieldErrors} />
        </form>
      )}
    </Dialog>
  );
}

function ImportKeyDialog({ open, onClose, onDone }: { open: boolean; onClose: () => void; onDone: (message: string) => void }) {
  const importKey = useImportKey();
  const feedback = useFormFeedback();
  const [file, setFile] = useState<{ name: string; data: string } | null>(null);
  const [pending, setPending] = useState<{ file: string; password: string } | null>(null);
  useEffect(() => {
    if (open) {
      feedback.reset();
      setFile(null);
      setPending(null);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  const run = (command: { file: string; password: string; confirmReplace: boolean }) => importKey.mutate(command, {
    onSuccess: (result) => { setPending(null); onDone(result.changed ? text.importDialog.done : text.importDialog.unchanged); },
    onError: (reason) => {
      if (reason instanceof ApiRequestError && reason.code === "SHARE_KEY_IMPORT_CONFIRMATION_REQUIRED" && !command.confirmReplace) {
        setPending({ file: command.file, password: command.password });
        return;
      }
      setPending(null);
      feedback.showError(reason);
    },
  });

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    feedback.reset();
    const password = String(new FormData(event.currentTarget).get("importPassword") ?? "");
    const errors: FieldErrors = {};
    if (!file) errors.File = messages.errors.REQUIRED;
    if (!password) errors.Password = messages.errors.REQUIRED;
    if (Object.keys(errors).length > 0 || !file) {
      feedback.showFieldErrors(errors);
      return;
    }
    run({ file: file.data, password, confirmReplace: false });
  }

  return (
    <>
      <Dialog open={open && pending === null} title={text.importDialog.title} description={text.importDialog.intro} onClose={onClose}
        footer={(
          <>
            <Button type="submit" form="share-import-form" icon={<FileKey aria-hidden="true" size={18} />} loading={importKey.isPending}>{text.importDialog.submit}</Button>
            <Button variant="secondary" icon={<X aria-hidden="true" size={18} />} onClick={onClose}>{messages.app.cancel}</Button>
          </>
        )}>
        <Alert tone="error" message={feedback.error} />
        <form id="share-import-form" ref={feedback.formRef} className="form-stack" noValidate onSubmit={submit} onInput={feedback.clearFieldFromEvent}>
          <Field id="share-import-file" label={text.importDialog.file} error={feedback.fieldErrors.File}>
            <div className="share-file-row">
              <FileUpload id="share-import-file" label={text.importDialog.file} accept=".sstkey" onSelect={(chosen) => {
                void fileToBase64(chosen).then((data) => setFile({ name: chosen.name, data }));
              }} />
              <span className="card-note">{file ? text.importDialog.chosen(isolate(file.name)) : text.importDialog.noFile}</span>
            </div>
          </Field>
          <PasswordField id="importPassword" label={text.importDialog.password} autoComplete="off" minLength={1} field="Password" errors={feedback.fieldErrors} />
        </form>
      </Dialog>
      <ConfirmDialog open={pending !== null} title={text.importDialog.replaceTitle} consequence={text.importDialog.replaceMessage} danger
        confirmLabel={text.importDialog.replaceConfirm} confirmIcon={<FileKey aria-hidden="true" size={18} />} loading={importKey.isPending}
        onConfirm={() => pending && run({ ...pending, confirmReplace: true })} onCancel={() => setPending(null)} />
    </>
  );
}

function RegenerateKeyDialog({ open, onClose, onDone }: { open: boolean; onClose: () => void; onDone: () => void }) {
  const regenerate = useRegenerateKey();
  const [first, setFirst] = useState(false);
  const [second, setSecond] = useState(false);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    if (open) {
      setFirst(false);
      setSecond(false);
      setError(null);
    }
  }, [open]);
  return (
    <ConfirmDialog open={open} title={text.regenerateDialog.title} consequence={text.regenerateDialog.message} danger confirmDisabled={!first || !second}
      confirmLabel={text.regenerateDialog.submit} confirmIcon={<RefreshCw aria-hidden="true" size={18} />} loading={regenerate.isPending}
      onConfirm={() => regenerate.mutate(undefined, { onSuccess: onDone, onError: (reason) => setError(reason instanceof ApiRequestError ? reason.arabicMessage : messages.errors.UNKNOWN_ERROR) })}
      onCancel={onClose}>
      <Alert tone="error" message={error} />
      <div className="form-stack">
        <Checkbox checked={first} onChange={(event) => setFirst(event.target.checked)}>{text.regenerateDialog.confirmFirst}</Checkbox>
        <Checkbox checked={second} onChange={(event) => setSecond(event.target.checked)}>{text.regenerateDialog.confirmSecond}</Checkbox>
      </div>
    </ConfirmDialog>
  );
}

function PairingSection({ status }: { status: SharingStatus }) {
  const pairing = usePairingQr();
  const download = useDownload();
  const feedback = useFormFeedback();
  const blocked = status.key?.readable === false;
  return (
    <SettingsSection id="share-pairing" icon={ShieldCheck} title={text.pairing.title} description={text.pairing.intro}>
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error ?? (pairing.error ? (pairing.error instanceof ApiRequestError ? pairing.error.arabicMessage : messages.errors.UNKNOWN_ERROR) : null)} />
      {pairing.data?.code.qr && (
        <figure className="share-qr">
          <QrCode code={pairing.data.code.qr} label={text.pairing.alt} />
          <figcaption>{`${pairing.data.schoolName} — ${text.key.fingerprint}: `}<bdi dir="ltr">{pairing.data.fingerprint}</bdi></figcaption>
        </figure>
      )}
      <div className="form-actions">
        <Button icon={<QrIcon aria-hidden="true" size={18} />} disabled={blocked} loading={pairing.isPending} onClick={() => { feedback.reset(); pairing.mutate(); }}>{text.pairing.show}</Button>
        <Button variant="secondary" icon={<Download aria-hidden="true" size={18} />} disabled={blocked} loading={download.isPending}
          onClick={() => { feedback.reset(); download.mutate({ path: "/api/v1/sharing/pairing.sst", fallback: "school-pairing.sst" }, { onSuccess: () => feedback.showSuccess(text.done.downloaded), onError: feedback.showError }); }}>
          {text.pairing.download}
        </Button>
      </div>
    </SettingsSection>
  );
}

function TeacherSection({ status }: { status: SharingStatus }) {
  const format = useFormatter();
  const qr = useTeacherQr();
  const download = useDownload();
  const feedback = useFormFeedback();
  const [teacherId, setTeacherId] = useState<number | null>(null);
  const enabled = canExport(status);
  const chosen = status.teachers.find((teacher) => teacher.id === teacherId);
  const shown = qr.data && qr.data.teacherId === teacherId ? qr.data : null;
  return (
    <SettingsSection id="share-teacher" icon={UserRound} title={text.teacher.title} description={text.teacher.allHint}>
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      {status.approved && status.teachers.length === 0 && <p className="card-note">{text.teacher.none}</p>}
      <Field id="share-teacher-choice" label={text.teacher.choose}>
        <Select id="share-teacher-choice" value={teacherId === null ? "" : String(teacherId)} disabled={!enabled || status.teachers.length === 0}
          onChange={(event) => { setTeacherId(event.target.value ? Number(event.target.value) : null); feedback.reset(); }}
          options={[{ value: "", label: text.teacher.placeholder }, ...status.teachers.map((teacher) => ({
            value: String(teacher.id), label: `${teacher.name} — ${text.teacher.lessons(format.count(teacher.lessons, "lesson"))}`,
          }))]} />
      </Field>
      {shown && (shown.code.qr
        ? (
          <figure className="share-qr">
            <QrCode code={shown.code.qr} label={text.teacher.alt(isolate(shown.name))} />
            <figcaption>{shown.name}</figcaption>
          </figure>
        )
        : <Alert tone="warning" message={text.teacher.tooLarge} />)}
      <div className="form-actions">
        <Button icon={<QrIcon aria-hidden="true" size={18} />} disabled={!enabled || !chosen} loading={qr.isPending}
          onClick={() => { feedback.reset(); if (chosen) qr.mutate(chosen.id, { onError: feedback.showError }); }}>{text.teacher.show}</Button>
        <Button variant="secondary" icon={<Download aria-hidden="true" size={18} />} disabled={!enabled || !chosen} loading={download.isPending && download.variables?.path.includes("/teachers/")}
          onClick={() => {
            feedback.reset();
            if (chosen) download.mutate({ path: `/api/v1/sharing/teachers/${chosen.id}/package.sst`, fallback: `teacher-${chosen.id}.sst` }, { onSuccess: () => feedback.showSuccess(text.done.downloaded), onError: feedback.showError });
          }}>
          {text.teacher.download}
        </Button>
        <Button variant="secondary" icon={<FileArchive aria-hidden="true" size={18} />} disabled={!enabled || status.teachers.length === 0} loading={download.isPending && download.variables?.path.endsWith(".zip")}
          onClick={() => { feedback.reset(); download.mutate({ path: "/api/v1/sharing/teachers.zip", fallback: "teachers.zip" }, { onSuccess: () => feedback.showSuccess(text.done.downloaded), onError: feedback.showError }); }}>
          {text.teacher.all}
        </Button>
      </div>
    </SettingsSection>
  );
}

function PrincipalSection({ status }: { status: SharingStatus }) {
  const download = useDownload();
  const feedback = useFormFeedback();
  return (
    <SettingsSection id="share-principal" icon={UsersRound} title={text.principal.title} description={text.principal.intro}>
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      <Alert tone="warning" message={text.principal.privacy} />
      <div className="form-actions">
        <Button icon={<Download aria-hidden="true" size={18} />} disabled={!canExport(status)} loading={download.isPending}
          onClick={() => { feedback.reset(); download.mutate({ path: "/api/v1/sharing/principal.sst", fallback: "principal.sst" }, { onSuccess: () => feedback.showSuccess(text.done.downloaded), onError: feedback.showError }); }}>
          {text.principal.download}
        </Button>
      </div>
    </SettingsSection>
  );
}

function CardsSection({ status }: { status: SharingStatus }) {
  const settings = useSharingSettings();
  const save = useSaveSharingSettings();
  const cards = useTeacherCards();
  const feedback = useFormFeedback();
  const [link, setLink] = useState<string | null>(null);
  const [printing, setPrinting] = useState<TeacherCards | null>(null);
  const value = link ?? settings.data?.appLink ?? "";

  // Print once the cards are in the page, then take them out again.
  useEffect(() => {
    if (!printing) return undefined;
    const done = () => setPrinting(null);
    window.addEventListener("afterprint", done, { once: true });
    const frame = window.requestAnimationFrame(() => window.print());
    return () => { window.cancelAnimationFrame(frame); window.removeEventListener("afterprint", done); };
  }, [printing]);

  return (
    <SettingsSection id="share-cards" icon={Printer} title={text.cards.title} description={text.cards.intro} wide>
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      <form ref={feedback.formRef} className="form-stack" noValidate onInput={feedback.clearFieldFromEvent} onSubmit={(event) => {
        event.preventDefault();
        feedback.reset();
        save.mutate({ appLink: value.trim() || null, version: settings.data?.version ?? 0 }, {
          onSuccess: () => { setLink(null); feedback.showSuccess(text.cards.linkSaved); },
          onError: feedback.showError,
        });
      }}>
        <Field id="share-app-link" label={text.cards.appLink} hint={text.cards.appLinkHint} error={feedback.fieldErrors.AppLink}>
          <Input id="share-app-link" dir="ltr" inputMode="url" autoComplete="off" value={value} data-field="AppLink" aria-describedby="share-app-link-hint"
            aria-invalid={feedback.fieldErrors.AppLink ? true : undefined} onChange={(event) => setLink(event.target.value)} />
        </Field>
        <div className="form-actions">
          <Button type="submit" variant="secondary" icon={<Link2 aria-hidden="true" size={18} />} loading={save.isPending} disabled={!settings.data || value.trim() === (settings.data.appLink ?? "")}>{text.cards.saveLink}</Button>
          <Button type="button" icon={<Printer aria-hidden="true" size={18} />} disabled={!canExport(status) || status.teachers.length === 0} loading={cards.isPending}
            onClick={() => { feedback.reset(); cards.mutate(null, { onSuccess: setPrinting, onError: feedback.showError }); }}>
            {cards.isPending ? text.cards.preparing : text.cards.print}
          </Button>
        </div>
        <p className="card-note">{text.cards.printHint}</p>
      </form>
      {printing && <TeacherCardsPrint cards={printing} />}
    </SettingsSection>
  );
}

function KioskSection() {
  const kiosk = useKiosk();
  const feedback = useFormFeedback();
  return (
    <SettingsSection id="share-kiosk" icon={MonitorPlay} title={text.kiosk.title} description={text.kiosk.intro}>
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      <p className="card-note">{text.kiosk.openHint}</p>
      <div className="form-actions">
        <Button icon={<MonitorPlay aria-hidden="true" size={18} />} loading={kiosk.start.isPending}
          onClick={() => {
            feedback.reset();
            // The launcher opens /kiosk in its own full-screen window (on a second screen when there is one); a browser opens a tab.
            kiosk.start.mutate(undefined, { onSuccess: ({ key }) => { window.open(`/kiosk#k=${key}`, "_blank", "noopener"); }, onError: feedback.showError });
          }}>
          {text.kiosk.open}
        </Button>
        <Button variant="secondary" icon={<MonitorStop aria-hidden="true" size={18} />} loading={kiosk.stop.isPending}
          onClick={() => { feedback.reset(); kiosk.stop.mutate(undefined, { onSuccess: () => feedback.showSuccess(text.kiosk.stopped), onError: feedback.showError }); }}>
          {text.kiosk.stop}
        </Button>
      </div>
    </SettingsSection>
  );
}
