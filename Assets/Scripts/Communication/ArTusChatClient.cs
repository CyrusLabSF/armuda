using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

[Serializable]
public class ChatRequest
{
    public string message;
}

[Serializable]
public class ChatResponse
{
    public string reply;
}

public class ArTusChatClient : MonoBehaviour
{
    [Header("Server Settings")]
    public string apiURL = "http://127.0.0.1:8000/chat";

    [Header("UI Reference")]
    public TMPro.TextMeshProUGUI chatOutput;

    public void SendToArTus(string userMessage)
    {
        ChatRequest req = new ChatRequest { message = userMessage };
        string json = JsonUtility.ToJson(req);

        StartCoroutine(SendRequest(json));
    }

    private IEnumerator SendRequest(string json)
    {
        using (UnityWebRequest www = new UnityWebRequest(apiURL, "POST"))
        {
            byte[] body = Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(body);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string raw = www.downloadHandler.text;
                ChatResponse res = null;

                try
                {
                    res = JsonUtility.FromJson<ChatResponse>(raw);
                }
                catch
                {
                    res = null;
                }

                DisplayReply(string.IsNullOrWhiteSpace(res?.reply) ? raw : res.reply);
            }
            else
            {
                DisplayReply("? Error: " + www.error);
            }
        }
    }

    private void DisplayReply(string text)
    {
        if (chatOutput != null)
        {
            chatOutput.text += "\n<color=cyan>ArTus:</color> " + text;
        }
    }
}
