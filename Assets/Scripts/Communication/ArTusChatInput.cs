using UnityEngine;

public class ArTusChatInput : MonoBehaviour
{
    public ArTusChatClient chatClient;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return))
        {
            SendTestMessage();
        }
    }

    void SendTestMessage()
    {
        chatClient.SendToArTus("Hello ArTus, do you copy?");
    }
}
