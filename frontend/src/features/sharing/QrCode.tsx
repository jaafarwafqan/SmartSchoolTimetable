import type { QrCode as QrCodeData } from "./sharingApi";

/** One SVG path from the module rows: each run of dark modules in a row becomes one rectangle (`M x y h w v 1 h -w z`). */
export function qrPath(rows: readonly string[]): string {
  const parts: string[] = [];
  rows.forEach((row, y) => {
    let x = 0;
    while (x < row.length) {
      if (row[x] !== "1") {
        x++;
        continue;
      }
      const start = x;
      while (x < row.length && row[x] === "1") x++;
      parts.push(`M${start} ${y}h${x - start}v1h${start - x}z`);
    }
  });
  return parts.join("");
}

type QrCodeProps = {
  code: QrCodeData;
  label: string;
  /** CSS size of the whole code with its quiet zone, for example "6cm" on a printed card. */
  size?: string;
  className?: string;
};

/**
 * A QR code drawn from the server's module matrix (the server is the one QR encoder, ADR 0056). Always dark on white, whatever
 * the theme: phone cameras read inverted codes badly. The quiet zone is part of the matrix.
 */
export function QrCode({ code, label, size, className = "" }: QrCodeProps) {
  return (
    <svg
      className={`qr-code ${className}`.trim()}
      role="img"
      aria-label={label}
      viewBox={`0 0 ${code.size} ${code.size}`}
      shapeRendering="crispEdges"
      style={size ? { inlineSize: size, blockSize: size } : undefined}
    >
      <title>{label}</title>
      <rect width={code.size} height={code.size} className="qr-code-light" />
      <path d={qrPath(code.rows)} className="qr-code-dark" />
    </svg>
  );
}
