#if UNITY_EDITOR
using CreatureClimb;
using UnityEditor;
using UnityEngine;

namespace CreatureClimb.Editor
{
    /// <summary>
    /// Draws Shop Mode as Free / Buy With Coins / Unlock With Character,
    /// then only the matching field (price OR character dropdown).
    /// Layout is single-line rows so nothing overlaps in ReorderableLists.
    /// </summary>
    public static class ShopUnlockDrawer
    {
        const float Pad = 2f;

        public static float Line => EditorGUIUtility.singleLineHeight + Pad;

        /// <summary>Header + mode + one detail row.</summary>
        public static float Height()
        {
            return Line * 3f + 6f;
        }

        public static float Draw(
            Rect rect,
            SerializedProperty shopMode,
            SerializedProperty shopPrice,
            SerializedProperty unlockChar)
        {
            var lineH = EditorGUIUtility.singleLineHeight;
            var y = rect.y + 2f;
            var prevLabel = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Min(120f, rect.width * 0.4f);

            EditorGUI.LabelField(new Rect(rect.x, y, rect.width, lineH), "Shop unlock", EditorStyles.boldLabel);
            y += lineH + Pad;

            if (shopMode == null)
            {
                EditorGUIUtility.labelWidth = prevLabel;
                return y - rect.y;
            }

            // Enum popup — short label so text never collides with the control.
            EditorGUI.BeginChangeCheck();
            var mode = (ShopUnlockMode)Mathf.Clamp(shopMode.enumValueIndex, 0, 2);
            mode = (ShopUnlockMode)EditorGUI.EnumPopup(
                new Rect(rect.x, y, rect.width, lineH),
                "Mode",
                mode);
            if (EditorGUI.EndChangeCheck())
            {
                shopMode.enumValueIndex = (int)mode;
                // Keep unused fields clean when switching mode.
                if (mode != ShopUnlockMode.BuyWithCoins && shopPrice != null)
                    shopPrice.intValue = 0;
                if (mode != ShopUnlockMode.UnlockWithCharacter && unlockChar != null)
                    unlockChar.stringValue = "";
            }

            y += lineH + Pad;

            switch (mode)
            {
                case ShopUnlockMode.BuyWithCoins:
                    if (shopPrice != null)
                    {
                        EditorGUI.PropertyField(
                            new Rect(rect.x, y, rect.width, lineH),
                            shopPrice,
                            new GUIContent("Price"));
                    }

                    break;

                case ShopUnlockMode.UnlockWithCharacter:
                    DrawCharacterPopup(new Rect(rect.x, y, rect.width, lineH), unlockChar);
                    break;

                default:
                    EditorGUI.LabelField(
                        new Rect(rect.x, y, rect.width, lineH),
                        new GUIContent("Detail"),
                        new GUIContent("Always owned"));
                    break;
            }

            y += lineH + Pad;
            EditorGUIUtility.labelWidth = prevLabel;
            return y - rect.y;
        }

        static void DrawCharacterPopup(Rect rect, SerializedProperty unlockChar)
        {
            if (unlockChar == null) return;

            ShopCatalog.GetCreatureChoices(out var labels, out var ids);
            if (labels == null || labels.Length == 0)
            {
                EditorGUI.LabelField(rect, "Character", "(none)");
                return;
            }

            var current = unlockChar.stringValue ?? "";
            var index = 0;
            for (var i = 0; i < ids.Length; i++)
            {
                if (string.Equals(ids[i], current, System.StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    break;
                }
            }

            var next = EditorGUI.Popup(rect, "Character", index, labels);
            if (next >= 0 && next < ids.Length)
                unlockChar.stringValue = ids[next] ?? "";
        }
    }
}
#endif
