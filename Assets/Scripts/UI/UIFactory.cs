using System;
using UnityEngine;
using UnityEngine.UI;
using MonsterMart.Art;

namespace MonsterMart.UI
{
    /// <summary>
    /// 程序化构建 uGUI 的工具箱。工程里没有任何 UI 预制体或图集。
    ///
    /// 文本使用旧版 UnityEngine.UI.Text + 系统动态字体：
    /// TextMeshPro 需要预先烘焙字体资产（中文字量大、必须手工生成），
    /// 而动态 OS 字体开箱即用且能正常渲染中文 —— 目标平台是 Windows PC（文档 §1.4）。
    /// </summary>
    public static class UIFactory
    {
        public static readonly Color Ink = new Color(0.93f, 0.93f, 0.97f);
        public static readonly Color InkDim = new Color(0.68f, 0.68f, 0.78f);
        public static readonly Color Accent = new Color(0.85f, 0.62f, 1.00f);
        public static readonly Color Good = new Color(0.45f, 0.88f, 0.52f);
        public static readonly Color Warn = new Color(0.98f, 0.78f, 0.32f);
        public static readonly Color Bad = new Color(0.95f, 0.42f, 0.38f);
        public static readonly Color PanelBg = new Color(0.09f, 0.08f, 0.14f, 0.96f);
        public static readonly Color PanelBgSoft = new Color(0.14f, 0.12f, 0.20f, 0.94f);
        public static readonly Color Scrim = new Color(0f, 0f, 0f, 0.62f);
        public static readonly Color ButtonBg = new Color(0.24f, 0.20f, 0.34f, 1f);
        public static readonly Color ButtonBgHi = new Color(0.36f, 0.29f, 0.52f, 1f);

        static Font _font;
        static Sprite _whiteSprite;

        /// <summary>中文字体回退链。数组重载会由 Unity 依次尝试。</summary>
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;

                string[] candidates =
                {
                    "Microsoft YaHei UI", "Microsoft YaHei", "微软雅黑",
                    "SimHei", "黑体", "SimSun", "宋体",
                    "Noto Sans CJK SC", "Source Han Sans SC",
                    "PingFang SC", "Heiti SC",
                    "Arial Unicode MS", "Segoe UI", "Arial"
                };

                _font = Font.CreateDynamicFontFromOSFont(candidates, 24);

                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (_font == null) Debug.LogWarning("[UIFactory] 找不到可用字体，UI 文本可能不显示。");

                return _font;
            }
        }

        public static Sprite White
        {
            get
            {
                if (_whiteSprite == null) _whiteSprite = SpriteFactory.Solid(Color.white);
                return _whiteSprite;
            }
        }

        // ------------------------------------------------------------------
        // 基础节点
        // ------------------------------------------------------------------
        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            return rt;
        }

        /// <summary>铺满父节点的矩形。</summary>
        public static RectTransform Stretch(RectTransform rt, float left = 0, float bottom = 0,
                                            float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Anchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
                                           Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Panel(Transform parent, Color color, string name = "Panel")
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = White;
            img.color = color;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color,
                                 TextAnchor alignment = TextAnchor.UpperLeft, string name = "Label")
        {
            var rt = NewRect(name, parent);
            var label = rt.gameObject.AddComponent<Text>();
            label.font = Font;
            label.fontSize = size;
            label.color = color;
            label.text = text;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = true;
            label.raycastTarget = false;
            return label;
        }

        public static Button Button(Transform parent, string caption, Action onClick,
                                    int fontSize = 20, Color? background = null)
        {
            var rt = NewRect("Button_" + caption, parent);

            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = White;
            img.color = background ?? ButtonBg;

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.25f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.88f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.55f, 0.6f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var label = Label(rt, caption, fontSize, Ink, TextAnchor.MiddleCenter, "Caption");
            Stretch(label.rectTransform, 8, 4, 8, 4);

            if (onClick != null) button.onClick.AddListener(() => onClick());
            return button;
        }

        public static VerticalLayoutGroup VerticalGroup(Transform parent, float spacing,
                                                        RectOffset padding = null,
                                                        TextAnchor alignment = TextAnchor.UpperLeft)
        {
            var rt = NewRect("VGroup", parent);
            var group = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            group.padding = padding ?? new RectOffset(0, 0, 0, 0);
            group.childAlignment = alignment;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            group.childControlWidth = true;
            group.childControlHeight = true;
            return group;
        }

        public static HorizontalLayoutGroup HorizontalGroup(Transform parent, float spacing,
                                                            RectOffset padding = null,
                                                            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var rt = NewRect("HGroup", parent);
            var group = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = spacing;
            group.padding = padding ?? new RectOffset(0, 0, 0, 0);
            group.childAlignment = alignment;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            group.childControlWidth = true;
            group.childControlHeight = true;
            return group;
        }

        public static LayoutElement Size(GameObject go, float minWidth = -1, float minHeight = -1,
                                         float preferredWidth = -1, float preferredHeight = -1,
                                         float flexibleWidth = -1, float flexibleHeight = -1)
        {
            var element = go.GetComponent<LayoutElement>();
            if (element == null) element = go.AddComponent<LayoutElement>();

            if (minWidth >= 0) element.minWidth = minWidth;
            if (minHeight >= 0) element.minHeight = minHeight;
            if (preferredWidth >= 0) element.preferredWidth = preferredWidth;
            if (preferredHeight >= 0) element.preferredHeight = preferredHeight;
            if (flexibleWidth >= 0) element.flexibleWidth = flexibleWidth;
            if (flexibleHeight >= 0) element.flexibleHeight = flexibleHeight;
            return element;
        }

        public static Image Icon(Transform parent, Sprite sprite, float size, string name = "Icon")
        {
            var rt = NewRect(name, parent);
            rt.sizeDelta = new Vector2(size, size);

            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;

            Size(rt.gameObject, size, size, size, size);
            return img;
        }

        /// <summary>一条带填充的进度条，返回填充 Image（改 fillAmount 即可）。</summary>
        public static Image Bar(Transform parent, Color background, Color fill,
                                float width, float height, string name = "Bar")
        {
            var back = Panel(parent, background, name);
            back.rectTransform.sizeDelta = new Vector2(width, height);
            Size(back.gameObject, width, height, width, height);

            var fillRt = NewRect("Fill", back.transform);
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(2, 2);
            fillRt.offsetMax = new Vector2(-2, -2);

            var fillImg = fillRt.gameObject.AddComponent<Image>();
            fillImg.sprite = White;
            fillImg.color = fill;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 1f;
            fillImg.raycastTarget = false;
            return fillImg;
        }

        /// <summary>标题 + 分隔线。</summary>
        public static Text SectionTitle(Transform parent, string text)
        {
            var label = Label(parent, text, 26, Accent, TextAnchor.MiddleLeft, "SectionTitle");
            Size(label.gameObject, -1, 34, -1, 34);
            return label;
        }

        public static Image Divider(Transform parent)
        {
            var img = Panel(parent, new Color(1f, 1f, 1f, 0.12f), "Divider");
            Size(img.gameObject, -1, 2, -1, 2);
            return img;
        }
    }
}
