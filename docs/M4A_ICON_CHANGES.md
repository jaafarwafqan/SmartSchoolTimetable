# قائمة تغييرات الأيقونات (M4a)

مولَّدة من فرق الإيداع `727c461` (العناصر التي تغيّرت فقط؛ `-` قبل، `+` بعد).

النتيجة: كل عنوان قسم (h2/h3) صار `SectionTitle` بأيقونة، وكل `PageHeader` بأيقونة، وكل `Badge` غير محايد بأيقونة، وكل تبويب بأيقونة.
عدد الأسطر المتغيرة: 197

### frontend/src/components/ui/tab-nav.tsx
```diff
+              <tab.icon aria-hidden="true" size={18} />
```

### frontend/src/features/academic-years/AcademicYearsPage.tsx
```diff
+        icon={CalendarDays} title={messages.school.nav.academicYears}
```

### frontend/src/features/academic-years/TermsPanel.tsx
```diff
-        <h2 id="terms-title">{text.termsOf(year.label)}</h2>
+        <SectionTitle level={2} icon={CalendarDays} id="terms-title">{text.termsOf(year.label)}</SectionTitle>
```

### frontend/src/features/calendar/CalendarPage.tsx
```diff
-          <Badge tone={day.affectsSchedule ? "primary" : "neutral"}>{day.affectsSchedule ? text.affects : text.noEffect}</Badge>
+            ? <Badge tone="primary" icon={<CalendarCheck aria-hidden="true" size={16} />}>{text.affects}</Badge>
+            : <Badge icon={<CalendarX aria-hidden="true" size={16} />}>{text.noEffect}</Badge>}
-      <PageHeader title={text.title} description={text.description}
+      <PageHeader icon={CalendarRange} title={text.title} description={text.description}
```

### frontend/src/features/calendar/MonthView.tsx
```diff
-        <h2 id="month-title">{format.month(month)}</h2>
+        <SectionTitle level={2} icon={CalendarDays} id="month-title">{format.month(month)}</SectionTitle>
```

### frontend/src/features/curriculum/CurriculumHelpers.tsx
```diff
-      <h3 id="repeat-title" className="tool-title"><Repeat2 aria-hidden="true" size={20} />{text.addRepeatTitle}</h3>
+      <SectionTitle level={3} icon={Repeat2} id="repeat-title" className="tool-title">{text.addRepeatTitle}</SectionTitle>
```

### frontend/src/features/curriculum/CurriculumPage.tsx
```diff
-        <h2 id="curriculum-title">{text.title}</h2>
+        <SectionTitle level={2} icon={BookOpen} id="curriculum-title">{text.title}</SectionTitle>
-          <h2 id="curriculum-helpers-title">{text.helpers}</h2>
+          <SectionTitle level={2} icon={Wrench} id="curriculum-helpers-title">{text.helpers}</SectionTitle>
-      <PageHeader title={text.title} description={text.description} />
+      <PageHeader icon={BookOpen} title={text.title} description={text.description} />
```

### frontend/src/features/curriculum/DailySuggestionPanel.tsx
```diff
-      <h2 id="daily-suggestion-title" className="tool-title"><CalendarRange aria-hidden="true" size={22} />{text.title}</h2>
+      <SectionTitle level={2} icon={CalendarRange} id="daily-suggestion-title" className="tool-title">{text.title}</SectionTitle>
```

### frontend/src/features/curriculum/SubjectTemplatePanel.tsx
```diff
-                <Badge tone="success">{text.addedBadge}</Badge>
+                <Badge tone="success" icon={<CircleCheck aria-hidden="true" size={16} />}>{text.addedBadge}</Badge>
```

### frontend/src/features/curriculum/SuggestedCurriculumPanel.tsx
```diff
-        <Badge tone="primary">{text.badge}</Badge>
+        <Badge tone="primary" icon={<Sparkles aria-hidden="true" size={16} />}>{text.badge}</Badge>
-                    <Badge tone={line.action === "exists" ? "success" : "primary"}>
+                    <Badge tone={line.action === "exists" ? "success" : "primary"} icon={line.action === "exists" ? <CircleCheck aria-hidden="true" size={16} /> : <Plus aria-hidden="true" size={16} /
```

### frontend/src/features/dashboard/CurriculumStatusCard.tsx
```diff
-      <h2 id="curriculum-status-title"><Link to="/classes/curriculum">{text.title}</Link></h2>
+      <SectionTitle level={2} icon={BookOpen} id="curriculum-status-title"><Link to="/classes/curriculum">{text.title}</Link></SectionTitle>
```

### frontend/src/features/dashboard/DashboardPage.tsx
```diff
-      <PageHeader title={messages.school.nav.dashboard} description={messages.school.dashboard.description} />
+      <PageHeader icon={LayoutDashboard} title={messages.school.nav.dashboard} description={messages.school.dashboard.description} />
-              <h2 id="setup-resume-title">{messages.school.wizard.title}</h2>
+              <SectionTitle level={2} icon={Rocket} id="setup-resume-title">{messages.school.wizard.title}</SectionTitle>
-            <h2 id="counts-title">{messages.school.dashboard.countsTitle}</h2>
+            <SectionTitle level={2} icon={ChartColumn} id="counts-title">{messages.school.dashboard.countsTitle}</SectionTitle>
```

### frontend/src/features/dashboard/SetupChecklist.tsx
```diff
-      <h2 id="checklist-title">{messages.school.dashboard.checklistTitle}</h2>
+      <SectionTitle level={2} icon={ListChecks} id="checklist-title">{messages.school.dashboard.checklistTitle}</SectionTitle>
```

### frontend/src/features/design-guide/GuideSection.tsx
```diff
-      <h2 id={id}>{title}</h2>
+      <SectionTitle level={2} icon={Palette} id={id}>{title}</SectionTitle>
```

### frontend/src/features/design-guide/SchedulingSection.tsx
```diff
-      <h3>{text.matrix}</h3>
+      <SectionTitle level={3} icon={Grid3x3}>{text.matrix}</SectionTitle>
-      <h3>{text.loadTitle}</h3>
+      <SectionTitle level={3} icon={Gauge}>{text.loadTitle}</SectionTitle>
-      <h3>{text.readinessTitle}</h3>
+      <SectionTitle level={3} icon={ShieldCheck}>{text.readinessTitle}</SectionTitle>
```

### frontend/src/features/design-guide/TimetableSection.tsx
```diff
-      <h3>{guideMessages.subjectsHeading}</h3>
+      <SectionTitle level={3} icon={Palette}>{guideMessages.subjectsHeading}</SectionTitle>
```

### frontend/src/features/generation/GenerationPage.tsx
```diff
+  return <Badge tone={tone === "neutral" ? "primary" : tone} icon={icon}>{text.statuses[status]}</Badge>;
-        <h2 id="generation-progress-title">{text.progressTitle}</h2>
+        <SectionTitle level={2} icon={LoaderCircle} id="generation-progress-title">{text.progressTitle}</SectionTitle>
-        <h2 id="generation-result-title">{text.resultTitle}</h2>
+        <SectionTitle level={2} icon={ClipboardCheck} id="generation-result-title">{text.resultTitle}</SectionTitle>
-      <h2 id="generation-diagnostics-title">{text.diagnosticsTitle}</h2>
+      <SectionTitle level={2} icon={Stethoscope} id="generation-diagnostics-title">{text.diagnosticsTitle}</SectionTitle>
-      <PageHeader title={text.title} description={text.description} />
+      <PageHeader icon={Cpu} title={text.title} description={text.description} />
-            <h2 id="generation-readiness-title">{text.readinessTitle}</h2>
+            <SectionTitle level={2} icon={ShieldCheck} id="generation-readiness-title">{text.readinessTitle}</SectionTitle>
-                <Badge tone={readiness.data.errors ? "danger" : "success"}>{errorCount(readiness.data.errors, format)}</Badge>
+                <Badge tone={readiness.data.errors ? "danger" : "success"} icon={readiness.data.errors ? <CircleAlert aria-hidden="true" size={16} /> : <CircleCheck aria-hidden="true" size={16} />}>{
-          <h2 id="generation-options-title">{text.optionsTitle}</h2>
+          <SectionTitle level={2} icon={SlidersHorizontal} id="generation-options-title">{text.optionsTitle}</SectionTitle>
-          <h2 id="generation-history-title">{text.history}</h2>
+          <SectionTitle level={2} icon={History} id="generation-history-title">{text.history}</SectionTitle>
-            { key: "status", header: text.resultTitle, cell: (item) => <Badge tone={statusTone(item.status)}>{text.statuses[item.status]}</Badge> },
```

### frontend/src/features/readiness/ReadinessCard.tsx
```diff
-      <h2 id="readiness-card-title">{text.title}</h2>
+      <SectionTitle level={2} icon={ShieldCheck} id="readiness-card-title">{text.title}</SectionTitle>
```

### frontend/src/features/readiness/ReadinessPage.tsx
```diff
-      <PageHeader title={text.title} description={text.description} actions={(
+      <PageHeader icon={ShieldCheck} title={text.title} description={text.description} actions={(
-              <Badge tone={readiness.data.errors ? "danger" : "success"}>{errorCount(readiness.data.errors, format)}</Badge>
+              <Badge tone={readiness.data.errors ? "danger" : "success"} icon={readiness.data.errors ? <CircleAlert aria-hidden="true" size={16} /> : <CircleCheck aria-hidden="true" size={16} />}>{er
-            <h2 id="readiness-findings-title">{text.findings}</h2>
+            <SectionTitle level={2} icon={ListChecks} id="readiness-findings-title">{text.findings}</SectionTitle>
-                  <h3>{group.entity.name ? isolate(group.entity.name) : text.title}</h3>
+                  <SectionTitle level={3} icon={FolderOpen}>{group.entity.name ? isolate(group.entity.name) : text.title}</SectionTitle>
-                      <Badge tone={finding.severity === "error" ? "danger" : "warning"}>
+                      <Badge tone={finding.severity === "error" ? "danger" : "warning"} icon={finding.severity === "error" ? <CircleAlert aria-hidden="true" size={16} /> : <TriangleAlert aria-hidden=
```

### frontend/src/features/resources/ResourcesPage.tsx
```diff
-      <PageHeader title={text.title} description={text.description} />
+      <PageHeader icon={DoorOpen} title={text.title} description={text.description} />
```

### frontend/src/features/scheduling-profile/SchedulingProfilePage.tsx
```diff
-        {profile.isDefault && <> <Badge tone="success">{text.isDefault}</Badge></>}
+        {profile.isDefault && <> <Badge tone="success" icon={<CircleCheck aria-hidden="true" size={16} />}>{text.isDefault}</Badge></>}
-      <PageHeader title={text.title} description={text.description} />
+      <PageHeader icon={SlidersHorizontal} title={text.title} description={text.description} />
```

### frontend/src/features/school-profile/AssetPanel.tsx
```diff
-      <h3 id={`${kind}-title`}>{label}</h3>
+      <SectionTitle level={3} icon={Image} id={`${kind}-title`}>{label}</SectionTitle>
```

### frontend/src/features/school-profile/ProfileForm.tsx
```diff
-      <h2>{text.detailsTitle}</h2>
+      <SectionTitle level={2} icon={School}>{text.detailsTitle}</SectionTitle>
-      <h2>{text.displayTitle}</h2>
+      <SectionTitle level={2} icon={Paintbrush}>{text.displayTitle}</SectionTitle>
```

### frontend/src/features/school-profile/SchoolProfilePage.tsx
```diff
-      <PageHeader title={messages.school.nav.profile} description={messages.school.profile.description} />
+      <PageHeader icon={School} title={messages.school.nav.profile} description={messages.school.profile.description} />
-            <h2 id="images-title">{messages.school.profile.imagesTitle}</h2>
+            <SectionTitle level={2} icon={Images} id="images-title">{messages.school.profile.imagesTitle}</SectionTitle>
```

### frontend/src/features/settings/BackupSection.tsx
```diff
-    <SettingsSection id="backup-section" icon={<DatabaseBackup size={20} strokeWidth={2} />} title={text.title} description={text.description}>
+    <SettingsSection id="backup-section" icon={DatabaseBackup} title={text.title} description={text.description}>
-      <h3 className="settings-subtitle">{text.restoreTitle}</h3>
+      <SectionTitle level={3} icon={ArchiveRestore} className="settings-subtitle">{text.restoreTitle}</SectionTitle>
```

### frontend/src/features/settings/InactivitySection.tsx
```diff
-      icon={<Clock size={20} strokeWidth={2} />}
+      icon={Clock}
```

### frontend/src/features/settings/PasswordSection.tsx
```diff
-      icon={<KeyRound size={20} strokeWidth={2} />}
+      icon={KeyRound}
```

### frontend/src/features/settings/RecoveryCodeSection.tsx
```diff
-      icon={<LifeBuoy size={20} strokeWidth={2} />}
+      icon={LifeBuoy}
```

### frontend/src/features/settings/SettingsScreen.tsx
```diff
-      <PageHeader title={messages.app.settings} description={messages.app.settingsDescription} />
+      <PageHeader icon={Settings} title={messages.app.settings} description={messages.app.settingsDescription} />
```

### frontend/src/features/settings/SettingsSection.tsx
```diff
-          <h2 id={`${id}-title`}>{title}</h2>
+          <SectionTitle level={2} icon={icon} id={`${id}-title`}>{title}</SectionTitle>
```

### frontend/src/features/settings/SetupWizardSection.tsx
```diff
-    <SettingsSection id="setup-wizard" icon={<Wand2 size={20} />} title={text.settingsTitle} description={text.settingsDescription}>
+    <SettingsSection id="setup-wizard" icon={Wand2} title={text.settingsTitle} description={text.settingsDescription}>
```

### frontend/src/features/setup-wizard/DataSteps.tsx
```diff
-            <h3 id="review-counts">{text.review.counts}</h3>
+            <SectionTitle level={3} icon={ListChecks} id="review-counts">{text.review.counts}</SectionTitle>
-            <h3 id="review-warnings">{text.review.warnings}</h3>
+            <SectionTitle level={3} icon={TriangleAlert} id="review-warnings">{text.review.warnings}</SectionTitle>
```

### frontend/src/features/setup-wizard/SetupWizardPage.tsx
```diff
-      <PageHeader title={text.title} description={text.description} />
+      <PageHeader icon={Wand2} title={text.title} description={text.description} />
-            <h2 id="wizard-step-title" ref={headingRef} tabIndex={-1}>{text.steps[step]}</h2>
+            <SectionTitle level={2} icon={stepIcons[step]} id="wizard-step-title" ref={headingRef} tabIndex={-1}>{text.steps[step]}</SectionTitle>
```

### frontend/src/features/setup-wizard/TimingStep.tsx
```diff
-      <h3 id={`wizard-shift-${kind}`}>{name}</h3>
+      <SectionTitle level={3} icon={Clock} id={`wizard-shift-${kind}`}>{name}</SectionTitle>
```

### frontend/src/features/stages-sections/SectionsPanel.tsx
```diff
-        <h2 id="sections-title">{text.sectionsOf(stage.name)}</h2>
+        <SectionTitle level={2} icon={LayoutGrid} id="sections-title">{text.sectionsOf(stage.name)}</SectionTitle>
```

### frontend/src/features/stages-sections/StageCardsPanel.tsx
```diff
-        <Badge tone={custom ? "primary" : "neutral"}>{custom ? text.ownCounts : text.inherits}</Badge>
+          ? <Badge tone="primary" icon={<Pencil aria-hidden="true" size={16} />}>{text.ownCounts}</Badge>
+          : <Badge icon={<Link2 aria-hidden="true" size={16} />}>{text.inherits}</Badge>}
-      <h2 id="stage-cards-title">{text.title}</h2>
+      <SectionTitle level={2} icon={Layers} id="stage-cards-title">{text.title}</SectionTitle>
-                <h3>{card.stage.name}</h3>
+                <SectionTitle level={3} icon={GraduationCap}>{card.stage.name}</SectionTitle>
```

### frontend/src/features/stages-sections/StagesPanel.tsx
```diff
-          <h2 id="stages-title">{text.stages}</h2>
+          <SectionTitle level={2} icon={Layers} id="stages-title">{text.stages}</SectionTitle>
```

### frontend/src/features/stages-sections/StagesSectionsPage.tsx
```diff
-      <PageHeader title={text.title} description={text.description} />
+      <PageHeader icon={Layers} title={text.title} description={text.description} />
```

### frontend/src/features/subjects/SubjectsPage.tsx
```diff
-      <PageHeader title={text.title} description={text.description} />
+      <PageHeader icon={Library} title={text.title} description={text.description} />
-                  {resourceName(subject.requiredResourceId) && <Badge tone="primary">{messages.school.requiredResource.badge(resourceName(subject.requiredResourceId) ?? "")}</Badge>}
+                  {resourceName(subject.requiredResourceId) && <Badge tone="primary" icon={<Wrench aria-hidden="true" size={16} />}>{messages.school.requiredResource.badge(resourceName(subject.requir
```

### frontend/src/features/teachers/BulkAddPanel.tsx
```diff
-        <h2 id="bulk-title">{text.bulkTitle}</h2>
+        <SectionTitle level={2} icon={ListPlus} id="bulk-title">{text.bulkTitle}</SectionTitle>
```

### frontend/src/features/teachers/TeacherConstraints.tsx
```diff
-      <h3 id={`${prefix}-constraints-title`}>{text.constraints}</h3>
+      <SectionTitle level={3} icon={SlidersHorizontal} id={`${prefix}-constraints-title`}>{text.constraints}</SectionTitle>
```

### frontend/src/features/teachers/TeachersPage.tsx
```diff
-      <PageHeader title={text.title} description={text.description}
+      <PageHeader icon={UsersRound} title={text.title} description={text.description}
-                    <Badge tone="primary">{messages.school.specializations.summary(specializationSummary(teacher.specializationIds))}</Badge>
+                    <Badge tone="primary" icon={<GraduationCap aria-hidden="true" size={16} />}>{messages.school.specializations.summary(specializationSummary(teacher.specializationIds))}</Badge>
```

### frontend/src/features/timetable-structure/BellSettingsCard.tsx
```diff
-        <h2 id="bell-title">{text.bellSettings}</h2>
+        <SectionTitle level={2} icon={Bell} id="bell-title">{text.bellSettings}</SectionTitle>
```

### frontend/src/features/timetable-structure/BlockedPeriodsEditor.tsx
```diff
-      <h3 id={`${id}-title`}>{text.title}</h3>
+      <SectionTitle level={3} icon={Ban} id={`${id}-title`}>{text.title}</SectionTitle>
```

### frontend/src/features/timetable-structure/DayLessonsEditor.tsx
```diff
-      <h3 id={`day-lessons-${shift.id}`}>{text.dayLessonsTitle}</h3>
+      <SectionTitle level={3} icon={ListOrdered} id={`day-lessons-${shift.id}`}>{text.dayLessonsTitle}</SectionTitle>
```

### frontend/src/features/timetable-structure/PeriodsEditor.tsx
```diff
-        <h3 id="periods-title">{text.periodsFor(shift.name)}</h3>
+        <SectionTitle level={3} icon={Clock} id="periods-title">{text.periodsFor(shift.name)}</SectionTitle>
```

### frontend/src/features/timetable-structure/ScheduleStructurePage.tsx
```diff
-      <PageHeader title={text.title} description={text.description} />
+      <PageHeader icon={Clock} title={text.title} description={text.description} />
```

### frontend/src/features/timetable-structure/SessionsCard.tsx
```diff
-      <h3 id="sessions-morning-title"><Sun aria-hidden="true" size={18} /><span>{text.morningTiming}</span></h3>
+      <SectionTitle level={3} icon={Sun} id="sessions-morning-title">{text.morningTiming}</SectionTitle>
-            <h3 id="sessions-evening-title"><Moon aria-hidden="true" size={18} /><span>{text.eveningTiming}</span></h3>
+            <SectionTitle level={3} icon={Moon} id="sessions-evening-title">{text.eveningTiming}</SectionTitle>
-            <h3 id="sessions-mapping-title"><SunMoon aria-hidden="true" size={18} /><span>{text.mapping}</span></h3>
+            <SectionTitle level={3} icon={SunMoon} id="sessions-mapping-title">{text.mapping}</SectionTitle>
-      <h2 id="sessions-title">{text.title}</h2>
+      <SectionTitle level={2} icon={SunMoon} id="sessions-title">{text.title}</SectionTitle>
```

### frontend/src/features/timetable-structure/ShiftModeCard.tsx
```diff
-      <h2 id="shift-mode-title">{text.title}</h2>
+      <SectionTitle level={2} icon={Split} id="shift-mode-title">{text.title}</SectionTitle>
```

### frontend/src/features/timetable-structure/ShiftsCard.tsx
```diff
-        <h2 id="shifts-title">{text.shifts}</h2>
+        <SectionTitle level={2} icon={Layers} id="shifts-title">{text.shifts}</SectionTitle>
```

### frontend/src/features/timetable-structure/WorkingDaysCard.tsx
```diff
-      <h2 id="working-days-title">{text.workingDays}</h2>
+      <SectionTitle level={2} icon={CalendarDays} id="working-days-title">{text.workingDays}</SectionTitle>
```

### frontend/src/features/timetable/TimetableEditor.tsx
```diff
-      <h3>{text.editTitle}</h3>
+      <SectionTitle level={3} icon={Pencil}>{text.editTitle}</SectionTitle>
```

### frontend/src/features/timetable/TimetablePage.tsx
```diff
-      <PageHeader title={text.title} description={text.description} />
+      <PageHeader icon={CalendarCheck} title={text.title} description={text.description} />
-            <h2 id="timetable-versions-title">{text.versions}</h2>
+            <SectionTitle level={2} icon={History} id="timetable-versions-title">{text.versions}</SectionTitle>
-                  <h2 id="timetable-main-title">{text.version(format.number(summary.number))}</h2>
+                  <SectionTitle level={2} icon={CalendarRange} id="timetable-main-title">{text.version(format.number(summary.number))}</SectionTitle>
```

### frontend/src/features/workload/AssignmentSuggester.tsx
```diff
-          <h3 id="suggestion-assignments-title">{text.assignments}</h3>
+          <SectionTitle level={3} icon={Link2} id="suggestion-assignments-title">{text.assignments}</SectionTitle>
-          <h3 id="suggestion-loads-title">{text.loads}</h3>
+          <SectionTitle level={3} icon={Gauge} id="suggestion-loads-title">{text.loads}</SectionTitle>
-          <h3 id="suggestion-unassigned-title">{text.unassigned}</h3>
+          <SectionTitle level={3} icon={CircleAlert} id="suggestion-unassigned-title">{text.unassigned}</SectionTitle>
-                <Badge tone="warning">{text.reasons[item.reason as keyof typeof text.reasons] ?? text.reasons.fallback}</Badge>
+                <Badge tone="warning" icon={<TriangleAlert aria-hidden="true" size={16} />}>{text.reasons[item.reason as keyof typeof text.reasons] ?? text.reasons.fallback}</Badge>
-        {embedded ? <h3 id="assignment-suggester-title">{text.title}</h3> : <h2 id="assignment-suggester-title">{text.title}</h2>}
+        <SectionTitle level={embedded ? 3 : 2} icon={Sparkles} id="assignment-suggester-title">{text.title}</SectionTitle>
```

### frontend/src/features/workload/BulkActions.tsx
```diff
-            <Badge tone={line.action === "skip" || line.action === "unchanged" ? "neutral" : line.action === "remove" ? "danger" : "primary"}>{bulk.actions[line.action]}</Badge>
+              ? <Badge icon={<Minus aria-hidden="true" size={16} />}>{bulk.actions[line.action]}</Badge>
+              : <Badge tone={line.action === "remove" ? "danger" : "primary"} icon={line.action === "remove" ? <UserMinus aria-hidden="true" size={16} /> : <UserPlus aria-hidden="true" size={16} />}>
-      <h2 id="workload-bulk-title">{bulk.title}</h2>
+      <SectionTitle level={2} icon={ClipboardList} id="workload-bulk-title">{bulk.title}</SectionTitle>
```

### frontend/src/features/workload/WorkloadPage.tsx
```diff
-      <PageHeader title={text.title} description={text.description} />
+      <PageHeader icon={Gauge} title={text.title} description={text.description} />
```

### frontend/src/layout/PageHeader.tsx
```diff
-export function PageHeader({ title, description, actions }: PageHeaderProps) {
+export function PageHeader({ title, icon: Icon, description, actions }: PageHeaderProps) {
```

### frontend/src/layout/TopBar.tsx
```diff
-        <Badge tone={term ? "primary" : "neutral"}>
+        <Badge tone={term ? "primary" : "neutral"} icon={<CalendarDays aria-hidden="true" size={16} />}>
```

## لقطات الشاشة المُعاد تأسيسها (80 ملفاً، 20 مجموعة × 4 عروض)

السبب الوحيد: أيقونات جديدة (عنوان الصفحة، عناوين الأقسام، التبويبات، شارة الفصل في الشريط العلوي، شارات الحالة). شوهدت الصور قبل وبعد.
عند عرض 375 بكسل تزداد الصفحة نحو 34 بكسل لأن شارة الفصل في الشريط العلوي تلتف إلى سطر ثانٍ وتلتف التبويبات الأربعة إلى ثلاثة أسطر؛ مقبول ولا يوجد تمرير أفقي.

| المجموعة | الصفحة |
|---|---|
| settings | الإعدادات (أيقونة داخل العنوان بدل شارة الأيقونة المنفصلة) |
| dashboard, teachers, curriculum, periods | لوحة التحكم، المعلمون، المنهج، الدوام |
| wizard-step-1…8 | خطوات المعالج |
| phase3-profile, phase3-readiness, phase3-resources, phase3-resource-shortage | الملف، الجاهزية، الموارد |
| phase3-suggester-preview, phase3-workload-matrix, phase3-workload-teachers | الأنصبة |
