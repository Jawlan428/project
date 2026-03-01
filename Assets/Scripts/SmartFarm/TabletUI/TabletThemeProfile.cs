using TMPro;
using UnityEngine;

namespace SmartFarm
{
    [CreateAssetMenu(fileName = "TabletThemeProfile", menuName = "SmartFarm/Tablet Theme Profile")]
    public class TabletThemeProfile : ScriptableObject
    {
        [Header("Sprites")]
        public Sprite appBackgroundSprite;
        public Sprite headerBackgroundSprite;
        public Sprite tabBarBackgroundSprite;
        public Sprite pinBarBackgroundSprite;
        public Sprite modalBackgroundSprite;
        public Sprite cardBackgroundSprite;
        public Sprite buttonBackgroundSprite;
        public Sprite badgeBackgroundSprite;

        [Header("Optional Tab Icons")]
        public Sprite overviewIcon;
        public Sprite irrigationIcon;
        public Sprite alertsIcon;
        public Sprite pollsIcon;
        public Sprite historyIcon;

        [Header("Colors")]
        public Color imageTint = Color.white;
        public Color primaryTextColor = Color.white;
        public Color secondaryTextColor = new Color(0.8f, 0.87f, 0.95f);
        public Color buttonNormalColor = new Color(0.2f, 0.7f, 0.3f, 1f);
        public Color buttonHoverColor = new Color(0.3f, 0.85f, 0.45f, 1f);

        [Header("Typography (Optional)")]
        public TMP_FontAsset textFont;
    }
}
