using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace VocaNerd.EditorTools
{
    public static partial class PrefabGenerator
    {
        private const string PrefabDir = "Assets/Prefabs";

        [MenuItem("VocaNerd/Generate/All Sample Prefabs")]
        public static void Generate()
        {
            RunGenerate("All sample prefabs", () =>
            {
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
            });
        }

        [MenuItem("VocaNerd/Generate/MainCanvas")]
        public static void GenerateMainCanvas() => RunGenerate("MainCanvas", CreateMainCanvas);

        [MenuItem("VocaNerd/Generate/TitlePanel")]
        public static void GenerateTitlePanel() => RunGenerate("TitlePanel", CreateTitlePanel);

        [MenuItem("VocaNerd/Generate/SelectPanel (+ExplainPanel)")]
        public static void GenerateSelectPanel() => RunGenerate("SelectPanel", CreateSelectPanel);

        [MenuItem("VocaNerd/Generate/ExplainPanel")]
        public static void GenerateExplainPanel() => RunGenerate("ExplainPanel", () => CreateExplainPanel());

        [MenuItem("VocaNerd/Generate/MiniGamePanel")]
        public static void GenerateMiniGamePanel() => RunGenerate("MiniGamePanel", CreateMiniGamePanel);

        [MenuItem("VocaNerd/Generate/QuickDrawGame")]
        public static void GenerateQuickDrawGame() => RunGenerate("QuickDrawGame", CreateQuickDrawGame);

        [MenuItem("VocaNerd/Generate/MashRaceGame")]
        public static void GenerateMashRaceGame() => RunGenerate("MashRaceGame", CreateMashRaceGame);

        [MenuItem("VocaNerd/Generate/HopscotchRaceGame")]
        public static void GenerateHopscotchRaceGame() => RunGenerate("HopscotchRaceGame", CreateHopscotchRaceGame);

        [MenuItem("VocaNerd/Generate/BlockDropGame")]
        public static void GenerateBlockDropGame() => RunGenerate("BlockDropGame", CreateBlockDropGame);

        [MenuItem("VocaNerd/Generate/BlackFadeOverlay")]
        public static void GenerateBlackFadeOverlay() => RunGenerate("BlackFadeOverlay", CreateBlackFadeOverlay);

        [MenuItem("VocaNerd/Generate/AudioManager")]
        public static void GenerateAudioManager() => RunGenerate("AudioManager", CreateAudioManager);

        private static void RunGenerate(string label, Action body)
        {
            if (!AssetDatabase.IsValidFolder(PrefabDir))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            body();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PrefabGenerator] {label} generated under {PrefabDir}/");
        }

        private static void CreateTitlePanel()
        {
            var root = CreatePanelRoot("TitlePanel");
            var panel = root.AddComponent<TitlePanel>();

            var titleLeft = CreateTMPText(root.transform, "TitleLabel_Voca", "Voca", new Vector2(-140, 200), 96);
            var titleRight = CreateTMPText(root.transform, "TitleLabel_Nerd", "Nerd", new Vector2(140, 200), 96);
            var startBtn = CreateUIButton(root.transform, "StartButton", "Start", new Vector2(0, -100));
            var exitBtn = CreateUIButton(root.transform, "ExitButton", "Exit", new Vector2(0, -220));

            // 矢印インジケーター (▶) — 選択中のボタンの左に表示、CanvasGroup 付きで Blink 可能
            var arrowGO = new GameObject("SelectionArrow",
                typeof(RectTransform), typeof(CanvasGroup), typeof(CanvasGroupBlinker));
            arrowGO.transform.SetParent(root.transform, false);
            var arrowRt = (RectTransform)arrowGO.transform;
            arrowRt.sizeDelta = new Vector2(48, 48);
            arrowRt.anchoredPosition = Vector2.zero;
            var arrowTmp = arrowGO.AddComponent<TextMeshProUGUI>();
            arrowTmp.text = "▶";
            arrowTmp.alignment = TextAlignmentOptions.Center;
            arrowTmp.fontSize = 48;
            arrowTmp.color = Color.white;
            var arrowBlinker = arrowGO.GetComponent<CanvasGroupBlinker>();
            AssignField(arrowBlinker, "canvasGroup", arrowGO.GetComponent<CanvasGroup>());

            var indicator = root.AddComponent<SelectionIndicator>();
            AssignField(indicator, "select", arrowRt);
            AssignField(indicator, "selectGroup", arrowGO.GetComponent<CanvasGroup>());
            AssignArray(indicator, "targets", new Selectable[] { startBtn, exitBtn });
            AssignField(indicator, "blinker", arrowBlinker);

            AssignField(panel, "canvasGroup", root.GetComponent<CanvasGroup>());
            AssignField(panel, "defaultSelected", startBtn);
            AssignField(panel, "startButton", startBtn);
            AssignField(panel, "exitButton", exitBtn);
            AssignField(panel, "selectionIndicator", indicator);
            AssignArray(panel, "titleLabelRects", new RectTransform[] { titleLeft.rectTransform, titleRight.rectTransform });
            AssignField(panel, "startButtonRect", (RectTransform)startBtn.transform);
            AssignField(panel, "exitButtonRect", (RectTransform)exitBtn.transform);
            SavePrefab(root);
        }

        private static void CreateSelectPanel()
        {
            var explainPrefab = CreateExplainPanel();

            var root = CreatePanelRoot("SelectPanel");
            var panel = root.AddComponent<SelectPanel>();

            var header = CreateTMPText(root.transform, "Header", "Select MiniGame", new Vector2(0, 350), 56);

            var buttons = new Button[4];
            var thumbs = new Image[4];
            var buttonRects = new RectTransform[4];
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
                buttonRects[i] = rt;
            }

            // SelectionIndicator の表示要素 — 選択中のミニゲームボタンの左に▶を表示、Blink 可能
            var arrowGO = new GameObject("SelectionArrow",
                typeof(RectTransform), typeof(CanvasGroup), typeof(CanvasGroupBlinker));
            arrowGO.transform.SetParent(root.transform, false);
            var arrowRt = (RectTransform)arrowGO.transform;
            arrowRt.sizeDelta = new Vector2(48, 48);
            arrowRt.anchoredPosition = Vector2.zero;
            var arrowTmp = arrowGO.AddComponent<TextMeshProUGUI>();
            arrowTmp.text = "▶";
            arrowTmp.alignment = TextAlignmentOptions.Center;
            arrowTmp.fontSize = 48;
            arrowTmp.color = Color.white;
            var arrowBlinker = arrowGO.GetComponent<CanvasGroupBlinker>();
            AssignField(arrowBlinker, "canvasGroup", arrowGO.GetComponent<CanvasGroup>());

            var indicator = root.AddComponent<SelectionIndicator>();
            AssignField(indicator, "select", arrowRt);
            AssignField(indicator, "selectGroup", arrowGO.GetComponent<CanvasGroup>());
            AssignArray(indicator, "targets", buttons);
            AssignField(indicator, "blinker", arrowBlinker);

            // ExplainPanel をここに生成する root
            var explainRootGO = new GameObject("ExplainRoot", typeof(RectTransform));
            explainRootGO.transform.SetParent(root.transform, false);
            var explainRt = (RectTransform)explainRootGO.transform;
            explainRt.anchorMin = Vector2.zero;
            explainRt.anchorMax = Vector2.one;
            explainRt.offsetMin = Vector2.zero;
            explainRt.offsetMax = Vector2.zero;

            AssignField(panel, "canvasGroup", root.GetComponent<CanvasGroup>());
            AssignField(panel, "defaultSelected", buttons[0]);
            AssignArray(panel, "miniGameButtons", buttons);
            AssignArray(panel, "miniGameThumbnails", thumbs);
            AssignField(panel, "explainPanelPrefab", explainPrefab);
            AssignField(panel, "explainRoot", explainRt);
            AssignField(panel, "selectionIndicator", indicator);
            AssignField(panel, "headerRect", header.rectTransform);
            AssignArray(panel, "miniGameButtonRects", buttonRects);
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

            var desc = CreateTMPText(root.transform, "DescriptionText", "Description",
                new Vector2(360, 60), 32);
            var descRt = (RectTransform)desc.transform;
            descRt.sizeDelta = new Vector2(720, 400);
            desc.alignment = TextAlignmentOptions.Left;
            desc.enableWordWrapping = true;

            var videoDisplay = CreateRawImage(root.transform, "VideoDisplay",
                new Vector2(-360, 60), new Vector2(640, 360));

            var videoGO = new GameObject("VideoPlayer", typeof(RectTransform));
            videoGO.transform.SetParent(root.transform, false);
            var video = videoGO.AddComponent<VideoPlayer>();
            video.playOnAwake = false;
            video.renderMode = VideoRenderMode.RenderTexture;
            video.audioOutputMode = VideoAudioOutputMode.Direct;

            var playBtn = CreateUIButton(root.transform, "PlayButton", "Play", new Vector2(150, -350));
            var backBtn = CreateUIButton(root.transform, "BackButton", "Back", new Vector2(-150, -350));

            // SelectionIndicator の表示要素 — 選択中のボタンの左に▶を表示、Blink 可能
            var arrowGO = new GameObject("SelectionArrow",
                typeof(RectTransform), typeof(CanvasGroup), typeof(CanvasGroupBlinker));
            arrowGO.transform.SetParent(root.transform, false);
            var arrowRt = (RectTransform)arrowGO.transform;
            arrowRt.sizeDelta = new Vector2(48, 48);
            arrowRt.anchoredPosition = Vector2.zero;
            var arrowTmp = arrowGO.AddComponent<TextMeshProUGUI>();
            arrowTmp.text = "▶";
            arrowTmp.alignment = TextAlignmentOptions.Center;
            arrowTmp.fontSize = 48;
            arrowTmp.color = Color.white;
            var arrowBlinker = arrowGO.GetComponent<CanvasGroupBlinker>();
            AssignField(arrowBlinker, "canvasGroup", arrowGO.GetComponent<CanvasGroup>());

            var indicator = root.AddComponent<SelectionIndicator>();
            AssignField(indicator, "select", arrowRt);
            AssignField(indicator, "selectGroup", arrowGO.GetComponent<CanvasGroup>());
            AssignArray(indicator, "targets", new Selectable[] { playBtn, backBtn });
            AssignField(indicator, "blinker", arrowBlinker);

            AssignField(panel, "canvasGroup", root.GetComponent<CanvasGroup>());
            AssignField(panel, "defaultSelected", playBtn);
            AssignField(panel, "descriptionText", desc);
            AssignField(panel, "videoPlayer", video);
            AssignField(panel, "videoDisplay", videoDisplay);
            AssignField(panel, "playButton", playBtn);
            AssignField(panel, "backButton", backBtn);
            AssignField(panel, "selectionIndicator", indicator);
            AssignField(panel, "descriptionTextRect", desc.rectTransform);
            AssignField(panel, "videoDisplayRect", (RectTransform)videoDisplay.transform);
            AssignField(panel, "playButtonRect", (RectTransform)playBtn.transform);
            AssignField(panel, "backButtonRect", (RectTransform)backBtn.transform);
            var saved = SavePrefabReturning(root);
            return saved.GetComponent<ExplainPanel>();
        }

        private static GameObject SavePrefabReturning(GameObject root)
        {
            var path = $"{PrefabDir}/{root.name}.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
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
            AssignField(panel, "defaultSelected", backBtn);
            AssignField(panel, "titleText", title);
            AssignField(panel, "backButton", backBtn);
            AssignField(panel, "gameContainer", containerRt);
            AssignField(panel, "titleTextRect", title.rectTransform);
            AssignField(panel, "backButtonRect", (RectTransform)backBtn.transform);
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

        private static void CreateMashRaceGame()
        {
            var root = new GameObject("MashRaceGame", typeof(RectTransform), typeof(CanvasGroup));
            var rt = (RectTransform)root.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var game = root.AddComponent<MashRaceGame>();

            // ---- 共有背景 (全画面) ----
            var bg = new GameObject("Background", typeof(RectTransform));
            bg.transform.SetParent(root.transform, false);
            StretchFull((RectTransform)bg.transform);

            // 星 (BACK): 中央・正方形。Z 回転し続け、後半ゆっくり縮小
            var stars = CreateBgImage(bg.transform, "Stars", "Assets/Texture/MashRace/BACK.jpg",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1600, 1600));

            // 地面 (machi): 下起点アンカー (pivot/anchor 下中央)。上昇→縮小→下降で消える
            var ground = CreateBgImage(bg.transform, "Ground", "Assets/Texture/MashRace/machi.png",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(2600, 1760));

            // 地球 (earth): 中央・正方形。下から競り上がる (初期は下 & 非表示)
            var earth = CreateBgImage(bg.transform, "Earth", "Assets/Texture/MashRace/earth.png",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1100, 1100));
            ((RectTransform)earth.transform).anchoredPosition = new Vector2(0, -1600);
            earth.SetActive(false);

            // ---- 白 fadein オーバーレイ (背景の直上 = キャラより下) ----
            // 白到達後もキャラは白の上に残って回転するので、白は背景だけを覆う
            var whiteGO = new GameObject("WhiteFade", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            whiteGO.transform.SetParent(root.transform, false);
            StretchFull((RectTransform)whiteGO.transform);
            var whiteImg = whiteGO.GetComponent<Image>();
            whiteImg.color = Color.white;
            whiteImg.raycastTarget = false;
            var whiteGroup = whiteGO.GetComponent<CanvasGroup>();
            whiteGroup.alpha = 0f;
            whiteGroup.blocksRaycasts = false;

            // ---- 自キャラ (SpriteAnimation) ----
            var rukaFrames = LoadSprites(new[]
            {
                "Assets/Texture/MashRace/ruka_01.png",
                "Assets/Texture/MashRace/ruka_02.png",
                "Assets/Texture/MashRace/ruka_03.png",
                "Assets/Texture/MashRace/ruka_04.png",
                "Assets/Texture/MashRace/ruka_05.png",
            });
            var (p1Char, p1Anim) = CreateSpriteAnimCharacter(root.transform, "P1Character", rukaFrames, new Vector2(-420, -500));
            var (p2Char, p2Anim) = CreateSpriteAnimCharacter(root.transform, "P2Character", rukaFrames, new Vector2(420, -500));

            // ラベル
            CreateTMPText(root.transform, "P1Label", "P1 (A / D)", new Vector2(-420, 520), 32);
            CreateTMPText(root.transform, "P2Label", "P2 (← / →)", new Vector2(420, 520), 32);

            // Miss バッジ
            var (_, p1MissGroup) = CreateMissBadge(root.transform, "P1Miss", new Vector2(-420, 360));
            var (_, p2MissGroup) = CreateMissBadge(root.transform, "P2Miss", new Vector2(420, 360));

            // ---- 中央オーバーレイ ----
            var introText = CreateTMPText(root.transform, "IntroText", "", Vector2.zero, 72);
            var countdownText = CreateTMPText(root.transform, "CountdownText", "", Vector2.zero, 128);
            var timerText = CreateTMPText(root.transform, "TimerText", "10.0", new Vector2(0, -560), 48);

            // ---- Result ----
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

            // ---- Assign ----
            AssignField(game, "canvasGroup", root.GetComponent<CanvasGroup>());
            AssignField(game, "introText", introText);
            AssignField(game, "countdownText", countdownText);
            AssignField(game, "timerText", timerText);
            AssignField(game, "resultGroup", resultGroup);
            AssignField(game, "winnerText", winnerText);
            AssignField(game, "playAgainButton", againBtn);
            AssignField(game, "starsRect", (RectTransform)stars.transform);
            AssignField(game, "groundRect", (RectTransform)ground.transform);
            AssignField(game, "earthRect", (RectTransform)earth.transform);
            AssignField(game, "whiteFade", whiteGroup);
            AssignField(game, "player1Character", p1Char);
            AssignField(game, "player2Character", p2Char);
            AssignField(game, "player1Anim", p1Anim);
            AssignField(game, "player2Anim", p2Anim);
            AssignField(game, "player1MissGroup", p1MissGroup);
            AssignField(game, "player2MissGroup", p2MissGroup);

            SavePrefab(root);
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static GameObject CreateBgImage(Transform parent, string name, string spritePath,
            Vector2 pivot, Vector2 anchor, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.sprite = LoadSprite(spritePath);
            img.raycastTarget = false;
            img.preserveAspect = true;
            return go;
        }

        private static (RectTransform rt, SpriteAnimation anim) CreateSpriteAnimCharacter(
            Transform parent, string name, Sprite[] frames, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(SpriteAnimation));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(220, 320);
            rt.anchoredPosition = anchoredPos;
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            if (frames != null && frames.Length > 0) img.sprite = frames[0];

            var anim = go.GetComponent<SpriteAnimation>();
            AssignField(anim, "image", img);
            AssignArray(anim, "sprites", frames);
            AssignFloat(anim, "frameDuration", 0.12f);
            AssignBool(anim, "loop", true);
            AssignBool(anim, "playOnAwake", true);
            return (rt, anim);
        }

        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;

            // Multiple モード等でメインが Sprite でない場合はサブアセットを探す
            foreach (var rep in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
                if (rep is Sprite s) return s;

            // それでも取れなければ Sprite(Single) に矯正して再インポート
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            Debug.LogWarning($"[PrefabGenerator] Sprite not found: {path}");
            return null;
        }

        private static Sprite[] LoadSprites(string[] paths)
        {
            var list = new System.Collections.Generic.List<Sprite>();
            foreach (var p in paths)
            {
                var s = LoadSprite(p);
                if (s != null) list.Add(s);
            }
            return list.ToArray();
        }

        private static void AssignFloat(UnityEngine.Object target, string fieldName, float value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null) return;
            prop.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignBool(UnityEngine.Object target, string fieldName, bool value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null) return;
            prop.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
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

        private static void CreateHopscotchRaceGame()
        {
            var cellPrefab = CreateHopscotchCellPrefab();
            var startCellPrefab = CreateHopscotchStartCellPrefab();

            var root = new GameObject("HopscotchRaceGame", typeof(RectTransform), typeof(CanvasGroup));
            var rt = (RectTransform)root.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var game = root.AddComponent<HopscotchRaceGame>();

            // Player 1 (left half — vertical split)
            var p1Area = CreateHalfAreaVertical(root.transform, "Player1Area", isLeft: true);
            CreateTMPText(p1Area, "P1Label", "P1 (A / D)", new Vector2(0, 520), 32);
            var p1Track = CreateTrackContainer(p1Area, "P1Track");
            var p1Character = CreateHopscotchCharacter(p1Track, "P1Character", new Color(0.3f, 0.9f, 0.4f));

            // Player 2 (right half — vertical split)
            var p2Area = CreateHalfAreaVertical(root.transform, "Player2Area", isLeft: false);
            CreateTMPText(p2Area, "P2Label", "P2 (← / →)", new Vector2(0, 520), 32);
            var p2Track = CreateTrackContainer(p2Area, "P2Track");
            var p2Character = CreateHopscotchCharacter(p2Track, "P2Character", new Color(0.9f, 0.5f, 0.3f));

            // Divider (center vertical line)
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
            AssignField(game, "player1Character", p1Character);
            AssignField(game, "player1CharacterBlinker", p1Character.GetComponent<CanvasGroupBlinker>());
            AssignField(game, "player2Track", p2Track);
            AssignField(game, "player2Character", p2Character);
            AssignField(game, "player2CharacterBlinker", p2Character.GetComponent<CanvasGroupBlinker>());
            AssignField(game, "cellPrefab", cellPrefab);
            AssignField(game, "startCellPrefab", startCellPrefab);

            SavePrefab(root);
        }

        private static RectTransform CreateHopscotchCharacter(Transform parent, string name, Color color)
        {
            var go = new GameObject(name,
                typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(CanvasGroupBlinker));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(90f, 120f);
            rt.anchoredPosition = new Vector2(-250f, -350f);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            var blinker = go.GetComponent<CanvasGroupBlinker>();
            AssignField(blinker, "canvasGroup", go.GetComponent<CanvasGroup>());
            return rt;
        }

        private static RectTransform CreateTrackContainer(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(700, 1000);
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
            AssignField(game, "player1CharacterBlinker", p1Char.GetComponent<CanvasGroupBlinker>());
            AssignField(game, "player2Stack", p2Stack);
            AssignField(game, "player2Character", p2Char);
            AssignField(game, "player2CharacterBlinker", p2Char.GetComponent<CanvasGroupBlinker>());
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
            var go = new GameObject(name,
                typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(CanvasGroupBlinker));
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
            var blinker = go.GetComponent<CanvasGroupBlinker>();
            AssignField(blinker, "canvasGroup", go.GetComponent<CanvasGroup>());
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

            // CursorController on root - hide mouse cursor on start
            root.AddComponent<CursorController>();

            // EventSystem child - required for keyboard/gamepad UI navigation
            var esGO = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule),
                typeof(SelectionKeeper));
            esGO.transform.SetParent(root.transform, false);

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

        private static void AssignField(UnityEngine.Object target, string fieldName, UnityEngine.Object value)
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

        private static void AssignArray(UnityEngine.Object target, string fieldName, UnityEngine.Object[] values)
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
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
