// UnityMCPBridge.cs
// Place this file in your Unity project under: Assets/Editor/UnityMCPBridge.cs
// Requires Unity 2022.3+ | Compatible with Unity XR Toolkit
//
// Features:
//   - Send prompts to your Ollama MCP server from inside Unity Editor
//   - Auto-build scenes from JSON returned by build_scene_json tool
//   - Save generated scripts directly into your Assets folder

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// ── Ollama API Models (JsonUtility-compatible) ─────────────────────────────────
[Serializable]
public class OllamaMessage
{
    public string role;
    public string content;
}

[Serializable]
public class OllamaOptions
{
    public float temperature;
}

[Serializable]
public class OllamaChatRequest
{
    public string model;
    public bool stream;
    public OllamaOptions options;
    public OllamaMessage[] messages;
}

[Serializable]
public class OllamaChatResponse
{
    public OllamaMessage message;
}

[Serializable]
public class SceneObject
{
    public string name;
    public string primitive = "Cube";
    public float[] position = { 0, 0, 0 };
    public float[] rotation = { 0, 0, 0 };
    public float[] scale = { 1, 1, 1 };
    public string parent;
    public string tag;
    public string layer;
    public string[] components;
    public SceneMaterial material;
}

[Serializable]
public class SceneMaterial
{
    public float[] color = { 1, 1, 1, 1 };
    public float metallic = 0f;
    public float smoothness = 0.5f;
}

[Serializable]
public class SceneLighting
{
    public float[] ambientColor = { 0.2f, 0.2f, 0.2f };
    public float[] sunColor = { 1f, 0.95f, 0.84f };
    public float sunIntensity = 1f;
    public float[] sunRotation = { 50f, -30f, 0f };
}

[Serializable]
public class SceneData
{
    public string sceneName;
    public SceneObject[] objects;
    public SceneLighting lighting;
}

// ── Editor Window ──────────────────────────────────────────────────────────────
public class UnityMCPBridge : EditorWindow
{
    // ── Config ─────────────────────────────────────────────────────────────
    private string _ollamaUrl = "http://localhost:11434";
    private string _model = "qwen3:4b";

    // ── UI State ────────────────────────────────────────────────────────────
    private int _tabIndex = 0;
    private readonly string[] _tabs = { "💬 Ask AI", "🏗️ Build Scene", "🥽 VR Design", "🔧 Refactor" };

    // Ask AI tab
    private string _askPrompt = "";
    private string _askSystemContext = "You are an expert Unity developer and VR engineer.";
    private string _askResult = "";
    private bool _saveAsScript = false;
    private string _saveScriptName = "GeneratedScript";

    // Build Scene tab
    private string _sceneDescription = "";
    private string _sceneName = "NewScene";
    private bool _sceneIsVR = true;
    private string _sceneJsonPreview = "";

    // VR Design tab
    private string _vrEnvironmentType = "office";
    private string _vrInteractions = "grab objects, teleport, UI panels";
    private string _vrPlatform = "Quest";
    private string _vrSize = "5x5";
    private string _vrResult = "";

    // Refactor tab
    private string _refactorCode = "";
    private string _refactorFocus = "all";
    private string _refactorResult = "";

    // Shared
    private bool _isBusy = false;
    private string _statusMessage = "";
    private Vector2 _scrollPos;
    private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };

    // ── Menu entry ──────────────────────────────────────────────────────────
    [MenuItem("Tools/Unity MCP Bridge (Ollama)")]
    public static void ShowWindow()
    {
        var win = GetWindow<UnityMCPBridge>("MCP Bridge");
        win.minSize = new Vector2(520, 600);
    }

    // ── GUI ─────────────────────────────────────────────────────────────────
    private void OnGUI()
    {
        DrawHeader();
        DrawConfig();
        EditorGUILayout.Space(4);
        _tabIndex = GUILayout.Toolbar(_tabIndex, _tabs, GUILayout.Height(28));
        EditorGUILayout.Space(4);

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
        switch (_tabIndex)
        {
            case 0: DrawAskTab(); break;
            case 1: DrawBuildSceneTab(); break;
            case 2: DrawVRDesignTab(); break;
            case 3: DrawRefactorTab(); break;
        }
        EditorGUILayout.EndScrollView();

        DrawStatus();
    }

    private void DrawHeader()
    {
        var style = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("🤖 Unity MCP Bridge — Ollama", style, GUILayout.Height(28));
    }

    private void DrawConfig()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Ollama URL", GUILayout.Width(80));
            _ollamaUrl = EditorGUILayout.TextField(_ollamaUrl);
            EditorGUILayout.LabelField("Model", GUILayout.Width(42));
            _model = EditorGUILayout.TextField(_model, GUILayout.Width(110));
        }
    }

    // ── Ask AI Tab ──────────────────────────────────────────────────────────
    private void DrawAskTab()
    {
        EditorGUILayout.LabelField("System Context");
        _askSystemContext = EditorGUILayout.TextArea(_askSystemContext, GUILayout.Height(40));

        EditorGUILayout.LabelField("Your Prompt");
        _askPrompt = EditorGUILayout.TextArea(_askPrompt, GUILayout.Height(80));

        using (new EditorGUILayout.HorizontalScope())
        {
            _saveAsScript = EditorGUILayout.Toggle("Save as .cs", _saveAsScript, GUILayout.Width(100));
            if (_saveAsScript)
            {
                EditorGUILayout.LabelField("Class name", GUILayout.Width(72));
                _saveScriptName = EditorGUILayout.TextField(_saveScriptName);
            }
        }

        GUI.enabled = !_isBusy && !string.IsNullOrEmpty(_askPrompt);
        if (GUILayout.Button("Ask Ollama ▶", GUILayout.Height(30)))
            _ = AskOllama();
        GUI.enabled = true;

        if (!string.IsNullOrEmpty(_askResult))
        {
            EditorGUILayout.LabelField("Result");
            EditorGUILayout.TextArea(_askResult, GUILayout.Height(240));
            if (_saveAsScript && GUILayout.Button("💾 Save Script to Assets"))
                SaveScript(_saveScriptName, _askResult);
        }
    }

    // ── Build Scene Tab ─────────────────────────────────────────────────────
    private void DrawBuildSceneTab()
    {
        EditorGUILayout.LabelField("Describe your scene in plain English");
        _sceneDescription = EditorGUILayout.TextArea(_sceneDescription, GUILayout.Height(80));

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Scene Name", GUILayout.Width(80));
            _sceneName = EditorGUILayout.TextField(_sceneName);
        }
        _sceneIsVR = EditorGUILayout.Toggle("VR Scale (1u=1m)", _sceneIsVR);

        GUI.enabled = !_isBusy && !string.IsNullOrEmpty(_sceneDescription);
        if (GUILayout.Button("Generate Scene JSON ▶", GUILayout.Height(30)))
            _ = GenerateSceneJSON();
        GUI.enabled = true;

        if (!string.IsNullOrEmpty(_sceneJsonPreview))
        {
            EditorGUILayout.LabelField("Scene JSON Preview");
            EditorGUILayout.TextArea(_sceneJsonPreview, GUILayout.Height(160));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("🏗️ Build Scene in Unity", GUILayout.Height(30)))
                    BuildSceneFromJSON(_sceneJsonPreview);
                if (GUILayout.Button("📋 Copy JSON"))
                    GUIUtility.systemCopyBuffer = _sceneJsonPreview;
            }
        }
    }

    // ── VR Design Tab ───────────────────────────────────────────────────────
    private void DrawVRDesignTab()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Environment Type", GUILayout.Width(120));
            _vrEnvironmentType = EditorGUILayout.TextField(_vrEnvironmentType);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Interactions", GUILayout.Width(120));
            _vrInteractions = EditorGUILayout.TextField(_vrInteractions);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Platform", GUILayout.Width(120));
            _vrPlatform = EditorGUILayout.TextField(_vrPlatform);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Size (metres)", GUILayout.Width(120));
            _vrSize = EditorGUILayout.TextField(_vrSize);
        }

        GUI.enabled = !_isBusy;
        if (GUILayout.Button("Design VR Environment ▶", GUILayout.Height(30)))
            _ = DesignVREnvironment();
        GUI.enabled = true;

        if (!string.IsNullOrEmpty(_vrResult))
        {
            EditorGUILayout.LabelField("VR Design Plan");
            EditorGUILayout.TextArea(_vrResult, GUILayout.Height(300));
            if (GUILayout.Button("💾 Save as Markdown"))
                SaveText("VREnvironmentDesign", _vrResult, ".md");
        }
    }

    // ── Refactor Tab ────────────────────────────────────────────────────────
    private void DrawRefactorTab()
    {
        EditorGUILayout.LabelField("Paste your C# code");
        _refactorCode = EditorGUILayout.TextArea(_refactorCode, GUILayout.Height(160));

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Focus", GUILayout.Width(50));
            int focusIdx = Array.IndexOf(new[] { "all", "performance", "architecture", "xr" }, _refactorFocus);
            focusIdx = EditorGUILayout.Popup(focusIdx, new[] { "All", "Performance", "Architecture", "XR" });
            _refactorFocus = new[] { "all", "performance", "architecture", "xr" }[focusIdx];
        }

        GUI.enabled = !_isBusy && !string.IsNullOrEmpty(_refactorCode);
        if (GUILayout.Button("Refactor Code ▶", GUILayout.Height(30)))
            _ = RefactorCode();
        GUI.enabled = true;

        if (!string.IsNullOrEmpty(_refactorResult))
        {
            EditorGUILayout.LabelField("Refactored Result");
            EditorGUILayout.TextArea(_refactorResult, GUILayout.Height(240));
        }
    }

    private void DrawStatus()
    {
        if (!string.IsNullOrEmpty(_statusMessage))
        {
            var style = new GUIStyle(EditorStyles.helpBox) { wordWrap = true };
            EditorGUILayout.LabelField(_statusMessage, style);
        }
    }

    // ── Ollama API Calls ────────────────────────────────────────────────────
    private async Task AskOllama()
    {
        _isBusy = true;
        _statusMessage = "⏳ Asking Ollama...";
        Repaint();
        try
        {
            _askResult = await OllamaChat(_askSystemContext, _askPrompt);
            _statusMessage = "✅ Done!";
        }
        catch (Exception e) { _statusMessage = $"❌ Error: {e.Message}"; }
        finally { _isBusy = false; Repaint(); }
    }

    private async Task GenerateSceneJSON()
    {
        _isBusy = true;
        _statusMessage = "⏳ Generating scene JSON...";
        Repaint();
        try
        {
            string sysPrompt = @"You are a Unity scene architect. Output ONLY valid JSON matching this schema exactly:
{""sceneName"":""string"",""objects"":[{""name"":""string"",""primitive"":""Cube|Sphere|Plane|Cylinder|Capsule|Empty"",
""position"":[x,y,z],""rotation"":[x,y,z],""scale"":[x,y,z],""parent"":null,""tag"":null,
""material"":{""color"":[r,g,b,1.0],""metallic"":0.0,""smoothness"":0.5}}],
""lighting"":{""ambientColor"":[r,g,b],""sunColor"":[r,g,b],""sunIntensity"":1.0,""sunRotation"":[50,-30,0]}}
No markdown, no explanation, ONLY JSON.";
            string userPrompt = $"Scene name: {_sceneName}\n" +
                                (_sceneIsVR ? "VR scale (1 unit = 1 metre).\n" : "") +
                                $"Description: {_sceneDescription}";
            string raw = await OllamaChat(sysPrompt, userPrompt, 0.4f);
            // Strip markdown fences if present
            raw = raw.Trim();
            if (raw.StartsWith("```")) { raw = raw.Substring(raw.IndexOf('\n') + 1); raw = raw.Substring(0, raw.LastIndexOf("```")).Trim(); }
            _sceneJsonPreview = raw;
            _statusMessage = "✅ Scene JSON generated. Preview below.";
        }
        catch (Exception e) { _statusMessage = $"❌ Error: {e.Message}"; }
        finally { _isBusy = false; Repaint(); }
    }

    private async Task DesignVREnvironment()
    {
        _isBusy = true;
        _statusMessage = "⏳ Designing VR environment...";
        Repaint();
        try
        {
            string sys = "You are a VR/XR environment designer for Unity XR Toolkit and OpenXR. Use real-world metres.";
            string user = $"Design a complete VR environment:\nType: {_vrEnvironmentType}\nPlatform: {_vrPlatform}\n" +
                          $"Size: {_vrSize} metres\nInteractions: {_vrInteractions}\n\n" +
                          "Provide: XR Rig setup, scene layout, lighting, performance budget, comfort guidelines, and a C# XR Manager script stub.";
            _vrResult = await OllamaChat(sys, user, 0.6f);
            _statusMessage = "✅ VR design ready!";
        }
        catch (Exception e) { _statusMessage = $"❌ Error: {e.Message}"; }
        finally { _isBusy = false; Repaint(); }
    }

    private async Task RefactorCode()
    {
        _isBusy = true;
        _statusMessage = "⏳ Analysing code...";
        Repaint();
        try
        {
            string sys = "You are a senior Unity C# code reviewer. Be direct and provide corrected code.";
            string user = $"Review this Unity C# code. Focus: {_refactorFocus}.\n```csharp\n{_refactorCode}\n```";
            _refactorResult = await OllamaChat(sys, user, 0.2f);
            _statusMessage = "✅ Refactor complete!";
        }
        catch (Exception e) { _statusMessage = $"❌ Error: {e.Message}"; }
        finally { _isBusy = false; Repaint(); }
    }

    private async Task<string> OllamaChat(string system, string user, float temperature = 0.4f)
    {
        // Use JsonUtility with proper serializable classes — no manual string building
        var request = new OllamaChatRequest
        {
            model = _model,
            stream = false,
            options = new OllamaOptions { temperature = temperature },
            messages = new[]
            {
                new OllamaMessage { role = "system", content = system },
                new OllamaMessage { role = "user",   content = user   }
            }
        };

        string payload = JsonUtility.ToJson(request);
        var httpContent = new StringContent(payload, Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync($"{_ollamaUrl}/api/chat", httpContent);

        if (!resp.IsSuccessStatusCode)
        {
            string errBody = await resp.Content.ReadAsStringAsync();
            throw new Exception($"Ollama {(int)resp.StatusCode}: {errBody}");
        }

        string raw = await resp.Content.ReadAsStringAsync();

        // Ollama /api/chat returns a single JSON object (stream:false)
        var parsed = JsonUtility.FromJson<OllamaChatResponse>(raw);
        if (parsed?.message?.content == null)
            throw new Exception($"Unexpected Ollama response: {raw}");

        return parsed.message.content;
    }

    // ── Scene Builder ───────────────────────────────────────────────────────
    private void BuildSceneFromJSON(string sceneJson)
    {
        try
        {
            var data = JsonUtility.FromJson<SceneData>(sceneJson);
            if (data == null || data.objects == null) { _statusMessage = "❌ Invalid scene JSON"; return; }

            var lookup = new Dictionary<string, GameObject>();

            // Apply lighting
            if (data.lighting != null)
            {
                var l = data.lighting;
                RenderSettings.ambientLight = new Color(l.ambientColor[0], l.ambientColor[1], l.ambientColor[2]);
                var sun = GameObject.FindObjectOfType<Light>();
                if (sun != null)
                {
                    sun.color = new Color(l.sunColor[0], l.sunColor[1], l.sunColor[2]);
                    sun.intensity = l.sunIntensity;
                    sun.transform.eulerAngles = new Vector3(l.sunRotation[0], l.sunRotation[1], l.sunRotation[2]);
                }
            }

            foreach (var obj in data.objects)
            {
                GameObject go = CreatePrimitive(obj.primitive);
                go.name = obj.name;
                go.transform.position = new Vector3(obj.position[0], obj.position[1], obj.position[2]);
                go.transform.eulerAngles = new Vector3(obj.rotation[0], obj.rotation[1], obj.rotation[2]);
                go.transform.localScale = new Vector3(obj.scale[0], obj.scale[1], obj.scale[2]);

                if (!string.IsNullOrEmpty(obj.tag)) try { go.tag = obj.tag; } catch { }
                if (!string.IsNullOrEmpty(obj.parent) && lookup.TryGetValue(obj.parent, out var parentGO))
                    go.transform.SetParent(parentGO.transform, true);

                if (obj.material != null)
                {
                    var renderer = go.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        var mat = new Material(Shader.Find("Standard"));
                        var c = obj.material.color;
                        mat.color = new Color(c[0], c[1], c[2], c.Length > 3 ? c[3] : 1f);
                        mat.SetFloat("_Metallic", obj.material.metallic);
                        mat.SetFloat("_Glossiness", obj.material.smoothness);
                        renderer.material = mat;
                    }
                }

                lookup[obj.name] = go;
                Undo.RegisterCreatedObjectUndo(go, $"Create {obj.name}");
            }

            _statusMessage = $"✅ Built scene '{data.sceneName}' with {data.objects.Length} objects!";
        }
        catch (Exception e)
        {
            _statusMessage = $"❌ Scene build failed: {e.Message}";
        }
    }

    private GameObject CreatePrimitive(string type)
    {
        return type?.ToLower() switch
        {
            "sphere" => GameObject.CreatePrimitive(PrimitiveType.Sphere),
            "plane" => GameObject.CreatePrimitive(PrimitiveType.Plane),
            "cylinder" => GameObject.CreatePrimitive(PrimitiveType.Cylinder),
            "capsule" => GameObject.CreatePrimitive(PrimitiveType.Capsule),
            "quad" => GameObject.CreatePrimitive(PrimitiveType.Quad),
            "empty" => new GameObject(),
            _ => GameObject.CreatePrimitive(PrimitiveType.Cube),
        };
    }

    // ── File Helpers ────────────────────────────────────────────────────────
    private void SaveScript(string className, string code)
    {
        string dir = Path.Combine(Application.dataPath, "MCPGenerated");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"{className}.cs");
        File.WriteAllText(path, code);
        AssetDatabase.Refresh();
        _statusMessage = $"✅ Saved to Assets/MCPGenerated/{className}.cs";
    }

    private void SaveText(string name, string content, string ext = ".txt")
    {
        string dir = Path.Combine(Application.dataPath, "MCPGenerated");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"{name}{ext}");
        File.WriteAllText(path, content);
        AssetDatabase.Refresh();
        _statusMessage = $"✅ Saved to Assets/MCPGenerated/{name}{ext}";
    }
}
