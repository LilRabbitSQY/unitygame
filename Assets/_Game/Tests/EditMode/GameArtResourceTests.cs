using System;
using System.Linq;
using FinalDefense.Combat;
using FinalDefense.UI;
using NUnit.Framework;
using UnityEngine;

namespace FinalDefense.Tests
{
    public sealed class GameArtResourceTests
    {
        [Serializable] private sealed class Mapping { public int version; public Entry[] assets; }
        [Serializable] private sealed class Entry
        {
            public string category, key, name, resourcePath;
            public int width, height;
            public float[] rect;
        }

        [Test]
        public void EverySuppliedImageImportsAsAnActualSpriteAndItsCropStaysInsideTheTexture()
        {
            var json = Resources.Load<TextAsset>("GameArt/mapping");
            Assert.That(json, Is.Not.Null);
            var mapping = JsonUtility.FromJson<Mapping>(json.text);
            Assert.That(mapping.version, Is.EqualTo(1));
            Assert.That(mapping.assets.Length, Is.EqualTo(45));
            Assert.That(mapping.assets.Select(entry => entry.resourcePath).Distinct().Count(), Is.EqualTo(45));
            foreach (var entry in mapping.assets)
            {
                var original = Resources.Load<Sprite>(entry.resourcePath);
                Assert.That(original, Is.Not.Null, "Native Unity Sprite import: " + entry.resourcePath);
                Assert.That(original.texture.width, Is.GreaterThan(1));
                Assert.That(original.texture.height, Is.GreaterThan(1));
                Assert.That(entry.rect.Length, Is.EqualTo(4));
                Assert.That(entry.rect[0], Is.GreaterThanOrEqualTo(0));
                Assert.That(entry.rect[1], Is.GreaterThanOrEqualTo(0));
                Assert.That(entry.rect[2], Is.GreaterThan(0));
                Assert.That(entry.rect[3], Is.GreaterThan(0));
                Assert.That(entry.rect[0] + entry.rect[2], Is.LessThanOrEqualTo(entry.width));
                Assert.That(entry.rect[1] + entry.rect[3], Is.LessThanOrEqualTo(entry.height));
                Sprite displayed = entry.category == "Towers" ? GameArt.Tower(entry.key)
                    : entry.category == "Portraits" ? GameArt.Portrait(entry.key) : GameArt.Item(entry.key);
                Assert.That(displayed, Is.Not.Null, "Production GameArt lookup: " + entry.category + "/" + entry.key);
                Assert.That(displayed.texture, Is.SameAs(original.texture), "Display uses the supplied texture");
                Assert.That(displayed.rect.xMin, Is.GreaterThanOrEqualTo(0));
                Assert.That(displayed.rect.yMin, Is.GreaterThanOrEqualTo(0));
                Assert.That(displayed.rect.xMax, Is.LessThanOrEqualTo(original.texture.width));
                Assert.That(displayed.rect.yMax, Is.LessThanOrEqualTo(original.texture.height));
                Assert.That(displayed.rect.width, Is.EqualTo(entry.rect[2] * original.texture.width / entry.width).Within(.02));
                Assert.That(displayed.rect.height, Is.EqualTo(entry.rect[3] * original.texture.height / entry.height).Within(.02));
            }
        }

        [Test]
        public void AllSixteenProductionTowerIdsResolveToTheirCorrectSuppliedCharacters()
        {
            var mapping = JsonUtility.FromJson<Mapping>(Resources.Load<TextAsset>("GameArt/mapping").text);
            var catalog = JsonUtility.FromJson<UnitCatalog>(Resources.Load<TextAsset>("Battle/Towers").text).units;
            Assert.That(catalog.Length, Is.EqualTo(16));
            Assert.That(mapping.assets.Count(entry => entry.category == "Towers"), Is.EqualTo(16));
            foreach (var tower in catalog)
            {
                var entry = mapping.assets.Single(item => item.category == "Towers" && item.key == tower.skill.id);
                Assert.That(entry.name, Is.EqualTo(tower.name), "Character art must match the authored identity");
                Assert.That(GameArt.Tower(tower.id).texture, Is.SameAs(Resources.Load<Sprite>(entry.resourcePath).texture));
            }
        }
    }
}
