using UnityEngine;
using FinalDefense.Data;

namespace FinalDefense.Enemy
{
    public class EnemySetup : MonoBehaviour
    {
        private static Sprite fallbackSprite;

        private void Start()
        {
            ApplyVisuals(null);
        }

        public void ApplyVisuals(EnemyData data)
        {
            SetEnemyLayer();
            SetupBodyVisual(data);
            SetupLabel(data);
            SetupHealthBarVisuals();

            if (data != null)
            {
                float scale = data.maxHP > 80 ? 1.8f : data.maxHP > 30 ? 1.4f : 1.0f;
                transform.localScale = Vector3.one * scale;
            }
        }

        private void SetEnemyLayer()
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer < 0) return;

            gameObject.layer = enemyLayer;
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = enemyLayer;
            }
        }

        private void SetupBodyVisual(EnemyData data)
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();

            if (sr.sprite == null)
            {
                sr.sprite = GetFallbackSprite();
            }

            if (data != null)
            {
                sr.color = data.enemyColor;
            }

            sr.sortingOrder = 20;
        }

        private void SetupHealthBarVisuals()
        {
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr == null || sr.gameObject == gameObject) continue;

                if (sr.sprite == null)
                {
                    sr.sprite = GetFallbackSprite();
                }

                if (sr.gameObject.name.Contains("HealthBarBG"))
                {
                    sr.color = new Color(0.15f, 0.15f, 0.15f, 1f);
                    sr.sortingOrder = 21;
                }
                else if (sr.gameObject.name.Contains("HealthBarFill"))
                {
                    sr.color = Color.green;
                    sr.sortingOrder = 22;
                }
            }
        }

        private static Sprite GetFallbackSprite()
        {
            if (fallbackSprite != null) return fallbackSprite;

            const int size = 48;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.45f;
            float innerRadius = size * 0.36f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist > radius)
                        colors[y * size + x] = Color.clear;
                    else if (dist > innerRadius)
                        colors[y * size + x] = new Color(1f, 1f, 1f, 0.95f);
                    else
                        colors[y * size + x] = Color.white;
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            fallbackSprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
            fallbackSprite.name = "GeneratedEnemyBadgeSprite";
            return fallbackSprite;
        }

        private void SetupLabel(EnemyData data)
        {
            var labelTransform = transform.Find("EnemyTypeLabel");
            TextMesh label;
            if (labelTransform == null)
            {
                var labelGo = new GameObject("EnemyTypeLabel");
                labelGo.transform.SetParent(transform, false);
                labelGo.transform.localPosition = new Vector3(0f, -0.03f, -0.01f);
                label = labelGo.AddComponent<TextMesh>();
            }
            else
            {
                label = labelTransform.GetComponent<TextMesh>();
                if (label == null)
                    label = labelTransform.gameObject.AddComponent<TextMesh>();
            }

            label.text = GetEnemyShortLabel(data);
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 22;
            label.characterSize = 0.08f;
            label.color = Color.white;

            var renderer = label.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sortingOrder = 23;
        }

        private string GetEnemyShortLabel(EnemyData data)
        {
            if (data == null || string.IsNullOrEmpty(data.enemyName))
                return "Q";

            string name = data.enemyName.ToLower();
            if (name.Contains("multiple") || name.Contains("choice"))
                return "MC";
            if (name.Contains("fill") || name.Contains("blank"))
                return "FB";
            if (name.Contains("essay"))
                return "ES";

            return "Q";
        }
    }
}
