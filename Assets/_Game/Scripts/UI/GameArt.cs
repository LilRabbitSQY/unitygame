using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FinalDefense.UI
{
    // Original supplied images stay intact. Cropped sprites only remove transparent margins.
    public static class GameArt
    {
        [Serializable] private sealed class Manifest { public Entry[] assets; }
        [Serializable] private sealed class Entry
        {
            public string category, key, name, resourcePath;
            public int width, height;
            public float[] rect;
        }
        private static Manifest manifest;
        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        public static Sprite Tower(string idOrName) => Load("Towers", idOrName != null && idOrName.StartsWith("tower_") ? idOrName.Substring(6) : idOrName);
        public static Sprite Portrait(string keyOrName) => Load("Portraits", keyOrName);
        public static Sprite Item(string keyOrName) => Load("Items", keyOrName);
        private static Sprite Load(string category, string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            string cacheKey = category + "/" + key;
            if (sprites.TryGetValue(cacheKey, out var cached) && cached != null) return cached;
            if (manifest == null)
            {
                var json = Resources.Load<TextAsset>("GameArt/mapping");
                if (json != null) manifest = JsonUtility.FromJson<Manifest>(json.text);
            }
            var entry = manifest?.assets?.FirstOrDefault(e => e.category == category && (e.key == key || e.name == key));
            if (entry == null && category == "Portraits" && int.TryParse(key, out int index))
                entry = manifest?.assets?.Where(e => e.category == category).ElementAtOrDefault(Math.Max(0, index));
            string path = entry != null ? entry.resourcePath : "GameArt/" + category + "/" + key;
            var original = Resources.Load<Sprite>(path);
            if (original == null) return null;
            Sprite result = original;
            if (entry != null && entry.rect != null && entry.rect.Length == 4 && entry.width > 0 && entry.height > 0)
            {
                float sx = (float)original.texture.width / entry.width, sy = (float)original.texture.height / entry.height;
                var crop = new Rect(entry.rect[0] * sx, entry.rect[1] * sy, entry.rect[2] * sx, entry.rect[3] * sy);
                result = Sprite.Create(original.texture, crop, new Vector2(.5f, .5f), original.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                result.name = original.name + " visible";
            }
            sprites[cacheKey] = result;
            return result;
        }
    }
}
