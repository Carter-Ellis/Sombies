using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class DownedPlayerIndicatorUI : MonoBehaviour
{
    public static DownedPlayerIndicatorUI Instance { get; private set; }

    [Header("Indicator Settings")]
    [SerializeField] private float edgeMargin = 70f;
    [SerializeField] private float pulseSpeed = 6f;
    [SerializeField] private float minPulseScale = 0.88f;
    [SerializeField] private float maxPulseScale = 1.18f;
    [SerializeField] private bool hideWhenOnScreen = false;
    [SerializeField] private float hideDistance = 2.5f;

    [Header("Visual Customization")]
    [SerializeField] private Sprite customArrowSprite;
    [SerializeField] private Color fullTimerColor = Color.white;
    [SerializeField] private Color expiredTimerColor = new Color(0.5f, 0.05f, 0.05f, 1f);

    private Canvas _canvas;
    private Camera _mainCam;
    private List<IndicatorData> _indicators = new List<IndicatorData>();
    private Sprite _arrowSprite;

    private class IndicatorData
    {
        public ReviveController targetRevive;
        public GameObject rootObj;
        public RectTransform rootRect;
        public RectTransform arrowRect;
        public Image arrowImage;
        public TextMeshProUGUI distanceText;
        public RectTransform textRect;
        public Image bgImage;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        GetTargetCanvas();
        _arrowSprite = customArrowSprite != null ? customArrowSprite : CreateArrowSprite();
    }

    private void Start()
    {
        _mainCam = Camera.main;
    }

    private void Update()
    {
        if (_mainCam == null)
        {
            _mainCam = Camera.main;
            if (_mainCam == null) return;
        }

        // Find local player
        Player localPlayer = GetLocalPlayer();
        if (localPlayer == null)
        {
            HideAllIndicators();
            return;
        }

        ReviveController localRevive = localPlayer.GetComponent<ReviveController>();
        // Only show indicators to active (up) players
        if (localRevive != null && localRevive.IsDownedSync.Value)
        {
            HideAllIndicators();
            return;
        }

        // Find all downed players in scene
        ReviveController[] allRevives = Object.FindObjectsByType<ReviveController>(FindObjectsInactive.Exclude);
        List<ReviveController> downedTargets = new List<ReviveController>();

        foreach (var rc in allRevives)
        {
            if (rc == null || rc.gameObject == localPlayer.gameObject) continue;
            if (rc.IsDownedSync.Value)
            {
                downedTargets.Add(rc);
            }
        }

        // Sync pool with active downed targets
        SyncIndicatorPool(downedTargets);

        // Update each active indicator
        float pulse = Mathf.Lerp(minPulseScale, maxPulseScale, (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f);

        for (int i = 0; i < _indicators.Count; i++)
        {
            IndicatorData ind = _indicators[i];
            if (ind.targetRevive == null || !ind.targetRevive.IsDownedSync.Value)
            {
                if (ind.rootObj != null) ind.rootObj.SetActive(false);
                continue;
            }

            float dist = Vector3.Distance(localPlayer.transform.position, ind.targetRevive.transform.position);

            Vector3 targetPos = ind.targetRevive.transform.position + Vector3.up * 0.3f;
            Vector3 screenPos = _mainCam.WorldToScreenPoint(targetPos);

            bool isBehind = screenPos.z < 0;
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 fromCenter = (Vector2)screenPos - screenCenter;

            if (isBehind)
            {
                fromCenter = -fromCenter;
            }

            float xMax = (Screen.width * 0.5f) - edgeMargin;
            float yMax = (Screen.height * 0.5f) - edgeMargin;

            bool isOnScreen = !isBehind &&
                              screenPos.x >= edgeMargin &&
                              screenPos.x <= Screen.width - edgeMargin &&
                              screenPos.y >= edgeMargin &&
                              screenPos.y <= Screen.height - edgeMargin;

            if ((hideWhenOnScreen && isOnScreen) || dist <= hideDistance)
            {
                if (ind.rootObj != null) ind.rootObj.SetActive(false);
                continue;
            }

            if (ind.rootObj != null && !ind.rootObj.activeSelf)
            {
                ind.rootObj.SetActive(true);
            }

            Vector2 indicatorScreenPos;

            if (isOnScreen)
            {
                indicatorScreenPos = screenPos;
            }
            else
            {
                float slopeX = Mathf.Abs(fromCenter.x) > 0.001f ? xMax / Mathf.Abs(fromCenter.x) : float.MaxValue;
                float slopeY = Mathf.Abs(fromCenter.y) > 0.001f ? yMax / Mathf.Abs(fromCenter.y) : float.MaxValue;

                float scale = Mathf.Min(slopeX, slopeY);
                indicatorScreenPos = screenCenter + fromCenter * scale;
            }

            Vector2 anchoredPos = indicatorScreenPos - screenCenter;
            ind.rootRect.anchoredPosition = anchoredPos;

            // Calculate rotation towards downed player
            Vector2 dir = fromCenter.sqrMagnitude > 0.001f ? fromCenter.normalized : Vector2.up;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            ind.arrowRect.localRotation = Quaternion.Euler(0, 0, angle);
            ind.arrowRect.localScale = Vector3.one * pulse;

            // Bleedout timer color transition (White -> Dark Red as timer approaches 0)
            float timerValue = ind.targetRevive.NetDownedTimer.Value;
            float maxTimer = ind.targetRevive != null ? ind.targetRevive.DownedBleedoutDuration : 30f;
            float timerRatio = Mathf.Clamp01(timerValue / maxTimer);
            Color indicatorColor = Color.Lerp(expiredTimerColor, fullTimerColor, timerRatio);

            if (ind.arrowImage != null)
            {
                ind.arrowImage.color = indicatorColor;
            }

            // Distance display
            if (ind.distanceText != null)
            {
                ind.distanceText.text = $"{Mathf.RoundToInt(dist)}m";
                ind.distanceText.color = indicatorColor;
            }

            if (ind.bgImage != null)
            {
                ind.bgImage.color = new Color(indicatorColor.r * 0.2f, indicatorColor.g * 0.2f, indicatorColor.b * 0.2f, 0.75f);
            }
        }
    }

    private Canvas GetTargetCanvas()
    {
        if (_canvas != null && _canvas.enabled) return _canvas;

        if (UIManager.Instance != null && UIManager.Instance.HudCanvas != null)
        {
            _canvas = UIManager.Instance.HudCanvas;
            return _canvas;
        }

        _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null) _canvas = GetComponent<Canvas>();
        if (_canvas == null) _canvas = GetComponentInChildren<Canvas>();

        if (_canvas == null)
        {
            _canvas = Object.FindAnyObjectByType<Canvas>();
        }

        return _canvas;
    }

    private Player GetLocalPlayer()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            return NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<Player>();
        }

        Player[] players = Object.FindObjectsByType<Player>(FindObjectsInactive.Exclude);
        foreach (var p in players)
        {
            if (p.IsOwner) return p;
        }
        if (players.Length > 0) return players[0];
        return null;
    }

    private void SyncIndicatorPool(List<ReviveController> downedTargets)
    {
        for (int i = 0; i < _indicators.Count; i++)
        {
            if (_indicators[i].targetRevive != null && !downedTargets.Contains(_indicators[i].targetRevive))
            {
                _indicators[i].targetRevive = null;
                if (_indicators[i].rootObj != null) _indicators[i].rootObj.SetActive(false);
            }
        }

        foreach (var target in downedTargets)
        {
            bool exists = false;
            foreach (var ind in _indicators)
            {
                if (ind.targetRevive == target)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                IndicatorData freeInd = null;
                foreach (var ind in _indicators)
                {
                    if (ind.targetRevive == null)
                    {
                        freeInd = ind;
                        break;
                    }
                }

                if (freeInd == null)
                {
                    freeInd = CreateIndicatorUI();
                    _indicators.Add(freeInd);
                }

                freeInd.targetRevive = target;
                if (freeInd.rootObj != null) freeInd.rootObj.SetActive(true);
            }
        }
    }

    private void HideAllIndicators()
    {
        foreach (var ind in _indicators)
        {
            ind.targetRevive = null;
            if (ind.rootObj != null) ind.rootObj.SetActive(false);
        }
    }

    private IndicatorData CreateIndicatorUI()
    {
        Canvas targetCanvas = GetTargetCanvas();
        Transform parentTransform = targetCanvas != null ? targetCanvas.transform : transform;

        GameObject root = new GameObject("DownedIndicator", typeof(RectTransform));
        root.transform.SetParent(parentTransform, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(80f, 80f);

        // Arrow Image
        GameObject arrowObj = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
        arrowObj.transform.SetParent(root.transform, false);
        RectTransform arrowRect = arrowObj.GetComponent<RectTransform>();
        arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
        arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
        arrowRect.pivot = new Vector2(0.5f, 0.5f);
        arrowRect.sizeDelta = new Vector2(48f, 48f);

        Image arrowImg = arrowObj.GetComponent<Image>();
        arrowImg.sprite = _arrowSprite;
        arrowImg.color = fullTimerColor;

        // Text Badge Container
        GameObject textBg = new GameObject("TextBg", typeof(RectTransform), typeof(Image));
        textBg.transform.SetParent(root.transform, false);
        RectTransform textBgRect = textBg.GetComponent<RectTransform>();
        textBgRect.anchorMin = new Vector2(0.5f, 0.5f);
        textBgRect.anchorMax = new Vector2(0.5f, 0.5f);
        textBgRect.pivot = new Vector2(0.5f, 0.5f);
        textBgRect.anchoredPosition = new Vector2(0f, -32f);
        textBgRect.sizeDelta = new Vector2(54f, 22f);

        Image bgImg = textBg.GetComponent<Image>();
        bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.75f);

        // Text
        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(textBg.transform, false);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 13;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;

        IndicatorData data = new IndicatorData
        {
            rootObj = root,
            rootRect = rootRect,
            arrowRect = arrowRect,
            arrowImage = arrowImg,
            distanceText = tmp,
            textRect = textBgRect,
            bgImage = bgImg
        };

        return data;
    }

    private static Sprite CreateArrowSprite()
    {
        int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color transparent = new Color(0, 0, 0, 0);
        Color fill = Color.white;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                texture.SetPixel(x, y, transparent);
            }
        }

        Vector2 tip = new Vector2(58, 32);
        Vector2 topWing = new Vector2(14, 56);
        Vector2 botWing = new Vector2(14, 8);
        Vector2 notch = new Vector2(26, 32);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 pt = new Vector2(x, y);
                if (IsPointInTriangle(pt, tip, topWing, notch) || IsPointInTriangle(pt, tip, notch, botWing))
                {
                    texture.SetPixel(x, y, fill);
                }
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static bool IsPointInTriangle(Vector2 p, Vector2 p0, Vector2 p1, Vector2 p2)
    {
        float dX = p.x - p2.x;
        float dY = p.y - p2.y;
        float dX21 = p2.x - p1.x;
        float dY12 = p1.y - p2.y;
        float D = dY12 * (p0.x - p2.x) + dX21 * (p0.y - p2.y);
        float s = dY12 * dX + dX21 * dY;
        float t = (p2.y - p0.y) * dX + (p0.x - p2.x) * dY;
        if (D < 0) return s <= 0 && t <= 0 && s + t >= D;
        return s >= 0 && t >= 0 && s + t <= D;
    }
}

