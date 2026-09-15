using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FinalDefense.Audio;
using FinalDefense.Combat;
using FinalDefense.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace FinalDefense.Tests
{
    public sealed class BackgroundMusicTests
    {
        private BackgroundMusic music;
        private bool listenerPaused,runInBackground,editorMuted;
        private float timeScale;
        private string evidencePath;
        [UnitySetUp]
        public IEnumerator PrepareRealAudioEngine()
        {
            listenerPaused=AudioListener.pause;timeScale=Time.timeScale;runInBackground=Application.runInBackground;
            AudioListener.pause=false;Time.timeScale=1;Application.runInBackground=true;
#if UNITY_EDITOR
            editorMuted=UnityEditor.EditorUtility.audioMasterMute;
            UnityEditor.EditorUtility.audioMasterMute=false;
            UnityEditor.EditorApplication.ExecuteMenuItem("Window/General/Game");
#endif
            string output=Environment.GetEnvironmentVariable("FINAL_DEFENSE_AUDIO_OUTPUT");
            if(string.IsNullOrEmpty(output))output=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"final-defense-audio-tests");
            Directory.CreateDirectory(output);evidencePath=System.IO.Path.Combine(output,TestContext.CurrentContext.Test.Name+".txt");
            File.WriteAllText(evidencePath,"Unity native audio-thread measurements\n");
            foreach(var gm in Object.FindObjectsByType<GameManager>(FindObjectsInactive.Include))Object.Destroy(gm.gameObject);
            yield return null;
            // CampaignSaveIsolation preserves the original editor key for the suite.
            // Each audio case starts with an independent campaign, so a prior
            // retry/result cannot redirect the scene under test.
            PlayerPrefs.DeleteKey("FinalDefense.Campaign.v1");PlayerPrefs.Save();
            music=BackgroundMusic.EnsureInstance();music.SetVolume(.35f);
            yield return Load("MainMenu");
            foreach(var source in music.MusicSources) if(source.GetComponent<AudioPcmProbe>()==null)source.gameObject.AddComponent<AudioPcmProbe>();
            yield return Stable("MainMenuShop");
        }
        [UnityTearDown]
        public IEnumerator RestoreAudioSettings()
        {
            AudioListener.pause=listenerPaused;Time.timeScale=timeScale;Application.runInBackground=runInBackground;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.audioMasterMute=editorMuted;
#endif
            if(music!=null)foreach(var source in music.MusicSources)
                foreach(var probe in source.GetComponents<AudioPcmProbe>())Object.Destroy(probe);
            foreach(var gm in Object.FindObjectsByType<GameManager>(FindObjectsInactive.Include))Object.Destroy(gm.gameObject);
            yield return null;
        }
        private void Record(string text)=>File.AppendAllText(evidencePath,DateTime.UtcNow.ToString("O")+" "+text+Environment.NewLine);
        private IEnumerator Load(string scene)
        {
            yield return SceneManager.LoadSceneAsync(scene,LoadSceneMode.Single);
            yield return new WaitForEndOfFrame();
            Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo(scene));
            Record("SCENE "+scene);
        }
        private AudioSource Playing(string track)=>music.MusicSources.Single(source=>source.isPlaying&&source.clip!=null&&source.clip.name==track&&source.volume>.001f);
        private IEnumerator Stable(string track)
        {
            float end=Time.realtimeSinceStartup+8;
            while((music.CurrentTrack!=track||music.IsTransitioning)&&Time.realtimeSinceStartup<end)yield return null;
            Assert.That(music.CurrentTrack,Is.EqualTo(track));Assert.That(music.IsTransitioning,Is.False);
            Assert.That(Object.FindObjectsByType<BackgroundMusic>().Length,Is.EqualTo(1));
            Assert.That(music.MusicSources.Count,Is.EqualTo(2));
            Assert.That(music.GetComponentsInChildren<AudioSource>(true).Length,Is.EqualTo(2));
            Assert.That(music.GetComponentsInChildren<AudioListener>(true),Is.Empty,"The service does not add a second listener");
            Assert.That(music.MusicSources.Count(source=>source.isPlaying),Is.EqualTo(1),"Only one voice remains after a crossfade");
            foreach(var source in music.MusicSources)
            {Assert.That(source.loop,Is.True);Assert.That(source.playOnAwake,Is.False);Assert.That(source.spatialBlend,Is.EqualTo(0));Assert.That(source.ignoreListenerPause,Is.True);}
            Assert.That(Playing(track).volume,Is.EqualTo(music.Volume).Within(.002));
        }
        private IEnumerator Audible(AudioSource source,string label)
        {
            var probe=source.GetComponent<AudioPcmProbe>();Assert.That(probe,Is.Not.Null);
            probe.ResetCapture();float end=Time.realtimeSinceStartup+8;AudioPcmProbe.Snapshot snapshot;
            do {yield return new WaitForSecondsRealtime(.05f);snapshot=probe.Read();}
            while((snapshot.samples<1024||snapshot.rms<.00001)&&Time.realtimeSinceStartup<end);
            Record("PCM "+label+" samples="+snapshot.samples+" blocks="+snapshot.blocks+" channels="+snapshot.channels+" RMS="+snapshot.rms.ToString("R"));
            Assert.That(snapshot.blocks,Is.GreaterThan(0),"Unity audio thread must invoke OnAudioFilterRead");
            Assert.That(snapshot.samples,Is.GreaterThanOrEqualTo(1024));
            Assert.That(snapshot.rms,Is.GreaterThan(.00001),"Actual decoded PCM output must be non-silent: "+label);
        }
        private IEnumerator PointerClick(string name)
        {
            float end=Time.realtimeSinceStartup+3;Button button=null;Vector2 point=Vector2.zero;bool ready=false;
            do
            {
                Canvas.ForceUpdateCanvases();button=Object.FindObjectsByType<Button>().SingleOrDefault(b=>b.name==name&&b.gameObject.activeInHierarchy);
                if(button!=null)
                {
                    point=RectTransformUtility.WorldToScreenPoint(null,button.transform.position);
                    var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
                    ready=point.x>=0&&point.y>=0&&point.x<Screen.width&&point.y<Screen.height&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button;
                }
                if(!ready)yield return new WaitForEndOfFrame();
            }while(!ready&&Time.realtimeSinceStartup<end);
            Assert.That(ready,Is.True,"Real UI hit: "+name);Assert.That(button.IsInteractable(),Is.True);
            var pointer=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
            Record("POINTER "+name);yield return new WaitForEndOfFrame();
        }
        [UnityTest,Timeout(60000)]
        public IEnumerator SceneRoutingKeepsOneServiceAndSameMenuShopPlaybackCursor()
        {
            Assert.That(BackgroundMusic.EnsureInstance(),Is.SameAs(music));Assert.That(BackgroundMusic.EnsureInstance(),Is.SameAs(music));
            var menu=Playing("MainMenuShop");menu.time=5;yield return Audible(menu,"MainMenuShop");
            int before=menu.timeSamples;var singleton=music;
            yield return Load("Shop");yield return Stable("MainMenuShop");yield return new WaitForSecondsRealtime(.2f);
            Assert.That(BackgroundMusic.Instance,Is.SameAs(singleton));Assert.That(Playing("MainMenuShop"),Is.SameAs(menu));
            Assert.That(menu.timeSamples,Is.GreaterThan(before),"MainMenu→Shop does not restart the shared track");
            yield return Load("PersonalityTest");yield return Stable("Story");Playing("Story").time=5;yield return Audible(Playing("Story"),"Story");
            yield return Load("Schedule");yield return Stable("Battle");Playing("Battle").time=5;yield return Audible(Playing("Battle"),"Battle on Schedule");
            int scheduleCursor=Playing("Battle").timeSamples;
            yield return Load("Battle");yield return Stable("Battle");
            Assert.That(Playing("Battle").timeSamples,Is.GreaterThanOrEqualTo(scheduleCursor),"Selection→Battle shares its playback cursor");
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest,Timeout(60000)]
        public IEnumerator AudioThreadContinuesDuringPauseAndAllThreeTracksLoopAtTheirEnds()
        {
            yield return Load("Battle");yield return Stable("Battle");var view=Object.FindAnyObjectByType<BattleView>();
            Assert.That(view.Simulation.Paused,Is.True);var source=Playing("Battle");source.time=5;
            AudioListener.pause=true;Time.timeScale=0;int before=source.timeSamples;
            yield return Audible(source,"paused battle with listener pause and timescale zero");yield return new WaitForSecondsRealtime(.25f);
            Assert.That(source.timeSamples,Is.GreaterThan(before),"Paused gameplay does not stop background music");
            AudioListener.pause=false;Time.timeScale=1;
            foreach(string track in new[]{"MainMenuShop","Story","Battle"})
            {
                Assert.That(music.PlayTrack(track),Is.True);yield return Stable(track);source=Playing(track);
                source.time=source.clip.length-.25f;var probe=source.GetComponent<AudioPcmProbe>();probe.ResetCapture();
                yield return new WaitForSecondsRealtime(.85f);
                Assert.That(source.isPlaying,Is.True);Assert.That(source.time,Is.LessThan(1.8f),"Native loop wrapped to track start: "+track);
                Record("LOOP "+track+" length="+source.clip.length+" cursorAfterWrap="+source.time);
                yield return Audible(source,"looped "+track);
            }
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest,Timeout(60000)]
        public IEnumerator RapidCrossfadeReversalReusesTheAudibleTrackWithoutResetOrExtraVoice()
        {
            music.PlayTrack("Story");yield return Stable("Story");var story=Playing("Story");story.time=5;yield return Audible(story,"story before reversal");
            int before=story.timeSamples;music.PlayTrack("Battle");yield return new WaitForSecondsRealtime(.15f);
            music.PlayTrack("Story");yield return Stable("Story");
            Assert.That(Playing("Story"),Is.SameAs(story));Assert.That(story.timeSamples,Is.GreaterThan(before),"Reversal does not restart the fading-out track");
            yield return Audible(story,"story after reversal");
            Assert.That(music.PlayTrack("does-not-exist"),Is.False);Assert.That(music.CurrentTrack,Is.EqualTo("Story"));
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest,Timeout(60000)]
        public IEnumerator RetainedRuntimeObjectReinitializesWithoutExtraVoicesAndVolumeClamps()
        {
            var retained=music;var sources=music.MusicSources.ToArray();
            music.SetVolume(-1);Assert.That(music.Volume,Is.EqualTo(0));
            Assert.That(sources.All(source=>source.volume==0),Is.True);
            music.SetVolume(2);Assert.That(music.Volume,Is.EqualTo(1));
            Assert.That(Playing("MainMenuShop").volume,Is.EqualTo(1));
            music.SetVolume(float.NaN);Assert.That(music.Volume,Is.EqualTo(0));
            var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
            typeof(BackgroundMusic).GetMethod("ResetStatics",flags).Invoke(null,null);
            typeof(BackgroundMusic).GetMethod("InitializeOnLoad",flags).Invoke(null,null);
            music=BackgroundMusic.Instance;
            Assert.That(music,Is.SameAs(retained),"Runtime init with a retained object reuses the singleton");
            Assert.That(music.MusicSources.ToArray(),Is.EqualTo(sources));
            Assert.That(music.Volume,Is.EqualTo(.35f));yield return Stable("MainMenuShop");
            yield return Load("PersonalityTest");yield return Stable("Story");Playing("Story").time=5;
            yield return Audible(Playing("Story"),"retained runtime reinitialization");
            Record("REINITIALIZED retained singleton and two existing sources; volume clamps 0..1 including NaN");
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest,Timeout(60000)]
        public IEnumerator BattleResultAndActualRetrySwitchTracksWithoutPersistentOverlap()
        {
            yield return Load("Battle");yield return Stable("Battle");var oldView=Object.FindAnyObjectByType<BattleView>();
            yield return PointerClick("Start");oldView.Simulation.Tick(1000);Assert.That(oldView.Simulation.Finished,Is.True);
            yield return Stable("MainMenuShop");yield return Audible(Playing("MainMenuShop"),"in-battle result music");
            yield return PointerClick("Retry");float deadline=Time.realtimeSinceStartup+10;
            while((Object.FindAnyObjectByType<BattleView>()==null||Object.FindAnyObjectByType<BattleView>()==oldView)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(Object.FindAnyObjectByType<BattleView>(),Is.Not.Null);Assert.That(Object.FindAnyObjectByType<BattleView>(),Is.Not.SameAs(oldView));
            yield return Stable("Battle");yield return Audible(Playing("Battle"),"actual retry music");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
