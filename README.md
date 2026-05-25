# Unity Ollama MCP Server

Connect **qwen3:8b** (via Ollama) to Unity Editor for AI-assisted game development and VR environment creation.

---

## Architecture

```
Unity Editor (C# Bridge) ──► Ollama (qwen3:8b)
                                    ▲
Claude Desktop / MCP Client ───────┘
         (via MCP server.js)
```

---

## Quick Start

### 1. Install the MCP server

```bash
cd unity-ollama-mcp
npm install
```

### 2. Verify Ollama is running

```bash
ollama run qwen3:8b
# Keep it running, then Ctrl+C to exit the chat (server stays up)
```

### 3. Test the server

```bash
node server.js
# Should print: Unity Ollama MCP Server running | Model: qwen3:8b
```

### 4. Register with Claude Desktop

Edit `~/Library/Application Support/Claude/claude_desktop_config.json` (macOS)  
or `%APPDATA%\Claude\claude_desktop_config.json` (Windows):

```json
{
  "mcpServers": {
    "unity-ollama": {
      "command": "node",
      "args": ["/ABSOLUTE/PATH/TO/unity-ollama-mcp/server.js"],
      "env": {
        "OLLAMA_URL": "http://localhost:11434",
        "OLLAMA_MODEL": "qwen3:8b"
      }
    }
  }
}
```

Restart Claude Desktop. You'll see the 🔧 tools icon with your Unity tools.

---

## Unity Editor Setup (Direct Mode)

If you want to use AI *inside* the Unity Editor without going through Claude Desktop:

1. Copy `unity-scripts/UnityMCPBridge.cs` → `YourProject/Assets/Editor/UnityMCPBridge.cs`
2. Unity will compile it automatically
3. Open via menu: **Tools → Unity MCP Bridge (Ollama)**
4. Make sure Ollama is running on `localhost:11434`

### Editor Window Tabs

| Tab | What it does |
|-----|-------------|
| 💬 Ask AI | Free-form prompt to qwen3:8b. Can save response as `.cs` file |
| 🏗️ Build Scene | Describe a scene → get JSON → click to build it in the editor |
| 🥽 VR Design | Design complete VR environments with XR Toolkit setup |
| 🔧 Refactor | Paste code → get review + refactored version |

---

## MCP Tools (for Claude Desktop)

| Tool | Description |
|------|-------------|
| `generate_unity_script` | Generate MonoBehaviour, ScriptableObject, Editor scripts |
| `build_scene_json` | Describe a scene → get buildable JSON |
| `design_vr_environment` | Full VR environment design with XR Rig setup |
| `explain_unity_concept` | Explain Unity/XR APIs with examples |
| `refactor_unity_code` | Review and improve C# code |
| `generate_prefab_layout` | Position a list of prefabs in a scene |
| `write_xr_interaction` | Write XR Toolkit interaction scripts |

---

## Example Prompts (Claude Desktop)

> "Build a scene JSON for a VR art gallery with 5 floating display pedestals, soft ambient lighting, and a skylight"

> "Generate a Unity script called HandPhysicsController that simulates realistic hand physics in VR using XR Toolkit"

> "Design a VR training simulator environment for a hospital ward — Quest platform, 6x8 metres, with grab interactions and teleportation"

> "Refactor this Unity code for performance — it's running in Update() and causing GC spikes" *(paste code)*

---

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `OLLAMA_URL` | `http://localhost:11434` | Ollama server URL |
| `OLLAMA_MODEL` | `qwen3:8b` | Model to use |

---

## Troubleshooting

**"Connection refused" from Unity Bridge**  
→ Make sure `ollama serve` is running: `ollama serve &`

**Slow responses**  
→ qwen3:8b needs ~6GB RAM. Close other apps. Or try `qwen3:4b` for speed.

**Scene JSON builds wrong objects**  
→ Click "Generate Scene JSON" again — LLM output varies. Check the JSON preview before building.

**Claude Desktop doesn't show tools**  
→ Check the absolute path in `claude_desktop_config.json`. Run `node server.js` manually to confirm no errors.
