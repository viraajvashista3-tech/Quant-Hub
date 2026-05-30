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

    const contextSection = context
      ? `## Live Market Data Snapshot
\`\`\`json
${JSON.stringify(context, null, 2)}
\`\`\``
      : "";

    const systemPrompt = `You are a senior quantitative analyst embedded in a professional trading terminal. You have real-time access to market data and provide institutional-quality research.

${ticker ? `**Currently analysing: ${ticker}**` : ""}
${contextSection}

## Your mandate
- Deliver research-grade analysis grounded in the data provided above
- Be specific — cite actual numbers (RSI levels, P/E ratios, price targets, beta, margins)
- Structure every response clearly using markdown: use **bold** for key figures, ## headings for sections, bullet points for lists
- Present **Bull Case**, **Bear Case**, and **Base Case** when discussing outlook
- Highlight both quantitative signals AND qualitative risks
- Be direct and confident — avoid vague language like "it could go either way"
- Be honest about data limitations and uncertainty

## Formatting rules (always follow)
- Use ## for major sections, ### for sub-sections
- Use **bold** for key numbers and important insights
- Use bullet points (-) for lists of factors or risks
- Use > blockquote for important caveats or warnings
- Keep total response to 300-600 words unless complexity demands more
- End with a "**Key Watch Items**" section listing 2-3 specific metrics or events to monitor

## Hard rules
- Never say "buy" or "sell" as a direct recommendation — describe what the data implies
- Always include: "> This is research assistance only, not financial advice."
- Never make up data not provided — if something is missing from the snapshot, say so`;

    res.setHeader("Content-Type", "text/event-stream");
    res.setHeader("Cache-Control", "no-cache");
    res.setHeader("Connection", "keep-alive");

    const stream = await openai.chat.completions.create({
      model: "gpt-4.1",
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
