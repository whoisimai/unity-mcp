using UnityEngine;
using NativeWebSocket;
using System.Text;

public class WSClient : MonoBehaviour
{
    WebSocket websocket;

    async void Start()
    {
        websocket = new WebSocket("ws://localhost:3001");

        websocket.OnOpen += () =>
        {
            Debug.Log("Connected to server");
            SendReady();
        };

        websocket.OnMessage += (bytes) =>
        {
            var message = Encoding.UTF8.GetString(bytes);
            HandleMessage(message);
        };

        await websocket.Connect();
    }

    void SendReady()
    {
        websocket.SendText("{\"type\":\"unity_ready\"}");
    }

    void HandleMessage(string json)
    {
        var msg = JsonUtility.FromJson<Message>(json);

        if (msg.type == "action")
        {
            ExecuteAction(msg);
        }
    }

    void ExecuteAction(Message msg)
    {
        switch (msg.action)
        {
            case "create_cube":
                Vector3 pos = new Vector3(
                    msg.payload.x,
                    msg.payload.y,
                    msg.payload.z
                );

                GameObject.CreatePrimitive(PrimitiveType.Cube)
                          .transform.position = pos;
                break;
        }
    }

    private async void Update()
    {
        await websocket.DispatchMessageQueue();
    }
}

[System.Serializable]
public class Message
{
    public string type;
    public string action;
    public Payload payload;
}

[System.Serializable]
public class Payload
{
    public float x;
    public float y;
    public float z;
}