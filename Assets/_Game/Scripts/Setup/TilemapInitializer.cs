using UnityEngine;
using UnityEngine.Tilemaps;

namespace FinalDefense.Setup
{
    public class TilemapInitializer : MonoBehaviour
    {
        [SerializeField] private Tilemap placeableTilemap;
        [SerializeField] private TileBase placeTile;

        [Header("Auto-generate placeable area")]
        [SerializeField] private bool autoGenerate = true;
        [SerializeField] private Vector2Int areaMin = new Vector2Int(-8, -5);
        [SerializeField] private Vector2Int areaMax = new Vector2Int(8, 5);

        [Header("Path cells to exclude (auto-filled from PathManager)")]
        [SerializeField] private float pathExcludeRadius = 1.2f;

        private void Awake()
        {
            if (autoGenerate && placeableTilemap != null)
            {
                GeneratePlaceableArea();
                GeneratePathMarkers();
            }
        }

        private void GeneratePlaceableArea()
        {
            if (placeTile == null)
            {
                placeTile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
                var tile = placeTile as UnityEngine.Tilemaps.Tile;
                if (tile != null)
                {
                    const int size = 32;
                    Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                    Color[] colors = new Color[size * size];
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            bool border = x < 2 || y < 2 || x > size - 3 || y > size - 3;
                            colors[y * size + x] = border
                                ? new Color(0.35f, 0.78f, 0.43f, 0.55f)
                                : new Color(0.42f, 0.86f, 0.50f, 0.34f);
                        }
                    }
                    tex.SetPixels(colors);
                    tex.Apply();
                    tile.sprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
                    tile.color = Color.white;
                }
            }

            var renderer = placeableTilemap.GetComponent<TilemapRenderer>();
            if (renderer != null)
                renderer.sortingOrder = 0;

            var pathMgr = FindFirstObjectByType<FinalDefense.Path.PathManager>();
            System.Collections.Generic.List<Vector3> pathPositions = new();
            if (pathMgr != null)
            {
                for (int i = 0; i < pathMgr.WaypointCount; i++)
                {
                    pathPositions.Add(pathMgr.GetWaypointPosition(i));
                }
            }

            for (int x = areaMin.x; x <= areaMax.x; x++)
            {
                for (int y = areaMin.y; y <= areaMax.y; y++)
                {
                    Vector3Int cellPos = new Vector3Int(x, y, 0);
                    Vector3 worldPos = placeableTilemap.GetCellCenterWorld(cellPos);

                    bool onPath = false;
                    for (int i = 0; i < pathPositions.Count - 1; i++)
                    {
                        float dist = DistanceToSegment(worldPos, pathPositions[i], pathPositions[i + 1]);
                        if (dist < pathExcludeRadius)
                        {
                            onPath = true;
                            break;
                        }
                    }

                    if (!onPath)
                    {
                        placeableTilemap.SetTile(cellPos, placeTile);
                        placeableTilemap.SetTileFlags(cellPos, TileFlags.None);
                        bool checker = (x + y) % 2 == 0;
                        placeableTilemap.SetColor(cellPos, checker
                            ? new Color(1f, 1f, 1f, 1f)
                            : new Color(0.88f, 1f, 0.9f, 1f));
                    }
                }
            }
        }

        private void GeneratePathMarkers()
        {
            var pathMgr = FindFirstObjectByType<FinalDefense.Path.PathManager>();
            if (pathMgr == null || pathMgr.WaypointCount == 0) return;

            CreateWorldLabel("StartMarker", "START", pathMgr.GetWaypointPosition(0) + Vector3.up * 0.7f, new Color(0.3f, 0.9f, 0.45f, 1f));
            CreateWorldLabel("GoalMarker", "GOAL", pathMgr.GetWaypointPosition(pathMgr.WaypointCount - 1) + Vector3.up * 0.7f, new Color(1f, 0.45f, 0.45f, 1f));
        }

        private void CreateWorldLabel(string name, string text, Vector3 position, Color color)
        {
            var existing = GameObject.Find(name);
            GameObject labelGo = existing != null ? existing : new GameObject(name);
            labelGo.transform.position = position;
            labelGo.transform.localScale = Vector3.one;

            var label = labelGo.GetComponent<TextMesh>();
            if (label == null)
                label = labelGo.AddComponent<TextMesh>();

            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 24;
            label.characterSize = 0.08f;
            label.color = color;

            var renderer = label.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sortingOrder = 30;
        }

        private float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            Vector3 ap = point - a;
            float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / Vector3.Dot(ab, ab));
            Vector3 closest = a + t * ab;
            return Vector3.Distance(point, closest);
        }
    }
}
