import type { Browser } from "@playwright/test";
import { spawn, type ChildProcess } from "node:child_process";
import { once } from "node:events";
import { mkdtemp, rm } from "node:fs/promises";
import { createServer } from "node:net";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const apiAssembly = join(repositoryRoot, "src", "SmartSchoolTimetable.Api", "bin", "Release", "net9.0", "SmartSchoolTimetable.Api.dll");
const dotnetHost = process.env.DOTNET_HOST_PATH ??
  join(process.env.ProgramFiles ?? "C:\\Program Files", "dotnet", "dotnet.exe");

async function freePort(): Promise<number> {
  const server = createServer();
  server.listen(0, "127.0.0.1");
  await once(server, "listening");
  const address = server.address();
  if (!address || typeof address === "string") throw new Error("Could not allocate a local test port.");
  const port = address.port;
  await new Promise<void>((resolveClose, reject) => server.close((error) => error ? reject(error) : resolveClose()));
  return port;
}

/** Starts the real Release API on a free loopback port with a fresh temporary database. */
export class ApiServer {
  baseUrl = "";
  process: ChildProcess | null = null;
  private databaseDirectory = "";
  private output = "";

  async start(browser: Browser, prefix: string): Promise<void> {
    const port = await freePort();
    this.baseUrl = `http://127.0.0.1:${port}`;
    this.databaseDirectory = await mkdtemp(join(tmpdir(), `smart-school-${prefix}-`));
    const child = spawn(dotnetHost, [apiAssembly], {
      cwd: dirname(apiAssembly),
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: "Production",
        Database__Path: join(this.databaseDirectory, "e2e.db"),
        LocalHost__Port: String(port),
      },
      stdio: ["ignore", "pipe", "pipe"],
    });
    child.stdout?.on("data", (chunk: Buffer) => { this.output += chunk.toString(); });
    child.stderr?.on("data", (chunk: Buffer) => { this.output += chunk.toString(); });
    this.process = child;

    const page = await browser.newPage();
    try {
      await this.waitUntilServing(page);
    } finally {
      await page.close();
    }
  }

  private async waitUntilServing(page: import("@playwright/test").Page): Promise<void> {
    const startedAt = Date.now();
    while (Date.now() - startedAt < 30_000) {
      try {
        const response = await page.request.get(`${this.baseUrl}/api/v1/bootstrap`, { timeout: 1_000 });
        if (response.ok()) {
          const html = await (await page.request.get(this.baseUrl)).text();
          const scriptPath = html.match(/src="([^"]+\.js)"/)?.[1];
          if (!scriptPath) throw new Error("The API index does not reference a built JavaScript asset.");
          const script = await page.request.get(new URL(scriptPath, this.baseUrl).toString());
          if (!script.ok()) throw new Error(`The JavaScript asset returned HTTP ${script.status()}.`);
          return;
        }
      } catch {
        await new Promise((resolveWait) => setTimeout(resolveWait, 150));
      }
      if (this.process?.exitCode !== null) throw new Error(`API exited early: ${this.output}`);
    }
    throw new Error(`API did not start: ${this.output}`);
  }

  /** Stops the process (used both for teardown and for the "server stopped" scenario). */
  async kill(): Promise<void> {
    const child = this.process;
    if (child && child.exitCode === null && child.pid) {
      child.kill();
      await Promise.race([once(child, "exit"), new Promise((resolveWait) => setTimeout(resolveWait, 5_000))]);
    }
  }

  async stop(): Promise<void> {
    await this.kill();
    if (this.databaseDirectory) await rm(this.databaseDirectory, { recursive: true, force: true });
  }
}
