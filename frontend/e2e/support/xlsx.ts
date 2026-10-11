import { execFileSync } from "node:child_process";
import { mkdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";

const escapeXml = (value: string) => value.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");

const columnLetter = (index: number) => String.fromCharCode("A".charCodeAt(0) + index);

/**
 * Writes a minimal one-sheet .xlsx (inline strings) for upload tests: no spreadsheet library is installed, so the package parts are written
 * as text and zipped with .NET's ZipFile through PowerShell (available on Windows developer machines and the CI runner).
 */
export function writeWorkbook(folder: string, name: string, sheetName: string, rows: string[][]): string {
  const root = join(folder, `${name}-parts`);
  mkdirSync(join(root, "_rels"), { recursive: true });
  mkdirSync(join(root, "xl", "_rels"), { recursive: true });
  mkdirSync(join(root, "xl", "worksheets"), { recursive: true });
  writeFileSync(join(root, "[Content_Types].xml"),
    '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">'
    + '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/>'
    + '<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>'
    + '<Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>'
    + '<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>');
  writeFileSync(join(root, "_rels", ".rels"),
    '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
    + '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>');
  writeFileSync(join(root, "xl", "workbook.xml"),
    '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" '
    + `xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="${escapeXml(sheetName)}" sheetId="1" r:id="rId1"/></sheets></workbook>`);
  writeFileSync(join(root, "xl", "_rels", "workbook.xml.rels"),
    '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
    + '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>'
    + '<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>');
  writeFileSync(join(root, "xl", "styles.xml"),
    '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">'
    + '<fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts><fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>'
    + '<borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>'
    + '<cellXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/></cellXfs></styleSheet>');
  const sheetRows = rows.map((row, rowIndex) => `<row r="${rowIndex + 1}">${row.map((value, columnIndex) =>
    (value === "" ? "" : `<c r="${columnLetter(columnIndex)}${rowIndex + 1}" t="inlineStr"><is><t>${escapeXml(value)}</t></is></c>`)).join("")}</row>`).join("");
  writeFileSync(join(root, "xl", "worksheets", "sheet1.xml"),
    `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>${sheetRows}</sheetData></worksheet>`);

  const target = join(folder, `${name}.xlsx`);
  // Entry names use forward slashes (Windows PowerShell's CreateFromDirectory writes backslashes, which a package reader does not find).
  const script = [
    "Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem",
    "$root = (Resolve-Path $env:SST_XLSX_ROOT).Path",
    "$zip = [System.IO.Compression.ZipFile]::Open($env:SST_XLSX_TARGET, 'Create')",
    "foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File) {",
    "  $name = $file.FullName.Substring($root.Length).TrimStart('\\', '/').Replace('\\', '/')",
    "  [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $name) | Out-Null",
    "}",
    "$zip.Dispose()",
  ].join("; ");
  const shell = process.platform === "win32" ? "powershell" : "pwsh";
  execFileSync(shell, ["-NoProfile", "-NonInteractive", "-Command", script], { env: { ...process.env, SST_XLSX_ROOT: root, SST_XLSX_TARGET: target } });
  return target;
}
