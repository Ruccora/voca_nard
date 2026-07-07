using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace VocaNerd.EditorTools
{
    public static class PrefabGenerator
    {
        private const string PrefabDir = "Assets/Prefabs";

        [MenuItem("VocaNerd/Generate Sample Prefabs")]
        public static void Generate()
        {
            if (!AssetDatabase.IsValidFolder(PrefabDir))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            CreateMainCanvas();
            CreateTitlePanel();
            CreateSelectPanel();
            CreateMiniGamePanel();
            CreateQuickDrawGame();
            CreateMashRaceGame();
            CreateHopscotchRaceGame();
            CreateBlockDropGame();
            CreateBlackFadeOverlay();
            CreateAudioManager();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Sample prefabs generated under {PrefabDir}/");
        }

        private static void CreateTitlePanel()
        {
            var root = CreatePanelRoot("TitlePanel");
            var panel = root.AddComponent<TitlePanel>();

            CreateTMPText(root.transform, "TitleLabel", "VocaNerd", new Vector2(0, 200), 96);
            var button = CreateUIButton(root.transform, "StartButton", "Start", new Vector2(0, -100));

            AssignField(panel, "canvasGroup", root.GetComponent<CanvasGroup>());
            AssignField(panel, "startButton", button);
            SavePrefab(root);
        }

        private static void CreateSelectPanel()
        {
            var explainPrefab = CreateExplainPanel();

            var root = CreatePanelRoot("SelectPanel");
            var panel = root.AddComponent<SelectPanel>();

            CreateTMPText(root.transform, "Header", "Select MiniGame", new Vector2(0, 350), 56);

            var buttons = new Button[4];
            var thumbs = new Image[4];
            var positions = new[]
            {
                new Vector2(-220, 100),
                new Vector2(220, 100),
                new Vector2(-220, -120),
                new Vector2(220, -120),
            };
            for (var i = 0; i < 4; i++)
            {
                var b = CreateUIButton(root.transform, $"MiniGameButton_{i}", $"Game {i + 1}", positions[i]);
                var rt = (RectTransform)b.transform;
                rt.sizeDelta = new Vector2(320, 180);
                buttons[i] = b;
                thumbs[i] = b.GetComponent<Image>();
            }

            // ExplainPanel をここに生成する root
            var explainRootGO = new GameObject("ExplainRoot", typeof(RectTransform));
            explainRootGO.transform.SetParent(root.transform, false);
            var explainRt = (RectTransform)explainRootGO.transform;
            explainRt.anchorMin = Vector2.zero;
            explainRt.anchorMax = Vector2.one;
            explainRt.offsetMin = Vector2.zero;
            explainRt.offsetMax = Vector2.zero;

            AssignField(panel, "canvasGroup", root.GetComponent<CanvasGroup>());
            AssignArray(panel, "miniGameButtons", buttons);
            AssignArray(panel, "miniGameThumbnails", thumbs);
            AssignField(panel, "explainPanelPrefab", explainPrefab);
            AssignField(panel, "explainRoot", explainRt);
            SavePrefab(root);
        }

        private static ExplainPanel CreateExplainPanel()
        {
            var root = CreatePanelRoot("ExplainPanel");
            var panel = root.AddComponent<ExplainPanel>();

            // モーダル背景 (半透明黒、SelectPanel を暗くする)
            var scrimGO = new GameObject("Scrim", typeof(RectTransform), typeof(Image));
            scrimGO.transform.SetParent(root.transform, false);
            scrimGO.transform.SetAsFirstSibling();
            var scrimRt = (RectTransform)scrimGO.transform;
            scrimRt.anchorMin = Vector2.zero;
            scrimRt.anchorMax = Vector2.one;
            scrimRt.offsetMin = Vector2.zero;
            scrimRt.offsetMax = Vector2.zero;
            var scrimImg = scrimGO.GetComponent<Image>();
            scrimImg.color = new Color(0f, 0f, 0f, 0.7f);

            var title = CreateTMPText(root.transform, "TitleText", "Title", new Vector2(0, 350), 56);
            var desc = CreateTMPText(root.transform, "DescriptionText", "Description",
                new Vector2(0, 220), 28);
            var descRt = (RectTransform)desc.transform;
            descRt.sizeDelta = new Vector2(900, 140);

            var videoDisplay = CreateRawImage(root.transform, "VideoDisplay",
                new Vector2(0, -30), new Vector2(640, 360));

            var videoGO = new GameObject("VideoPlayer", typeof(RectTransform));
            videoGO.transform.SetParent(root.transform, false);
            var video = videoGO.AddComponent<VideoPlayer>();
            video.playOnAwake = false;
            video.renderMode = VideoRenderMode.RenderTexture;
            video.audioOutputMode = VideoAudioOutputMode.Direct;

            var playBtn = CreateUIButton(root.transform, "PlayButton", "Play", new Vector2(150, -320));
            var backBtn = CreateUIButton(root.transform, "BackButton", "Back", new Vector2(-150, -320));

            AssignField(panel, "canvasGroup", root.GetComponent<CanvasGroup>());
            AssignField(panel, "titleText", title);
            AssignField(panel, "descriptionText", desc);
            AssignField(panel, "videoPlayer", video);
            AssignField(panel, "videoDisplay", videoDisplay);
            AssignField(panel, "playButton", playBtn);
            AssignField(panel, "backButton", backBtn);
            var saved = SavePrefabReturning(root);
            return saved.GetComponent<ExplainPanel>();
        }

        private static GameObject SavePrefabReturning(GameObject root)
        {
            var path = $"{PrefabDir}/{root.name}.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static void CreateMiniGamePanel()
        {
            var root = CreatePanelRoot("MiniGamePanel");
            var panel = root.AddComponent<MiniGamePanel>();

            var title = CreateTMPText(root.transform, "TitleText", "MiniGame", new Vector2(0, 400), 48);

            var containerGO = new GameObject("GameContainer", typeof(RectTransform));
            containerGO.transform.SetParent(root.transform, false);
            var containerRt = (RectTransform)containerGO.transform;
            containerRt.anchorMin = new Vector2(0.5f, 0.5f);
            containerRt.anchorMax = new Vector2(0.5f, 0.5f);
            containerRt.sizeDelta = new Vector2(1200, 700);
            containerRt.anchoredPosition = Vector2.zero;

            var backBtn = CreateUIButton(root.transform, "BackButton", "Back", new Vector2(0, -420));

            AssignField(panel, "canvasGroup", root.GetComponent<CanvasGroup>());
            AssignField(panel, "titleText", title);
            AssignField(panel, "backButton", backBtn);
            AssignField(panel, "gameContainer", containerRt);
            SavePrefab(root);
        }

        private static void CreateQuickDrawGame()
        {
            var root = new GameObject("QuickDrawGame", typeof(RectTransform), typeof(CanvasGroup));
            var rt = (RectTransform)root.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var game = root.AddComponent<QuickDrawGame>();

            var introGO = new GameObject("Intro", typeof(RectTransform), typeof(CanvasGroup));
            introGO.transform.SetParent(root.transform, false);
            var introRt = (RectTransform)introGO.transform;
            introRt.anchorMin = new Vector2(0.5f, 0.5f);
            introRt.anchorMax = new Vector2(0.5f, 0.5f);
            introRt.sizeDelta = new Vector2(800, 200);
            introRt.anchoredPosition = new Vector2(0, 100);
            var introGroup = introGO.GetComponent<CanvasGroup>();
            var introText = CreateTMPText(introGO.transform, "IntroText", "READY?", Vector2.zero, 96);

            // Player key guide labels (top corners)
            var p1Label = CreateTMPText(root.transform, "P1Label", "P1 (A)", new Vector2(-600, 450), 56);
            p1Label.color = new Color(0.4f, 0.7f, 1f);
            p1Label.alignment = TextAlignmentOptions.Center;
            var p2Label = CreateTMPText(root.transform, "P2Label", "P2 (L)", new Vector2(600, 450), 56);
            p2Label.color = new Color(1f, 0.5f, 0.4f);
            p2Label.alignment = TextAlignmentOptions.Center;

            var statusText = CreateTMPText(root.transform, "StatusText", "", new Vector2(0, 220), 72);

            var imageGO = new GameObject("TargetImage", typeof(RectTransform), typeof(Image));
            imageGO.transform.SetParent(root.transform, false);
            var imgRt = (RectTransform)imageGO.transform;
            imgRt.sizeDelta = new Vector2(320, 320);
            imgRt.anchoredPosition = Vector2.zero;
            var targetImage = imageGO.GetComponent<Image>();
            targetImage.color = Color.red;
            targetImage.enabled = false;

            var resultGO = new GameObject("Result", typeof(RectTransform), typeof(CanvasGroup));
            resultGO.transform.SetParent(root.transform, false);
            var resultRt = (RectTransform)resultGO.transform;
            resultRt.anchorMin = new Vector2(0.5f, 0.5f);
            resultRt.anchorMax = new Vector2(0.5f, 0.5f);
            resultRt.sizeDelta = new Vector2(1000, 400);
            resultRt.anchoredPosition = new Vector2(0, -100);
            var resultGroup = resultGO.GetComponent<CanvasGroup>();
            resultGroup.alpha = 0f;
            var resultText = CreateTMPText(resultGO.transform, "ResultText", "", new Vector2(0, 60), 72);
            var againBtn = CreateUIButton(resultGO.transform, "PlayAgainButton", "Play Again", new Vector2(0, -100));

            AssignField(game, "canvasGroup", root.GetComponent<CanvasGroup>());
            AssignField(game, "introGroup", introGroup);
            AssignField(game, "introText", introText);
            AssignField(game, "statusText", statusText);
            AssignField(game, "targetImage", targetImage);
            AssignField(game, "resultGroup", resultGroup);
            AssignField(game, "resultText", resultText);
            AssignField(game, "playAgainButton", againBtn);

            SavePrefab(root);
        }

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
            Object.DestroyImmediate(tmp);
            return saved.GetComponent<MashRaceFlyObject>();
        }

        private static void CreateMashRaceGame()
        {
            var flyObjectPrefab = CreateFlyObjectPrefab();

            var root = new GameObject("MashRaceGame", typeof(RectTransform), typeof(CanvasGroup));
            var rt = (RectTransform)root.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var game = root.AddComponent<MashRaceGame>();

            // Player 1 area (left half)
            var p1Area = CreateHalfAreaVertical(root.transform, "Player1Area", isLeft: true);
            CreateSolidBase(p1Area, "P1Base", new Color(0.15f, 0.25f, 0.45f));
            var p1Bg = CreateBackgroundLayer(p1Area, "P1Background");
            var p1ObjLayer = CreateObjectLayer(p1Area, "P1ObjectLayer");
            CreateTMPText(p1Area, "P1Label", "P1 (A / D)", new Vector2(0, 520), 36);
            var p1Fly = CreateFlyIcon(p1Area, "P1FlyIcon", new Color(0.4f, 0.7f, 1f), new Vector2(0, -500));
            var (_, p1MissGroup) = CreateMissBadge(p1Area, "P1Miss", new Vector2(0, 380));

            // Player 2 area (right half)
            var p2Area = CreateHalfAreaVertical(root.transform, "Player2Area", isLeft: false);
            CreateSolidBase(p2Area, "P2Base", new Color(0.4f, 0.2f, 0.2f));
            var p2Bg = CreateBackgroundLayer(p2Area, "P2Background");
            var p2ObjLayer = CreateObjectLayer(p2Area, "P2ObjectLayer");
            CreateTMPText(p2Area, "P2Label", "P2 (← / →)", new Vector2(0, 520), 36);
            var p2Fly = CreateFlyIcon(p2Area, "P2FlyIcon", new Color(1f, 0.5f, 0.4f), new Vector2(0, -500));
            var (_, p2MissGroup) = CreateMissBadge(p2Area, "P2Miss", new Vector2(0, 380));

            // Divider line at center (vertical)
            var dividerGO = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            dividerGO.transform.SetParent(root.transform, false);
            var divRt = (RectTransform)dividerGO.transform;
            divRt.anchorMin = new Vector2(0.5f, 0);
            divRt.anchorMax = new Vector2(0.5f, 1);
            divRt.sizeDelta = new Vector2(4, 0);
            divRt.anchoredPosition = Vector2.zero;
            dividerGO.GetComponent<Image>().color = new Color(0.8f, 0.8f, 0.8f, 1f);

            // Center overlays
            var introText = CreateTMPText(root.transform, "IntroText", "", Vector2.zero, 72);
            var countdownText = CreateTMPText(root.transform, "CountdownText", "", Vector2.zero, 128);
            var timerText = CreateTMPText(root.transform, "TimerText", "10.0", new Vector2(0, -560), 48);

            // Result group
            var resultGO = new GameObject("Result", typeof(RectTransform), typeof(CanvasGroup));
            resultGO.transform.SetParent(root.transform, false);
            var resultRt = (RectTransform)resultGO.transform;
            resultRt.anchorMin = new Vector2(0.5f, 0.5f);
            resultRt.anchorMax = new Vector2(0.5f, 0.5f);
            resultRt.sizeDelta = new Vector2(900, 400);
            resultRt.anchoredPosition = Vector2.zero;
            var resultGroup = resultGO.GetComponent<CanvasGroup>();
            resultGroup.alpha = 0f;
            var winnerText = CreateTMPText(resultGO.transform, "WinnerText", "", new Vector2(0, 60), 72);
            var againBtn = CreateUIButton(resultGO.transform, "PlayAgainButton", "Play Again", new Vector2(0, -100));

            AssignField(game, "canvasGroup", root.GetComponent<CanvasGroup>());
            AssignField(game, "introText", introText);
            AssignField(game, "countdownText", countdownText);
            AssignField(game, "timerText", timerText);
            AssignField(game, "resultGroup", resultGroup);
            AssignField(game, "winnerText", winnerText);
            AssignField(game, "playAgainButton", againBtn);
            AssignField(game, "player1FlyIcon", p1Fly);
            AssignField(game, "player1Background", p1Bg);
            AssignField(game, "player1ObjectLayer", p1ObjLayer);
            AssignField(game, "player1MissGroup", p1MissGroup);
            AssignField(game, "player2FlyIcon", p2Fly);
            AssignField(game, "player2Background", p2Bg);
            AssignField(game, "player2ObjectLayer", p2ObjLayer);
            AssignField(game, "player2MissGroup", p2MissGroup);
            AssignField(game, "flyObjectPrefab", flyObjectPrefab);

            SavePrefab(root);
        }

        private static void CreateSolidBase(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.transform.SetAsFirstSibling();
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        private static RectTransform CreateBackgroundLayer(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(0, 2000);
            rt.anchoredPosition = Vector2.zero;

            // 背景に固定の装飾（差し替え可能）
            for (var i = 0; i < 30; i++)
            {
                var stripeGO = new GameObject($"BgStripe_{i}", typeof(RectTransform), typeof(Image));
                stripeGO.transform.SetParent(go.transform, false);
                var srt = (RectTransform)stripeGO.transform;
                srt.anchorMin = new Vector2(0, 0);
                srt.anchorMax = new Vector2(1, 0);
                srt.pivot = new Vector2(0.5f, 0);
                srt.sizeDelta = new Vector2(-40, 18);
                srt.anchoredPosition = new Vector2(0, 60 + i * 70);
                var img = stripeGO.GetComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0.12f);
                img.raycastTarget = false;
            }

            return rt;
        }

        private static RectTransform CreateObjectLayer(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static Transform CreateHalfAreaVertical(Transform parent, string name, bool isLeft)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RectMask2D));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            if (isLeft)
            {
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(0.5f, 1);
            }
            else
            {
                rt.anchorMin = new Vector2(0.5f, 0);
                rt.anchorMax = new Vector2(1, 1);
            }
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go.transform;
        }

        private static RectTransform CreateFlyIcon(Transform parent, string name, Color color, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(100, 100);
            rt.anchoredPosition = anchoredPos;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        private static Transform CreateHalfArea(Transform parent, string name, bool isTop)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            if (isTop)
            {
                rt.anchorMin = new Vector2(0, 0.5f);
                rt.anchorMax = new Vector2(1, 1);
            }
            else
            {
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(1, 0.5f);
            }
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go.transform;
        }

        private static (GameObject go, CanvasGroup group) CreateMissBadge(Transform parent, string name, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(400, 100);
            rt.anchoredPosition = anchoredPos;
            var group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            var text = CreateTMPText(go.transform, "Text", "MISS", Vector2.zero, 64);
            text.color = Color.red;
            return (go, group);
        }

        private static HopscotchCell CreateHopscotchCellPrefab()
        {
            var tmp = new GameObject("HopscotchCell",
                typeof(RectTransform), typeof(Image), typeof(HopscotchCell));
            var rt = (RectTransform)tmp.transform;
            rt.sizeDelta = new Vector2(40f, 40f);
            var bg = tmp.GetComponent<Image>();
            bg.color = Color.white;

            // Toggle mark (child, behind background via SetAsFirstSibling later? no—needs to be visible.
            // Place as first sibling: appears behind background; place as last: appears above background.
            // We want it to show as a colored outline; use larger size behind.)
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

            var cell = tmp.GetComponent<HopscotchCell>();
            AssignField(cell, "background", bg);
            AssignField(cell, "label", label);
            AssignField(cell, "toggleMark", toggleImg);

            var path = $"{PrefabDir}/HopscotchCell.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(tmp, path);
            Object.DestroyImmediate(tmp);
            return saved.GetComponent<HopscotchCell>();
        }

        private static void CreateHopscotchRaceGame()
        {
            var cellPrefab = CreateHopscotchCellPrefab();

            var root = new GameObject("HopscotchRaceGame", typeof(RectTransform), typeof(CanvasGroup));
            var rt = (RectTransform)root.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var game = root.AddComponent<HopscotchRaceGame>();

            // Player 1 (top)
            var p1Area = CreateHalfArea(root.transform, "Player1Area", isTop: true);
            CreateTMPText(p1Area, "P1Label", "P1 (A / D)", new Vector2(-620, 220), 28);
            var p1Track = CreateTrackContainer(p1Area, "P1Track");
            var p1Marker = CreatePlayerMarker(p1Track, "P1Marker", new Color(0.3f, 0.9f, 0.4f));

            // Player 2 (bottom)
            var p2Area = CreateHalfArea(root.transform, "Player2Area", isTop: false);
            CreateTMPText(p2Area, "P2Label", "P2 (← / →)", new Vector2(-620, 220), 28);
            var p2Track = CreateTrackContainer(p2Area, "P2Track");
            var p2Marker = CreatePlayerMarker(p2Track, "P2Marker", new Color(0.9f, 0.5f, 0.3f));

            // Divider
            var dividerGO = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            dividerGO.transform.SetParent(root.transform, false);
            var divRt = (RectTransform)dividerGO.transform;
            divRt.anchorMin = new Vector2(0, 0.5f);
            divRt.anchorMax = new Vector2(1, 0.5f);
            divRt.sizeDelta = new Vector2(0, 4);
            divRt.anchoredPosition = Vector2.zero;
            dividerGO.GetComponent<Image>().color = new Color(0.8f, 0.8f, 0.8f, 1f);

            // Center overlays
            var introText = CreateTMPText(root.transform, "IntroText", "", Vector2.zero, 72);
            var countdownText = CreateTMPText(root.transform, "CountdownText", "", Vector2.zero, 128);
            var goalText = CreateTMPText(root.transform, "GoalText", "", new Vector2(0, 60), 96);

            // Result group
            var resultGO = new GameObject("Result", typeof(RectTransform), typeof(CanvasGroup));
            resultGO.transform.SetParent(root.transform, false);
            var resultRt = (RectTransform)resultGO.transform;
            resultRt.anchorMin = new Vector2(0.5f, 0.5f);
            resultRt.anchorMax = new Vector2(0.5f, 0.5f);
            resultRt.sizeDelta = new Vector2(900, 400);
            resultRt.anchoredPosition = Vector2.zero;
            var resultGroup = resultGO.GetComponent<CanvasGroup>();
            resultGroup.alpha = 0f;
            var winnerText = CreateTMPText(resultGO.transform, "WinnerText", "", new Vector2(0, 60), 72);
            var againBtn = CreateUIButton(resultGO.transform, "PlayAgainButton", "Play Again", new Vector2(0, -100));

            AssignField(game, "canvasGroup", root.GetComponent<CanvasGroup>());
            AssignField(game, "introText", introText);
            AssignField(game, "countdownText", countdownText);
            AssignField(game, "goalText", goalText);
            AssignField(game, "resultGroup", resultGroup);
            AssignField(game, "winnerText", winnerText);
            AssignField(game, "playAgainButton", againBtn);
            AssignField(game, "player1Track", p1Track);
            AssignField(game, "player1Marker", p1Marker);
            AssignField(game, "player2Track", p2Track);
            AssignField(game, "player2Marker", p2Marker);
            AssignField(game, "cellPrefab", cellPrefab);

            SavePrefab(root);
        }

        private static RectTransform CreateTrackContainer(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1400, 500);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        private static RectTransform CreatePlayerMarker(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(56f, 56f);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

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
            Object.DestroyImmediate(tmp);
            return saved.GetComponent<BlockDropBlock>();
        }

        private static void CreateBlockDropGame()
        {
            var blockPrefab = CreateBlockDropBlockPrefab();

            var root = new GameObject("BlockDropGame", typeof(RectTransform), typeof(CanvasGroup));
            var rt = (RectTransform)root.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var game = root.AddComponent<BlockDropGame>();

            // Player 1 (left half)
            var p1Area = CreateHalfAreaVertical(root.transform, "Player1Area", isLeft: true);
            CreateTMPText(p1Area, "P1Label", "P1 (A/D + W/S)", new Vector2(0, 520), 32);
            var p1Stack = CreateStackContainer(p1Area, "P1Stack");
            var p1Char = CreateCharacter(p1Area, "P1Character", new Color(0.4f, 0.7f, 1f), new Vector2(160f, -430f));

            // Player 2 (right half)
            var p2Area = CreateHalfAreaVertical(root.transform, "Player2Area", isLeft: false);
            CreateTMPText(p2Area, "P2Label", "P2 (←/→ + ↑/↓)", new Vector2(0, 520), 32);
            var p2Stack = CreateStackContainer(p2Area, "P2Stack");
            var p2Char = CreateCharacter(p2Area, "P2Character", new Color(1f, 0.5f, 0.4f), new Vector2(160f, -430f));

            // Center vertical divider
            var dividerGO = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            dividerGO.transform.SetParent(root.transform, false);
            var divRt = (RectTransform)dividerGO.transform;
            divRt.anchorMin = new Vector2(0.5f, 0);
            divRt.anchorMax = new Vector2(0.5f, 1);
            divRt.sizeDelta = new Vector2(4, 0);
            divRt.anchoredPosition = Vector2.zero;
            dividerGO.GetComponent<Image>().color = new Color(0.8f, 0.8f, 0.8f, 1f);

            // Center overlays
            var introText = CreateTMPText(root.transform, "IntroText", "", Vector2.zero, 72);
            var countdownText = CreateTMPText(root.transform, "CountdownText", "", Vector2.zero, 128);

            // Result group
            var resultGO = new GameObject("Result", typeof(RectTransform), typeof(CanvasGroup));
            resultGO.transform.SetParent(root.transform, false);
            var resultRt = (RectTransform)resultGO.transform;
            resultRt.anchorMin = new Vector2(0.5f, 0.5f);
            resultRt.anchorMax = new Vector2(0.5f, 0.5f);
            resultRt.sizeDelta = new Vector2(900, 400);
            resultRt.anchoredPosition = Vector2.zero;
            var resultGroup = resultGO.GetComponent<CanvasGroup>();
            resultGroup.alpha = 0f;
            var winnerText = CreateTMPText(resultGO.transform, "WinnerText", "", new Vector2(0, 60), 72);
            var againBtn = CreateUIButton(resultGO.transform, "PlayAgainButton", "Play Again", new Vector2(0, -100));

            AssignField(game, "canvasGroup", root.GetComponent<CanvasGroup>());
            AssignField(game, "introText", introText);
            AssignField(game, "countdownText", countdownText);
            AssignField(game, "resultGroup", resultGroup);
            AssignField(game, "winnerText", winnerText);
            AssignField(game, "playAgainButton", againBtn);
            AssignField(game, "player1Stack", p1Stack);
            AssignField(game, "player1Character", p1Char);
            AssignField(game, "player2Stack", p2Stack);
            AssignField(game, "player2Character", p2Char);
            AssignField(game, "blockPrefab", blockPrefab);

            SavePrefab(root);
        }

        private static RectTransform CreateStackContainer(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400, 1100);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        private static RectTransform CreateCharacter(Transform parent, string name, Color color, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(50f, 70f);
            rt.anchoredPosition = anchoredPos;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        private static void CreateMainCanvas()
        {
            var root = new GameObject("MainCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 1200);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Letterbox background (black, full screen)
            var bgGO = new GameObject("LetterboxBackground", typeof(RectTransform), typeof(Image));
            bgGO.transform.SetParent(root.transform, false);
            var bgRt = (RectTransform)bgGO.transform;
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImg = bgGO.GetComponent<Image>();
            bgImg.color = Color.black;
            bgImg.raycastTarget = false;

            // Aspect frame (fits 4:3 into parent, letterboxed)
            var frameGO = new GameObject("AspectFrame",
                typeof(RectTransform),
                typeof(AspectRatioFitter));
            frameGO.transform.SetParent(root.transform, false);
            var frameRt = (RectTransform)frameGO.transform;
            frameRt.anchorMin = new Vector2(0.5f, 0.5f);
            frameRt.anchorMax = new Vector2(0.5f, 0.5f);
            frameRt.pivot = new Vector2(0.5f, 0.5f);
            frameRt.anchoredPosition = Vector2.zero;
            var fitter = frameGO.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 4f / 3f;

            // ScreenController attached to AspectFrame with root = self
            var controller = frameGO.AddComponent<ScreenController>();
            AssignField(controller, "root", frameRt);

            SavePrefab(root);
        }

        private static void CreateBlackFadeOverlay()
        {
            var root = new GameObject("BlackFadeOverlay",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            var group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var overlay = root.AddComponent<BlackFadeOverlay>();
            AssignField(overlay, "canvasGroup", group);

            var imgGO = new GameObject("Black", typeof(RectTransform), typeof(Image));
            imgGO.transform.SetParent(root.transform, false);
            var rt = (RectTransform)imgGO.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = imgGO.GetComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = true;

            SavePrefab(root);
        }

        private static void CreateAudioManager()
        {
            var root = new GameObject("AudioManager");
            var manager = root.AddComponent<AudioManager>();

            var bgmA = CreateAudioSourceChild(root.transform, "BgmSourceA", loop: true, volume: 0f);
            var bgmB = CreateAudioSourceChild(root.transform, "BgmSourceB", loop: true, volume: 0f);
            var se = CreateAudioSourceChild(root.transform, "SeSource", loop: false, volume: 1f);

            AssignField(manager, "bgmSourceA", bgmA);
            AssignField(manager, "bgmSourceB", bgmB);
            AssignField(manager, "seSource", se);

            SavePrefab(root);
        }

        private static AudioSource CreateAudioSourceChild(Transform parent, string name, bool loop, float volume)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.volume = volume;
            src.spatialBlend = 0f; // 2D
            return src;
        }

        private static GameObject CreatePanelRoot(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go;
        }

        private static Button CreateUIButton(Transform parent, string name, string label, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(220, 80);
            rt.anchoredPosition = anchoredPos;

            var textGO = new GameObject("Label", typeof(RectTransform));
            textGO.transform.SetParent(go.transform, false);
            var trt = (RectTransform)textGO.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 28;
            tmp.color = Color.black;

            return go.GetComponent<Button>();
        }

        private static TMP_Text CreateTMPText(Transform parent, string name, string text, Vector2 anchoredPos, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(900, 120);
            rt.anchoredPosition = anchoredPos;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = fontSize;
            return tmp;
        }

        private static RawImage CreateRawImage(Transform parent, string name, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            return go.GetComponent<RawImage>();
        }

        private static void AssignField(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"Field '{fieldName}' not found on {target.GetType().Name}");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignArray(Object target, string fieldName, Object[] values)
        {
            var so = new SerializedObject(target);
            var arr = so.FindProperty(fieldName);
            arr.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SavePrefab(GameObject root)
        {
            var path = $"{PrefabDir}/{root.name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }
    }
}
