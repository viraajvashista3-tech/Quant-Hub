import { useState, useRef, useEffect, useCallback } from "react";
import { useTicker } from "@/lib/ticker-context";
import { useGetStockOverview, useGetStockFundamentals, getGetStockOverviewQueryKey, getGetStockFundamentalsQueryKey } from "@workspace/api-client-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { Badge } from "@/components/ui/badge";
import { Send, Bot, User, AlertTriangle, Loader2, RotateCcw, ChevronDown } from "lucide-react";

interface Message {
  role: "user" | "assistant";
  content: string;
  streaming?: boolean;
}

const SUGGESTED_QUESTIONS = [
  "What are the key risks facing this company right now?",
  "How does the valuation compare to historical norms?",
  "What does the technical picture suggest about near-term momentum?",
  "Walk me through the bull case and bear case for this stock.",
  "What should I watch in the next earnings report?",
  "How sensitive is this stock to interest rate changes?",
];

export default function AiChat() {
  const { activeTicker } = useTicker();
  const [messages, setMessages] = useState<Message[]>([]);
  const [input, setInput] = useState("");
  const [isStreaming, setIsStreaming] = useState(false);
  const abortRef = useRef<AbortController | null>(null);
  const bottomRef = useRef<HTMLDivElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  const { data: overview } = useGetStockOverview(activeTicker, {
    query: { enabled: !!activeTicker, queryKey: getGetStockOverviewQueryKey(activeTicker) },
  });
  const { data: fundamentals } = useGetStockFundamentals(activeTicker, {
    query: { enabled: !!activeTicker, queryKey: getGetStockFundamentalsQueryKey(activeTicker) },
  });

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages]);

  const buildContext = useCallback(() => {
    if (!overview && !fundamentals) return undefined;
    return {
      ticker: activeTicker,
      price: overview?.price,
      changePercent: overview?.changePercent,
      quantScore: overview?.quantScore,
      signal: overview?.signal,
      rsi: overview?.rsi,
      macd: overview?.macd,
      beta: overview?.beta,
      annualizedVolatility: overview?.annualizedVolatility,
      sentimentScore: overview?.sentimentScore,
      marketCap: fundamentals?.marketCap,
      pe: fundamentals?.pe,
      forwardPe: fundamentals?.forwardPe,
      peg: fundamentals?.peg,
      evToEbitda: fundamentals?.evToEbitda,
      debtToEquity: fundamentals?.debtToEquity,
      returnOnEquity: fundamentals?.returnOnEquity,
      profitMargins: fundamentals?.profitMargins,
      revenueGrowth: fundamentals?.revenueGrowth,
      earningsGrowth: fundamentals?.earningsGrowth,
      fiftyTwoWeekHigh: fundamentals?.fiftyTwoWeekHigh,
      fiftyTwoWeekLow: fundamentals?.fiftyTwoWeekLow,
      sector: overview?.sector,
      name: overview?.name,
    };
  }, [activeTicker, overview, fundamentals]);

  const sendMessage = useCallback(async (text: string) => {
    if (!text.trim() || isStreaming) return;

    const userMsg: Message = { role: "user", content: text.trim() };
    const newMessages = [...messages, userMsg];
    setMessages(newMessages);
    setInput("");
    setIsStreaming(true);

    const assistantMsg: Message = { role: "assistant", content: "", streaming: true };
    setMessages((prev) => [...prev, assistantMsg]);

    abortRef.current = new AbortController();

    try {
      const BASE = import.meta.env.BASE_URL.replace(/\/$/, "");
      const res = await fetch(`${BASE}/api/ai/chat`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        signal: abortRef.current.signal,
        body: JSON.stringify({
          ticker: activeTicker || undefined,
          context: buildContext(),
          messages: newMessages.map((m) => ({ role: m.role, content: m.content })),
        }),
      });

      if (!res.ok) throw new Error(`HTTP ${res.status}`);

      const reader = res.body!.getReader();
      const decoder = new TextDecoder();
      let buffer = "";
      let fullContent = "";

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split("\n");
        buffer = lines.pop() ?? "";
        for (const line of lines) {
          if (!line.startsWith("data: ")) continue;
          try {
            const parsed = JSON.parse(line.slice(6));
            if (parsed.content) {
              fullContent += parsed.content;
              setMessages((prev) => {
                const updated = [...prev];
                updated[updated.length - 1] = { role: "assistant", content: fullContent, streaming: true };
                return updated;
              });
            }
            if (parsed.done) {
              setMessages((prev) => {
                const updated = [...prev];
                updated[updated.length - 1] = { role: "assistant", content: fullContent, streaming: false };
                return updated;
              });
            }
          } catch {}
        }
      }
    } catch (err: unknown) {
      if (err instanceof Error && err.name === "AbortError") {
        setMessages((prev) => {
          const updated = [...prev];
          if (updated[updated.length - 1]?.streaming) {
            updated[updated.length - 1] = { ...updated[updated.length - 1], streaming: false };
          }
          return updated;
        });
      } else {
        setMessages((prev) => {
          const updated = [...prev];
          updated[updated.length - 1] = {
            role: "assistant",
            content: "Sorry, something went wrong. Please try again.",
            streaming: false,
          };
          return updated;
        });
      }
    } finally {
      setIsStreaming(false);
      abortRef.current = null;
      setTimeout(() => textareaRef.current?.focus(), 100);
    }
  }, [messages, isStreaming, activeTicker, buildContext]);

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      sendMessage(input);
    }
  };

  const stopStream = () => {
    abortRef.current?.abort();
  };

  const clearChat = () => {
    if (isStreaming) abortRef.current?.abort();
    setMessages([]);
    setInput("");
  };

  return (
    <div className="flex flex-col h-full max-h-[calc(100vh-6rem)]">
      {/* Header */}
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-3xl font-bold tracking-tight text-primary uppercase flex items-center gap-3">
            <Bot className="h-7 w-7" />
            AI Research Assistant
            {activeTicker && <span className="text-foreground font-normal text-xl">— {activeTicker}</span>}
          </h1>
          <p className="text-muted-foreground text-sm mt-0.5">
            Powered by GPT-5 · Context-aware analysis using live market data
          </p>
        </div>
        {messages.length > 0 && (
          <Button variant="ghost" size="sm" onClick={clearChat} className="gap-2 text-muted-foreground hover:text-foreground">
            <RotateCcw className="h-3.5 w-3.5" />
            New chat
          </Button>
        )}
      </div>

      {/* Disclaimer */}
      <Card className="bg-amber-950/20 border-amber-800/50 rounded-none mb-4">
        <CardContent className="p-3 flex gap-2 items-start">
          <AlertTriangle className="h-4 w-4 text-amber-500 mt-0.5 shrink-0" />
          <p className="text-xs text-amber-200/80 leading-relaxed">
            <span className="font-semibold text-amber-400">Disclaimer:</span> This AI provides research assistance only and may be inaccurate. It is not financial advice and does not constitute a recommendation to buy or sell any security. Always do your own research and consult a qualified advisor before making investment decisions.
          </p>
        </CardContent>
      </Card>

      {/* Chat area */}
      <div className="flex-1 overflow-y-auto space-y-4 mb-4 pr-1 min-h-0">
        {messages.length === 0 ? (
          <div className="space-y-6">
            {activeTicker && overview && (
              <Card className="bg-card border-border rounded-none">
                <CardContent className="p-4">
                  <div className="flex items-center gap-3 mb-3">
                    <div>
                      <span className="font-mono font-bold text-primary text-lg">{activeTicker}</span>
                      <span className="text-muted-foreground ml-2 text-sm">{overview.name}</span>
                    </div>
                    <Badge variant={overview.signal === "BUY" ? "default" : overview.signal === "AVOID" ? "destructive" : "secondary"} className="rounded-none text-xs ml-auto">
                      {overview.signal}
                    </Badge>
                  </div>
                  <div className="grid grid-cols-3 gap-3 text-xs">
                    <div><span className="text-muted-foreground">Price</span><div className="font-mono font-bold">${overview.price?.toFixed(2)}</div></div>
                    <div><span className="text-muted-foreground">Quant Score</span><div className="font-mono font-bold">{overview.quantScore?.toFixed(1)}</div></div>
                    <div><span className="text-muted-foreground">RSI</span><div className="font-mono font-bold">{overview.rsi?.toFixed(1)}</div></div>
                    {fundamentals?.pe && <div><span className="text-muted-foreground">P/E</span><div className="font-mono font-bold">{fundamentals.pe.toFixed(1)}x</div></div>}
                    {fundamentals?.profitMargins && <div><span className="text-muted-foreground">Net Margin</span><div className="font-mono font-bold">{(fundamentals.profitMargins * 100).toFixed(1)}%</div></div>}
                    {overview.beta && <div><span className="text-muted-foreground">Beta</span><div className="font-mono font-bold">{overview.beta.toFixed(2)}</div></div>}
                  </div>
                </CardContent>
              </Card>
            )}

            <div>
              <p className="text-xs text-muted-foreground uppercase tracking-widest mb-3 flex items-center gap-2">
                <ChevronDown className="h-3 w-3" /> Suggested questions
              </p>
              <div className="grid grid-cols-1 gap-2">
                {SUGGESTED_QUESTIONS.map((q) => (
                  <button
                    key={q}
                    onClick={() => sendMessage(q)}
                    className="text-left text-sm px-4 py-3 border border-border bg-card hover:border-primary/60 hover:bg-primary/5 transition-colors rounded-none text-foreground/80 hover:text-foreground"
                  >
                    {q}
                  </button>
                ))}
              </div>
            </div>
          </div>
        ) : (
          messages.map((msg, i) => (
            <div key={i} className={`flex gap-3 ${msg.role === "user" ? "justify-end" : "justify-start"}`}>
              {msg.role === "assistant" && (
                <div className="w-7 h-7 rounded-full bg-primary/20 border border-primary/40 flex items-center justify-center shrink-0 mt-0.5">
                  <Bot className="h-3.5 w-3.5 text-primary" />
                </div>
              )}
              <div className={`max-w-[80%] rounded-none px-4 py-3 text-sm leading-relaxed whitespace-pre-wrap ${
                msg.role === "user"
                  ? "bg-primary/15 border border-primary/30 text-foreground"
                  : "bg-card border border-border text-foreground"
              }`}>
                {msg.content}
                {msg.streaming && (
                  <span className="inline-block ml-1 w-1.5 h-4 bg-primary/70 animate-pulse align-text-bottom" />
                )}
              </div>
              {msg.role === "user" && (
                <div className="w-7 h-7 rounded-full bg-muted border border-border flex items-center justify-center shrink-0 mt-0.5">
                  <User className="h-3.5 w-3.5 text-muted-foreground" />
                </div>
              )}
            </div>
          ))
        )}
        <div ref={bottomRef} />
      </div>

      {/* Input */}
      <div className="border border-border bg-card rounded-none p-3 flex gap-3 items-end">
        <Textarea
          ref={textareaRef}
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder={activeTicker ? `Ask anything about ${activeTicker}…` : "Select a ticker, then ask a question…"}
          className="flex-1 bg-transparent border-none resize-none focus-visible:ring-0 focus-visible:ring-offset-0 p-0 min-h-[40px] max-h-[120px] text-sm placeholder:text-muted-foreground/50"
          rows={1}
          disabled={isStreaming}
        />
        {isStreaming ? (
          <Button onClick={stopStream} size="sm" variant="ghost" className="shrink-0 text-muted-foreground hover:text-foreground h-9 px-3">
            <Loader2 className="h-4 w-4 animate-spin mr-1" />
            Stop
          </Button>
        ) : (
          <Button
            onClick={() => sendMessage(input)}
            disabled={!input.trim()}
            size="sm"
            className="shrink-0 h-9 px-4 rounded-none gap-2"
          >
            <Send className="h-3.5 w-3.5" />
            Send
          </Button>
        )}
      </div>
      <p className="text-[10px] text-muted-foreground/50 text-center mt-1.5">Press Enter to send · Shift+Enter for new line</p>
    </div>
  );
}
