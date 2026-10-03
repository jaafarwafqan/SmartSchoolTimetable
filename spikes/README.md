# Phase 0 spike results

## C# CP-SAT scheduling spike

Implementation: `CSharpSpikes/`, targeting .NET 9 and Google.OrTools NuGet 9.15.6755. Run `dotnet run --project spikes/CSharpSpikes/CSharpSpikes.csproj` to execute all cases and the diagnostic. Individual benchmarks use `--benchmark <sections> <teachers> <workers>`.

The model uses five working days and eight periods per day. Each section has exactly 40 weekly lessons split across five subjects (1, 10, 10, 10, 9 lessons). Teacher/subject workload ownership is fixed as input to isolate time-slot scheduling.

Hard constraints exercised:
- exactly one lesson in every section slot and exact subject workload;
- teacher conflict avoidance;
- teacher off day and blocked lesson;
- teacher maximum daily lessons (8) and weekly lessons (30);
- one shared Laboratory resource with capacity one per period.

Soft objective weights exercised:
- teacher gaps: 30;
- repeated lessons of a subject on the same day (spread preference): 20;
- adjacent lessons of the same subject (double-period preference): 10.

Benchmark mode stops after the first feasible solution. Timings do not represent optimization-to-proven-optimality; objective scores are incumbent values. Each measurement was run in a separate process.

| Sections / teachers | Workers | Status | Build | Solve | Total | Working-set delta |
|---|---:|---|---:|---:|---:|---:|
| 12 / 20 | 1 | Feasible | 0.164 s | 2.844 s | 3.151 s | 29.1 MB |
| 12 / 20 | 8 | Feasible | 0.155 s | 2.127 s | 2.440 s | 30.9 MB |
| 40 / 20 | 1 | Infeasible | 0.231 s | 0.367 s | 0.836 s | 46.6 MB |
| 40 / 20 | 8 | Infeasible | 0.240 s | 0.258 s | 0.737 s | 46.6 MB |

The 40-section / 20-teacher case is necessarily infeasible with these inputs: 1,600 required lessons exceed the 600-lesson aggregate weekly maximum. At least 54 teachers are needed by that bound alone. This is not evidence that a feasible 40-section case meets the performance target.

An exploratory 40-section / 54-teacher case reached the 30-second limit without a feasible incumbent (`UNKNOWN`). It is not a performance pass; the current fixed teacher/subject workload allocation and soft objective need further work for the large feasible case.

The same C# spike runs an assumption-core diagnostic for Ahmed's 28 required lessons and 25 available slots. CP-SAT reported infeasibility with both named assumption groups in the core, a shortage of 3, and the suggested correction to reduce workload or open at least 3 slots.

Two independent one-worker runs of the 12/20 case, with fixed seed `12345`, returned the same incumbent objective (5,600) and assignment count (480), while solve times differed (2.796 s and 1.521 s). Memory is the process working-set delta around model construction and solve; it is not a peak native-memory profile. One worker is the reproducibility mode; multi-worker results can differ.

## QuestPDF Arabic RTL spike

Implementation: `CSharpSpikes/` with QuestPDF 2026.9.1 and the Google Fonts Noto Naskh Arabic font (OFL license included). Generate with `dotnet run --project spikes/CSharpSpikes/CSharpSpikes.csproj -- --pdf`.

Generated `CSharpSpikes/rtl_questpdf_sample.pdf`, then rendered it to `CSharpSpikes/rtl_questpdf_sample.png` for visual inspection. Result: one A4 landscape page; Arabic glyphs joined and remained legible; columns were ordered right-to-left; the content fit without clipping. This passes the Phase 0 feasibility check, but production-container rendering and a visual regression baseline remain Phase 6 release gates.
