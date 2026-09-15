using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FinalDefense.Tests
{
    public sealed class BackgroundMusicResourceTests
    {
        [TestCase("MainMenuShop",48.9795918)]
        [TestCase("Story",239.9375057)]
        [TestCase("Battle",166.3928571)]
        public void AuthoredMusicImportsAsStereoStreamingVorbis(string name,double seconds)
        {
            var clip=Resources.Load<AudioClip>("Audio/Bgm/"+name);
            Assert.That(clip,Is.Not.Null,"The real Resources path imports an AudioClip");
            Assert.That(clip.channels,Is.EqualTo(2));Assert.That(clip.frequency,Is.EqualTo(44100));
            Assert.That(clip.length,Is.EqualTo(seconds).Within(.08));
            var importer=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) as AudioImporter;
            Assert.That(importer,Is.Not.Null);Assert.That(importer.forceToMono,Is.False);
            Assert.That(importer.loadInBackground,Is.True);Assert.That(importer.ambisonic,Is.False);
            var settings=importer.defaultSampleSettings;
            Assert.That(settings.loadType,Is.EqualTo(AudioClipLoadType.Streaming));
            Assert.That(settings.compressionFormat,Is.EqualTo(AudioCompressionFormat.Vorbis));
            Assert.That(settings.quality,Is.EqualTo(.7f).Within(.001));
            Assert.That(settings.preloadAudioData,Is.False,"Streaming music must not preload the full decoded waveform");
        }
    }
}
