const fetch = (globalThis as any).fetch;
import { tools } from "./tools/registry";

export async function queryLLM(prompt: string) {
  const res = await fetch("http://localhost:11434/api/chat", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      model: "qwen3:8b",
      messages: [
        {
          role: "system",
          content: `
You are a Unity scene designer AI.
Only respond with tool calls in JSON.
Available tools: ${tools.map(t => t.name).join(", ")}
`,
        },
        {
          role: "user",
          content: prompt,
        },
      ],
    }),
  });

  const data = await res.json();
  return data.message.content;
}