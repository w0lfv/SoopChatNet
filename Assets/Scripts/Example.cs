using UnityEngine;
using SoopChatNet;
using SoopChatNet.Data;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Example : MonoBehaviour
{
    public string bjid = "ecvhao";
    SoopChat soopChat;

    private void OnDestroy()
    {
        soopChat.Dispose();
    }

    public void StartChat()
    {
        if (soopChat != null)
            soopChat.Dispose();
        soopChat = new SoopChat(bjid);
        soopChat.OnMessageReceived += OnChat;
        soopChat.OnSocketOpened += OnOpen;
        soopChat.OnSocketError += OnError;
        soopChat.OnSocketClosed += (message) => Debug.Log(message);
        _ = soopChat.OpenAsync();
    }

    public void StopChat()
    {
        _ = soopChat.CloseAsync();
    }

    void Update()
    {
        if(soopChat != null && soopChat.IsConnected)
            soopChat.Update();
    }

    void OnOpen(string message)
    {
        Debug.Log(message);
    }

    void OnError(string message)
    {
        Debug.Log(message);
    }

    void OnChat(Chat chat)
    {
        Debug.Log($"{chat.nickname}({chat.sender}) : {chat.message}");
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(Example))]
public class ExampleButton : Editor 
{ 
    public override void OnInspectorGUI() {
        base.OnInspectorGUI();
        Example script = (Example)target; 
        if (GUILayout.Button("Start"))
        { 
            script.StartChat();
        }

        if (GUILayout.Button("Stop"))
        {
            script.StopChat();
        }
    }
}

#endif