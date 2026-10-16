import { execFileSync } from "node:child_process";

/**
 * Memory of a process and of the whole tree below it, in megabytes, read from Windows with one PowerShell call
 * (working set = what the process holds in RAM). Used only by the on-demand performance measurement (M3).
 */
const script = `
$root = [int]$env:SST_PM_ROOT
$all = Get-CimInstance Win32_Process | Select-Object ProcessId, ParentProcessId, WorkingSetSize
$ids = New-Object System.Collections.Generic.HashSet[int]
$queue = New-Object System.Collections.Queue
$queue.Enqueue($root)
while ($queue.Count -gt 0) {
  $current = $queue.Dequeue()
  foreach ($process in $all) { if ($process.ParentProcessId -eq $current -and $ids.Add([int]$process.ProcessId)) { $queue.Enqueue([int]$process.ProcessId) } }
}
$tree = ($all | Where-Object { $ids.Contains([int]$_.ProcessId) } | Measure-Object -Property WorkingSetSize -Sum).Sum
$own = ($all | Where-Object { $_.ProcessId -eq $root } | Select-Object -First 1).WorkingSetSize
Write-Output ("{0} {1}" -f [int64]$own, [int64]$tree)
`;

export type MemorySample = { ownMb: number; treeMb: number };

export function sampleMemory(rootPid: number): MemorySample {
  const output = execFileSync("powershell", ["-NoProfile", "-NonInteractive", "-Command", script], { env: { ...process.env, SST_PM_ROOT: String(rootPid) } }).toString().trim();
  const [own, tree] = output.split(/\s+/).map(Number);
  return { ownMb: own / 1_048_576, treeMb: tree / 1_048_576 };
}

/** Limits a running process to the first `cores` processors (a school PC with two cores). */
export function limitCores(pid: number, cores: number): void {
  const mask = (1 << cores) - 1;
  execFileSync("powershell", ["-NoProfile", "-NonInteractive", "-Command", `(Get-Process -Id ${pid}).ProcessorAffinity = ${mask}`]);
}
