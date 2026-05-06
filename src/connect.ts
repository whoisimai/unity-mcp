import WebSocket, { WebSocketServer } from "ws";

const wss = new WebSocketServer({ port: 3001 });

let unityClient: WebSocket | null = null;

wss.on("connection", (ws) => {
  console.log("Client connected");

  ws.on("message", (message) => {
    const data = JSON.parse(message.toString());

    if (data.type === "unity_ready") {
      unityClient = ws;
      console.log("Unity connected");
    }

    if (data.type === "response") {
      console.log("Unity response:", data);
    }
  });

  ws.on("close", () => {
    if (ws === unityClient) unityClient = null;
  });
});