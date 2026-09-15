using System;
using System.Linq;
using System.Collections.Generic;
using FinalDefense.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Object=UnityEngine.Object;

namespace FinalDefense.Tests
{
    // The reference is committed UITheme/UIStyler at 30b6d962, not the later
    // full-page portrait redesign. UI actions retain their own raycast checks.
    internal static class OriginalUIAssertions
    {
        [Serializable]private sealed class TextLayout
        {
            public string name,path,text;public float fontSize,width,height,preferredHeight,xMin,yMin,xMax,yMax;
            public bool overflowing,culled;
        }
        [Serializable]private sealed class Snapshot { public string scene;public int screenWidth,screenHeight;public List<TextLayout> texts=new List<TextLayout>(); }
        internal static void RecordTextLayout(string output)
        {
            var snapshot=new Snapshot {scene=SceneManager.GetActiveScene().name,screenWidth=Screen.width,screenHeight=Screen.height};
            foreach(var label in Object.FindObjectsByType<TextMeshProUGUI>().Where(item=>item.gameObject.scene==SceneManager.GetActiveScene()))
            {
                string path=label.name;for(var parent=label.transform.parent;parent!=null;parent=parent.parent)path=parent.name+"/"+path;
                var canvas=label.GetComponentInParent<Canvas>();var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
                var corners=new Vector3[4];label.rectTransform.GetWorldCorners(corners);
                var points=corners.Select(point=>RectTransformUtility.WorldToScreenPoint(camera,point)).ToArray();
                snapshot.texts.Add(new TextLayout {name=label.name,path=path,text=label.text,fontSize=label.fontSize,width=label.rectTransform.rect.width,
                    height=label.rectTransform.rect.height,preferredHeight=label.preferredHeight,overflowing=label.isTextOverflowing,culled=label.canvasRenderer.cull,
                    xMin=points.Min(point=>point.x),yMin=points.Min(point=>point.y),xMax=points.Max(point=>point.x),yMax=points.Max(point=>point.y)});
            }
            System.IO.File.WriteAllText(output,JsonUtility.ToJson(snapshot,true));
        }
        private static Color Hex(byte r,byte g,byte b)=>new Color(r/255f,g/255f,b/255f);
        private static void ColorEquals(Color actual,Color expected,string label)
        {
            Assert.That(actual.r,Is.EqualTo(expected.r).Within(.001),label+" red");
            Assert.That(actual.g,Is.EqualTo(expected.g).Within(.001),label+" green");
            Assert.That(actual.b,Is.EqualTo(expected.b).Within(.001),label+" blue");
        }
        internal static RectTransform Rect(string name)
        {
            var found=Object.FindObjectsByType<RectTransform>().Where(rect=>rect.gameObject.scene==SceneManager.GetActiveScene()&&rect.name==name&&rect.gameObject.activeInHierarchy).ToArray();
            Assert.That(found.Length,Is.EqualTo(1),"One original layout element remains visible: "+name);
            Assert.That(found[0].rect.width,Is.GreaterThan(0),name+" width");
            Assert.That(found[0].rect.height,Is.GreaterThan(0),name+" height");
            return found[0];
        }
        private static UnityEngine.Rect ScreenRect(RectTransform rect)
        {
            var canvas=rect.GetComponentInParent<Canvas>();var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            var points=corners.Select(point=>RectTransformUtility.WorldToScreenPoint(camera,point)).ToArray();
            return UnityEngine.Rect.MinMaxRect(points.Min(point=>point.x),points.Min(point=>point.y),points.Max(point=>point.x),points.Max(point=>point.y));
        }
        internal static float VisibleCardFraction(RectTransform card)
        {
            var bounds=ScreenRect(card);float xMin=Mathf.Max(0,bounds.xMin),yMin=Mathf.Max(0,bounds.yMin);
            float xMax=Mathf.Min(Screen.width,bounds.xMax),yMax=Mathf.Min(Screen.height,bounds.yMax);
            foreach(var mask in card.GetComponentsInParent<RectMask2D>())
            {
                if(!mask.isActiveAndEnabled)continue;var clip=ScreenRect((RectTransform)mask.transform);
                xMin=Mathf.Max(xMin,clip.xMin);yMin=Mathf.Max(yMin,clip.yMin);xMax=Mathf.Min(xMax,clip.xMax);yMax=Mathf.Min(yMax,clip.yMax);
            }
            foreach(var mask in card.GetComponentsInParent<Mask>())
            {
                if(!mask.isActiveAndEnabled)continue;var clip=ScreenRect((RectTransform)mask.transform);
                xMin=Mathf.Max(xMin,clip.xMin);yMin=Mathf.Max(yMin,clip.yMin);xMax=Mathf.Min(xMax,clip.xMax);yMax=Mathf.Min(yMax,clip.yMax);
            }
            return bounds.width<=0||bounds.height<=0?0:Mathf.Max(0,xMax-xMin)*Mathf.Max(0,yMax-yMin)/(bounds.width*bounds.height);
        }
        internal static void AssertScene(string scene)
        {
            var theme=UITheme.Instance;
            ColorEquals(theme.background,Hex(0xff,0xf8,0xfb),"Original page palette");
            ColorEquals(theme.primary,Hex(0xe6,0x6b,0x8a),"Original pink primary");
            ColorEquals(theme.foreground,Hex(0x2c,0x21,0x34),"Original dark text");
            Assert.That(theme.radiusLarge,Is.EqualTo(28));Assert.That(theme.radiusButton,Is.EqualTo(18));
            if(scene=="Battle")
            {
                Rect("Canvas");var panel=Rect("TowerPanel");
                Assert.That(panel.sizeDelta.x,Is.EqualTo(600).Within(.1));
                Assert.That(panel.sizeDelta.y,Is.EqualTo(100).Within(.1));
                foreach(string name in new[]{"CostText","GPAText","WaveText"}) {Rect(name);Rect(name+"_IntegratedCard");}
                return;
            }
            string[] containers;
            switch(scene)
            {
                case "MainMenu":containers=new[]{"GPAStatCard","PersonalityInfoCard"};break;
                case "PersonalityTest":containers=new[]{"PersonalityQuestionCard","ProgressText_Backdrop"};break;
                case "Schedule":containers=new[]{"ActivityListPanel","StatsInfoCard","DDLPanelCard"};break;
                case "Shop":containers=new[]{"ShopItemListPanel"};break;
                case "Result":containers=new[]{"ResultHeroCard","ResultGPACard","ResultGoldCard"};break;
                default:Assert.Fail("Original layout contract is missing for "+scene);return;
            }
            foreach(string name in containers)Rect(name);
            var background=Rect(scene+"Background");
            Assert.That(background.anchorMin,Is.EqualTo(Vector2.zero));Assert.That(background.anchorMax,Is.EqualTo(Vector2.one));
            var image=background.GetComponent<Image>();Assert.That(image,Is.Not.Null);Assert.That(image.raycastTarget,Is.False,"Original background does not cover input");
            if(scene=="PersonalityTest") ColorEquals(image.color,Hex(0xff,0xf8,0xfb),"Original personality background");
            else
            {
                var gradient=background.GetComponent<UIGradient>();Assert.That(gradient,Is.Not.Null);
                ColorEquals(gradient.topColor,Hex(0xff,0xfa,0xfc),"Original background gradient start");
                ColorEquals(gradient.bottomColor,Hex(0xff,0xf2,0xf7),"Original background gradient end");
            }
#if UNITY_EDITOR
            foreach(var graphic in Object.FindObjectsByType<Image>().Where(item=>item.gameObject.scene==SceneManager.GetActiveScene()&&item.sprite!=null&&item.color.a>.01f))
            {
                string path=UnityEditor.AssetDatabase.GetAssetPath(graphic.sprite.texture);
                if(!path.Contains("/GameArt/"))continue;
                Assert.That(scene,Is.EqualTo("Shop"),"Full-page character art must not replace the original page: "+graphic.name);
                Assert.That(graphic.name,Is.EqualTo("ItemIcon"),"Supplied art stays in the existing shop icon slot");
                Assert.That(graphic.rectTransform.rect.width,Is.LessThanOrEqualTo(56.1f));
                Assert.That(graphic.rectTransform.rect.height,Is.LessThanOrEqualTo(56.1f));
            }
#endif
        }
    }
}
