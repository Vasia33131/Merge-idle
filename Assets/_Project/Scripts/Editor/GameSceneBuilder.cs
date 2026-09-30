using System;
using System.IO;
using System.Linq;
using CatMerge.Audio;
using CatMerge.Data;
using CatMerge.Game;
using CatMerge.Input;
using CatMerge.Localization;
using CatMerge.UI;
using CatMerge.Views;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CatMerge.Editor
{
    /// <summary>
    /// Generates placeholder art, cat configs, the Cat prefab and the Game scene.
    /// Runs once automatically if the scene is missing; re-run via Tools/CatMerge/Build Game Scene.
    /// </summary>
    [InitializeOnLoad]
    public static class GameSceneBuilder
    {
        private const string Root = "Assets/_Project";
        private const string ScenePath = Root + "/Scenes/Game.unity";
        private const string PrefabPath = Root + "/Prefabs/Cat.prefab";
        private const string CoinPopupPath = Root + "/Prefabs/CoinPopup.prefab";
        private const string CirclePath = Root + "/Art/Placeholders/Circle.png";
        private const string SquarePath = Root + "/Art/Placeholders/Square.png";
        private const string UnlitSpriteMaterialPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        private const string TmpEssentialsPackage =
            "Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage";

        private const string AutoBuildKey = "CatMerge.AutoBuildAttempted";
        private const string TmpImportKey = "CatMerge.TmpImportAttempted";

        private static readonly Color BackgroundColor = new Color(0.93f, 0.87f, 0.80f);
        private static readonly Color FloorColor = new Color(0.78f, 0.64f, 0.50f);
        private static readonly Color ButtonColor = new Color(0.96f, 0.56f, 0.33f);
        private static readonly Color DangerColor = new Color(0.85f, 0.32f, 0.28f);
        private static readonly Color TextDark = new Color(0.30f, 0.22f, 0.18f);
        private static readonly Color TextMuted = new Color(0.50f, 0.42f, 0.37f);
        private static readonly Color WindowColor = new Color(1.00f, 0.96f, 0.90f);
        private static readonly Color RowColor = new Color(0.95f, 0.88f, 0.79f);
        private static readonly Color CoinColor = new Color(1.00f, 0.80f, 0.25f);
        private static readonly Color CoinRimColor = new Color(0.85f, 0.55f, 0.12f);
        private static readonly Color PopupTextColor = new Color(0.45f, 0.27f, 0.05f);
        private static readonly Color TrackColor = new Color(0.86f, 0.76f, 0.66f);
        private static readonly Color StartScreenColor = new Color(0.98f, 0.93f, 0.86f, 0.94f);
        private static readonly Color FishColor = new Color(0.22f, 0.60f, 0.68f);
        private static readonly Color GlowColor = new Color(1.00f, 0.86f, 0.40f, 0.45f);

        /// <summary>Font of every generated text: Nunito from <see cref="FontAssetGenerator"/>, or TMP's default if the TTFs are missing.</summary>
        private static TMP_FontAsset _font;

        static GameSceneBuilder()
        {
            EditorApplication.delayCall += AutoBuildOnce;
        }

        private static void AutoBuildOnce()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(ScenePath) || SessionState.GetBool(AutoBuildKey, false)) return;

            if (HasTmpEssentials())
            {
                SessionState.SetBool(AutoBuildKey, true);
            }
            else
            {
                // The import may reload the domain and drop its callback; the next delayCall resumes here.
                if (SessionState.GetBool(TmpImportKey, false)) return;
                SessionState.SetBool(TmpImportKey, true);
            }

            Build();
        }

        [MenuItem("Tools/CatMerge/Build Game Scene")]
        public static void BuildFromMenu()
        {
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("CatMerge", "Game scene already exists. Rebuild it from scratch?", "Rebuild", "Cancel"))
            {
                return;
            }
            Build();
        }

        private static void Build()
        {
            if (!HasTmpEssentials())
            {
                ImportTmpEssentialsThen(Build);
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            WarnIfOldInputOnly();

            foreach (var dir in new[] { "Art/Placeholders", "Prefabs", "Scenes", "Settings", "Audio/Meows", "Audio/Music" })
            {
                Directory.CreateDirectory(Path.Combine(Root, dir));
            }
            AssetDatabase.Refresh();

            var circle = LoadOrCreateCircleSprite();
            var square = CreateSpriteAsset(SquarePath, 4, circle: false);
            var spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(UnlitSpriteMaterialPath);
            var (database, balance) = CatDatabaseGenerator.Generate();
            var upgrades = UpgradeDatabaseGenerator.Generate();
            var homes = HomeDatabaseGenerator.Generate();
            var mixer = AudioMixerGenerator.LoadOrCreate();
            _font = FontAssetGenerator.Generate();
            if (_font == null) _font = TMP_Settings.defaultFontAsset;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var catPrefab = CreateCatPrefab(circle, spriteMaterial);
            var popupPrefab = CreateCoinPopupPrefab(circle, spriteMaterial);
            BuildScene(square, spriteMaterial, database, balance, upgrades, homes, mixer, catPrefab, popupPrefab);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();

            Debug.Log($"[CatMerge] Game scene built: {ScenePath}. Press Play and drag cats onto each other.");
        }

        // ---------- scene ----------

        private static void BuildScene(Sprite square, Material spriteMaterial, CatDatabase database, BalanceConfig balance,
            UpgradeDatabase upgrades, HomeDatabase homes, AudioMixer mixer, CatView catPrefab, CoinPopup popupPrefab)
        {
            var cam = CreateCamera();

            var roomGo = new GameObject("Room");
            var room = roomGo.AddComponent<Room>();
            var floor = CreateSprite("Floor", roomGo.transform, square, spriteMaterial, FloorColor, -1000);
            floor.transform.localScale = new Vector3(9f, 6f, 1f);
            Set(room, "floor", floor);

            var catsRoot = new GameObject("Cats").transform;

            var gameGo = new GameObject("Game");
            var game = gameGo.AddComponent<GameController>();
            Set(game, "catDatabase", database);
            Set(game, "balance", balance);
            Set(game, "upgradeDatabase", upgrades);
            Set(game, "homeDatabase", homes);
            Set(game, "room", room);
            Set(game, "catPrefab", catPrefab);
            Set(game, "catsRoot", catsRoot);

            var decor = roomGo.AddComponent<RoomDecor>();
            Set(decor, "game", game);
            Set(decor, "worldCamera", cam);
            Set(decor, "floor", floor);

            var drag = gameGo.AddComponent<DragController>();
            Set(drag, "worldCamera", cam);
            Set(drag, "game", game);
            Set(drag, "room", room);

            var popups = new GameObject("CoinPopups").AddComponent<CoinPopupPool>();
            Set(popups, "prefab", popupPrefab);

            var income = gameGo.AddComponent<IncomeSystem>();
            Set(income, "game", game);
            Set(income, "popups", popups);

            var save = gameGo.AddComponent<SaveService>();
            Set(save, "game", game);

            var audio = CreateAudio(mixer);
            var meows = gameGo.AddComponent<CatMeows>();
            Set(meows, "game", game);
            Set(meows, "audioService", audio);
            Set(meows, "drag", drag);

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var hudScaler = CreateHud(game, save, audio);

            var cameraFit = cam.GetComponent<RoomCameraFit>();
            Set(cameraFit, "room", room);
            Set(cameraFit, "hudScaler", hudScaler);
        }

        private static AudioService CreateAudio(AudioMixer mixer)
        {
            var audio = new GameObject("Audio").AddComponent<AudioService>();
            Set(audio, "mixer", mixer);
            Set(audio, "musicGroup", AudioMixerGenerator.FindGroup(mixer, AudioMixerGenerator.MusicGroup));
            Set(audio, "sfxGroup", AudioMixerGenerator.FindGroup(mixer, AudioMixerGenerator.SfxGroup));
            // First clip in Audio/Music, if any; otherwise the slot stays empty and the game is just quiet.
            Set(audio, "musicClip", FindFirstClip(AudioMixerGenerator.AudioDir + "/Music"));
            return audio;
        }

        public static AudioClip FindFirstClip(string folder, string nameFilter = "")
        {
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            string path = AssetDatabase.FindAssets($"{nameFilter} t:AudioClip", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => nameFilter.Length == 0 || Path.GetFileNameWithoutExtension(p).Equals(nameFilter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.Ordinal)
                .FirstOrDefault();
            return path == null ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static Camera CreateCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0f, 0f, -10f);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BackgroundColor;
            go.AddComponent<AudioListener>();
            go.AddComponent<RoomCameraFit>();
            return cam;
        }

        /// <summary>HUD canvas (1920×1080 reference, width/height match 0.5) with everything inside the safe area.</summary>
        private static CanvasScaler CreateHud(GameController game, SaveService save, AudioService audio)
        {
            var canvasGo = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var canvas = canvasGo.transform;
            var hud = CreateSafeArea("SafeArea", canvas);
            var topLeft = new Vector2(0f, 1f);

            // Coins + income, top-left.
            var coinsRow = CreateUiRect("Coins", hud);
            coinsRow.anchorMin = coinsRow.anchorMax = coinsRow.pivot = topLeft;
            coinsRow.anchoredPosition = new Vector2(36f, -28f);
            coinsRow.sizeDelta = new Vector2(560f, 90f);
            AddRowLayout(coinsRow, TextAnchor.MiddleLeft, 16f);
            var coinsIcon = CreateUiCoin("Icon", coinsRow, 76f);
            var coinsLabel = CreateUiText("Value", coinsRow, "50", 72f, TextDark);
            coinsLabel.alignment = TextAlignmentOptions.Left;
            coinsLabel.enableWordWrapping = false;

            var income = CreateUiText("Income", hud, "+0/сек", 42f, TextMuted);
            income.alignment = TextAlignmentOptions.Left;
            var incomeRect = income.rectTransform;
            incomeRect.anchorMin = incomeRect.anchorMax = incomeRect.pivot = topLeft;
            incomeRect.anchoredPosition = new Vector2(36f + 76f + 16f, -118f);
            incomeRect.sizeDelta = new Vector2(500f, 56f);

            var fish = CreateUiText("Fish", hud, "Рыбки: 5 (+50% к доходу)", 36f, FishColor);
            fish.alignment = TextAlignmentOptions.Left;
            fish.enableWordWrapping = false;
            var fishRect = fish.rectTransform;
            fishRect.anchorMin = fishRect.anchorMax = fishRect.pivot = topLeft;
            fishRect.anchoredPosition = new Vector2(36f + 76f + 16f, -172f);
            fishRect.sizeDelta = new Vector2(500f, 50f);

            // Cat counter and a column of buttons, top-right. Narrow enough to leave the coins alone on a portrait phone.
            var counter = CreateUiText("CatCounter", hud, "Коты: 0/12", 50f, TextDark);
            counter.alignment = TextAlignmentOptions.Right;
            counter.enableWordWrapping = false;
            var counterRect = counter.rectTransform;
            counterRect.anchorMin = counterRect.anchorMax = counterRect.pivot = Vector2.one;
            counterRect.anchoredPosition = new Vector2(-36f, -40f);
            counterRect.sizeDelta = new Vector2(300f, 80f);

            var settingsButton = CreateUiButton("SettingsButton", hud, "Настройки", 38f, out var settingsLabel);
            Localize(settingsLabel, "settings_button");
            var settingsRect = (RectTransform)settingsButton.transform;
            settingsRect.anchorMin = settingsRect.anchorMax = settingsRect.pivot = Vector2.one;
            settingsRect.anchoredPosition = new Vector2(-36f, -130f);
            settingsRect.sizeDelta = new Vector2(260f, 84f);

            var codexButton = CreateUiButton("CodexButton", hud, "Котопедия", 38f, out var codexLabel);
            Localize(codexLabel, "codex_button");
            var codexRect = (RectTransform)codexButton.transform;
            codexRect.anchorMin = codexRect.anchorMax = codexRect.pivot = Vector2.one;
            codexRect.anchoredPosition = new Vector2(-36f, -230f);
            codexRect.sizeDelta = new Vector2(260f, 84f);

            // Pivot in the middle so its pulse grows around the centre; hidden by HudPanel until the top cat is reached.
            var moveButton = CreateUiButton("MoveButton", hud, "Переезд!", 40f, out var moveLabel);
            Localize(moveLabel, "move_button");
            moveButton.GetComponent<Image>().color = FishColor;
            var moveRect = (RectTransform)moveButton.transform;
            moveRect.anchorMin = moveRect.anchorMax = Vector2.one;
            moveRect.anchoredPosition = new Vector2(-36f - 130f, -330f - 45f);
            moveRect.sizeDelta = new Vector2(260f, 90f);

            // Bottom bar: quick buy (kitten name on top, coin + price below), shop and upgrades. The layout keeps the
            // preferred 1000 px on wide screens and squeezes the buttons towards their min widths on narrow ones.
            var bar = CreateUiRect("BottomBar", hud);
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = new Vector2(0f, 32f);
            bar.sizeDelta = new Vector2(-48f, 150f);
            var barLayout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            barLayout.childAlignment = TextAnchor.MiddleCenter;
            barLayout.spacing = 20f;
            barLayout.childControlWidth = barLayout.childControlHeight = true;
            barLayout.childForceExpandWidth = false;
            barLayout.childForceExpandHeight = true;

            var buyButton = CreateUiButton("BuyButton", bar, "Кроха · 1 ур.", 40f, out var buyCaption);
            SetBarWidth(buyButton, 330f, 480f);
            buyCaption.alignment = TextAlignmentOptions.Bottom;
            buyCaption.enableWordWrapping = false;
            var captionRect = buyCaption.rectTransform;
            captionRect.anchorMin = new Vector2(0f, 0.5f);
            captionRect.offsetMin = new Vector2(16f, 0f);
            captionRect.offsetMax = new Vector2(-16f, -14f);

            var priceRow = CreateUiRect("Price", buyButton.transform);
            Stretch(priceRow, 0f);
            priceRow.anchorMax = new Vector2(1f, 0.5f);
            priceRow.offsetMin = new Vector2(16f, 12f);
            priceRow.offsetMax = new Vector2(-16f, 0f);
            AddRowLayout(priceRow, TextAnchor.MiddleCenter, 12f);
            CreateUiCoin("Icon", priceRow, 50f);
            var buyPrice = CreateUiText("Value", priceRow, "10", 56f, Color.white);
            buyPrice.enableWordWrapping = false;

            var shopButton = CreateUiButton("ShopButton", bar, "Магазин", 40f, out var shopLabel);
            Localize(shopLabel, "shop_button");
            SetBarWidth(shopButton, 180f, 220f);

            var upgradesButton = CreateUiButton("UpgradesButton", bar, "Улучшения", 40f, out var upgradesLabel);
            Localize(upgradesLabel, "upgrades_button");
            SetBarWidth(upgradesButton, 210f, 260f);

            // Created last so the modal overlays draw over the HUD; the offline popup goes on top of everything.
            var shop = CreateShop(canvas, game);
            var upgrades = CreateUpgrades(canvas, game);
            var codex = CreateCodex(canvas, game, audio);
            var prestige = CreatePrestige(canvas, game, save);
            var settings = CreateSettings(canvas, save, audio);
            CreateOfflinePopup(canvas, save);
            CreateNewCatPopup(canvas, game, audio);
            CreateStartScreen(canvas, audio);

            var panel = canvasGo.AddComponent<HudPanel>();
            Set(panel, "game", game);
            Set(panel, "shop", shop);
            Set(panel, "settings", settings);
            Set(panel, "settingsButton", settingsButton);
            Set(panel, "coinsLabel", coinsLabel);
            Set(panel, "coinsIcon", coinsIcon);
            Set(panel, "incomeLabel", income);
            Set(panel, "counterLabel", counter);
            Set(panel, "buyButton", buyButton);
            Set(panel, "buyCaption", buyCaption);
            Set(panel, "buyPrice", buyPrice);
            Set(panel, "shopButton", shopButton);
            Set(panel, "upgrades", upgrades);
            Set(panel, "upgradesButton", upgradesButton);
            Set(panel, "codex", codex);
            Set(panel, "prestige", prestige);
            Set(panel, "fishLabel", fish);
            Set(panel, "codexButton", codexButton);
            Set(panel, "moveButton", moveButton);
            return scaler;
        }

        /// <summary>Button in the bottom bar: <paramref name="preferred"/> width when there is room, down to <paramref name="min"/>.</summary>
        private static void SetBarWidth(Button button, float min, float preferred)
        {
            var element = button.gameObject.AddComponent<LayoutElement>();
            element.minWidth = min;
            element.preferredWidth = preferred;
        }

        /// <summary>Full-screen child that <see cref="SafeArea"/> shrinks to the notch-free part of the screen.</summary>
        private static RectTransform CreateSafeArea(string name, Transform parent)
        {
            var rect = CreateUiRect(name, parent);
            Stretch(rect, 0f);
            rect.gameObject.AddComponent<SafeArea>();
            return rect;
        }

        /// <summary>Static caption: <see cref="LocalizedText"/> shows <paramref name="key"/> in the current language.</summary>
        private static void Localize(TMP_Text label, string key)
        {
            var localized = label.gameObject.AddComponent<LocalizedText>();
            var so = new SerializedObject(localized);
            so.FindProperty("key").stringValue = key;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Children laid out left to right at their preferred sizes (icon + text that changes width).</summary>
        private static void AddRowLayout(RectTransform rect, TextAnchor alignment, float spacing)
        {
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = alignment;
            layout.spacing = spacing;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        }

        /// <summary>Placeholder coin: rim circle with a lighter face.</summary>
        private static RectTransform CreateUiCoin(string name, Transform parent, float size)
        {
            var circle = LoadOrCreateCircleSprite();
            var rim = CreateUiRect(name, parent);
            var element = rim.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = element.preferredHeight = size;
            rim.sizeDelta = new Vector2(size, size);
            var rimImage = rim.gameObject.AddComponent<Image>();
            rimImage.sprite = circle;
            rimImage.color = CoinRimColor;
            rimImage.raycastTarget = false;

            var face = CreateUiRect("Face", rim);
            Stretch(face, size * 0.14f);
            var faceImage = face.gameObject.AddComponent<Image>();
            faceImage.sprite = circle;
            faceImage.color = CoinColor;
            faceImage.raycastTarget = false;
            return rim;
        }

        /// <summary>
        /// Dimmed full-screen overlay (blocks the room) with a centred window and a title. With
        /// <paramref name="closable"/>, tapping the dim area or the × button is meant to close it.
        /// The window sits in a safe-area frame; <see cref="PanelAnimator"/> animates it and shrinks it to fit small screens.
        /// </summary>
        private static PanelAnimator CreateModal(Transform canvas, string name, Vector2 size, string titleText, bool closable,
            out RectTransform window, out TextMeshProUGUI title, out Button close, out Button background)
        {
            var overlay = CreateUiRect(name, canvas);
            Stretch(overlay, 0f);
            var dim = overlay.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.45f);
            background = null;
            if (closable)
            {
                background = overlay.gameObject.AddComponent<Button>();
                background.transition = Selectable.Transition.None;
            }

            overlay.gameObject.AddComponent<CanvasGroup>();
            var frame = CreateSafeArea("SafeFrame", overlay); // no graphic: taps around the window reach the dim button

            window = CreateUiRect("Window", frame);
            window.anchorMin = window.anchorMax = new Vector2(0.5f, 0.5f);
            window.sizeDelta = size;
            AddSlicedImage(window.gameObject, WindowColor);

            title = CreateUiText("Title", window, titleText, 56f, TextDark);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(120f, -120f);
            titleRect.offsetMax = new Vector2(-120f, 0f);

            close = null;
            if (closable)
            {
                close = CreateUiButton("Close", window, "×", 64f, out _);
                var closeRect = (RectTransform)close.transform;
                closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(1f, 1f);
                closeRect.anchoredPosition = new Vector2(-20f, -20f);
                closeRect.sizeDelta = new Vector2(90f, 90f);
            }

            var animator = overlay.gameObject.AddComponent<PanelAnimator>();
            Set(animator, "window", window);
            return animator;
        }

        /// <summary>Stretches <paramref name="rect"/> across the window at <paramref name="y"/> from its centre.</summary>
        private static void PlaceInWindow(RectTransform rect, float y, float height, float sideInset = 60f)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(-2f * sideInset, height);
        }

        private static SettingsPanel CreateSettings(Transform canvas, SaveService save, AudioService audio)
        {
            var overlay = CreateModal(canvas, "Settings", new Vector2(800f, 820f), "Настройки", closable: true,
                out var window, out var title, out var close, out var background);
            Localize(title, "settings_title");

            var music = CreateVolumeRow(window, "Music", "Музыка", 215f, out var musicLabel);
            Localize(musicLabel, "settings_music");
            var sfx = CreateVolumeRow(window, "Sfx", "Звуки", 105f, out var sfxLabel);
            Localize(sfxLabel, "settings_sfx");
            var languages = CreateLanguageRow(window, -5f);

            var reset = CreateUiButton("ResetButton", window, "Сбросить прогресс", 42f, out var resetLabel);
            reset.GetComponent<Image>().color = DangerColor;
            PlaceInWindow((RectTransform)reset.transform, -150f, 120f, 100f);
            Localize(resetLabel, "settings_reset");

            var hint = CreateUiText("ResetHint", window, "Отладка: удаляет сохранение и перезапускает игру.", 30f, TextMuted);
            hint.fontStyle = FontStyles.Normal;
            PlaceInWindow(hint.rectTransform, -270f, 80f);
            Localize(hint, "settings_reset_hint");

            var panel = canvas.gameObject.AddComponent<SettingsPanel>();
            Set(panel, "saveService", save);
            Set(panel, "audioService", audio);
            Set(panel, "musicSlider", music);
            Set(panel, "sfxSlider", sfx);
            SetArray(panel, "languageButtons", languages);
            Set(panel, "panel", overlay);
            Set(panel, "closeButton", close);
            Set(panel, "backgroundButton", background);
            Set(panel, "resetButton", reset);
            Set(panel, "resetLabel", resetLabel.GetComponent<LocalizedText>());
            overlay.gameObject.SetActive(false);
            return panel;
        }

        /// <summary>
        /// "Language" caption and one button per language (order of <see cref="LocalizationService.Languages"/>).
        /// Each button is captioned in its own language, so it can always be found.
        /// </summary>
        private static Object[] CreateLanguageRow(Transform window, float y)
        {
            var row = CreateUiRect("Language", window);
            PlaceInWindow(row, y, 90f);

            var label = CreateUiText("Label", row, "Язык", 40f, TextDark);
            label.alignment = TextAlignmentOptions.Left;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.sizeDelta = new Vector2(210f, 0f);
            Localize(label, "settings_language");

            var buttons = CreateUiRect("Buttons", row);
            Stretch(buttons, 0f);
            buttons.offsetMin = new Vector2(230f, 0f);
            var layout = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;

            var names = new[] { "Русский", "English" };
            var result = new Object[LocalizationService.Languages.Count];
            for (int i = 0; i < result.Length; i++)
            {
                string code = LocalizationService.ToCode(LocalizationService.Languages[i]);
                result[i] = CreateUiButton("Lang_" + code, buttons, i < names.Length ? names[i] : code, 36f, out _);
            }
            return result;
        }

        /// <summary>Caption on the left, 0..1 slider filling the rest of the row.</summary>
        private static Slider CreateVolumeRow(Transform window, string name, string text, float y, out TextMeshProUGUI label)
        {
            var row = CreateUiRect(name, window);
            PlaceInWindow(row, y, 90f);

            label = CreateUiText("Label", row, text, 40f, TextDark);
            label.alignment = TextAlignmentOptions.Left;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.sizeDelta = new Vector2(210f, 0f);

            var slider = CreateUiSlider("Slider", row, 64f);
            var sliderRect = (RectTransform)slider.transform;
            Stretch(sliderRect, 0f);
            sliderRect.offsetMin = new Vector2(230f, 13f);
            sliderRect.offsetMax = new Vector2(0f, -13f);
            return slider;
        }

        private static Slider CreateUiSlider(string name, Transform parent, float handleSize)
        {
            var root = CreateUiRect(name, parent);
            float halfHandle = handleSize * 0.5f;

            var track = CreateUiRect("Background", root);
            track.anchorMin = new Vector2(0f, 0.35f);
            track.anchorMax = new Vector2(1f, 0.65f);
            track.offsetMin = track.offsetMax = Vector2.zero;
            AddSlicedImage(track.gameObject, TrackColor);

            // Fill and handle areas are inset by half a handle so the handle never leaves the track;
            // the fill reaches back over the inset so it starts at the track's left edge.
            var fillArea = CreateUiRect("Fill Area", root);
            fillArea.anchorMin = new Vector2(0f, 0.35f);
            fillArea.anchorMax = new Vector2(1f, 0.65f);
            fillArea.offsetMin = new Vector2(halfHandle, 0f);
            fillArea.offsetMax = new Vector2(-halfHandle, 0f);
            var fill = CreateUiRect("Fill", fillArea);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0.5f, 1f);
            fill.offsetMin = new Vector2(-halfHandle, 0f);
            fill.offsetMax = Vector2.zero;
            AddSlicedImage(fill.gameObject, ButtonColor).raycastTarget = false;

            var handleArea = CreateUiRect("Handle Slide Area", root);
            Stretch(handleArea, 0f);
            handleArea.offsetMin = new Vector2(halfHandle, 0f);
            handleArea.offsetMax = new Vector2(-halfHandle, 0f);
            var handle = CreateUiRect("Handle", handleArea);
            handle.anchorMin = new Vector2(0.5f, 0f);
            handle.anchorMax = new Vector2(0.5f, 1f);
            handle.sizeDelta = new Vector2(handleSize, 0f);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = LoadOrCreateCircleSprite();
            handleImage.color = ButtonColor;

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.5f;
            return slider;
        }

        private static void CreateStartScreen(Transform canvas, AudioService audio)
        {
            // Created last so it covers everything, including an offline popup waiting underneath.
            var overlay = CreateUiRect("StartScreen", canvas);
            Stretch(overlay, 0f);
            overlay.gameObject.AddComponent<Image>().color = StartScreenColor;
            var group = overlay.gameObject.AddComponent<CanvasGroup>();
            var button = overlay.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;

            var label = CreateUiText("Label", overlay, "Нажми, чтобы начать", 72f, TextDark);
            Stretch(label.rectTransform, 60f);

            var screen = canvas.gameObject.AddComponent<StartScreen>();
            Set(screen, "audioService", audio);
            Set(screen, "root", group);
            Set(screen, "button", button);
            Set(screen, "label", label);
        }

        private static void CreateOfflinePopup(Transform canvas, SaveService save)
        {
            var overlay = CreateModal(canvas, "OfflinePopup", new Vector2(820f, 640f), "С возвращением!", closable: false,
                out var window, out var title, out _, out _);

            var body = CreateUiText("Body", window, "Пока тебя не было (2 ч),\nкотики заработали:", 38f, TextMuted);
            PlaceInWindow(body.rectTransform, 110f, 130f);

            var amountRow = CreateUiRect("Amount", window);
            PlaceInWindow(amountRow, -20f, 110f);
            AddRowLayout(amountRow, TextAnchor.MiddleCenter, 18f);
            CreateUiCoin("Icon", amountRow, 90f);
            var amount = CreateUiText("Value", amountRow, "+1.2K", 84f, TextDark);
            amount.enableWordWrapping = false;

            var collect = CreateUiButton("Collect", window, "Забрать", 50f, out var collectLabel);
            var collectRect = (RectTransform)collect.transform;
            collectRect.anchorMin = collectRect.anchorMax = collectRect.pivot = new Vector2(0.5f, 0f);
            collectRect.anchoredPosition = new Vector2(0f, 50f);
            collectRect.sizeDelta = new Vector2(420f, 130f);

            var popup = canvas.gameObject.AddComponent<OfflinePopup>();
            Set(popup, "saveService", save);
            Set(popup, "panel", overlay);
            Set(popup, "titleLabel", title);
            Set(popup, "bodyLabel", body);
            Set(popup, "amountLabel", amount);
            Set(popup, "collectButton", collect);
            Set(popup, "collectLabel", collectLabel);
            overlay.gameObject.SetActive(false);
        }

        private static ShopPanel CreateShop(Transform canvas, GameController game)
        {
            var overlay = CreateListModal(canvas, "Shop", "Котята · 0/12",
                out var title, out var close, out var background, out var scroll, out var row);

            var shop = canvas.gameObject.AddComponent<ShopPanel>();
            Set(shop, "game", game);
            Set(shop, "panel", overlay);
            Set(shop, "closeButton", close);
            Set(shop, "backgroundButton", background);
            Set(shop, "titleLabel", title);
            Set(shop, "scroll", scroll);
            Set(shop, "rowTemplate", row);
            overlay.gameObject.SetActive(false);
            return shop;
        }

        private static UpgradesPanel CreateUpgrades(Transform canvas, GameController game)
        {
            var overlay = CreateListModal(canvas, "Upgrades", "Улучшения",
                out var title, out var close, out var background, out var scroll, out var row);

            var panel = canvas.gameObject.AddComponent<UpgradesPanel>();
            Set(panel, "game", game);
            Set(panel, "panel", overlay);
            Set(panel, "closeButton", close);
            Set(panel, "backgroundButton", background);
            Set(panel, "titleLabel", title);
            Set(panel, "scroll", scroll);
            Set(panel, "rowTemplate", row);
            overlay.gameObject.SetActive(false);
            return panel;
        }

        /// <summary>Closable modal with a vertical scroll list and a (hidden at runtime) <see cref="ShopRow"/> template.</summary>
        private static PanelAnimator CreateListModal(Transform canvas, string name, string titleText, out TextMeshProUGUI title,
            out Button close, out Button background, out ScrollRect scroll, out ShopRow row)
        {
            var overlay = CreateScrollModal(canvas, name, new Vector2(900f, 920f), titleText,
                out title, out close, out background, out scroll);

            var content = scroll.content;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            row = CreateShopRow(content);
            return overlay;
        }

        /// <summary>Closable modal whose window holds a vertical <see cref="ScrollRect"/>; the caller lays out its content.</summary>
        private static PanelAnimator CreateScrollModal(Transform canvas, string name, Vector2 size, string titleText,
            out TextMeshProUGUI title, out Button close, out Button background, out ScrollRect scroll)
        {
            // Tapping outside the window closes it.
            var overlay = CreateModal(canvas, name, size, titleText, closable: true,
                out var window, out title, out close, out background);

            // Scroll list.
            var scrollRect = CreateUiRect("List", window);
            Stretch(scrollRect, 0f);
            scrollRect.offsetMin = new Vector2(30f, 30f);
            scrollRect.offsetMax = new Vector2(-30f, -130f);
            scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            var viewport = CreateUiRect("Viewport", scrollRect);
            Stretch(viewport, 0f);
            viewport.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0f); // catches drags between rows
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = CreateUiRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;

            scroll.viewport = viewport;
            scroll.content = content;
            return overlay;
        }

        // ---------- Catpedia ----------

        private static CodexPanel CreateCodex(Transform canvas, GameController game, AudioService audio)
        {
            var overlay = CreateScrollModal(canvas, "Codex", new Vector2(960f, 1000f), "Котопедия · 1/15",
                out var title, out var close, out var background, out var scroll);

            // 3 columns: 3 × 284 + 2 × 14 + 2 × 4 = 892 of the 900 px list width.
            var content = scroll.content;
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(284f, 400f);
            grid.spacing = new Vector2(14f, 14f);
            grid.padding = new RectOffset(4, 4, 4, 4);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var card = CreateCodexCard(content);

            var panel = canvas.gameObject.AddComponent<CodexPanel>();
            Set(panel, "game", game);
            Set(panel, "audioService", audio);
            Set(panel, "panel", overlay);
            Set(panel, "closeButton", close);
            Set(panel, "backgroundButton", background);
            Set(panel, "titleLabel", title);
            Set(panel, "scroll", scroll);
            Set(panel, "cardTemplate", card);
            overlay.gameObject.SetActive(false);
            return panel;
        }

        /// <summary>Picture on top, then name, description and the meow button at the bottom (284 × 400 grid cell).</summary>
        private static CodexCard CreateCodexCard(Transform parent)
        {
            var cardRect = CreateUiRect("CardTemplate", parent);
            AddSlicedImage(cardRect.gameObject, RowColor);

            var swatch = CreateUiSwatch(cardRect, 150f, out var swatchLabel);
            var swatchRect = (RectTransform)swatch.transform;
            swatchRect.anchorMin = swatchRect.anchorMax = swatchRect.pivot = new Vector2(0.5f, 1f);
            swatchRect.anchoredPosition = new Vector2(0f, -20f);

            var nameLabel = CreateUiText("Name", cardRect, "Кроха", 34f, TextDark);
            nameLabel.enableWordWrapping = false;
            nameLabel.enableAutoSizing = true;
            nameLabel.fontSizeMin = 22f;
            nameLabel.fontSizeMax = 34f;
            PlaceFromTop(nameLabel.rectTransform, 182f, 48f, 12f);

            var descLabel = CreateUiText("Desc", cardRect, "Помещается в варежку, а мурчит как трактор.", 24f, TextMuted);
            descLabel.fontStyle = FontStyles.Normal;
            descLabel.alignment = TextAlignmentOptions.Top;
            descLabel.overflowMode = TextOverflowModes.Ellipsis;
            PlaceFromTop(descLabel.rectTransform, 234f, 76f, 14f);

            var meow = CreateUiButton("Meow", cardRect, "Послушать мяу", 26f, out var meowLabel);
            meowLabel.enableAutoSizing = true;
            meowLabel.fontSizeMin = 18f;
            meowLabel.fontSizeMax = 26f;
            meowLabel.margin = new Vector4(10f, 0f, 10f, 0f);
            var meowRect = (RectTransform)meow.transform;
            meowRect.anchorMin = meowRect.anchorMax = meowRect.pivot = new Vector2(0.5f, 0f);
            meowRect.anchoredPosition = new Vector2(0f, 18f);
            meowRect.sizeDelta = new Vector2(244f, 66f);

            var card = cardRect.gameObject.AddComponent<CodexCard>();
            Set(card, "swatch", swatch);
            Set(card, "swatchLabel", swatchLabel);
            Set(card, "nameLabel", nameLabel);
            Set(card, "descLabel", descLabel);
            Set(card, "meowButton", meow);
            Set(card, "meowLabel", meowLabel);
            return card;
        }

        /// <summary>Round cat picture with the level number on it.</summary>
        private static Image CreateUiSwatch(Transform parent, float size, out TextMeshProUGUI label)
        {
            var rect = CreateUiRect("Swatch", parent);
            rect.sizeDelta = new Vector2(size, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = LoadOrCreateCircleSprite();
            image.raycastTarget = false;

            label = CreateUiText("Level", rect, "1", size * 0.42f, TextDark);
            Stretch(label.rectTransform, 0f);
            return image;
        }

        /// <summary>Stretches <paramref name="rect"/> across its parent, <paramref name="top"/> px below the parent's top edge.</summary>
        private static void PlaceFromTop(RectTransform rect, float top, float height, float sideInset)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(-2f * sideInset, height);
        }

        private static void CreateNewCatPopup(Transform canvas, GameController game, AudioService audio)
        {
            var overlay = CreateModal(canvas, "NewCatPopup", new Vector2(780f, 860f), "Новый котик!", closable: false,
                out var window, out var title, out _, out _);

            var glowRect = CreateUiRect("Glow", window);
            glowRect.anchorMin = glowRect.anchorMax = new Vector2(0.5f, 0.5f);
            glowRect.anchoredPosition = new Vector2(0f, 130f);
            glowRect.sizeDelta = new Vector2(300f, 300f);
            var glow = glowRect.gameObject.AddComponent<Image>();
            glow.sprite = LoadOrCreateCircleSprite();
            glow.color = GlowColor;
            glow.raycastTarget = false;

            var swatch = CreateUiSwatch(window, 250f, out var swatchLabel);
            var swatchRect = (RectTransform)swatch.transform;
            swatchRect.anchorMin = swatchRect.anchorMax = new Vector2(0.5f, 0.5f);
            swatchRect.anchoredPosition = new Vector2(0f, 130f);

            var nameLabel = CreateUiText("Name", window, "Зефирка", 60f, TextDark);
            PlaceInWindow(nameLabel.rectTransform, -60f, 80f);

            var descLabel = CreateUiText("Desc", window, "Мягкая, сладкая и всё время просится на ручки.", 36f, TextMuted);
            descLabel.fontStyle = FontStyles.Normal;
            PlaceInWindow(descLabel.rectTransform, -150f, 100f);

            var meow = CreateUiButton("Meow", window, "Послушать мяу", 40f, out var meowLabel);
            PlaceWindowButton((RectTransform)meow.transform, -175f, 320f);
            var ok = CreateUiButton("Ok", window, "Ура!", 50f, out var okLabel);
            PlaceWindowButton((RectTransform)ok.transform, 175f, 320f);

            var popup = canvas.gameObject.AddComponent<NewCatPopup>();
            Set(popup, "game", game);
            Set(popup, "audioService", audio);
            Set(popup, "panel", overlay);
            Set(popup, "titleLabel", title);
            Set(popup, "swatch", swatch);
            Set(popup, "swatchLabel", swatchLabel);
            Set(popup, "glow", glowRect);
            Set(popup, "nameLabel", nameLabel);
            Set(popup, "descLabel", descLabel);
            Set(popup, "meowButton", meow);
            Set(popup, "meowLabel", meowLabel);
            Set(popup, "okButton", ok);
            Set(popup, "okLabel", okLabel);
            overlay.gameObject.SetActive(false);
        }

        /// <summary>One of two buttons side by side at the bottom of a window.</summary>
        private static void PlaceWindowButton(RectTransform rect, float x, float width)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(x, 50f);
            rect.sizeDelta = new Vector2(width, 120f);
        }

        // ---------- moving to a new home ----------

        private static PrestigePanel CreatePrestige(Transform canvas, GameController game, SaveService save)
        {
            var overlay = CreateModal(canvas, "Prestige", new Vector2(880f, 940f), "Переезд в новый дом", closable: true,
                out var window, out var title, out var close, out var background);

            var home = CreateUiText("Home", window, "Новый дом: Дача", 44f, TextDark);
            PlaceInWindow(home.rectTransform, 300f, 64f);

            var earned = CreateUiText("Earned", window, "Заработано за забег: 1.5B", 34f, TextMuted);
            earned.fontStyle = FontStyles.Normal;
            PlaceInWindow(earned.rectTransform, 235f, 50f);

            var fishCaption = CreateUiText("FishCaption", window, "Рыбки за переезд:", 38f, TextDark);
            PlaceInWindow(fishCaption.rectTransform, 160f, 56f);

            var fish = CreateUiText("FishValue", window, "+5", 110f, FishColor);
            fish.enableWordWrapping = false;
            PlaceInWindow(fish.rectTransform, 60f, 130f);

            var total = CreateUiText("Total", window, "Всего рыбок: 5 · доход ×1.5", 36f, TextDark);
            PlaceInWindow(total.rectTransform, -40f, 60f);

            var resets = CreateUiText("Resets", window, "Сбросится: коты, монеты и улучшения.\nОстанутся: рыбки и котопедия.", 32f, TextMuted);
            resets.fontStyle = FontStyles.Normal;
            PlaceInWindow(resets.rectTransform, -150f, 110f);

            var confirm = CreateUiButton("Confirm", window, "Переехать", 48f, out var confirmLabel);
            confirm.GetComponent<Image>().color = FishColor;
            PlaceWindowButton((RectTransform)confirm.transform, -185f, 340f);
            var cancel = CreateUiButton("Cancel", window, "Остаться", 48f, out var cancelLabel);
            PlaceWindowButton((RectTransform)cancel.transform, 185f, 340f);

            var panel = canvas.gameObject.AddComponent<PrestigePanel>();
            Set(panel, "game", game);
            Set(panel, "saveService", save);
            Set(panel, "panel", overlay);
            Set(panel, "closeButton", close);
            Set(panel, "backgroundButton", background);
            Set(panel, "titleLabel", title);
            Set(panel, "homeLabel", home);
            Set(panel, "earnedLabel", earned);
            Set(panel, "fishCaption", fishCaption);
            Set(panel, "fishLabel", fish);
            Set(panel, "totalLabel", total);
            Set(panel, "resetsLabel", resets);
            Set(panel, "confirmButton", confirm);
            Set(panel, "confirmLabel", confirmLabel);
            Set(panel, "cancelButton", cancel);
            Set(panel, "cancelLabel", cancelLabel);
            overlay.gameObject.SetActive(false);
            return panel;
        }

        private static ShopRow CreateShopRow(Transform parent)
        {
            var rowRect = CreateUiRect("RowTemplate", parent);
            AddSlicedImage(rowRect.gameObject, RowColor);
            rowRect.gameObject.AddComponent<LayoutElement>().preferredHeight = 150f;

            var swatchRect = CreateUiRect("Swatch", rowRect);
            swatchRect.anchorMin = swatchRect.anchorMax = swatchRect.pivot = new Vector2(0f, 0.5f);
            swatchRect.anchoredPosition = new Vector2(20f, 0f);
            swatchRect.sizeDelta = new Vector2(116f, 116f);
            var swatch = swatchRect.gameObject.AddComponent<Image>();
            swatch.sprite = LoadOrCreateCircleSprite();
            swatch.raycastTarget = false;

            var swatchLabel = CreateUiText("Level", swatchRect, "1", 52f, TextDark);
            Stretch(swatchLabel.rectTransform, 0f);

            var nameLabel = CreateUiText("Name", rowRect, "Кроха", 44f, TextDark);
            nameLabel.alignment = TextAlignmentOptions.BottomLeft;
            // Upgrade titles ("Большая комната · 3/9") can be long: shrink instead of wrapping into the info line.
            nameLabel.enableWordWrapping = false;
            nameLabel.enableAutoSizing = true;
            nameLabel.fontSizeMin = 28f;
            nameLabel.fontSizeMax = 44f;
            var nameRect = nameLabel.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 0.5f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.offsetMin = new Vector2(156f, 0f);
            nameRect.offsetMax = new Vector2(-290f, -12f);

            var infoLabel = CreateUiText("Info", rowRect, "", 30f, TextMuted);
            infoLabel.fontStyle = FontStyles.Normal;
            infoLabel.alignment = TextAlignmentOptions.TopLeft;
            infoLabel.overflowMode = TextOverflowModes.Ellipsis;
            var infoRect = infoLabel.rectTransform;
            infoRect.anchorMin = new Vector2(0f, 0f);
            infoRect.anchorMax = new Vector2(1f, 0.5f);
            infoRect.offsetMin = new Vector2(156f, 10f);
            infoRect.offsetMax = new Vector2(-290f, -4f);

            var buy = CreateUiButton("Buy", rowRect, "Купить · 10", 36f, out var buyLabel);
            var buyRect = (RectTransform)buy.transform;
            buyRect.anchorMin = buyRect.anchorMax = buyRect.pivot = new Vector2(1f, 0.5f);
            buyRect.anchoredPosition = new Vector2(-20f, 0f);
            buyRect.sizeDelta = new Vector2(250f, 100f);

            var row = rowRect.gameObject.AddComponent<ShopRow>();
            Set(row, "swatch", swatch);
            Set(row, "swatchLabel", swatchLabel);
            Set(row, "nameLabel", nameLabel);
            Set(row, "infoLabel", infoLabel);
            Set(row, "buyButton", buy);
            Set(row, "buyLabel", buyLabel);
            return row;
        }

        private static Button CreateUiButton(string name, Transform parent, string text, float size, out TextMeshProUGUI label)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            AddSlicedImage(go, ButtonColor);
            var button = go.AddComponent<Button>();

            label = CreateUiText("Label", go.transform, text, size, Color.white);
            Stretch(label.rectTransform, 0f);
            // Shrinks rather than overflows when a translation is longer or the bottom bar is squeezed on a phone.
            label.enableAutoSizing = true;
            label.fontSizeMin = Mathf.Round(size * 0.6f);
            label.fontSizeMax = size;
            label.margin = new Vector4(10f, 0f, 10f, 0f);
            return button;
        }

        private static RectTransform CreateUiRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static Image AddSlicedImage(GameObject go, Color color)
        {
            var image = go.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = color;
            return image;
        }

        private static TextMeshProUGUI CreateUiText(string name, Transform parent, string text, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = _font != null ? _font : TMP_Settings.defaultFontAsset;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.raycastTarget = false;
            return tmp;
        }

        // ---------- cat prefab ----------

        private static CatView CreateCatPrefab(Sprite circle, Material spriteMaterial)
        {
            var root = new GameObject("Cat", typeof(SortingGroup), typeof(CatWander));
            var view = root.AddComponent<CatView>();

            var shadow = CreateSprite("Shadow", root.transform, circle, spriteMaterial, new Color(0f, 0f, 0f, 0.2f), -1);
            shadow.transform.localPosition = new Vector3(0f, -0.42f, 0f);
            shadow.transform.localScale = new Vector3(0.85f, 0.28f, 1f);

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            var body = CreateSprite("Body", visual, circle, spriteMaterial, Color.white, 0);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(visual, false);
            var label = labelGo.AddComponent<TextMeshPro>();
            label.font = _font;
            label.text = "1";
            label.fontSize = 5f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.rectTransform.sizeDelta = new Vector2(1f, 1f);
            label.sortingOrder = 1;

            Set(view, "visual", visual);
            Set(view, "body", body);
            Set(view, "label", label);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<CatView>();
        }

        // ---------- coin popup prefab ----------

        private static CoinPopup CreateCoinPopupPrefab(Sprite circle, Material spriteMaterial)
        {
            // Above every walking cat, below the one being dragged (CatView uses 30000 for it).
            var root = new GameObject("CoinPopup", typeof(SortingGroup));
            root.GetComponent<SortingGroup>().sortingOrder = 20000;
            var popup = root.AddComponent<CoinPopup>();

            var content = new GameObject("Content").transform;
            content.SetParent(root.transform, false);

            var coin = new GameObject("Coin").transform;
            coin.SetParent(content, false);
            coin.localScale = Vector3.one * 0.34f; // sprite is 1 unit, so this is the coin's width
            var rim = CreateSprite("Rim", coin, circle, spriteMaterial, CoinRimColor, 0);
            var face = CreateSprite("Face", coin, circle, spriteMaterial, CoinColor, 1);
            face.transform.localScale = Vector3.one * 0.72f;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(content, false);
            var label = labelGo.AddComponent<TextMeshPro>();
            label.font = _font;
            label.text = "+1";
            label.fontSize = 4f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Left;
            label.enableWordWrapping = false;
            label.color = PopupTextColor;
            label.rectTransform.pivot = new Vector2(0f, 0.5f);
            label.rectTransform.sizeDelta = new Vector2(3f, 0.6f);
            label.sortingOrder = 2;

            Set(popup, "content", content);
            Set(popup, "coin", coin);
            SetArray(popup, "coinSprites", rim, face);
            Set(popup, "label", label);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, CoinPopupPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<CoinPopup>();
        }

        private static SpriteRenderer CreateSprite(string name, Transform parent, Sprite sprite, Material material, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            if (material != null) sr.sharedMaterial = material;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        // ---------- assets ----------

        private static Sprite CreateSpriteAsset(string path, int size, bool circle)
        {
            if (!File.Exists(path))
            {
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color32[size * size];
                float r = size * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float a = 1f;
                        if (circle)
                        {
                            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                            a = Mathf.Clamp01(r - d); // 1px anti-aliased edge
                        }
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                }
                tex.SetPixels32(pixels);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = size; // exactly 1 world unit across
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = circle ? FilterMode.Bilinear : FilterMode.Point;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>White 1-unit circle used as the placeholder cat body.</summary>
        public static Sprite LoadOrCreateCircleSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath);
            if (existing != null) return existing;
            Directory.CreateDirectory(Path.GetDirectoryName(CirclePath));
            return CreateSpriteAsset(CirclePath, 256, circle: true);
        }

        // ---------- helpers ----------

        private static bool HasTmpEssentials() => Resources.Load<TMP_Settings>("TMP Settings") != null;

        private static void ImportTmpEssentialsThen(Action next)
        {
            void OnCompleted(string _)
            {
                Unsubscribe();
                EditorApplication.delayCall += () => next();
            }

            void OnFailed(string _, string error)
            {
                Unsubscribe();
                Debug.LogError($"[CatMerge] TMP Essential Resources import failed: {error}. " +
                               "Import via Window/TextMeshPro/Import TMP Essential Resources, then run Tools/CatMerge/Build Game Scene.");
            }

            void Unsubscribe()
            {
                AssetDatabase.importPackageCompleted -= OnCompleted;
                AssetDatabase.importPackageFailed -= OnFailed;
            }

            Debug.Log("[CatMerge] Importing TMP Essential Resources...");
            AssetDatabase.importPackageCompleted += OnCompleted;
            AssetDatabase.importPackageFailed += OnFailed;
            AssetDatabase.ImportPackage(Path.GetFullPath(TmpEssentialsPackage), false);
        }

        private static void WarnIfOldInputOnly()
        {
            var settings = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
            var handler = new SerializedObject(settings).FindProperty("activeInputHandler");
            if (handler != null && handler.intValue == 0)
            {
                Debug.LogWarning("[CatMerge] Active Input Handling is 'Input Manager (Old)'. Switch it to " +
                                 "'Input System Package (New)' in Project Settings/Player and restart the editor, " +
                                 "otherwise dragging cats won't work.");
            }
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[CatMerge] Field '{field}' not found on {target.GetType().Name}");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(Object target, string field, params Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null || !prop.isArray)
            {
                Debug.LogError($"[CatMerge] Array field '{field}' not found on {target.GetType().Name}");
                return;
            }
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
