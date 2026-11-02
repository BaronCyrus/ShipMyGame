// ShipMyGame — the compliance UI China's mini-game reviews ask for, built in code with uGUI (no prefabs):
// a consent dialog before any personal data is touched, the privacy policy, the age-rating tip, the age badge plus
// privacy button for the launch screen, and the standard health advice (健康游戏忠告).
// Texts: Resources/ShipCompliance/zh-Hans.txt (key=value per line, \n = line break; template in ../templates/).
// Font: WebGL has no system fallback, so assign a font that covers the Chinese texts before the first call
// (e.g. the subset from make_cjk_font.py): ShipCompliance.Font = myCjkFont;
//
//   ShipCompliance.Run(agreed => { if (agreed) StartLogin(); StartGame(); });
//   ShipCompliance.LaunchBadges(homeScreenRow);   // age badge + privacy policy button (both required on the launch screen)
//   ShipCompliance.HealthAdvice(homeScreenFooter);
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class ShipCompliance
{
    public static Font Font;
    public static string AgeRating = "8+";
    public static Color Accent = new Color(0.17f, 0.66f, 0.29f);
    const string AgreedKey = "ship_privacy_agreed_version";
    static Dictionary<string, string> _texts;
    static Canvas _canvas;

    public static string T(string key)
    {
        if (_texts == null)
        {
            _texts = new Dictionary<string, string>();
            var asset = Resources.Load<TextAsset>("ShipCompliance/zh-Hans");
            if (asset != null)
                foreach (var raw in asset.text.Split('\n'))
                {
                    var line = raw.TrimEnd('\r');
                    int i = line.IndexOf('=');
                    if (line.StartsWith("#") || i <= 0) continue;
                    _texts[line.Substring(0, i).Trim()] = line.Substring(i + 1).Replace("\\n", "\n");
                }
        }
        return _texts.TryGetValue(key, out var v) ? v : key;
    }

    /// <summary>
    /// Where the accepted policy version is kept: PlayerPrefs by default. If a device shows the consent dialog on
    /// every launch, PlayerPrefs does not persist on that platform: point these two at the platform's storage API.
    /// </summary>
    public static Func<string> LoadAgreed = () => PlayerPrefs.GetString(AgreedKey, "");
    public static Action<string> SaveAgreed = v => { PlayerPrefs.SetString(AgreedKey, v); PlayerPrefs.Save(); };

    /// <summary>True once the player accepted the current policy_version; a new version asks again.</summary>
    public static bool Agreed => LoadAgreed() == T("policy_version");

    /// <summary>Asks for consent unless the current policy is already accepted; declining keeps the game playable.</summary>
    public static void Run(Action<bool> done)
    {
        if (Agreed) { done?.Invoke(true); return; }
        var pop = Popup(T("consent_title"), T("consent"), 0.62f);
        AddButton(pop.buttons, T("read_policy"), new Color(0, 0, 0, 0.06f), Accent, () => ShowPolicy());
        AddButton(pop.buttons, T("agree"), Accent, Color.white, () =>
        {
            SaveAgreed(T("policy_version"));
            UnityEngine.Object.Destroy(pop.root);
            done?.Invoke(true);
        });
        AddButton(pop.buttons, T("disagree"), new Color(0, 0, 0, 0.06f), new Color(0.4f, 0.4f, 0.4f), () =>
        {
            UnityEngine.Object.Destroy(pop.root);
            done?.Invoke(false);
        });
    }

    public static void ShowPolicy() => ShowDocument(T("privacy_title"), T("privacy"));
    public static void ShowAgeTip() => ShowDocument(T("age_title"), T("age"));

    /// <summary>Age badge and privacy-policy button side by side, stretched over <paramref name="row"/>.</summary>
    public static void LaunchBadges(RectTransform row)
    {
        var age = MakeButton(row, AgeRating + "  " + T("age_title"), Color.white, new Color(0.2f, 0.2f, 0.2f), ShowAgeTip);
        Stretch(age, new Vector2(0, 0), new Vector2(0.48f, 1));
        var badge = Panel(age, Accent);
        badge.anchorMin = new Vector2(0, 0.1f); badge.anchorMax = new Vector2(0, 0.9f);
        badge.sizeDelta = new Vector2(8, 0); badge.anchoredPosition = new Vector2(4, 0);
        var policy = MakeButton(row, T("privacy_title"), Color.white, new Color(0.2f, 0.2f, 0.2f), ShowPolicy);
        Stretch(policy, new Vector2(0.52f, 0), new Vector2(1, 1));
    }

    /// <summary>The standard health advice, filling <paramref name="parent"/>.</summary>
    public static Text HealthAdvice(RectTransform parent)
    {
        var t = Label(parent, T("health"), 26, new Color(0, 0, 0, 0.55f), TextAnchor.MiddleCenter);
        Stretch(t.rectTransform, Vector2.zero, Vector2.one);
        t.resizeTextForBestFit = true; t.resizeTextMinSize = 14; t.resizeTextMaxSize = 26;
        return t;
    }

    static void ShowDocument(string title, string body)
    {
        var pop = Popup(title, body, 0.78f);
        GameObject root = pop.root;
        AddButton(pop.buttons, T("close"), Accent, Color.white, () => UnityEngine.Object.Destroy(root));
    }

    struct Pop { public GameObject root; public RectTransform buttons; }

    /// <summary>Dimmed full-screen popup: title, scrollable body, a row of buttons.</summary>
    static Pop Popup(string title, string body, float heightFraction)
    {
        var canvas = EnsureCanvas();
        var dim = Panel(canvas.transform, new Color(0, 0, 0, 0.55f));
        Stretch(dim, Vector2.zero, Vector2.one);
        var card = Panel(dim, Color.white);
        float margin = (1 - heightFraction) / 2;
        Stretch(card, new Vector2(0.07f, margin), new Vector2(0.93f, 1 - margin));

        var head = Label(card, title, 44, new Color(0.1f, 0.1f, 0.1f), TextAnchor.MiddleCenter);
        head.fontStyle = FontStyle.Bold;
        Stretch(head.rectTransform, new Vector2(0, 0.88f), new Vector2(1, 1));

        var view = Panel(card, new Color(1, 1, 1, 0));
        Stretch(view, new Vector2(0.06f, 0.24f), new Vector2(0.94f, 0.87f));
        view.gameObject.AddComponent<RectMask2D>();
        var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(view, false);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
        content.sizeDelta = Vector2.zero;
        var text = Label(content, body, 34, new Color(0.2f, 0.2f, 0.2f), TextAnchor.UpperLeft);
        Stretch(text.rectTransform, Vector2.zero, Vector2.one);
        var fit = content.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var column = content.gameObject.AddComponent<VerticalLayoutGroup>();
        column.childControlWidth = true; column.childControlHeight = true;
        column.childForceExpandWidth = true; column.childForceExpandHeight = false;
        var scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.content = content; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;

        var buttons = new GameObject("Buttons", typeof(RectTransform)).GetComponent<RectTransform>();
        buttons.SetParent(card, false);
        Stretch(buttons, new Vector2(0.06f, 0.03f), new Vector2(0.94f, 0.22f));
        var layout = buttons.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 16; layout.childForceExpandHeight = true; layout.childControlHeight = true; layout.childControlWidth = true;
        return new Pop { root = dim.gameObject, buttons = buttons };
    }

    static void AddButton(RectTransform parent, string label, Color bg, Color fg, Action onClick) => MakeButton(parent, label, bg, fg, onClick);

    static RectTransform MakeButton(RectTransform parent, string label, Color bg, Color fg, Action onClick)
    {
        var rt = Panel(parent, bg);
        rt.gameObject.AddComponent<Button>().onClick.AddListener(() => onClick());
        var t = Label(rt, label, 36, fg, TextAnchor.MiddleCenter);
        Stretch(t.rectTransform, Vector2.zero, Vector2.one);
        return rt;
    }

    static RectTransform Panel(Transform parent, Color color)
    {
        var go = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go.GetComponent<RectTransform>();
    }

    static Text Label(Transform parent, string s, int size, Color color, TextAnchor align)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = Font != null ? Font : BuiltinFont();
        t.text = s; t.fontSize = size; t.color = color; t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static Font BuiltinFont()
    {
        try { return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }   // Unity 2022.2 and later
        catch (ArgumentException) { return Resources.GetBuiltinResource<Font>("Arial.ttf"); }
    }

    static void Stretch(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static Canvas EnsureCanvas()
    {
        if (_canvas != null) return _canvas;
        var go = new GameObject("ShipCompliance", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        UnityEngine.Object.DontDestroyOnLoad(go);
        _canvas = go.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5000;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        if (EventSystem.current == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem));
            UnityEngine.Object.DontDestroyOnLoad(es);
            // Projects on the new Input System need its UI module; the legacy one throws there.
            var inputSystemModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            es.AddComponent(inputSystemModule ?? typeof(StandaloneInputModule));
        }
        return _canvas;
    }
}
