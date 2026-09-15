using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FinalDefense.Combat
{
    // Adapts simulation cells to the authored scene's Tilemap and Camera.
    // It owns presentation resources only; all rules remain in BattleSimulation.
    public sealed class BattleBoardPresentation : MonoBehaviour
    {
        private Tile tile;
        private Sprite sprite;
        private Texture2D texture;

        public static Vector3 WorldPoint(float x, float y) => new Vector3(x - 5, y - 4, 0);

        public void Initialize(BattleConfiguration config)
        {
            var map = GameObject.Find("Tilemap_Placeable")?.GetComponent<Tilemap>();
            if (map == null) throw new System.InvalidOperationException("Battle requires the authored Tilemap_Placeable.");
            map.ClearAllTiles();
            map.tileAnchor = Vector3.zero;
            // The original board uses a translucent green grid with a stronger border.
            const int size = 32;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = x < 2 || y < 2 || x > size - 3 || y > size - 3
                        ? new Color(.35f, .78f, .43f, .55f) : new Color(.42f, .86f, .50f, .34f);
            texture.SetPixels(pixels); texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size);
            tile = ScriptableObject.CreateInstance<Tile>(); tile.sprite = sprite;
            foreach (var cell in config.ground.Concat(config.highGround))
            {
                var position = new Vector3Int(cell.x - 5, cell.y - 4, 0);
                map.SetTile(position, tile); map.SetTileFlags(position, TileFlags.None);
                bool high = config.highGround.Any(c => c.x == cell.x && c.y == cell.y);
                map.SetColor(position, high ? new Color(.65f, .8f, 1f) :
                    (cell.x + cell.y) % 2 == 0 ? Color.white : new Color(.88f, 1f, .9f));
            }
            var line = GameObject.Find("PathVisual")?.GetComponent<LineRenderer>();
            if (line != null)
            {
                line.useWorldSpace = true;
                line.positionCount = config.path.Length;
                line.SetPositions(config.path.Select(c => WorldPoint(c.x, c.y)).ToArray());
            }
        }

        private void OnDestroy()
        {
            if (tile != null) Destroy(tile);
            if (sprite != null) Destroy(sprite);
            if (texture != null) Destroy(texture);
        }
    }
}
