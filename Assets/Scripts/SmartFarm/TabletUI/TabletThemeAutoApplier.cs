using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm
{
    /// <summary>
    /// Applies tablet visuals automatically from a theme profile and optional Resources sprites.
    /// Drop sprites in Resources/SmartFarmTablet/Sprites using expected names to auto-skin.
    /// </summary>
    public class TabletThemeAutoApplier : MonoBehaviour
    {
        [SerializeField] private TabletThemeProfile themeProfile;
        [SerializeField] private bool autoLoadDefaultProfile = true;
        [SerializeField] private string defaultProfilePath = "SmartFarmTablet/DefaultTabletTheme";

        [Header("Optional Auto Sprite Loader")]
        [SerializeField] private bool useNamedSpritesFromResources = true;
        [SerializeField] private string spritesFolderPath = "SmartFarmTablet/Sprites";

        [Header("Apply")]
        [SerializeField] private bool applyOnStart = true;

        private void Start()
        {
            if (applyOnStart) ApplyTheme();
        }

        [ContextMenu("Apply Theme")]
        public void ApplyTheme()
        {
            if (themeProfile == null && autoLoadDefaultProfile)
                themeProfile = Resources.Load<TabletThemeProfile>(defaultProfilePath);

            ApplyPanelSprite("AppBackground", ResolveSprite(themeProfile != null ? themeProfile.appBackgroundSprite : null, "app_background"));
            ApplyPanelSprite("Header", ResolveSprite(themeProfile != null ? themeProfile.headerBackgroundSprite : null, "header_background"));
            ApplyPanelSprite("TabBar", ResolveSprite(themeProfile != null ? themeProfile.tabBarBackgroundSprite : null, "tabbar_background"));
            ApplyPanelSprite("PinBar", ResolveSprite(themeProfile != null ? themeProfile.pinBarBackgroundSprite : null, "pinbar_background"));
            ApplyPanelSprite("PollModalRoot", ResolveSprite(themeProfile != null ? themeProfile.modalBackgroundSprite : null, "modal_background"));
            ApplyPanelSprite("BadgeRoot", ResolveSprite(themeProfile != null ? themeProfile.badgeBackgroundSprite : null, "badge_background"));

            Sprite card = ResolveSprite(themeProfile != null ? themeProfile.cardBackgroundSprite : null, "card_background");
            ApplyPanelSprite("AlertItemTemplate", card);
            ApplyPanelSprite("HistoryItemTemplate", card);

            ApplyButtonSprites(ResolveSprite(themeProfile != null ? themeProfile.buttonBackgroundSprite : null, "button_background"));
            ApplyTabIcons();
            ApplyTextTheme();
            ApplyButtonFeedbackTheme();
        }

        private void ApplyPanelSprite(string objectName, Sprite sprite)
        {
            if (sprite == null) return;
            var t = transform.Find(objectName);
            if (t == null) t = FindDeep(transform, objectName);
            if (t == null) return;
            var img = t.GetComponent<Image>();
            if (img == null) return;
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = themeProfile != null ? themeProfile.imageTint : Color.white;
        }

        private void ApplyButtonSprites(Sprite sprite)
        {
            if (sprite == null) return;
            var buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                var btn = buttons[i];
                var img = btn != null ? btn.targetGraphic as Image : null;
                if (img == null) continue;
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
                img.color = themeProfile != null ? themeProfile.buttonNormalColor : img.color;
            }
        }

        private void ApplyTabIcons()
        {
            AddOrUpdateIcon("Button_Overview", ResolveSprite(themeProfile != null ? themeProfile.overviewIcon : null, "icon_overview"));
            AddOrUpdateIcon("Button_Irrigation", ResolveSprite(themeProfile != null ? themeProfile.irrigationIcon : null, "icon_irrigation"));
            AddOrUpdateIcon("Button_Alerts", ResolveSprite(themeProfile != null ? themeProfile.alertsIcon : null, "icon_alerts"));
            AddOrUpdateIcon("Button_Polls", ResolveSprite(themeProfile != null ? themeProfile.pollsIcon : null, "icon_polls"));
            AddOrUpdateIcon("Button_History", ResolveSprite(themeProfile != null ? themeProfile.historyIcon : null, "icon_history"));
        }

        private void AddOrUpdateIcon(string buttonName, Sprite iconSprite)
        {
            if (iconSprite == null) return;
            var buttonTransform = FindDeep(transform, buttonName);
            if (buttonTransform == null) return;

            // Create or find the dedicated icon Image child
            var icon = buttonTransform.Find("Icon");
            if (icon == null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(buttonTransform, false);
                icon = iconGo.transform;
            }

            // Centre the icon inside the button, sized to fill most of the button height
            var rt = (RectTransform)icon;
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = new Vector2(28f, 28f);
            rt.anchoredPosition = Vector2.zero;

            var iconImage = icon.GetComponent<Image>();
            iconImage.sprite        = iconSprite;
            iconImage.color         = Color.white;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget  = false; // button Image handles click, not the icon

            // Hide the text label — icon replaces it
            var textChild = buttonTransform.Find("Text");
            if (textChild != null) textChild.gameObject.SetActive(false);
        }

        /// <summary>
        /// Applies only the tab icons from the theme profile.
        /// Right-click the component in Inspector → "Apply Tab Icons Only"
        /// or use Tools → Farm → Apply Tab Icons.
        /// </summary>
        [ContextMenu("Apply Tab Icons Only")]
        public void ApplyTabIconsOnly()
        {
            if (themeProfile == null && autoLoadDefaultProfile)
                themeProfile = Resources.Load<TabletThemeProfile>(defaultProfilePath);
            ApplyTabIcons();
        }

        private void ApplyTextTheme()
        {
            var texts = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                var t = texts[i];
                if (t == null) continue;
                if (themeProfile != null)
                {
                    t.color = themeProfile.primaryTextColor;
                    if (themeProfile.textFont != null) t.font = themeProfile.textFont;
                }
            }
        }

        private void ApplyButtonFeedbackTheme()
        {
            if (themeProfile == null) return;
            var feedback = GetComponentsInChildren<TabletUIButtonFeedback>(true);
            for (int i = 0; i < feedback.Length; i++)
            {
                var comp = feedback[i];
                if (comp == null) continue;
                SetPrivateField(comp, "normalColor", themeProfile.buttonNormalColor);
                SetPrivateField(comp, "hoverColor", themeProfile.buttonHoverColor);
            }
        }

        private Sprite ResolveSprite(Sprite profileSprite, string resourceName)
        {
            if (profileSprite != null) return profileSprite;
            if (!useNamedSpritesFromResources || string.IsNullOrWhiteSpace(resourceName)) return null;
            return Resources.Load<Sprite>($"{spritesFolderPath}/{resourceName}");
        }

        private static Transform FindDeep(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name) return child;
                var nested = FindDeep(child, name);
                if (nested != null) return nested;
            }
            return null;
        }

        private static void SetPrivateField(object obj, string fieldName, object value)
        {
            if (obj == null) return;
            var field = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(obj, value);
        }
    }
}
