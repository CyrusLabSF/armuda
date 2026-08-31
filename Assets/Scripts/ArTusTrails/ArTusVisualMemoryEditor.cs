#if UNITY_EDITOR        // 👈 Add this guard at the very top
using UnityEditor;
using UnityEngine;

public class ArTusVisualMemoryEditor : EditorWindow
{
    private string topic = "";
    private Texture2D imageToStore;
    private Texture2D recalledImage;
    private ArTusVisualMemory visualMemory;

    [MenuItem("ArTus/Visual Memory Tool")]
    public static void ShowWindow()
    {
        GetWindow<ArTusVisualMemoryEditor>("ArTus Visual Memory");
    }

    void OnGUI()
    {
        GUILayout.Label("📸 ArTus Visual Memory Interface", EditorStyles.boldLabel);

        GameObject artus = GameObject.Find("ArTus");
        if (artus == null)
        {
            EditorGUILayout.HelpBox("No GameObject named 'ArTus' found in scene.", MessageType.Warning);
            return;
        }

        visualMemory = artus.GetComponent<ArTusVisualMemory>();
        if (visualMemory == null)
        {
            EditorGUILayout.HelpBox("No 'ArTusVisualMemory' script found on ArTus.", MessageType.Warning);
            return;
        }

        topic = EditorGUILayout.TextField("Topic Name", topic);
        imageToStore = (Texture2D)EditorGUILayout.ObjectField("Image to Store", imageToStore, typeof(Texture2D), false);

        GUILayout.Space(10);

        if (GUILayout.Button("🧠 Store Image in Visual Memory"))
        {
            if (!string.IsNullOrEmpty(topic) && imageToStore != null)
            {
                visualMemory.StoreVisual(topic, imageToStore);
                Debug.Log($"[Editor] Stored visual for topic '{topic}'.");
            }
        }

        GUILayout.Space(10);

        if (GUILayout.Button("🔍 Recall Image"))
        {
            recalledImage = visualMemory.RecallVisual(topic);
        }

        if (recalledImage != null)
        {
            GUILayout.Label("📷 Recalled Visual:");
            GUILayout.Label(recalledImage, GUILayout.Width(256), GUILayout.Height(256));
        }
    }
}
#endif                  // 👈 Add this guard at the very bottom
