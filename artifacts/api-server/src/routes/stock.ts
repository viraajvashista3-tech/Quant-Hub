import { Router } from "express";
import { runPython } from "../lib/python-bridge.js";

const router = Router();

router.get("/stock/:ticker", async (req, res) => {
  const { ticker } = req.params;
  if (!ticker || !/^[A-Za-z.]{1,10}$/.test(ticker)) {
    res.status(400).json({ error: "Invalid ticker symbol" });
    return;
  }
  try {
    const data = await runPython(["overview", ticker.toUpperCase()]);
    res.json(data);
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    req.log.warn({ ticker, err: msg }, "overview failed");
    res.status(404).json({ error: msg });
  }
});

router.get("/stock/:ticker/history", async (req, res) => {
  const { ticker } = req.params;
  const period = (req.query["period"] as string) || "1y";
  const validPeriods = ["ytd", "6mo", "1y", "2y", "5y"];
  if (!validPeriods.includes(period)) {
    res.status(400).json({ error: "Invalid period" });
    return;
  }
  try {
    const data = await runPython(["history", ticker.toUpperCase(), "--period", period]);
    res.json(data);
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    req.log.warn({ ticker, err: msg }, "history failed");
    res.status(404).json({ error: msg });
  }
});

router.get("/stock/:ticker/fundamentals", async (req, res) => {
  const { ticker } = req.params;
  try {
    const data = await runPython(["fundamentals", ticker.toUpperCase()]);
    res.json(data);
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    req.log.warn({ ticker, err: msg }, "fundamentals failed");
    res.status(404).json({ error: msg });
  }
});

router.get("/stock/:ticker/news", async (req, res) => {
  const { ticker } = req.params;
  try {
    const data = await runPython(["news", ticker.toUpperCase()]);
    res.json(data);
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    req.log.warn({ ticker, err: msg }, "news failed");
    res.status(404).json({ error: msg });
  }
});

router.get("/stock/:ticker/peers", async (req, res) => {
  const { ticker } = req.params;
  const period = (req.query["period"] as string) || "1y";
  try {
    const data = await runPython(["peers", ticker.toUpperCase(), "--period", period]);
    res.json(data);
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    req.log.warn({ ticker, err: msg }, "peers failed");
    res.status(404).json({ error: msg });
  }
});

router.get("/stock/:ticker/analyst", async (req, res) => {
  const { ticker } = req.params;
  try {
    const data = await runPython(["analyst", ticker.toUpperCase()]);
    res.json(data);
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    req.log.warn({ ticker, err: msg }, "analyst failed");
    res.status(404).json({ error: msg });
  }
});

router.get("/stock/:ticker/insider", async (req, res) => {
  const { ticker } = req.params;
  if (!ticker || !/^[A-Za-z.]{1,10}$/.test(ticker)) {
    res.status(400).json({ error: "Invalid ticker symbol" });
    return;
  }
  try {
    const data = await runPython(["insider", ticker.toUpperCase()]);
    res.json(data);
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    req.log.warn({ ticker, err: msg }, "insider failed");
    res.status(404).json({ error: msg });
  }
});

router.get("/market/pulse", async (_req, res) => {
  try {
    const data = await runPython(["market_pulse"]);
    res.json(data);
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    _req.log.warn({ err: msg }, "market_pulse failed");
    res.status(500).json({ error: msg });
  }
});

router.get("/universe", async (_req, res) => {
  try {
    const data = await runPython(["universe"]);
    res.json(data);
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    res.status(500).json({ error: msg });
  }
});

export default router;
