import { WebSocketServer, WebSocket } from "ws";

let unityClient: WebSocket | null = null;

export function startWSServer() {
  const wss = new WebSocketServer({ port: 3001 });

  wss.on("connection", (ws) => {
    console.log("Client connected");

    ws.on("message", (msg) => {
      const data = JSON.parse(msg.toString());

      if (data.type === "unity_ready") {
        unityClient = ws;
        console.log("Unity connected");
      }

      if (data.type === "response") {
        console.log("Unity response:", data);
      }

      if (data.type === "scene_state") {
        console.log("Scene updated:", data);
      }
    });

    ws.on("close", () => {
      if (ws === unityClient) {
        unityClient = null;
        console.log("Unity disconnected");
      }
    });
  });

  console.log("WebSocket server running on ws://localhost:3001");
}

export function sendToUnity(action: string, payload: any) {
  if (!unityClient) throw new Error("Unity not connected");

  unityClient.send(
    JSON.stringify({
      type: "action",
      action,
      payload,
    })
  );
}