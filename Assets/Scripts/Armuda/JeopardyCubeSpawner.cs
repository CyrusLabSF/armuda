using UnityEngine;
using System.Collections.Generic;

public class JeopardyCubeSpawner : MonoBehaviour
{
    [System.Serializable]
    public class CategoryStat
    {
        public string category;
        public int count;
    }

    public GameObject cubePrefab;
    public Transform spawnArea;
    public float baseSize = 0.4f;

    private Dictionary<string, GameObject> cubes = new();

    public void SpawnOrUpdateCube(string category, int density)
    {
        if (!cubes.ContainsKey(category))
        {
            GameObject cube = Instantiate(cubePrefab, spawnArea);
            cube.name = $"Cube_{category}";
            cube.transform.localPosition = new Vector3(
                Random.Range(-5f, 5f),
                0f,
                Random.Range(-5f, 5f)
            );
            cubes[category] = cube;
        }

        float scale = baseSize + Mathf.Clamp(density / 5f, 0.1f, 2f);
        cubes[category].transform.localScale = Vector3.one * scale;

        Renderer r = cubes[category].GetComponent<Renderer>();
        if (r != null)
        {
            // Try to parse category as an EmotionState first
            Color baseColor;
            if (System.Enum.TryParse(category, true, out ArTusEmotionController.EmotionState parsed))
            {
                baseColor = ArTusEmotionData.GetColorForEmotion(parsed);
            }
            else
            {
                baseColor = GetColorForCategory(category); // fallback if not an emotion
            }

            float glow = Mathf.Clamp01(density / 10f); // optional intensity boost

            r.material.color = baseColor;
            r.material.SetColor("_EmissionColor", baseColor * glow);
            r.material.EnableKeyword("_EMISSION");
        }
    }

    private Color GetColorForCategory(string cat)
    {
        return cat.ToLower() switch
        {
            "emotion" => Color.magenta,
            "defense" => Color.red,
            "curiosity" => Color.cyan,
            "simulation" => Color.green,
            _ => Color.gray
        };
    }
}
