import { sendToUnity } from "../websocket";
import { ToolCall } from "../types";

export async function executeTool(call: ToolCall) {
  switch (call.name) {
    case "create_cube":
      return sendToUnity("create_cube", call.args);

    case "create_plane":
      return sendToUnity("create_plane", {});

    case "add_light":
      return sendToUnity("add_light", call.args);

    default:
      throw new Error(`Unknown tool: ${call.name}`);
  }
}