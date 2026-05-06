import { ToolCall } from "../types";

export function parseToolCalls(raw: string): ToolCall[] {
  try {
    return JSON.parse(raw);
  } catch {
    throw new Error("Failed to parse tool calls");
  }
}