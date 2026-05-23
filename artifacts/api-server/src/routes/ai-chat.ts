import { Router } from "express";
import { openai } from "@workspace/integrations-openai-ai-server";

const router = Router();

router.post("/ai/chat", async (req, res) => {
  try {
    const { messages, ticker, context } = req.body as {
      messages: { role: "user" | "assistant"; content: string }[];
      ticker?: string;
      context?: Record<string, unknown>;
    };

    if (!messages || !Array.isArray(messages)) {
      res.status(400).json({ error: "messages array required" });
      return;
    }

    const systemPrompt = `You are a professional financial research assistant embedded in a quantitative trading terminal. You have access to real-time data about stocks.

${ticker ? `The user is currently analysing: ${ticker}` : ""}
${context ? `\nCurrent market data snapshot:\n${JSON.stringify(context, null, 2)}` : ""}

Your role:
- Provide balanced, research-quality analysis drawing on the data provided
- Explain financial concepts clearly — use plain language unless the user asks for technical detail
- When discussing outlook or price movements, present multiple scenarios (bull case, bear case, base case) rather than a single directional call
- Highlight both risks and opportunities
- Reference specific numbers from the data when relevant (P/E, RSI, analyst targets, etc.)
- Never give a direct "buy" or "sell" instruction — instead, describe what the data suggests and let the user draw their own conclusions
- Be honest about uncertainty and data limitations
- Keep responses concise but substantive — 2–5 paragraphs unless a longer answer is genuinely warranted

Important: You are not a licensed financial advisor. Always remind the user of this if they ask for explicit trading recommendations.`;

    res.setHeader("Content-Type", "text/event-stream");
    res.setHeader("Cache-Control", "no-cache");
    res.setHeader("Connection", "keep-alive");

    const stream = await openai.chat.completions.create({
      model: "gpt-5.1",
      max_completion_tokens: 8192,
      messages: [
        { role: "system", content: systemPrompt },
        ...messages,
      ],
      stream: true,
    });

    for await (const chunk of stream) {
      const content = chunk.choices[0]?.delta?.content;
      if (content) {
        res.write(`data: ${JSON.stringify({ content })}\n\n`);
      }
    }

    res.write(`data: ${JSON.stringify({ done: true })}\n\n`);
    res.end();
  } catch (err) {
    req.log.error(err, "AI chat error");
    if (!res.headersSent) {
      res.status(500).json({ error: "AI service unavailable" });
    } else {
      res.write(`data: ${JSON.stringify({ error: "Stream interrupted" })}\n\n`);
      res.end();
    }
  }
});

export default router;
