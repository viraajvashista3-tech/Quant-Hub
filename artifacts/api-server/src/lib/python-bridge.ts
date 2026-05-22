import { spawn } from "child_process";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const SCRIPT_PATH = path.resolve(__dirname, "../../../scripts/python/stock_data.py");

export function runPython(args: string[]): Promise<unknown> {
  return new Promise((resolve, reject) => {
    const chunks: Buffer[] = [];
    const errChunks: Buffer[] = [];

    const proc = spawn("python3", [SCRIPT_PATH, ...args], {
      env: { ...process.env },
    });

    proc.stdout.on("data", (chunk: Buffer) => chunks.push(chunk));
    proc.stderr.on("data", (chunk: Buffer) => errChunks.push(chunk));

    proc.on("close", (code) => {
      const raw = Buffer.concat(chunks).toString("utf-8").trim();
      if (code !== 0 || !raw) {
        const errMsg = Buffer.concat(errChunks).toString("utf-8").trim();
        return reject(new Error(errMsg || `Python exited with code ${code}`));
      }
      try {
        const parsed = JSON.parse(raw);
        if (parsed && typeof parsed === "object" && "error" in parsed) {
          return reject(new Error(String(parsed.error)));
        }
        resolve(parsed);
      } catch {
        reject(new Error(`Invalid JSON from Python: ${raw.slice(0, 200)}`));
      }
    });

    proc.on("error", (err) => reject(err));
  });
}
