import express from "express";
import { startWSServer } from "./websocket";
import { queryLLM } from "./ollama";
import { parseToolCalls } from "./planner/planner";
import { executeTool } from "./tools/executor";

const app = express();
startWSServer();
const PORT = 3000;

app.use(express.json());


async function run(prompt: string) {
  console.log("Prompt:", prompt);

  const raw = await queryLLM(prompt);
  console.log("LLM Output:", raw);

  const calls = parseToolCalls(raw);

  for (const call of calls) {
    await executeTool(call);
  }
}

// CLI test
process.stdin.on("data", (data) => {
  run(data.toString().trim());
});

app.listen(PORT, () => {
  console.log(`Server running on http://localhost:${PORT}`);
});