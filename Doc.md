# Unity MCP Server (WebSocket + Ollama + Qwen)

A local AI-powered scene generation system that connects a language model to Unity using WebSockets.

This project allows you to control the Unity Editor using natural language, powered by a local LLM via Ollama.

---

## Features

* WebSocket-based real-time communication with Unity
* Natural language → structured tool execution
* Extensible tool system (create objects, lights, environments)
* Foundation for AI scene generation & level design
* Fully local (no external APIs required)

---

## Architecture

```
User Prompt
   ↓
Ollama (Qwen3:8b)
   ↓
Node.js MCP Server
   ├── Tool Registry
   ├── Planner (NL → Tool Calls)
   ├── Scene State
   └── WebSocket Server
           ↓
        Unity Client
```

---

## Project Structure

```
mcp-unity-server/
│
├── src/
│   ├── index.ts
│   ├── websocket.ts
│   ├── ollama.ts
│   ├── tools/
│   │   ├── registry.ts
│   │   └── executor.ts
│   ├── planner/
│   │   └── planner.ts
│   ├── state/
│   │   └── sceneState.ts
│   └── types.ts
│
├── package.json
├── tsconfig.json
```

---

## Requirements

* Node.js (v18+ recommended)
* Unity (2021+)
* Ollama installed locally
* Qwen model pulled:

```bash
ollama pull qwen3:8b
```

---

## Installation

### 1. Clone the repo

```bash
git clone <your-repo-url>
cd mcp-unity-server
```

---

### 2. Install dependencies

```bash
npm install
```

---

### 3. Run the server

```bash
npx ts-node src/index.ts
```

You should see:

```
WebSocket server running on ws://localhost:3001
```

---

## Start Ollama

In a separate terminal:

```bash
ollama run qwen3:8b
```

---

## Unity Setup

### 1. Install WebSocket package

Use:

* NativeWebSocket (recommended)

---

### 2. Add WebSocket Client Script

Attach your `WSClient` script to a GameObject in your scene.

---

### 3. Enter Play Mode

When Unity starts, you should see:

```
Connected to server
Unity connected
```

---

## Testing the System

In the terminal running your Node server, type:

```bash
Create a cube at 0 1 0
```

Or:

```bash
Create a floor and add a light above it
```

---

## Execution Flow

1. You type a prompt
2. Prompt is sent to Qwen via Ollama
3. Model returns structured tool calls (JSON)
4. Node parses and executes tools
5. Commands sent via WebSocket
6. Unity executes actions in real-time

---

## Available Tools (Example)

Defined in:

```
src/tools/registry.ts
```

Example:

```ts
create_cube
create_plane
add_light
```

---

## ➕ Adding New Tools

### 1. Define tool

```ts
{
  name: "create_sphere",
  description: "Create a sphere",
  parameters: { x: "number", y: "number", z: "number" }
}
```

---

### 2. Handle execution

```ts
case "create_sphere":
  return sendToUnity("create_sphere", call.args);
```

---

### 3. Implement in Unity

Add logic in your `ExecuteAction()` method.

---

## Common Issues

### Unity not connecting

* Check WebSocket URL (`ws://localhost:3001`)
* Ensure Play Mode is active
* Ensure `DispatchMessageQueue()` is called

---

### Model not returning JSON

Update system prompt in `ollama.ts`:

```ts
You must ONLY return valid JSON array of tool calls.
No explanations.
```

---

### JSON parsing fails

Log raw output:

```ts
console.log(raw);
```

---

## Limitations

* The model can only use tools you define
* Qwen may occasionally output invalid JSON
* No visual feedback loop (yet)

---

## Future Improvements

* Scene state awareness (send objects back to model)
* Action planning loop (multi-step refinement)
* Screenshot → AI (multimodal)
* Higher-level tools (create_room, build_house)
* Better structured output (function calling style)

---

## Example Prompt

```
Create a small room with a floor, four walls, and a light
```

---

## Notes

* This is a foundation for building:

  * AI level designers
  * Procedural scene generators
  * Unity dev assistants

---

## Contributing

Feel free to extend:

* Tool system
* Planner logic
* Unity actions

---

## License

MIT

---

## Final Thought

This is not just a script — it's the start of an **AI-driven game development workflow**.

---
