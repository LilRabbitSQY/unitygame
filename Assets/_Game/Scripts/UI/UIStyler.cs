using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FinalDefense.UI
{
    public static class UIStyler
    {
        private static UITheme Theme => UITheme.Instance;
        private static readonly Dictionary<float, Sprite> RoundedSpriteCache = new Dictionary<float, Sprite>();

        public static RectTransform EnsurePageBackground(Canvas canvas, string name = "PageBackground")
        {
            if (canvas == null) return null;

            var background = canvas.transform.Find(name) as RectTransform;
            if (background == null)
            {
                var bgGo = new GameObject(name, typeof(RectTransform), typeof(Image));
                bgGo.transform.SetParent(canvas.transform, false);
                background = bgGo.GetComponent<RectTransform>();
                background.SetAsFirstSibling();
            }

            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.offsetMin = Vector2.zero;
            background.offsetMax = Vector2.zero;

            var image = background.GetComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;

            var gradient = background.GetComponent<UIGradient>();
            if (gradient == null)
            {
                gradient = background.gameObject.AddComponent<UIGradient>();
            }
            gradient.topColor = Theme.gradientStart;
            gradient.bottomColor = Theme.gradientEnd;
            gradient.gradientDirection = UIGradient.GradientDirection.Vertical;

            return background;
        }

        public static RectTransform EnsureBackdrop(RectTransform target, string name, Vector2 padding, Color color, bool useMutedStyle = false)
        {
            if (target == null || target.parent == null) return null;

            var existing = target.parent.Find(name) as RectTransform;
            GameObject backdropGo;
            if (existing == null)
            {
                backdropGo = new GameObject(name, typeof(RectTransform), typeof(Image));
                backdropGo.transform.SetParent(target.parent, false);
                backdropGo.transform.SetSiblingIndex(Mathf.Max(0, target.GetSiblingIndex()));
                existing = backdropGo.GetComponent<RectTransform>();
            }
            else
            {
                backdropGo = existing.gameObject;
            }

            existing.anchorMin = target.anchorMin;
            existing.anchorMax = target.anchorMax;
            existing.pivot = target.pivot;
            existing.anchoredPosition = target.anchoredPosition;
            existing.sizeDelta = new Vector2(
                Mathf.Abs(target.sizeDelta.x) + padding.x,
                Mathf.Abs(target.sizeDelta.y) + padding.y);

            if (useMutedStyle)
            {
                ApplyMutedPanelStyle(backdropGo);
            }
            else
            {
                ApplyCardStyle(backdropGo);
            }

            var image = backdropGo.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
                image.raycastTarget = false;
            }

            return existing;
        }

        public static Transform FindDeepChild(Transform parent, string childName)
        {
            if (parent == null || string.IsNullOrEmpty(childName)) return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == childName)
                    return child;

                var found = FindDeepChild(child, childName);
                if (found != null)
                    return found;
            }

            return null;
        }

        public static void SetCenteredRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            if (rect == null) return;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        public static void SetTopRect(RectTransform rect, float topOffset, Vector2 size)
        {
            if (rect == null) return;

            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -topOffset);
            rect.sizeDelta = size;
        }

        public static void EnsureVerticalLayout(Transform container, float spacing = 12f, RectOffset padding = null)
        {
            if (container == null) return;

            var layout = container.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
                layout = container.gameObject.AddComponent<VerticalLayoutGroup>();

            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset(12, 12, 12, 12);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        public static void ApplyCardStyle(GameObject target, bool useSoftShadow = true)
        {
            if (target == null) return;

            var image = target.GetComponent<Image>();
            if (image == null)
            {
                image = target.AddComponent<Image>();
            }

            image.color = Theme.card;
            ApplyRoundedRect(target, Theme.radiusLarge);

            var outline = target.GetComponent<Outline>();
            if (outline == null)
            {
                outline = target.AddComponent<Outline>();
            }
            outline.effectColor = Theme.border;
            outline.effectDistance = new Vector2(1, -1);

            if (useSoftShadow)
            {
                ApplySoftShadow(target);
            }
        }

        public static void ApplyPrimaryButtonStyle(Button button)
        {
            if (button == null) return;

            var image = button.GetComponent<Image>();
            if (image == null)
            {
                image = button.gameObject.AddComponent<Image>();
            }

            image.color = Theme.primary;
            ApplyRoundedRect(button.gameObject, Theme.radiusButton);

            var colors = button.colors;
            colors.normalColor = Theme.primary;
            colors.highlightedColor = Color.Lerp(Theme.primary, Color.white, 0.15f);
            colors.pressedColor = Color.Lerp(Theme.primary, Color.black, 0.1f);
            colors.selectedColor = Theme.primary;
            colors.disabledColor = new Color(Theme.primary.r, Theme.primary.g, Theme.primary.b, 0.5f);
            button.colors = colors;

            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.color = Theme.primaryForeground;
                text.fontSize = Theme.bodyFontSize;
                text.fontStyle = FontStyles.Bold;
                text.alignment = TextAlignmentOptions.Center;
            }
        }

        public static void ApplySecondaryButtonStyle(Button button)
        {
            if (button == null) return;

            var image = button.GetComponent<Image>();
            if (image == null)
            {
                image = button.gameObject.AddComponent<Image>();
            }

            image.color = Color.white;
            ApplyRoundedRect(button.gameObject, Theme.radiusButton);

            var outline = button.gameObject.GetComponent<Outline>();
            if (outline == null)
            {
                outline = button.gameObject.AddComponent<Outline>();
            }
            outline.effectColor = Theme.border;
            outline.effectDistance = new Vector2(1, -1);

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Theme.muted;
            colors.pressedColor = Theme.accent;
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1, 1, 1, 0.5f);
            button.colors = colors;

            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.color = Theme.secondaryForeground;
                text.fontSize = Theme.bodyFontSize;
                text.fontStyle = FontStyles.Bold;
                text.alignment = TextAlignmentOptions.Center;
            }
        }

        public static void ApplyGradientButtonStyle(Button button)
        {
            if (button == null) return;

            ApplyPrimaryButtonStyle(button);

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                var gradient = button.gameObject.GetComponent<UIGradient>();
                if (gradient == null)
                {
                    gradient = button.gameObject.AddComponent<UIGradient>();
                }
                gradient.topColor = Theme.primaryGradientStart;
                gradient.bottomColor = Theme.primaryGradientEnd;
                gradient.gradientDirection = UIGradient.GradientDirection.Diagonal;
            }
        }

        public static void ApplyPanelStyle(GameObject target, bool useGradient = false)
        {
            if (target == null) return;

            var image = target.GetComponent<Image>();
            if (image == null)
            {
                image = target.AddComponent<Image>();
            }

            if (useGradient)
            {
                image.color = Color.white;
                var gradient = target.GetComponent<UIGradient>();
                if (gradient == null)
                {
                    gradient = target.AddComponent<UIGradient>();
                }
                gradient.topColor = Theme.gradientStart;
                gradient.bottomColor = Theme.gradientEnd;
                gradient.gradientDirection = UIGradient.GradientDirection.Vertical;
            }
            else
            {
                image.color = Theme.card;
            }

            ApplyRoundedRect(target, Theme.radiusLarge);

            var outline = target.GetComponent<Outline>();
            if (outline == null)
            {
                outline = target.AddComponent<Outline>();
            }
            outline.effectColor = Theme.border;
            outline.effectDistance = new Vector2(1, -1);
        }

        public static void ApplyMutedPanelStyle(GameObject target)
        {
            if (target == null) return;

            var image = target.GetComponent<Image>();
            if (image == null)
            {
                image = target.AddComponent<Image>();
            }

            image.color = Theme.muted;
            ApplyRoundedRect(target, Theme.radiusCard);

            var outline = target.GetComponent<Outline>();
            if (outline == null)
            {
                outline = target.AddComponent<Outline>();
            }
            outline.effectColor = Theme.border;
            outline.effectDistance = new Vector2(1, -1);
        }

        public static void ApplySidebarStyle(GameObject target)
        {
            if (target == null) return;

            var image = target.GetComponent<Image>();
            if (image == null)
            {
                image = target.AddComponent<Image>();
            }

            var gradient = target.GetComponent<UIGradient>();
            if (gradient == null)
            {
                gradient = target.AddComponent<UIGradient>();
            }
            gradient.topColor = new Color(0xFF / 255f, 0xF7 / 255f, 0xFA / 255f);
            gradient.bottomColor = new Color(0xFF / 255f, 0xED / 255f, 0xF3 / 255f);
            gradient.gradientDirection = UIGradient.GradientDirection.Vertical;

            ApplyRoundedRect(target, Theme.radiusLarge);

            var outline = target.GetComponent<Outline>();
            if (outline == null)
            {
                outline = target.AddComponent<Outline>();
            }
            outline.effectColor = Theme.border;
            outline.effectDistance = new Vector2(1, -1);
        }

        public static void ApplyHeaderTextStyle(TextMeshProUGUI text, bool isH1 = false)
        {
            if (text == null) return;

            text.color = Theme.foreground;
            text.fontSize = isH1 ? Theme.h1FontSize : Theme.h2FontSize;
            text.fontStyle = FontStyles.Bold;
            text.enableAutoSizing = false;
            text.lineSpacing = 8;
        }

        public static void ApplyBodyTextStyle(TextMeshProUGUI text)
        {
            if (text == null) return;

            text.color = Theme.mutedForeground;
            text.fontSize = Theme.bodyFontSize;
            text.fontStyle = FontStyles.Normal;
            text.lineSpacing = Theme.defaultLineSpacing * 10;
        }

        public static void ApplyCaptionTextStyle(TextMeshProUGUI text)
        {
            if (text == null) return;

            text.color = Theme.mutedForeground;
            text.fontSize = Theme.smallFontSize;
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 0.16f;
        }

        public static void ApplyStatValueTextStyle(TextMeshProUGUI text)
        {
            if (text == null) return;

            text.color = Theme.foreground;
            text.fontSize = 28;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Left;
        }

        public static void ApplyTagStyle(GameObject target, Color backgroundColor, Color textColor)
        {
            if (target == null) return;

            var textGraphic = target.GetComponent<TextMeshProUGUI>();
            if (textGraphic != null)
            {
                EnsureBackdrop(textGraphic.rectTransform, target.name + "TagBackground", new Vector2(24, 12), backgroundColor, true);
                textGraphic.color = textColor; textGraphic.fontSize = Theme.smallFontSize;
                textGraphic.alignment = TextAlignmentOptions.Center;
                return;
            }
            var image = target.GetComponent<Image>();
            if (image == null) image = target.AddComponent<Image>();
            if (image == null) return;

            image.color = backgroundColor;
            ApplyRoundedRect(target, Theme.radiusPill);

            var text = target.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.color = textColor;
                text.fontSize = Theme.smallFontSize;
                text.fontStyle = FontStyles.Bold;
                text.alignment = TextAlignmentOptions.Center;
            }
        }

        public static void ApplyProgressBarStyle(Image fillImage, Color fillColor)
        {
            if (fillImage == null) return;

            fillImage.color = fillColor;
            ApplyRoundedRect(fillImage.gameObject, Theme.radiusPill);

            var bgTransform = fillImage.transform.parent;
            if (bgTransform != null)
            {
                var bgImage = bgTransform.GetComponent<Image>();
                if (bgImage == null)
                {
                    bgImage = bgTransform.gameObject.AddComponent<Image>();
                }
                bgImage.color = new Color(0xF4 / 255f, 0xE2 / 255f, 0xEA / 255f);
                ApplyRoundedRect(bgTransform.gameObject, Theme.radiusPill);
            }
        }

        public static void ApplyRoundedRect(GameObject target, float radius)
        {
            if (target == null) return;

            var rect = target.GetComponent<RectTransform>();
            if (rect == null) return;

            int cornerCount = 4;
            float radiusScaled = radius;

            var sprite = CreateRoundedRectSprite(radiusScaled);
            var image = target.GetComponent<Image>();
            if (image != null && sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }
        }

        private static Sprite CreateRoundedRectSprite(float radius)
        {
            float roundedRadius = Mathf.Round(radius * 10f) / 10f;

            if (RoundedSpriteCache.TryGetValue(roundedRadius, out var cached) && cached != null)
                return cached;

            int size = Mathf.CeilToInt(roundedRadius * 4);
            if (size < 32) size = 32;

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            float r = roundedRadius / (size - 1);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = (float)x / (size - 1);
                    float fy = (float)y / (size - 1);

                    bool inside = true;

                    if (fx < r && fy < r)
                    {
                        float dx = r - fx;
                        float dy = r - fy;
                        inside = dx * dx + dy * dy <= r * r;
                    }
                    else if (fx > 1 - r && fy < r)
                    {
                        float dx = fx - (1 - r);
                        float dy = r - fy;
                        inside = dx * dx + dy * dy <= r * r;
                    }
                    else if (fx < r && fy > 1 - r)
                    {
                        float dx = r - fx;
                        float dy = fy - (1 - r);
                        inside = dx * dx + dy * dy <= r * r;
                    }
                    else if (fx > 1 - r && fy > 1 - r)
                    {
                        float dx = fx - (1 - r);
                        float dy = fy - (1 - r);
                        inside = dx * dx + dy * dy <= r * r;
                    }

                    pixels[y * size + x] = inside ? Color.white : Color.clear;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            float border = roundedRadius;
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));

            RoundedSpriteCache[roundedRadius] = sprite;
            return sprite;
        }

        private static void ApplySoftShadow(GameObject target)
        {
            var shadow = target.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = target.AddComponent<Shadow>();
            }
            shadow.effectColor = Theme.shadowCardColor;
            shadow.effectDistance = Theme.shadowCardOffset;
        }

        public static void ApplyTowerCardStyle(GameObject card, Color accentColor, bool active = false)
        {
            if (card == null) return;

            var image = card.GetComponent<Image>();
            if (image == null)
            {
                image = card.AddComponent<Image>();
            }

            var gradient = card.GetComponent<UIGradient>();
            if (gradient == null)
            {
                gradient = card.AddComponent<UIGradient>();
            }

            Color lightTint = Color.Lerp(accentColor, Color.white, 0.85f);
            Color lighterTint = Color.Lerp(accentColor, Color.white, 0.95f);

            gradient.topColor = lighterTint;
            gradient.bottomColor = lightTint;
            gradient.gradientDirection = UIGradient.GradientDirection.Vertical;

            ApplyRoundedRect(card, 24f);

            var outline = card.GetComponent<Outline>();
            if (outline == null)
            {
                outline = card.AddComponent<Outline>();
            }
            outline.effectColor = Color.Lerp(accentColor, Color.white, 0.5f);
            outline.effectDistance = new Vector2(1, -1);
        }

        public static void ApplyEnemyIconStyle(Image icon, Color enemyColor, string label)
        {
            if (icon == null) return;

            icon.color = enemyColor;
            ApplyRoundedRect(icon.gameObject, 100f);

            var parent = icon.transform.parent;
            if (parent != null)
            {
                var outline = parent.GetComponent<Outline>();
                if (outline == null)
                {
                    outline = parent.gameObject.AddComponent<Outline>();
                }
                outline.effectColor = Color.white;
                outline.effectDistance = new Vector2(2, -2);
            }
        }
    }
}
