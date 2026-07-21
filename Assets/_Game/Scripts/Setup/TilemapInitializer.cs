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
                    Texture2D tex = new Texture2D(32, 32);
                    Color[] colors = new Color[32 * 32];
                    for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
                    tex.SetPixels(colors);
                    tex.Apply();
                    tile.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), Vector2.one * 0.5f, 32);
                    tile.color = new Color(0.2f, 0.8f, 0.3f, 0.2f);
                }
            }

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
                    }
                }
            }
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
