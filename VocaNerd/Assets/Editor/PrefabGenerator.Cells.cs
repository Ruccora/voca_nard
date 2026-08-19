using Coffee.UIEffects;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace VocaNerd.EditorTools
{
    public static partial class PrefabGenerator
    {
        // セル配下の全 Graphic に UIEffect をベイク (奥行きの明暗 SetDarken 用)
        private static void AddUIEffectToGraphics(GameObject root)
        {
            foreach (var g in root.GetComponentsInChildren<Graphic>(true))
                if (g.GetComponent<UIEffect>() == null)
                    g.gameObject.AddComponent<UIEffect>();
        }

        // -------- MashRaceFlyObject --------
        private static MashRaceFlyObject CreateFlyObjectPrefab()
        {
            var tmp = new GameObject("MashRaceFlyObject",
                typeof(RectTransform), typeof(Image), typeof(MashRaceFlyObject));
            var rt = (RectTransform)tmp.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(200f, 24f);
            var img = tmp.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.25f);
            img.raycastTarget = false;

            var path = $"{PrefabDir}/MashRaceFlyObject.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(tmp, path);
            UnityEngine.Object.DestroyImmediate(tmp);
            return saved.GetComponent<MashRaceFlyObject>();
        }

        // -------- HopscotchCell (regular A/B/Toggle) --------
        private static HopscotchCell CreateHopscotchCellPrefab()
        {
            var tmp = new GameObject("HopscotchCell",
                typeof(RectTransform), typeof(Image), typeof(HopscotchCell));
            var rt = (RectTransform)tmp.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(100f, 40f);
            var bg = tmp.GetComponent<Image>();
            bg.color = Color.white;

            // Toggle mark (child, behind background)
            var toggleGO = new GameObject("Toggle", typeof(RectTransform), typeof(Image));
            toggleGO.transform.SetParent(tmp.transform, false);
            toggleGO.transform.SetAsFirstSibling();
            var trt = (RectTransform)toggleGO.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(-6f, -6f);
            trt.offsetMax = new Vector2(6f, 6f);
            var toggleImg = toggleGO.GetComponent<Image>();
            toggleImg.color = new Color(0.3f, 1f, 0.3f, 0.7f);
            toggleImg.raycastTarget = false;
            toggleGO.SetActive(false);

            // Label (child, above background)
            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(tmp.transform, false);
            var lrt = (RectTransform)labelGO.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var label = labelGO.AddComponent<TextMeshProUGUI>();
            label.text = "A";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 24;
            label.color = Color.white;

            // Secondary platform (B-type only, side-by-side)
            var secondaryGO = new GameObject("Secondary", typeof(RectTransform), typeof(Image));
            secondaryGO.transform.SetParent(tmp.transform, false);
            var srt = (RectTransform)secondaryGO.transform;
            srt.anchorMin = new Vector2(0.5f, 0.5f);
            srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(100f, 40f);
            srt.anchoredPosition = new Vector2(110f, 0f);
            var secondaryImg = secondaryGO.GetComponent<Image>();
            secondaryImg.color = Color.white;
            secondaryImg.raycastTarget = false;
            secondaryGO.SetActive(false);

            // Secondary toggle outline (behind secondary platform)
            var secondaryToggleGO = new GameObject("Toggle", typeof(RectTransform), typeof(Image));
            secondaryToggleGO.transform.SetParent(secondaryGO.transform, false);
            secondaryToggleGO.transform.SetAsFirstSibling();
            var strt = (RectTransform)secondaryToggleGO.transform;
            strt.anchorMin = Vector2.zero;
            strt.anchorMax = Vector2.one;
            strt.offsetMin = new Vector2(-6f, -6f);
            strt.offsetMax = new Vector2(6f, 6f);
            var secondaryToggleImg = secondaryToggleGO.GetComponent<Image>();
            secondaryToggleImg.color = new Color(0.3f, 1f, 0.3f, 0.7f);
            secondaryToggleImg.raycastTarget = false;
            secondaryToggleGO.SetActive(false);

            var cell = tmp.GetComponent<HopscotchCell>();
            AssignField(cell, "background", bg);
            AssignField(cell, "label", label);
            AssignField(cell, "toggleMark", toggleImg);
            AssignField(cell, "secondaryPlatform", secondaryGO);
            AssignField(cell, "secondaryImage", secondaryImg);
            AssignField(cell, "secondaryRect", srt);
            AssignField(cell, "secondaryToggleMark", secondaryToggleImg);

            AddUIEffectToGraphics(tmp);

            var path = $"{PrefabDir}/HopscotchCell.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(tmp, path);
            UnityEngine.Object.DestroyImmediate(tmp);
            return saved.GetComponent<HopscotchCell>();
        }

        // -------- HopscotchStartCell (start platform variant, plain gray) --------
        private static HopscotchCell CreateHopscotchStartCellPrefab()
        {
            var tmp = new GameObject("HopscotchStartCell",
                typeof(RectTransform), typeof(Image), typeof(HopscotchCell));
            var rt = (RectTransform)tmp.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(100f, 40f);
            var bg = tmp.GetComponent<Image>();
            bg.color = new Color(0.55f, 0.55f, 0.55f);

            var cell = tmp.GetComponent<HopscotchCell>();
            AssignField(cell, "background", bg);

            AddUIEffectToGraphics(tmp);

            var path = $"{PrefabDir}/HopscotchStartCell.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(tmp, path);
            UnityEngine.Object.DestroyImmediate(tmp);
            return saved.GetComponent<HopscotchCell>();
        }

        // -------- BlockDropBlock (with left/right sticks) --------
        private static BlockDropBlock CreateBlockDropBlockPrefab()
        {
            var tmp = new GameObject("BlockDropBlock",
                typeof(RectTransform), typeof(Image), typeof(BlockDropBlock));
            var rt = (RectTransform)tmp.transform;
            rt.sizeDelta = new Vector2(150f, 30f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            var body = tmp.GetComponent<Image>();
            body.color = new Color(0.7f, 0.7f, 0.7f);

            var leftStickGO = new GameObject("LeftStick", typeof(RectTransform), typeof(Image));
            leftStickGO.transform.SetParent(tmp.transform, false);
            var lsRt = (RectTransform)leftStickGO.transform;
            lsRt.anchorMin = new Vector2(0f, 0.5f);
            lsRt.anchorMax = new Vector2(0f, 0.5f);
            lsRt.pivot = new Vector2(1f, 0.5f);
            lsRt.sizeDelta = new Vector2(40f, 10f);
            lsRt.anchoredPosition = Vector2.zero;
            leftStickGO.GetComponent<Image>().color = new Color(0.95f, 0.75f, 0.2f);
            leftStickGO.SetActive(false);

            var rightStickGO = new GameObject("RightStick", typeof(RectTransform), typeof(Image));
            rightStickGO.transform.SetParent(tmp.transform, false);
            var rsRt = (RectTransform)rightStickGO.transform;
            rsRt.anchorMin = new Vector2(1f, 0.5f);
            rsRt.anchorMax = new Vector2(1f, 0.5f);
            rsRt.pivot = new Vector2(0f, 0.5f);
            rsRt.sizeDelta = new Vector2(40f, 10f);
            rsRt.anchoredPosition = Vector2.zero;
            rightStickGO.GetComponent<Image>().color = new Color(0.95f, 0.75f, 0.2f);
            rightStickGO.SetActive(false);

            var block = tmp.GetComponent<BlockDropBlock>();
            AssignField(block, "body", body);
            AssignField(block, "leftStick", leftStickGO);
            AssignField(block, "rightStick", rightStickGO);

            var path = $"{PrefabDir}/BlockDropBlock.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(tmp, path);
            UnityEngine.Object.DestroyImmediate(tmp);
            return saved.GetComponent<BlockDropBlock>();
        }
    }
}
