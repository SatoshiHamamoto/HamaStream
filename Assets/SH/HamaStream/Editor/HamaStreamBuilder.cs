using System.Collections.Generic;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDK3.Video.Components.AVPro;
using VRC.SDKBase;
using VRC.Udon;

public static class HamaStreamBuilder
{
    private const int MaxPlaylistItems = 10;

    private const string SourcePrefabPath =
        "Packages/com.vrchat.worlds/Samples/UdonExampleScene/Prefabs/VideoPlayers/UdonSyncPlayer (AVPro).prefab";

    private const string RootFolder = "Assets/SH/HamaStream";
    private const string PrefabFolder = RootFolder + "/Prefabs";
    private const string MaterialFolder = RootFolder + "/Materials";
    private const string RenderTextureFolder = RootFolder + "/RenderTextures";
    private const string PrefabPath = PrefabFolder + "/HamaStream.prefab";
    private const string MaterialPath = MaterialFolder + "/HamaStreamScreen.mat";
    private const string DisplayMaterialPath = MaterialFolder + "/HamaStreamDisplay.mat";
    private const string VideoBlitMaterialPath = MaterialFolder + "/HamaStreamVideoBlit.mat";
    private const string DisplayShaderPath =
        MaterialFolder + "/HamaStreamVideoScreen.shader";
    private const string BlitShaderPath =
        "Packages/dev.architech.protv/Resources/Shaders/ProTVBlit.shader";
    private const string OutputRenderTexturePath =
        RenderTextureFolder + "/HamaStreamOutput.renderTexture";
    private const float ContentScale = 0.25f;
    private const float ControlsGap = 0.42f;
    private const float PlaylistControlsGap = 0.65f;
    private const float SecondPlaylistGap = 0.45f;
    private const float PlaylistControlsVerticalOffset = 1.54f;

    [MenuItem("Tools/HamaStream/Rebuild Minimal Prefab")]
    public static void RebuildMinimalPrefab()
    {
        UdonSharpProgramAsset programAsset =
            UdonSharpEditorUtility.GetUdonSharpProgramAsset(typeof(HamaStreamController));
        if (programAsset == null)
        {
            Debug.LogError(
                "[HamaStream] HamaStreamController.asset is missing or is not linked to HamaStreamController.cs.");
            return;
        }

        UdonSharpProgramAsset playlistProgramAsset =
            UdonSharpEditorUtility.GetUdonSharpProgramAsset(typeof(HamaStreamPlaylistController));
        if (playlistProgramAsset == null)
        {
            Debug.LogError(
                "[HamaStream] HamaStreamPlaylistController.asset is missing or is not linked to HamaStreamPlaylistController.cs.");
            return;
        }

        if (programAsset.ScriptVersion < UdonSharpProgramVersion.CurrentVersion ||
            programAsset.CompiledVersion < UdonSharpProgramVersion.CurrentVersion)
        {
            Debug.LogError(
                "[HamaStream] HamaStreamController.asset is not compiled yet. Wait for UdonSharp compilation, then run the builder again.");
            return;
        }

        if (playlistProgramAsset.ScriptVersion < UdonSharpProgramVersion.CurrentVersion ||
            playlistProgramAsset.CompiledVersion < UdonSharpProgramVersion.CurrentVersion)
        {
            Debug.LogError(
                "[HamaStream] HamaStreamPlaylistController.asset is not compiled yet. Wait for UdonSharp compilation, then run the builder again.");
            return;
        }

        EnsureFolder(PrefabFolder);
        EnsureFolder(MaterialFolder);
        EnsureFolder(RenderTextureFolder);

        GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
        if (sourcePrefab == null)
        {
            Debug.LogError("[HamaStream] VRChat AVPro sample prefab was not found.");
            return;
        }

        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        HamaStreamController existingController = existingPrefab != null
            ? existingPrefab.GetComponent<HamaStreamController>() : null;
        Texture standbyTexture = existingController != null ? existingController.StandbyTexture : null;
        bool autoPlayOnStart = existingController != null && existingController.AutoPlayOnStart;
        VRCUrl autoPlayUrl = existingController != null && existingController.AutoPlayUrl != null
            ? existingController.AutoPlayUrl : new VRCUrl("");

        GameObject root = PrefabUtility.LoadPrefabContents(SourcePrefabPath);
        try
        {
            BuildPrefabContents(root, standbyTexture, autoPlayOnStart, autoPlayUrl);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        catch
        {
            PrefabUtility.UnloadPrefabContents(root);
            throw;
        }

        // AddUdonSharpComponent schedules backing behaviour setup for the next editor tick.
        // Keep the prefab contents alive until that setup has finished.
        EditorApplication.delayCall += () =>
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        };

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log("[HamaStream] Minimal prefab rebuilt: " + PrefabPath);
    }

    [MenuItem("Tools/HamaStream/Add Minimal Player To Scene")]
    public static void AddMinimalPlayerToScene()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            RebuildMinimalPrefab();
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        if (prefab == null)
        {
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, "Add HamaStream player");
        HamaStreamController controller = instance.GetComponent<HamaStreamController>();
        if (controller != null)
        {
            ConfigurePlaylistsForRoot(controller);
        }
        ConfigureSceneUrlLoadPeers();
        Selection.activeGameObject = instance;
    }

    [MenuItem("Tools/HamaStream/Add Playlist To Selected Player")]
    public static void AddPlaylistToSelectedPlayer()
    {
        HamaStreamController controller = Selection.activeGameObject == null
            ? null : Selection.activeGameObject.GetComponentInParent<HamaStreamController>();
        if (controller == null || EditorUtility.IsPersistent(controller) ||
            EditorApplication.isPlayingOrWillChangePlaymode ||
            PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            Debug.LogError("[HamaStream] Select a HamaStream player in Edit Mode.");
            return;
        }

        HamaStreamPlaylistController[] existing =
            controller.GetComponentsInChildren<HamaStreamPlaylistController>(true);
        HamaStreamPlaylistController template = existing.Length > 0
            ? existing[existing.Length - 1] : null;
        if (template == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null)
            {
                template = prefab.GetComponentInChildren<HamaStreamPlaylistController>(true);
            }
        }

        if (template == null)
        {
            Debug.LogError("[HamaStream] Rebuild the minimal prefab before adding a playlist.");
            return;
        }

        Transform parent = existing.Length > 0
            ? existing[existing.Length - 1].transform.parent
            : controller.transform.Find("Content");
        if (parent == null)
        {
            Debug.LogError("[HamaStream] Player Content object is missing.");
            return;
        }

        GameObject module = Object.Instantiate(template.gameObject, parent, false);
        Undo.RegisterCreatedObjectUndo(module, "Add HamaStream playlist");
        module.transform.SetAsLastSibling();
        HamaStreamPlaylistController playlist = module.GetComponent<HamaStreamPlaylistController>();
        playlist.PlaylistDisplayNames = new string[0];
        playlist.PlaylistUrls = new VRCUrl[0];
        if (existing.Length > 0)
        {
            PlacePlaylistControlsToRightOfPlaylist(
                module.GetComponent<RectTransform>(),
                existing[existing.Length - 1].GetComponent<RectTransform>());
        }

        ConfigurePlaylistsForRoot(controller);
        Selection.activeGameObject = module;
    }

    [MenuItem("Tools/HamaStream/Remove Selected Playlist")]
    public static void RemoveSelectedPlaylist()
    {
        HamaStreamPlaylistController playlist = Selection.activeGameObject == null
            ? null : Selection.activeGameObject.GetComponentInParent<HamaStreamPlaylistController>();
        if (playlist == null || EditorUtility.IsPersistent(playlist) ||
            EditorApplication.isPlayingOrWillChangePlaymode ||
            PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            Debug.LogError("[HamaStream] Select a PlaylistControls module in Edit Mode.");
            return;
        }

        HamaStreamController controller = playlist.GetComponentInParent<HamaStreamController>();
        if (controller == null || playlist.gameObject == controller.gameObject)
        {
            Debug.LogError("[HamaStream] The selected playlist is not a child module.");
            return;
        }

        Undo.DestroyObjectImmediate(playlist.gameObject);
        ConfigurePlaylistsForRoot(controller);
        Selection.activeGameObject = controller.gameObject;
    }

    [MenuItem("Tools/HamaStream/Configure Scene")]
    public static void ConfigureScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() ||
            !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene) ||
            PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            Debug.LogError("[HamaStream] Open the world scene in Edit Mode before configuring the scene.");
            return;
        }

        var controllers = new List<HamaStreamController>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            controllers.AddRange(root.GetComponentsInChildren<HamaStreamController>(true));
        }

        HamaStreamController[] peers = controllers.ToArray();
        foreach (HamaStreamController controller in peers)
        {
            ConfigurePlaylistsForRoot(controller);
        }
        ConfigureUrlLoadPeers(peers);

        if (peers.Length > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }
        Debug.Log("[HamaStream] Configured playlists and URL load peers for " + peers.Length +
                  " players in scene " + scene.name + ". Save the scene.");
    }

    private static void ConfigurePlaylistsForRoot(HamaStreamController controller)
    {
        HamaStreamPlaylistController[] playlists =
            controller.GetComponentsInChildren<HamaStreamPlaylistController>(true);
        Undo.RecordObject(controller, "Configure HamaStream playlists");
        controller.PlaylistControllers = playlists;
        for (int index = 0; index < playlists.Length; index++)
        {
            HamaStreamPlaylistController playlist = playlists[index];
            Undo.RecordObject(playlist, "Configure HamaStream playlists");
            Undo.RecordObject(playlist.gameObject, "Configure HamaStream playlists");
            playlist.MainController = controller;
            playlist.PlaylistNumber = index + 1;
            playlist.gameObject.name = "PlaylistControls" + (index + 1);
            Text header = playlist.transform.Find("PlaylistHeader")?.GetComponent<Text>();
            if (header != null)
            {
                Undo.RecordObject(header, "Configure HamaStream playlists");
                header.text = "Playlist " + (index + 1);
                PrefabUtility.RecordPrefabInstancePropertyModifications(header);
            }

            UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(playlist);
            if (backing != null)
            {
                Undo.RecordObject(backing, "Configure HamaStream playlists");
                backing.SyncMethod = VRC.SDKBase.Networking.SyncType.None;
                RebindPlaylistButtons(playlist, backing);
                PrefabUtility.RecordPrefabInstancePropertyModifications(backing);
            }
            UdonSharpEditorUtility.CopyProxyToUdon(playlist);
            PrefabUtility.RecordPrefabInstancePropertyModifications(playlist);
            PrefabUtility.RecordPrefabInstancePropertyModifications(playlist.gameObject);
            EditorUtility.SetDirty(playlist);
        }

        UdonSharpEditorUtility.CopyProxyToUdon(controller);
        PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        EditorUtility.SetDirty(controller);
    }

    private static void RebindPlaylistButtons(
        HamaStreamPlaylistController playlist, UdonBehaviour backing)
    {
        if (playlist.PlaylistButtons == null)
        {
            return;
        }

        for (int index = 0; index < playlist.PlaylistButtons.Length; index++)
        {
            Button button = playlist.PlaylistButtons[index];
            if (button == null)
            {
                continue;
            }

            bool hasModuleListener = false;
            for (int listener = 0; listener < button.onClick.GetPersistentEventCount(); listener++)
            {
                if (button.onClick.GetPersistentTarget(listener) == backing &&
                    button.onClick.GetPersistentMethodName(listener) == "SendCustomEvent")
                {
                    hasModuleListener = true;
                    break;
                }
            }
            if (hasModuleListener)
            {
                continue;
            }

            Undo.RecordObject(button, "Configure HamaStream playlists");
            for (int listener = button.onClick.GetPersistentEventCount() - 1;
                 listener >= 0; listener--)
            {
                if (button.onClick.GetPersistentTarget(listener) is UdonBehaviour &&
                    button.onClick.GetPersistentMethodName(listener) == "SendCustomEvent")
                {
                    UnityEventTools.RemovePersistentListener(button.onClick, listener);
                }
            }
            UnityEventTools.AddStringPersistentListener(
                button.onClick, backing.SendCustomEvent, GetPlaylistEventName(index));
            PrefabUtility.RecordPrefabInstancePropertyModifications(button);
        }
    }

    [MenuItem("Tools/HamaStream/Add Standalone Resync Controls To Prefab")]
    public static void AddStandaloneResyncControlsToPrefab()
    {
        const string resyncAssetPath = RootFolder + "/Runtime/HamaStreamResyncCube.asset";
        if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(resyncAssetPath) == null)
        {
            UdonSharpProgramAsset program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            program.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
                RootFolder + "/Runtime/HamaStreamResyncCube.cs");
            AssetDatabase.CreateAsset(program, resyncAssetPath);
        }
        UdonSharpProgramAsset.CompileAllCsPrograms(true);

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            HamaStreamController controller = root.GetComponent<HamaStreamController>();
            Canvas sourceCanvas = null;
            foreach (Canvas candidate in root.GetComponentsInChildren<Canvas>(true))
            {
                if (candidate.name == "Controls")
                {
                    sourceCanvas = candidate;
                    break;
                }
            }
            if (controller == null || sourceCanvas == null)
            {
                Debug.LogError("[HamaStream] Main controller or Controls canvas is missing.");
                return;
            }

            UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(controller);
            CreateStandaloneResyncControls(root, sourceCanvas, controller, backing);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureSceneUrlLoadPeers()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() ||
            !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene) ||
            PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            Debug.LogError("[HamaStream] Open the world scene in Edit Mode before configuring URL load peers.");
            return;
        }

        var controllers = new List<HamaStreamController>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            controllers.AddRange(root.GetComponentsInChildren<HamaStreamController>(true));
        }

        HamaStreamController[] peers = controllers.ToArray();
        ConfigureUrlLoadPeers(peers);

        if (peers.Length > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }

        Debug.Log("[HamaStream] Configured URL load peers for " + peers.Length +
                  " players in scene " + scene.name + ". Save the scene after checking the references.");
    }

    private static void ConfigureUrlLoadPeers(HamaStreamController[] peers)
    {
        foreach (HamaStreamController controller in peers)
        {
            UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(controller);
            Undo.RecordObject(controller, "Configure HamaStream URL load peers");
            if (backing != null)
            {
                Undo.RecordObject(backing, "Configure HamaStream URL load peers");
            }

            controller.UrlLoadPeers = peers;
            controller.UrlLoadCooldownSeconds = Mathf.Max(5f, controller.UrlLoadCooldownSeconds);
            UdonSharpEditorUtility.CopyProxyToUdon(controller);
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
            EditorUtility.SetDirty(controller);
            if (backing != null)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(backing);
                EditorUtility.SetDirty(backing);
            }
        }
    }

    private static void BuildPrefabContents(
        GameObject root, Texture standbyTexture, bool autoPlayOnStart, VRCUrl autoPlayUrl)
    {
        root.name = "HamaStream";
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        BoxCollider playbackArea = PreparePlaybackArea(root);

        foreach (UdonBehaviour behaviour in root.GetComponentsInChildren<UdonBehaviour>(true))
        {
            Object.DestroyImmediate(behaviour);
        }

        VRCAVProVideoPlayer videoPlayer = root.GetComponentInChildren<VRCAVProVideoPlayer>(true);
        VRCAVProVideoSpeaker videoSpeaker = root.GetComponentInChildren<VRCAVProVideoSpeaker>(true);
        VRCUrlInputField urlInput = root.GetComponentInChildren<VRCUrlInputField>(true);
        VRCAVProVideoScreen videoScreen = root.GetComponentInChildren<VRCAVProVideoScreen>(true);

        if (videoPlayer == null || videoSpeaker == null || urlInput == null || videoScreen == null)
        {
            throw new MissingReferenceException("The VRChat AVPro sample prefab structure has changed.");
        }

        SerializedObject videoPlayerSettings = new SerializedObject(videoPlayer);
        videoPlayerSettings.FindProperty("maximumResolution").intValue = 4320;
        videoPlayerSettings.FindProperty("useLowLatency").boolValue = true;
        videoPlayerSettings.ApplyModifiedPropertiesWithoutUndo();

        CopyScreenMaterial(videoScreen);
        Transform screen = videoScreen.transform;
        screen.localPosition = new Vector3(0f, 5f, 0f);
        screen.localScale = new Vector3(15f, 8.474577f, 65.70509f);
        MeshRenderer displayScreenRenderer = CreateDisplayScreen(videoScreen);
        videoScreen.GetComponent<MeshRenderer>().enabled = false;
        PrepareStereoSpeakers(videoSpeaker, videoPlayer);
        PrepareUrlInput(urlInput);

        Canvas canvas = urlInput.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            throw new MissingReferenceException("The URL input Canvas was not found.");
        }

        canvas.gameObject.name = "Controls";
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(480f, 200f);
        BoxCollider controlsCollider = canvas.GetComponent<BoxCollider>();
        if (controlsCollider == null)
        {
            controlsCollider = canvas.gameObject.AddComponent<BoxCollider>();
        }

        controlsCollider.center = Vector3.zero;
        controlsCollider.size = new Vector3(480f, 200f, 1f);
        controlsCollider.isTrigger = true;

        if (canvas.GetComponent<CanvasRenderer>() == null)
        {
            canvas.gameObject.AddComponent<CanvasRenderer>();
        }

        Image controlsBackground = canvas.GetComponent<Image>();
        if (controlsBackground == null)
        {
            controlsBackground = canvas.gameObject.AddComponent<Image>();
        }

        controlsBackground.sprite = CreateUiResources().background;
        controlsBackground.type = Image.Type.Sliced;
        controlsBackground.color = new Color(0.3584906f, 0.3584906f, 0.3584906f, 1f);
        PlaceControlsToLeft(canvasRect, videoScreen);

        Button playButton = CreatePlayButton(canvas.transform);
        Button pauseButton = CreateTransportButton(
            canvas.transform,
            "PauseButton",
            "Pause",
            new Vector2(231.1f, 18.9f));
        Button stopButton = CreateTransportButton(
            canvas.transform,
            "StopButton",
            "Stop",
            new Vector2(129.6f, 21.7f));
        Button resyncButton = CreateTransportButton(
            canvas.transform,
            "ResyncButton",
            "Resync",
            new Vector2(298.4f, 18.9f));
        Toggle loopToggle = CreateLoopToggle(canvas.transform);
        Text statusText = CreateStatusText(canvas.transform);
        Text currentUrlText = CreateCurrentUrlText(canvas.transform);
        Text ownerText = CreateOwnerText(canvas.transform);
        CreateVersionTag(canvas.transform);
        Canvas playlistCanvas1 = CreatePlaylistCanvas(canvas, "PlaylistControls1");
        PlacePlaylistControlsToRightOfScreen(
            playlistCanvas1.GetComponent<RectTransform>(), videoScreen, canvasRect);
        CreatePlaylistHeader(playlistCanvas1.transform, "Playlist 1");
        Text[] playlistDisplayNameLabels1;
        Text[] playlistUrlLabels1;
        Button[] playlistButtons1 = CreatePlaylistItems(
            playlistCanvas1.transform,
            out playlistDisplayNameLabels1,
            out playlistUrlLabels1);

        Canvas playlistCanvas2 = CreatePlaylistCanvas(canvas, "PlaylistControls2");
        PlacePlaylistControlsToRightOfPlaylist(
            playlistCanvas2.GetComponent<RectTransform>(),
            playlistCanvas1.GetComponent<RectTransform>());
        CreatePlaylistHeader(playlistCanvas2.transform, "Playlist 2");
        Text[] playlistDisplayNameLabels2;
        Text[] playlistUrlLabels2;
        Button[] playlistButtons2 = CreatePlaylistItems(
            playlistCanvas2.transform,
            out playlistDisplayNameLabels2,
            out playlistUrlLabels2);

        HamaStreamController controller = root.AddUdonSharpComponent<HamaStreamController>();
        HamaStreamPlaylistController playlistController1 =
            playlistCanvas1.gameObject.AddUdonSharpComponent<HamaStreamPlaylistController>();
        HamaStreamPlaylistController playlistController2 =
            playlistCanvas2.gameObject.AddUdonSharpComponent<HamaStreamPlaylistController>();
        controller.VideoPlayer = videoPlayer;
        controller.VideoScreenRenderer = videoScreen.GetComponent<MeshRenderer>();
        controller.DisplayScreenRenderer = displayScreenRenderer;
        controller.VideoBlitMaterial = GetVideoBlitMaterial();
        controller.StandbyTexture = standbyTexture;
        controller.AutoPlayOnStart = autoPlayOnStart;
        controller.AutoPlayUrl = autoPlayUrl;
        RenderTexture outputTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(
            OutputRenderTexturePath);
        if (outputTexture == null)
        {
            outputTexture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
            outputTexture.name = "HamaStreamOutput";
            AssetDatabase.CreateAsset(outputTexture, OutputRenderTexturePath);
        }
        controller.OutputRenderTexture = outputTexture;
        controller.UrlInput = urlInput;
        controller.LoopToggle = loopToggle;
        controller.StatusText = statusText;
        controller.CurrentUrlText = currentUrlText;
        controller.OwnerText = ownerText;
        controller.PlaylistControllers = new[] { playlistController1, playlistController2 };
        controller.PlaybackArea = playbackArea;
        controller.UrlLoadCooldownSeconds = 5f;
        controller.UrlLoadPeers = new[] { controller };
        playlistController1.MainController = controller;
        playlistController1.PlaylistNumber = 1;
        playlistController1.PlaylistButtons = playlistButtons1;
        playlistController1.PlaylistDisplayNameLabels = playlistDisplayNameLabels1;
        playlistController1.PlaylistUrlLabels = playlistUrlLabels1;
        playlistController2.MainController = controller;
        playlistController2.PlaylistNumber = 2;
        playlistController2.PlaylistButtons = playlistButtons2;
        playlistController2.PlaylistDisplayNameLabels = playlistDisplayNameLabels2;
        playlistController2.PlaylistUrlLabels = playlistUrlLabels2;

        UdonSharpEditorUtility.CopyProxyToUdon(controller);
        UdonSharpEditorUtility.CopyProxyToUdon(playlistController1);
        UdonSharpEditorUtility.CopyProxyToUdon(playlistController2);
        UdonBehaviour backingBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour(controller);
        UdonBehaviour playlistBackingBehaviour1 =
            UdonSharpEditorUtility.GetBackingUdonBehaviour(playlistController1);
        UdonBehaviour playlistBackingBehaviour2 =
            UdonSharpEditorUtility.GetBackingUdonBehaviour(playlistController2);
        backingBehaviour.SyncMethod = VRC.SDKBase.Networking.SyncType.Manual;
        playlistBackingBehaviour1.SyncMethod = VRC.SDKBase.Networking.SyncType.None;
        playlistBackingBehaviour2.SyncMethod = VRC.SDKBase.Networking.SyncType.None;
        UnityEventTools.AddStringPersistentListener(
            playButton.onClick,
            backingBehaviour.SendCustomEvent,
            nameof(HamaStreamController.PlayFromInput));
        UnityEventTools.AddStringPersistentListener(
            urlInput.onEndEdit,
            backingBehaviour.SendCustomEvent,
            nameof(HamaStreamController.PlayFromSubmittedUrl));
        UnityEventTools.AddStringPersistentListener(
            pauseButton.onClick,
            backingBehaviour.SendCustomEvent,
            nameof(HamaStreamController.PauseLocal));
        UnityEventTools.AddStringPersistentListener(
            stopButton.onClick,
            backingBehaviour.SendCustomEvent,
            nameof(HamaStreamController.GlobalStop));
        UnityEventTools.AddStringPersistentListener(
            resyncButton.onClick,
            backingBehaviour.SendCustomEvent,
            nameof(HamaStreamController.ResyncLocal));
        CreateStandaloneResyncControls(root, canvas, controller, backingBehaviour);
        UnityEventTools.AddStringPersistentListener(
            loopToggle.onValueChanged,
            backingBehaviour.SendCustomEvent,
            nameof(HamaStreamController.LoopToggleChanged));

        for (int index = 0; index < playlistButtons1.Length; index++)
        {
            UnityEventTools.AddStringPersistentListener(
                playlistButtons1[index].onClick,
                playlistBackingBehaviour1.SendCustomEvent,
                GetPlaylistEventName(index));
            UnityEventTools.AddStringPersistentListener(
                playlistButtons2[index].onClick,
                playlistBackingBehaviour2.SendCustomEvent,
                GetPlaylistEventName(index));
        }

        WrapContent(root);
    }

    private static BoxCollider PreparePlaybackArea(GameObject root)
    {
        BoxCollider playbackArea = root.GetComponent<BoxCollider>();
        if (playbackArea == null)
        {
            playbackArea = root.AddComponent<BoxCollider>();
        }

        playbackArea.isTrigger = true;
        playbackArea.center = new Vector3(0f, 2f, -4f);
        playbackArea.size = new Vector3(15f, 4f, 15f);
        return playbackArea;
    }

    private static void PlaceControlsToLeft(
        RectTransform controls,
        VRCAVProVideoScreen videoScreen)
    {
        Renderer screenRenderer = videoScreen.GetComponent<Renderer>();
        if (screenRenderer == null)
        {
            throw new MissingReferenceException("The AVPro video screen Renderer was not found.");
        }

        Canvas.ForceUpdateCanvases();
        float controlsWidth = controls.rect.width * Mathf.Abs(controls.lossyScale.x);
        Vector3 position = controls.position;
        position.x = screenRenderer.bounds.min.x - ControlsGap - controlsWidth * 0.5f;
        position.y = 3.41f;
        controls.position = position;
    }

    private static Canvas CreatePlaylistCanvas(Canvas sourceCanvas, string canvasName)
    {
        GameObject canvasObject = new GameObject(
            canvasName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(VRCUiShape),
            typeof(GraphicRaycaster));
        canvasObject.layer = sourceCanvas.gameObject.layer;
        canvasObject.transform.SetParent(sourceCanvas.transform.parent, false);

        RectTransform rect = canvasObject.GetComponent<RectTransform>();
        rect.localRotation = sourceCanvas.transform.localRotation;
        rect.localScale = sourceCanvas.transform.localScale;
        rect.sizeDelta = new Vector2(300f, 320f);

        BoxCollider collider = canvasObject.AddComponent<BoxCollider>();
        collider.size = new Vector3(300f, 320f, 1f);
        Image background = canvasObject.AddComponent<Image>();
        background.sprite = CreateUiResources().background;
        background.type = Image.Type.Sliced;
        background.color = new Color(0.3584906f, 0.3584906f, 0.3584906f, 1f);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = sourceCanvas.renderMode;
        canvas.worldCamera = sourceCanvas.worldCamera;
        canvas.planeDistance = sourceCanvas.planeDistance;
        canvas.sortingLayerID = sourceCanvas.sortingLayerID;
        canvas.sortingOrder = sourceCanvas.sortingOrder;

        CanvasScaler sourceScaler = sourceCanvas.GetComponent<CanvasScaler>();
        CanvasScaler playlistScaler = canvasObject.GetComponent<CanvasScaler>();
        if (sourceScaler != null)
        {
            playlistScaler.uiScaleMode = sourceScaler.uiScaleMode;
            playlistScaler.referencePixelsPerUnit = sourceScaler.referencePixelsPerUnit;
            playlistScaler.scaleFactor = sourceScaler.scaleFactor;
            playlistScaler.referenceResolution = sourceScaler.referenceResolution;
            playlistScaler.screenMatchMode = sourceScaler.screenMatchMode;
            playlistScaler.matchWidthOrHeight = sourceScaler.matchWidthOrHeight;
            playlistScaler.physicalUnit = sourceScaler.physicalUnit;
            playlistScaler.fallbackScreenDPI = sourceScaler.fallbackScreenDPI;
            playlistScaler.defaultSpriteDPI = sourceScaler.defaultSpriteDPI;
            playlistScaler.dynamicPixelsPerUnit = sourceScaler.dynamicPixelsPerUnit;
        }

        return canvas;
    }

    private static void CreateStandaloneResyncControls(
        GameObject root, Canvas sourceCanvas, HamaStreamController controller,
        UdonBehaviour backing)
    {
        foreach (Canvas candidate in root.GetComponentsInChildren<Canvas>(true))
        {
            if (candidate.name == "ResyncOnlyControls")
            {
                HamaStreamResyncCube existingCube = root.GetComponentInChildren<HamaStreamResyncCube>(true);
                if (existingCube != null)
                {
                    existingCube.GetComponent<BoxCollider>().isTrigger = false;
                    existingCube.Controller = controller;
                    UdonSharpEditorUtility.CopyProxyToUdon(existingCube);
                    UdonSharpEditorUtility.GetBackingUdonBehaviour(existingCube).interactText = "Resync";
                }
                return;
            }
        }

        Canvas canvas = CreatePlaylistCanvas(sourceCanvas, "ResyncOnlyControls");
        RectTransform rect = canvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(120f, 65f);
        rect.localPosition = sourceCanvas.transform.localPosition +
                             new Vector3(-2.36f, 3.58f, 0f);
        canvas.GetComponent<BoxCollider>().size = new Vector3(120f, 65f, 1f);

        Button button = CreateTransportButton(
            canvas.transform, "ResyncOnlyButton", "Resync", Vector2.zero);
        RectTransform buttonRect = button.GetComponent<RectTransform>();
        buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(95f, 35f);
        UnityEventTools.AddStringPersistentListener(
            button.onClick, backing.SendCustomEvent,
            nameof(HamaStreamController.ResyncLocal));

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "ResyncCube";
        cube.transform.SetParent(root.transform, false);
        cube.transform.localPosition = sourceCanvas.transform.localPosition +
                                       new Vector3(3.15f, 3.48f, 0f);
        cube.transform.localScale = Vector3.one;
        HamaStreamResyncCube trigger = cube.AddUdonSharpComponent<HamaStreamResyncCube>();
        trigger.Controller = controller;
        UdonSharpEditorUtility.CopyProxyToUdon(trigger);
        UdonSharpEditorUtility.GetBackingUdonBehaviour(trigger).interactText = "Resync";
    }

    private static void PlacePlaylistControlsToRightOfScreen(
        RectTransform playlistControls,
        VRCAVProVideoScreen videoScreen,
        RectTransform mainControls)
    {
        Renderer screenRenderer = videoScreen.GetComponent<Renderer>();
        Canvas.ForceUpdateCanvases();
        float playlistWidth =
            playlistControls.rect.width * Mathf.Abs(playlistControls.lossyScale.x);
        Vector3 position = mainControls.position;
        position.x = screenRenderer.bounds.max.x + PlaylistControlsGap +
                     playlistWidth * 0.5f;
        position.y += PlaylistControlsVerticalOffset;
        playlistControls.position = position;
    }

    private static void PlacePlaylistControlsToRightOfPlaylist(
        RectTransform playlistControls,
        RectTransform firstPlaylist)
    {
        Canvas.ForceUpdateCanvases();
        float playlistWidth =
            playlistControls.rect.width * Mathf.Abs(playlistControls.lossyScale.x);
        float firstWidth = firstPlaylist.rect.width * Mathf.Abs(firstPlaylist.lossyScale.x);
        Vector3 position = firstPlaylist.position;
        position.x += firstWidth * 0.5f + SecondPlaylistGap +
                      playlistWidth * 0.5f;
        playlistControls.position = position;
    }

    private static void PrepareStereoSpeakers(
        VRCAVProVideoSpeaker sourceSpeaker,
        VRCAVProVideoPlayer videoPlayer)
    {
        Transform source = sourceSpeaker.transform;
        GameObject audioSources = new GameObject("Audio Sources");
        Transform group = audioSources.transform;
        group.SetParent(source.parent, false);
        source.SetParent(group, false);
        source.name = "Audio Source R";
        source.localPosition = new Vector3(5f, 0f, 0f);

        GameObject left = Object.Instantiate(source.gameObject, group, false);
        left.name = "Audio Source L";
        left.transform.localPosition = new Vector3(-5f, 0f, 0f);

        ConfigureSpeaker(sourceSpeaker, videoPlayer, 2);
        ConfigureSpeaker(left.GetComponent<VRCAVProVideoSpeaker>(), videoPlayer, 1);
    }

    private static void ConfigureSpeaker(
        VRCAVProVideoSpeaker speaker,
        VRCAVProVideoPlayer videoPlayer,
        int mode)
    {
        SerializedObject serializedSpeaker = new SerializedObject(speaker);
        serializedSpeaker.FindProperty("videoPlayer").objectReferenceValue = videoPlayer;
        serializedSpeaker.FindProperty("mode").enumValueIndex = mode;
        serializedSpeaker.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WrapContent(GameObject root)
    {
        Transform rootTransform = root.transform;
        int childCount = rootTransform.childCount;
        Transform[] children = new Transform[childCount];

        for (int index = 0; index < childCount; index++)
        {
            children[index] = rootTransform.GetChild(index);
        }

        GameObject contentObject = new GameObject("Content");
        Transform content = contentObject.transform;
        content.SetParent(rootTransform, false);

        foreach (Transform child in children)
        {
            child.SetParent(content, false);
        }

        content.localScale = Vector3.one * ContentScale;
    }

    private static void PrepareUrlInput(VRCUrlInputField urlInput)
    {
        urlInput.gameObject.name = "URLInput";
        DisableNavigation(urlInput);

        SerializedObject serializedInput = new SerializedObject(urlInput);
        ClearPersistentCalls(serializedInput.FindProperty("m_OnEndEdit"));
        ClearPersistentCalls(serializedInput.FindProperty("m_OnValueChanged"));
        serializedInput.ApplyModifiedPropertiesWithoutUndo();

        RectTransform inputRect = urlInput.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0f, 0.5f);
        inputRect.anchorMax = new Vector2(1f, 0.5f);
        inputRect.pivot = new Vector2(0.5f, 0.5f);
        inputRect.anchoredPosition = new Vector2(0f, 61.7f);
        inputRect.sizeDelta = new Vector2(-60f, 30f);

        Transform placeholderTransform = urlInput.transform.Find("Placeholder");
        if (placeholderTransform != null && placeholderTransform.TryGetComponent(out Text placeholder))
        {
            placeholder.text = "Video or live stream URL";
        }
    }

    private static Button CreatePlayButton(Transform parent)
    {
        GameObject buttonObject = DefaultControls.CreateButton(CreateUiResources());
        buttonObject.name = "PlayButton";
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(30f, 21.7f);
        rect.sizeDelta = new Vector2(90f, 30f);

        Text label = buttonObject.GetComponentInChildren<Text>();
        label.text = "Play";
        label.fontSize = 17;
        label.color = Color.green;

        Button button = buttonObject.GetComponent<Button>();
        SetButtonColors(button, 0.238993645f, 0.666666567f,
            0.465408742f, 0.735849f);
        DisableNavigation(button);
        return button;
    }

    private static Button CreateTransportButton(
        Transform parent,
        string objectName,
        string labelText,
        Vector2 anchoredPosition)
    {
        GameObject buttonObject = DefaultControls.CreateButton(CreateUiResources());
        buttonObject.name = objectName;
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        bool emphasized = objectName == "StopButton";
        rect.sizeDelta = emphasized ? new Vector2(90f, 30f) :
            objectName == "PauseButton" ? new Vector2(60f, 25f) : new Vector2(65f, 25f);

        Text label = buttonObject.GetComponentInChildren<Text>();
        label.text = labelText;
        label.fontSize = emphasized ? 17 : 13;
        label.color = emphasized ? Color.green : Color.white;

        Button button = buttonObject.GetComponent<Button>();
        if (emphasized)
        {
            SetButtonColors(button, 0.239215687f, 0.666666567f,
                0.465408742f, 0.735849f);
        }
        else
        {
            SetButtonColors(button, 0.356862754f, 0.704402447f,
                0.5345911f, 0.9607843f);
        }
        DisableNavigation(button);
        return button;
    }

    private static Toggle CreateLoopToggle(Transform parent)
    {
        GameObject toggleObject = DefaultControls.CreateToggle(CreateUiResources());
        toggleObject.name = "LoopToggle";
        toggleObject.transform.SetParent(parent, false);

        RectTransform rect = toggleObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(373.9f, 18.77f);
        rect.sizeDelta = new Vector2(80f, 22f);

        Text label = toggleObject.GetComponentInChildren<Text>();
        label.text = "Loop";
        label.fontSize = 14;
        label.color = Color.white;

        Toggle toggle = toggleObject.GetComponent<Toggle>();
        DisableNavigation(toggle);
        return toggle;
    }

    private static Text CreateStatusText(Transform parent)
    {
        GameObject textObject = DefaultControls.CreateText(CreateUiResources());
        textObject.name = "StatusText";
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(30f, -11.8f);
        rect.sizeDelta = new Vector2(-60f, 22f);

        Text text = textObject.GetComponent<Text>();
        text.text = "Ready";
        text.alignment = TextAnchor.MiddleLeft;
        text.fontSize = 16;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static Text CreateCurrentUrlText(Transform parent)
    {
        GameObject textObject = DefaultControls.CreateText(CreateUiResources());
        textObject.name = "CurrentUrlText";
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(30f, -63.2f);
        rect.sizeDelta = new Vector2(-60f, 22f);

        Text text = textObject.GetComponent<Text>();
        text.text = "URL: --";
        text.alignment = TextAnchor.MiddleLeft;
        text.fontSize = 14;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 9;
        text.resizeTextMaxSize = 14;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static Text CreateOwnerText(Transform parent)
    {
        GameObject textObject = DefaultControls.CreateText(CreateUiResources());
        textObject.name = "OwnerText";
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(30f, -38.5f);
        rect.sizeDelta = new Vector2(-60f, 22f);

        Text text = textObject.GetComponent<Text>();
        text.text = "Owner: --";
        text.alignment = TextAnchor.MiddleLeft;
        text.fontSize = 14;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 10;
        text.resizeTextMaxSize = 14;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static void CreateVersionTag(Transform parent)
    {
        GameObject textObject = DefaultControls.CreateText(CreateUiResources());
        textObject.name = "tag";
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(309.7f, 85.9f);
        rect.sizeDelta = new Vector2(-339.7f, 18f);

        Text text = textObject.GetComponent<Text>();
        text.text = "HamaStream v1.0";
        text.alignment = TextAnchor.MiddleRight;
        text.fontSize = 10;
        text.color = Color.white;
        text.raycastTarget = false;
    }

    private static void CreatePlaylistHeader(Transform parent, string title)
    {
        GameObject textObject = DefaultControls.CreateText(CreateUiResources());
        textObject.name = "PlaylistHeader";
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-5f, 145.1f);
        rect.sizeDelta = new Vector2(-50f, 22f);

        Text text = textObject.GetComponent<Text>();
        text.text = title;
        text.alignment = TextAnchor.MiddleLeft;
        text.fontSize = 16;
        text.color = Color.white;
        text.raycastTarget = false;
    }

    private static Button[] CreatePlaylistItems(
        Transform parent,
        out Text[] displayNameLabels,
        out Text[] urlLabels)
    {
        Button[] buttons = new Button[MaxPlaylistItems];
        displayNameLabels = new Text[MaxPlaylistItems];
        urlLabels = new Text[MaxPlaylistItems];

        float[] rowPositions =
        {
            120.1f, 91.2f, 61.4f, 32.3f, 2.7f,
            -26.3f, -54.7f, -82.8f, -112.3f, -141.4f
        };

        for (int index = 0; index < MaxPlaylistItems; index++)
        {
            GameObject buttonObject = DefaultControls.CreateButton(CreateUiResources());
            buttonObject.name = "PlaylistItem" + index;
            buttonObject.transform.SetParent(parent, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(20f, rowPositions[index]);
            rect.sizeDelta = new Vector2(-40f, index < 5 ? 28f : 27.76f);

            Text displayNameLabel = buttonObject.GetComponentInChildren<Text>();
            displayNameLabel.gameObject.name = "DisplayNameText";
            displayNameLabel.text = "Playlist item";
            displayNameLabel.alignment = TextAnchor.MiddleLeft;
            displayNameLabel.fontSize = 13;
            displayNameLabel.color = Color.white;
            displayNameLabel.resizeTextForBestFit = true;
            displayNameLabel.resizeTextMinSize = 7;
            displayNameLabel.resizeTextMaxSize = 13;
            displayNameLabel.raycastTarget = false;

            RectTransform displayNameRect = displayNameLabel.rectTransform;
            displayNameRect.anchorMin = new Vector2(0f, 0.5f);
            displayNameRect.anchorMax = new Vector2(1f, 1f);
            displayNameRect.offsetMin = new Vector2(6f, 0f);
            displayNameRect.offsetMax = new Vector2(-6f, -1f);

            GameObject urlTextObject = DefaultControls.CreateText(CreateUiResources());
            urlTextObject.name = "UrlText";
            urlTextObject.transform.SetParent(buttonObject.transform, false);
            Text urlLabel = urlTextObject.GetComponent<Text>();
            urlLabel.text = "https://";
            urlLabel.alignment = TextAnchor.MiddleLeft;
            urlLabel.fontSize = 10;
            urlLabel.resizeTextForBestFit = true;
            urlLabel.resizeTextMinSize = 6;
            urlLabel.resizeTextMaxSize = 10;
            urlLabel.color = Color.white;
            urlLabel.raycastTarget = false;

            RectTransform urlRect = urlLabel.rectTransform;
            urlRect.anchorMin = new Vector2(0f, 0f);
            urlRect.anchorMax = new Vector2(1f, 0.5f);
            urlRect.offsetMin = new Vector2(6f, 1f);
            urlRect.offsetMax = new Vector2(-6f, 0f);

            Button button = buttonObject.GetComponent<Button>();
            SetButtonColors(button, 0.251572251f, 0.9607843f,
                0.784313738f, 0.9607843f, 0.250781149f);
            DisableNavigation(button);
            buttons[index] = button;
            displayNameLabels[index] = displayNameLabel;
            urlLabels[index] = urlLabel;
            buttonObject.SetActive(true);
        }

        return buttons;
    }

    private static void DisableNavigation(Selectable selectable)
    {
        Navigation navigation = selectable.navigation;
        navigation.mode = Navigation.Mode.None;
        selectable.navigation = navigation;
    }

    private static void SetButtonColors(
        Button button, float normal, float highlighted, float pressed,
        float selected, float normalGreenBlue = -1f)
    {
        ColorBlock colors = button.colors;
        float greenBlue = normalGreenBlue < 0f ? normal : normalGreenBlue;
        colors.normalColor = new Color(normal, greenBlue, greenBlue, 1f);
        colors.highlightedColor = new Color(highlighted, highlighted, highlighted, 1f);
        colors.pressedColor = new Color(pressed, pressed, pressed, 1f);
        colors.selectedColor = new Color(selected, selected, selected, 1f);
        button.colors = colors;
    }

    private static string GetPlaylistEventName(int index)
    {
        switch (index)
        {
            case 0: return nameof(HamaStreamPlaylistController.PlayPlaylistItem0);
            case 1: return nameof(HamaStreamPlaylistController.PlayPlaylistItem1);
            case 2: return nameof(HamaStreamPlaylistController.PlayPlaylistItem2);
            case 3: return nameof(HamaStreamPlaylistController.PlayPlaylistItem3);
            case 4: return nameof(HamaStreamPlaylistController.PlayPlaylistItem4);
            case 5: return nameof(HamaStreamPlaylistController.PlayPlaylistItem5);
            case 6: return nameof(HamaStreamPlaylistController.PlayPlaylistItem6);
            case 7: return nameof(HamaStreamPlaylistController.PlayPlaylistItem7);
            case 8: return nameof(HamaStreamPlaylistController.PlayPlaylistItem8);
            case 9: return nameof(HamaStreamPlaylistController.PlayPlaylistItem9);
            default: return "";
        }
    }

    private static DefaultControls.Resources CreateUiResources()
    {
        return new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
            dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd")
        };
    }

    private static void CopyScreenMaterial(VRCAVProVideoScreen videoScreen)
    {
        MeshRenderer renderer = videoScreen.GetComponent<MeshRenderer>();
        if (renderer == null || renderer.sharedMaterial == null)
        {
            throw new MissingReferenceException("The AVPro screen material was not found.");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(renderer.sharedMaterial);
            material.name = "HamaStreamScreen";
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else
        {
            EditorUtility.CopySerialized(renderer.sharedMaterial, material);
        }

        material.name = "HamaStreamScreen";
        EditorUtility.SetDirty(material);
        renderer.sharedMaterial = material;
    }

    private static Material GetVideoBlitMaterial()
    {
        Shader blitShader = AssetDatabase.LoadAssetAtPath<Shader>(BlitShaderPath);
        if (blitShader == null)
        {
            throw new MissingReferenceException("The ProTV blit shader was not found. Install ArchiTech.ProTV before rebuilding HamaStream.");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(VideoBlitMaterialPath);
        if (material == null)
        {
            material = new Material(blitShader);
            material.name = "HamaStreamVideoBlit";
            AssetDatabase.CreateAsset(material, VideoBlitMaterialPath);
        }
        else if (material.shader != blitShader)
        {
            material.shader = blitShader;
            EditorUtility.SetDirty(material);
        }

        return material;
    }

    private static MeshRenderer CreateDisplayScreen(VRCAVProVideoScreen videoScreen)
    {
        MeshFilter sourceFilter = videoScreen.GetComponent<MeshFilter>();
        MeshRenderer sourceRenderer = videoScreen.GetComponent<MeshRenderer>();
        if (sourceFilter == null || sourceFilter.sharedMesh == null ||
            sourceRenderer == null || sourceRenderer.sharedMaterial == null)
        {
            throw new MissingReferenceException("The AVPro screen mesh or material was not found.");
        }

        Shader displayShader = AssetDatabase.LoadAssetAtPath<Shader>(DisplayShaderPath);
        if (displayShader == null)
        {
            throw new MissingReferenceException("The HamaStream display shader was not found.");
        }

        Material displayMaterial = AssetDatabase.LoadAssetAtPath<Material>(DisplayMaterialPath);
        if (displayMaterial == null)
        {
            displayMaterial = new Material(displayShader);
            displayMaterial.name = "HamaStreamDisplay";
            AssetDatabase.CreateAsset(displayMaterial, DisplayMaterialPath);
        }
        else if (displayMaterial.shader != displayShader)
        {
            displayMaterial.shader = displayShader;
            EditorUtility.SetDirty(displayMaterial);
        }

        GameObject display = new GameObject("DisplayScreen", typeof(MeshFilter), typeof(MeshRenderer));
        display.transform.SetParent(videoScreen.transform, false);
        display.GetComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;

        MeshRenderer renderer = display.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = displayMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private static void ClearPersistentCalls(SerializedProperty unityEvent)
    {
        if (unityEvent == null)
        {
            return;
        }

        SerializedProperty calls = unityEvent.FindPropertyRelative("m_PersistentCalls")
            ?.FindPropertyRelative("m_Calls");
        calls?.ClearArray();
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];

        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[index]);
            }

            current = next;
        }
    }
}
