import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
} from "@modelcontextprotocol/sdk/types.js";

const OLLAMA_BASE_URL = process.env.OLLAMA_URL || "http://localhost:11434";
const OLLAMA_MODEL = process.env.OLLAMA_MODEL || "qwen3:4b";

// Ollama helper
async function ollamaChat(systemPrompt, userMessage, temperature = 0.4) {
  const response = await fetch(`${OLLAMA_BASE_URL}/api/chat`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      model: OLLAMA_MODEL,
      stream: false,
      options: { temperature },
      messages: [
        { role: "system", content: systemPrompt },
        { role: "user", content: userMessage },
      ],
    }),
  });

  if (!response.ok) {
    const err = await response.text();
    throw new Error(`Ollama error ${response.status}: ${err}`);
  }

  const data = await response.json();
  return data.message?.content ?? "";
}

// System prompts
const UNITY_DEV_SYSTEM = `You are an expert Unity developer specialising in C#, Unity Engine APIs, 
XR/VR development (Unity XR Toolkit, OpenXR), and real-time 3D. 
Produce clean, well-commented, production-ready Unity C# code.
Always include necessary using directives. Prefer Unity best practices.
When writing MonoBehaviours, use [SerializeField] over public fields.
Return only the requested code or explanation — no meta-commentary.`;

const SCENE_BUILDER_SYSTEM = `You are a Unity scene architect. You output structured JSON scene 
descriptions that can be parsed by a Unity Editor script to build scenes programmatically.
Scene JSON format:
{
  "sceneName": "string",
  "objects": [
    {
      "name": "string",
      "primitive": "Cube|Sphere|Plane|Cylinder|Capsule|Quad|Empty",
      "position": [x, y, z],
      "rotation": [x, y, z],
      "scale": [x, y, z],
      "parent": "parentName or null",
      "tag": "string or null",
      "layer": "string or null",
      "components": ["ComponentName"],
      "material": { "color": [r, g, b, a], "metallic": 0.0, "smoothness": 0.5 }
    }
  ],
  "lighting": {
    "ambientColor": [r, g, b],
    "sunColor": [r, g, b],
    "sunIntensity": 1.0,
    "sunRotation": [x, y, z]
  }
}
Return ONLY valid JSON. No markdown fences, no explanation.`;

const VR_SYSTEM = `You are a VR/XR environment designer using Unity XR Toolkit and OpenXR.
You design immersive, comfortable VR experiences following best practices:
- Locomotion: prefer teleportation for comfort, snap turning
- Scale: 1 Unity unit = 1 metre
- Performance: mobile VR targets 72fps, PC VR 90fps
- Interaction: use XRGrabInteractable, XRRayInteractor patterns
Output structured JSON or C# as requested.`;

const REFACTOR_SYSTEM = `You are a senior Unity C# code reviewer. 
Analyse code for: performance (GC allocations, Update() overhead, draw calls),
Unity best practices, XR-specific issues, and architecture.
Provide specific, actionable improvements with corrected code snippets.`;

// Tool definitions
const TOOLS = [
  {
    name: "generate_unity_script",
    description:
      "Generate a Unity C# MonoBehaviour or ScriptableObject script. Great for controllers, managers, gameplay systems, XR interactions.",
    inputSchema: {
      type: "object",
      properties: {
        description: {
          type: "string",
          description:
            "What the script should do. Be specific — include behaviours, events, interactions.",
        },
        script_type: {
          type: "string",
          enum: ["MonoBehaviour", "ScriptableObject", "Editor", "Interface", "Utility"],
          description: "Type of Unity script to generate",
        },
        class_name: {
          type: "string",
          description: "Desired class name (PascalCase)",
        },
        xr_enabled: {
          type: "boolean",
          description: "Include XR Toolkit / VR-specific code",
        },
      },
      required: ["description", "class_name"],
    },
  },
  {
    name: "build_scene_json",
    description:
      "Generate a JSON scene description that the UnityMCPBridge script can parse to build a Unity scene automatically. Describe the scene in plain English.",
    inputSchema: {
      type: "object",
      properties: {
        description: {
          type: "string",
          description:
            "Describe the scene: purpose, objects, layout, lighting, mood. E.g. 'A cozy VR living room with a sofa, coffee table, fireplace, warm lighting'",
        },
        scene_name: {
          type: "string",
          description: "Name for the Unity scene",
        },
        is_vr: {
          type: "boolean",
          description: "Optimise layout and scale for VR (1 unit = 1 metre)",
        },
      },
      required: ["description", "scene_name"],
    },
  },
  {
    name: "design_vr_environment",
    description:
      "Design a complete VR environment with interaction zones, locomotion setup, XR rig placement, and comfort guidelines.",
    inputSchema: {
      type: "object",
      properties: {
        environment_type: {
          type: "string",
          description:
            "Type of VR environment, e.g. 'office', 'forest', 'spaceship', 'training simulator', 'art gallery'",
        },
        interactions: {
          type: "array",
          items: { type: "string" },
          description:
            "List of interactions needed, e.g. ['grab objects', 'teleport', 'UI panels', 'hand tracking']",
        },
        target_platform: {
          type: "string",
          enum: ["Quest", "PC VR", "Both"],
          description: "Target VR platform",
        },
        size_metres: {
          type: "string",
          description: "Approximate play space size, e.g. '5x5', '10x10', 'room-scale'",
        },
      },
      required: ["environment_type"],
    },
  },
  {
    name: "explain_unity_concept",
    description:
      "Explain a Unity or XR concept, API, or pattern with practical examples tailored to your project.",
    inputSchema: {
      type: "object",
      properties: {
        concept: {
          type: "string",
          description:
            "The concept or API to explain. E.g. 'XRGrabInteractable events', 'Unity Job System', 'Scriptable Object architecture'",
        },
        context: {
          type: "string",
          description: "Optional: your project context so the explanation is relevant",
        },
        include_example: {
          type: "boolean",
          description: "Include a working code example",
        },
      },
      required: ["concept"],
    },
  },
  {
    name: "refactor_unity_code",
    description:
      "Review and refactor Unity C# code for performance, best practices, and XR compatibility.",
    inputSchema: {
      type: "object",
      properties: {
        code: {
          type: "string",
          description: "The C# code to review and refactor",
        },
        focus: {
          type: "string",
          enum: ["performance", "architecture", "xr", "all"],
          description: "Focus area for the review",
        },
      },
      required: ["code"],
    },
  },
  {
    name: "generate_prefab_layout",
    description:
      "Generate a layout plan for a set of prefabs in a Unity scene — positions, rotations, groupings — as JSON.",
    inputSchema: {
      type: "object",
      properties: {
        prefabs: {
          type: "array",
          items: { type: "string" },
          description: "List of prefab names to place",
        },
        space_description: {
          type: "string",
          description: "Describe the space and how prefabs should be arranged",
        },
        is_vr: {
          type: "boolean",
          description: "Use VR-scale positioning (metres)",
        },
      },
      required: ["prefabs", "space_description"],
    },
  },
  {
    name: "write_xr_interaction",
    description:
      "Write a complete XR Toolkit interaction script (grab, socket, climb, UI, hover effects, haptics).",
    inputSchema: {
      type: "object",
      properties: {
        interaction_type: {
          type: "string",
          description:
            "Type of interaction, e.g. 'two-handed grab', 'socket interactor with snapping', 'haptic feedback on hover', 'climbing system'",
        },
        object_description: {
          type: "string",
          description: "What object the interaction applies to",
        },
        haptics: {
          type: "boolean",
          description: "Include haptic feedback",
        },
      },
      required: ["interaction_type"],
    },
  },
];

// Tool handlers
async function handleTool(name, args) {
  switch (name) {
    // Generate Unity Script
    case "generate_unity_script": {
      const { description, class_name, script_type = "MonoBehaviour", xr_enabled = false } = args;
      const xrNote = xr_enabled
        ? "This script MUST use Unity XR Toolkit (com.unity.xr.interaction.toolkit). Import UnityEngine.XR.Interaction.Toolkit as needed."
        : "";
      const prompt = `Generate a Unity ${script_type} C# script named "${class_name}".
${xrNote}
Requirements: ${description}

Return ONLY the complete .cs file content, starting with the using directives.`;
      const result = await ollamaChat(UNITY_DEV_SYSTEM, prompt);
      return { content: [{ type: "text", text: result }] };
    }

    // Build Scene JSON
    case "build_scene_json": {
      const { description, scene_name, is_vr = false } = args;
      const vrNote = is_vr
        ? "Use real-world scale: 1 unit = 1 metre. Player eye height ~1.6m. Ensure comfortable VR proportions."
        : "";
      const prompt = `Create a Unity scene JSON for scene named "${scene_name}".
${vrNote}
Scene description: ${description}

Return ONLY valid JSON following the exact schema specified. No markdown.`;
      const result = await ollamaChat(SCENE_BUILDER_SYSTEM, prompt, 0.5);
      // Validate it's JSON
      try {
        JSON.parse(result);
      } catch {
        // Try to extract JSON if wrapped in markdown
        const match = result.match(/```(?:json)?\s*([\s\S]*?)```/);
        if (match) return { content: [{ type: "text", text: match[1].trim() }] };
      }
      return { content: [{ type: "text", text: result }] };
    }

    // Design VR Environment
    case "design_vr_environment": {
      const {
        environment_type,
        interactions = [],
        target_platform = "Both",
        size_metres = "room-scale",
      } = args;
      const prompt = `Design a complete VR environment in Unity.
Environment type: ${environment_type}
Target platform: ${target_platform}
Play space: ${size_metres}
Required interactions: ${interactions.join(", ") || "standard locomotion and interaction"}

Provide:
1. XR Rig setup (camera offset, controller mappings, locomotion providers)
2. Scene layout with key zones and waypoints
3. Lighting recommendations for VR comfort
4. Performance budget guidance for ${target_platform}
5. C# XR Manager script stub
6. Comfort considerations (vignette, snap turn, teleport anchors)`;
      const result = await ollamaChat(VR_SYSTEM, prompt, 0.6);
      return { content: [{ type: "text", text: result }] };
    }

    // Explain Unity Concept
    case "explain_unity_concept": {
      const { concept, context = "", include_example = true } = args;
      const prompt = `Explain: ${concept}
${context ? `Project context: ${context}` : ""}
${include_example ? "Include a practical, working code example." : ""}
Be concise but complete. Focus on practical usage over theory.`;
      const result = await ollamaChat(UNITY_DEV_SYSTEM, prompt, 0.3);
      return { content: [{ type: "text", text: result }] };
    }

    // Refactor Unity Code
    case "refactor_unity_code": {
      const { code, focus = "all" } = args;
      const prompt = `Review this Unity C# code with focus on: ${focus}.
\`\`\`csharp
${code}
\`\`\`
Provide: issues found, refactored code, explanation of changes.`;
      const result = await ollamaChat(REFACTOR_SYSTEM, prompt, 0.2);
      return { content: [{ type: "text", text: result }] };
    }

    // Generate Prefab Layout
    case "generate_prefab_layout": {
      const { prefabs, space_description, is_vr = false } = args;
      const prompt = `Generate Unity scene JSON layout for these prefabs: ${prefabs.join(", ")}.
Space: ${space_description}
${is_vr ? "VR scale: 1 unit = 1 metre." : ""}
Use the standard scene JSON schema. Return ONLY valid JSON.`;
      const result = await ollamaChat(SCENE_BUILDER_SYSTEM, prompt, 0.5);
      return { content: [{ type: "text", text: result }] };
    }

    // Write XR Interaction
    case "write_xr_interaction": {
      const { interaction_type, object_description = "generic interactable object", haptics = false } = args;
      const prompt = `Write a complete Unity XR Toolkit C# script for: ${interaction_type}
Applied to: ${object_description}
${haptics ? "Include haptic impulse feedback via XRBaseController.SendHapticImpulse()." : ""}
Use Unity XR Interaction Toolkit 2.x+ APIs.
Return only the complete .cs file.`;
      const result = await ollamaChat(UNITY_DEV_SYSTEM, prompt);
      return { content: [{ type: "text", text: result }] };
    }

    default:
      throw new Error(`Unknown tool: ${name}`);
  }
}

// MCP Server setup 
const server = new Server(
  { name: "unity-ollama-mcp", version: "1.0.0" },
  { capabilities: { tools: {} } }
);

server.setRequestHandler(ListToolsRequestSchema, async () => ({ tools: TOOLS }));

server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name, arguments: args } = request.params;
  try {
    return await handleTool(name, args);
  } catch (error) {
    return {
      content: [{ type: "text", text: `Error: ${error.message}` }],
      isError: true,
    };
  }
});

// ── Start ──────────────────────────────────────────────────────────────────────
const transport = new StdioServerTransport();
await server.connect(transport);
console.error(`Unity Ollama MCP Server running | Model: ${OLLAMA_MODEL} | Ollama: ${OLLAMA_BASE_URL}`);
