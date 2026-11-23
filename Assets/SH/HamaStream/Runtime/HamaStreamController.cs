using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components.AVPro;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class HamaStreamController : UdonSharpBehaviour
{
    private const float PlaybackAreaTolerance = 0.25f;

    [Header("Player References")]
    public VRCAVProVideoPlayer VideoPlayer;
    public VRCUrlInputField UrlInput;
    public Toggle LoopToggle;
    public bool DefaultLoop;
    public Text StatusText;
    public Text CurrentUrlText;
    public Text OwnerText;
    public HamaStreamPlaylistController[] PlaylistControllers = new HamaStreamPlaylistController[0];

    [Header("Auto Play")]
    public bool AutoPlayOnStart;
    public VRCUrl AutoPlayUrl = new VRCUrl("");

    [Header("Video Rendering")]
    public bool EnableRenderTextureOutput;
    public MeshRenderer VideoScreenRenderer;
    public MeshRenderer DisplayScreenRenderer;
    public RenderTexture OutputRenderTexture;
    public Material VideoBlitMaterial;
    public Texture StandbyTexture;

    [Header("Playback Area")]
    public BoxCollider PlaybackArea;

    [Header("Loading")]
    public HamaStreamController[] UrlLoadPeers = new HamaStreamController[0];
    [Min(5f)]
    public float UrlLoadCooldownSeconds = 5f;

    [System.NonSerialized]
    public float LocalNextUrlLoadTime;

    [UdonSynced]
    private VRCUrl _syncedUrl = new VRCUrl("");

    [UdonSynced]
    private bool _syncedPlaying;

    [UdonSynced]
    private bool _syncedLoop;

    [UdonSynced]
    private int _syncedPlayRevision;

    [UdonSynced]
    private int _syncedPlaylistSlot = -1;

    [UdonSynced]
    private int _syncedPlaylistIndex = -1;

    [System.NonSerialized]
    public VRCUrl PlaylistRequestUrl;

    [System.NonSerialized]
    public HamaStreamPlaylistController PlaylistRequestSource;

    [System.NonSerialized]
    public int PlaylistRequestIndex;

    private bool _manuallyPaused;
    private bool _isLocalPlayerInPlaybackArea;
    private bool _hasLoadedUrl;
    private bool _loadRetryScheduled;
    private bool _updatingUi;
    private bool _videoEnded;
    private bool _videoFailed;
    private bool _waitingForVideoReady;
    private string _loadedUrlText = "";
    private int _appliedPlayRevision = -1;
    private VRCUrl _emptyUrl = new VRCUrl("");
    private MaterialPropertyBlock _videoSourceProperties;
    private int _videoSourceMainTexId;
    private int _videoSourceMainTexSTId;
    private RenderTexture _correctedVideoTexture;

    private void Start()
    {
        if (!HasRequiredReferences())
        {
            SetStatus("Configuration error");
            return;
        }

        VideoScreenRenderer.enabled = false;
        DisplayScreenRenderer.enabled = true;
        UpdateInitialPlaybackAreaState();
        UpdateOwnerText();
        if (Networking.IsOwner(gameObject))
        {
            _syncedLoop = DefaultLoop;
            if (AutoPlayOnStart && !string.IsNullOrEmpty(GetUrlText(AutoPlayUrl)) &&
                !_syncedPlaying && string.IsNullOrEmpty(GetUrlText(_syncedUrl)) &&
                _syncedPlayRevision == 0)
            {
                _syncedUrl = AutoPlayUrl;
                _syncedPlaying = true;
                _syncedPlayRevision++;
            }
            RequestSerialization();
        }
        ApplySharedState();
    }

    private void Update()
    {
        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        SetLocalPlayerInPlaybackArea(
            Utilities.IsValid(localPlayer) && IsPlayerInsidePlaybackArea(localPlayer));
    }

    private void LateUpdate()
    {
        UpdateVideoTextures();
    }

    private void UpdateVideoTextures()
    {
        if (VideoPlayer == null)
        {
            return;
        }

        bool showStandby = !_syncedPlaying || !_hasLoadedUrl || _waitingForVideoReady ||
                           _videoEnded || _videoFailed;
        Texture videoTexture = null;
        Vector4 videoTextureST = new Vector4(1f, 1f, 0f, 0f);
        if (VideoScreenRenderer != null)
        {
            Vector4 sourceTextureST = Vector4.zero;
            if (VideoScreenRenderer.HasPropertyBlock())
            {
                if (_videoSourceProperties == null)
                {
                    _videoSourceProperties = new MaterialPropertyBlock();
                    _videoSourceMainTexId = VRCShader.PropertyToID("_MainTex");
                    _videoSourceMainTexSTId = VRCShader.PropertyToID("_MainTex_ST");
                }
                VideoScreenRenderer.GetPropertyBlock(_videoSourceProperties);
                if (_videoSourceProperties.HasTexture(_videoSourceMainTexId))
                {
                    videoTexture = _videoSourceProperties.GetTexture(_videoSourceMainTexId);
                    sourceTextureST = _videoSourceProperties.GetVector(_videoSourceMainTexSTId);
                }
            }
            if (videoTexture == null)
            {
                Material sourceMaterial = VideoScreenRenderer.material;
                if (sourceMaterial != null)
                {
                    videoTexture = sourceMaterial.GetTexture("_MainTex");
                    sourceTextureST = sourceMaterial.GetVector("_MainTex_ST");
                }
            }
            if (sourceTextureST != Vector4.zero)
            {
                videoTextureST = sourceTextureST;
            }
        }

        Texture sourceTexture = showStandby ? StandbyTexture : videoTexture;
        if (sourceTexture != null && VideoBlitMaterial != null)
        {
            if (_correctedVideoTexture == null)
            {
                _correctedVideoTexture = VRCRenderTexture.GetTemporary(
                    16, 16, 0, RenderTextureFormat.ARGB64, RenderTextureReadWrite.sRGB, 1);
                _correctedVideoTexture.wrapMode = TextureWrapMode.Clamp;
                _correctedVideoTexture.filterMode = FilterMode.Trilinear;
            }

            if (_correctedVideoTexture.width != sourceTexture.width ||
                _correctedVideoTexture.height != sourceTexture.height)
            {
                if (_correctedVideoTexture.IsCreated()) _correctedVideoTexture.Release();
                _correctedVideoTexture.width = sourceTexture.width;
                _correctedVideoTexture.height = sourceTexture.height;
            }
            if (!_correctedVideoTexture.IsCreated()) _correctedVideoTexture.Create();

            VideoBlitMaterial.SetVector("_MainTex_ST_Override", showStandby
                ? new Vector4(1f, 1f, 0f, 0f) : videoTextureST);
            VideoBlitMaterial.SetVector("_GammaZone", new Vector4(1f, 1f, 0f, 0f));
            VideoBlitMaterial.SetFloat("_AVPro", !showStandby && sourceTexture.name.Contains("AVPro") ? 1f : 0f);
            VideoBlitMaterial.SetFloat("_SkipGamma", 0f);
            VRCGraphics.Blit(sourceTexture, _correctedVideoTexture, VideoBlitMaterial, 0);
        }
        else if (_correctedVideoTexture != null && _correctedVideoTexture.IsCreated())
        {
            _correctedVideoTexture.Release();
        }

        if (DisplayScreenRenderer != null)
        {
            Material displayMaterial = DisplayScreenRenderer.material;
            if (displayMaterial != null)
            {
                bool hasDisplayTexture = sourceTexture != null && VideoBlitMaterial != null &&
                    _correctedVideoTexture != null && _correctedVideoTexture.IsCreated();
                displayMaterial.SetTexture("_MainTex", StandbyTexture);
                displayMaterial.SetTexture("_VideoTex", hasDisplayTexture ? _correctedVideoTexture : null);
                displayMaterial.SetFloat("_UseVideoTex", hasDisplayTexture ? 1f : 0f);
                displayMaterial.SetFloat("_SourceAspect", sourceTexture != null && sourceTexture.height > 0
                    ? (float)sourceTexture.width / sourceTexture.height : 1.777778f);
            }
        }

        if (!EnableRenderTextureOutput || OutputRenderTexture == null)
        {
            return;
        }

        if (sourceTexture == null || _correctedVideoTexture == null ||
            !_correctedVideoTexture.IsCreated())
        {
            if (OutputRenderTexture.IsCreated()) OutputRenderTexture.Release();
            return;
        }

        if (OutputRenderTexture != _correctedVideoTexture)
        {
            if (!OutputRenderTexture.IsCreated()) OutputRenderTexture.Create();
            VRCGraphics.Blit(_correctedVideoTexture, OutputRenderTexture);
        }
    }

    public void PlayFromSubmittedUrl()
    {
        if (UrlInput == null || string.IsNullOrEmpty(GetUrlText(UrlInput.GetUrl())))
        {
            return;
        }

        PlayFromInput();
    }

    public void PlayFromInput()
    {
        if (!HasRequiredReferences())
        {
            SetStatus("Configuration error");
            return;
        }

        VRCUrl inputUrl = UrlInput.GetUrl();
        string inputUrlText = GetUrlText(inputUrl);
        string sharedUrlText = GetUrlText(_syncedUrl);

        if (string.IsNullOrEmpty(inputUrlText))
        {
            if (string.IsNullOrEmpty(sharedUrlText))
            {
                SetStatus("Enter a URL");
                return;
            }

            if (_syncedPlaying)
            {
                _manuallyPaused = false;
                ApplySharedState();
                return;
            }

            if (!CanLoadUrlNow())
            {
                SetStatus("Please wait before loading another URL");
                return;
            }

            if (!TakeOwnership())
            {
                return;
            }

            _syncedPlaying = true;
            _syncedPlayRevision++;
            _manuallyPaused = false;
            RequestSerialization();
            ApplySharedState();
            return;
        }

        if (inputUrlText == sharedUrlText && _syncedPlaying)
        {
            if (!TakeOwnership())
            {
                return;
            }

            if (ClearPlaylistSelection())
            {
                RequestSerialization();
            }
            _manuallyPaused = false;
            ClearUrlInput();
            ApplySharedState();
            return;
        }

        if (!CanLoadUrlNow())
        {
            SetStatus("Please wait before loading another URL");
            return;
        }

        if (!TakeOwnership())
        {
            return;
        }

        _syncedUrl = inputUrl;
        _syncedPlaying = true;
        _syncedPlayRevision++;
        _manuallyPaused = false;
        ClearPlaylistSelection();
        RequestSerialization();
        ClearUrlInput();
        ApplySharedState();
    }

    public void PlayFromPlaylistRequest()
    {
        int playlistSlot = FindPlaylistSlot(PlaylistRequestSource);
        if (playlistSlot < 0 || PlaylistRequestIndex < 0)
        {
            SetStatus("Playlist is not registered");
            return;
        }

        if (!HasRequiredReferences())
        {
            SetStatus("Configuration error");
            return;
        }

        if (string.IsNullOrEmpty(GetUrlText(PlaylistRequestUrl)))
        {
            SetStatus("Playlist URL is empty");
            return;
        }

        if (!CanLoadUrlNow())
        {
            SetStatus("Please wait before loading another URL");
            return;
        }

        if (!TakeOwnership())
        {
            return;
        }

        _syncedUrl = PlaylistRequestUrl;
        _syncedPlaying = true;
        _syncedPlayRevision++;
        _syncedPlaylistSlot = playlistSlot;
        _syncedPlaylistIndex = PlaylistRequestIndex;
        _manuallyPaused = false;
        RequestSerialization();
        ApplySharedState();
        RefreshPlaylistUi();
    }

    public void PauseLocal()
    {
        if (!HasRequiredReferences())
        {
            SetStatus("Configuration error");
            return;
        }

        if (!_syncedPlaying || !_hasLoadedUrl)
        {
            SetStatus(_syncedPlaying ? "Waiting for video" : "Stopped");
            return;
        }

        _manuallyPaused = true;
        VideoPlayer.Pause();
        SetStatus(_isLocalPlayerInPlaybackArea ? "Paused (local)" : "Paused (outside range)");
    }

    public void GlobalStop()
    {
        if (!HasRequiredReferences())
        {
            SetStatus("Configuration error");
            return;
        }

        if (!TakeOwnership())
        {
            return;
        }

        _syncedPlaying = false;
        _manuallyPaused = false;
        RequestSerialization();
        ApplySharedState();
    }

    public void ResyncLocal()
    {
        if (!HasRequiredReferences())
        {
            SetStatus("Configuration error");
            return;
        }

        if (!_syncedPlaying)
        {
            SetStatus("Stopped");
            return;
        }

        if (!_isLocalPlayerInPlaybackArea)
        {
            SetStatus("Paused (outside range)");
            return;
        }

        string urlText = GetUrlText(_syncedUrl);
        if (string.IsNullOrEmpty(urlText))
        {
            SetStatus("Waiting for URL");
            return;
        }

        if (!CanLoadUrlNow())
        {
            SetStatus("Please wait before resyncing");
            return;
        }

        _manuallyPaused = false;
        VideoPlayer.Stop();
        LoadSharedUrl(urlText, "Resyncing");
    }

    public void LoopToggleChanged()
    {
        if (_updatingUi || LoopToggle == null)
        {
            return;
        }

        if (!TakeOwnership())
        {
            UpdateUiFromSharedState();
            return;
        }

        _syncedLoop = LoopToggle.isOn;
        RequestSerialization();
        ApplyLoopSetting();
        UpdateUiFromSharedState();
    }

    public void RetryApplySharedState()
    {
        _loadRetryScheduled = false;
        ApplySharedState();
    }

    public override void OnDeserialization()
    {
        ApplySharedState();
        RefreshPlaylistUi();
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        UpdateOwnerText();
    }

    public override void OnVideoReady()
    {
        _waitingForVideoReady = false;
        ApplyLoopSetting();

        if (!_syncedPlaying)
        {
            StopLocalPlayback();
        }
        else if (!_isLocalPlayerInPlaybackArea)
        {
            VideoPlayer.Pause();
            SetStatus("Paused (outside range)");
        }
        else if (_manuallyPaused)
        {
            VideoPlayer.Pause();
            SetStatus("Paused (local)");
        }
        else
        {
            SetStatus("Ready to play");
        }
    }

    public override void OnVideoStart()
    {
        HandleVideoPlaying();
    }

    public override void OnVideoPlay()
    {
        HandleVideoPlaying();
    }

    public override void OnVideoPause()
    {
        if (!_isLocalPlayerInPlaybackArea && _syncedPlaying)
        {
            SetStatus("Paused (outside range)");
        }
        else
        {
            SetStatus(_manuallyPaused ? "Paused (local)" : "Paused");
        }
    }

    public override void OnVideoEnd()
    {
        _videoEnded = !_syncedLoop;
        SetStatus(_syncedLoop ? "Looping" : "Ended");
    }

    public override void OnVideoError(VideoError videoError)
    {
        _waitingForVideoReady = false;
        _videoFailed = true;
        SetStatus("Video error: " + videoError);
    }

    private void ApplySharedState()
    {
        UpdateUiFromSharedState();

        if (!HasRequiredReferences())
        {
            SetStatus("Configuration error");
            return;
        }

        ApplyLoopSetting();

        if (!_syncedPlaying)
        {
            _manuallyPaused = false;
            _appliedPlayRevision = _syncedPlayRevision;
            StopLocalPlayback();
            return;
        }

        string urlText = GetUrlText(_syncedUrl);
        if (string.IsNullOrEmpty(urlText))
        {
            SetStatus("Waiting for URL");
            return;
        }

        bool hasNewPlayRequest = _appliedPlayRevision != _syncedPlayRevision;
        if (_loadedUrlText != urlText || hasNewPlayRequest)
        {
            _manuallyPaused = false;
        }

        if (!_isLocalPlayerInPlaybackArea)
        {
            if (_hasLoadedUrl)
            {
                VideoPlayer.Pause();
            }

            SetStatus("Paused (outside range)");
            return;
        }

        if (_manuallyPaused)
        {
            SetStatus("Paused (local)");
            return;
        }

        if (_hasLoadedUrl && _loadedUrlText == urlText && !hasNewPlayRequest)
        {
            VideoPlayer.Play();
            SetStatus("Playing");
            return;
        }

        if (!CanLoadUrlNow())
        {
            ScheduleLoadRetry();
            SetStatus("Waiting to load URL");
            return;
        }

        LoadSharedUrl(urlText, "Loading");
    }

    private void LoadSharedUrl(string urlText, string status)
    {
        _waitingForVideoReady = true;
        _videoEnded = false;
        _videoFailed = false;
        // Publish before PlayURL so another controller in this frame sees the cooldown.
        RecordUrlLoad();
        _hasLoadedUrl = true;
        _loadedUrlText = urlText;
        _appliedPlayRevision = _syncedPlayRevision;
        SetStatus(status);
        Debug.Log("[HamaStream] URL load: " + gameObject.name + " at " + Time.realtimeSinceStartup);
        VideoPlayer.PlayURL(_syncedUrl);
    }

    private void HandleVideoPlaying()
    {
        _waitingForVideoReady = false;
        _videoEnded = false;
        _videoFailed = false;
        if (!_syncedPlaying)
        {
            StopLocalPlayback();
        }
        else if (!_isLocalPlayerInPlaybackArea)
        {
            VideoPlayer.Pause();
            SetStatus("Paused (outside range)");
        }
        else if (_manuallyPaused)
        {
            VideoPlayer.Pause();
            SetStatus("Paused (local)");
        }
        else
        {
            SetStatus("Playing");
        }
    }

    private void StopLocalPlayback()
    {
        _waitingForVideoReady = false;
        VideoPlayer.Stop();
        _hasLoadedUrl = false;
        _loadedUrlText = "";
        SetStatus("Stopped");
    }

    private void ApplyLoopSetting()
    {
        if (VideoPlayer != null)
        {
            VideoPlayer.Loop = _syncedLoop;
        }
    }

    private void UpdateUiFromSharedState()
    {
        string urlText = GetUrlText(_syncedUrl);

        if (CurrentUrlText != null)
        {
            CurrentUrlText.text = string.IsNullOrEmpty(urlText) ? "URL: --" : "URL: " + urlText;
        }

        if (LoopToggle != null)
        {
            _updatingUi = true;
            LoopToggle.isOn = _syncedLoop;
            _updatingUi = false;
        }

        UpdateOwnerText();
    }

    private void UpdateOwnerText()
    {
        if (OwnerText == null)
        {
            return;
        }

        VRCPlayerApi owner = Networking.GetOwner(gameObject);
        OwnerText.text = Utilities.IsValid(owner) ? "Owner: " + owner.displayName : "Owner: --";
    }

    private bool TakeOwnership()
    {
        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        if (!Utilities.IsValid(localPlayer))
        {
            SetStatus("Local player unavailable");
            return false;
        }

        if (!Networking.IsOwner(gameObject))
        {
            Networking.SetOwner(localPlayer, gameObject);
        }

        if (!Networking.IsOwner(gameObject))
        {
            SetStatus("Could not take ownership");
            return false;
        }

        UpdateOwnerText();
        return true;
    }

    private bool CanLoadUrlNow()
    {
        return GetUrlLoadDelay() <= 0f;
    }

    private float GetUrlLoadDelay()
    {
        float nextLoadTime = LocalNextUrlLoadTime;
        if (UrlLoadPeers != null)
        {
            for (int index = 0; index < UrlLoadPeers.Length; index++)
            {
                HamaStreamController peer = UrlLoadPeers[index];
                if (peer != null)
                {
                    nextLoadTime = Mathf.Max(nextLoadTime, peer.LocalNextUrlLoadTime);
                }
            }
        }

        return Mathf.Max(0f, nextLoadTime - Time.realtimeSinceStartup);
    }

    private void RecordUrlLoad()
    {
        float cooldown = Mathf.Max(5f, UrlLoadCooldownSeconds);
        if (UrlLoadPeers != null)
        {
            for (int index = 0; index < UrlLoadPeers.Length; index++)
            {
                HamaStreamController peer = UrlLoadPeers[index];
                if (peer != null)
                {
                    cooldown = Mathf.Max(cooldown, peer.UrlLoadCooldownSeconds);
                }
            }
        }

        LocalNextUrlLoadTime = Time.realtimeSinceStartup + cooldown;
        if (UrlLoadPeers == null)
        {
            return;
        }

        for (int index = 0; index < UrlLoadPeers.Length; index++)
        {
            HamaStreamController peer = UrlLoadPeers[index];
            if (peer != null && peer != this)
            {
                peer.LocalNextUrlLoadTime = LocalNextUrlLoadTime;
            }
        }
    }

    private void ScheduleLoadRetry()
    {
        if (_loadRetryScheduled)
        {
            return;
        }

        float delay = Mathf.Max(0.1f, GetUrlLoadDelay() + 0.1f);
        _loadRetryScheduled = true;
        SendCustomEventDelayedSeconds(nameof(RetryApplySharedState), delay);
    }

    private bool HasRequiredReferences()
    {
        return VideoPlayer != null && UrlInput != null && PlaybackArea != null && PlaybackArea.isTrigger &&
               VideoScreenRenderer != null && DisplayScreenRenderer != null && VideoBlitMaterial != null &&
               (!EnableRenderTextureOutput || OutputRenderTexture != null);
    }

    private void UpdateInitialPlaybackAreaState()
    {
        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        _isLocalPlayerInPlaybackArea =
            Utilities.IsValid(localPlayer) && IsPlayerInsidePlaybackArea(localPlayer);
    }

    private bool IsPlayerInsidePlaybackArea(VRCPlayerApi player)
    {
        if (PlaybackArea == null || !PlaybackArea.enabled || !PlaybackArea.gameObject.activeInHierarchy)
        {
            return false;
        }

        Vector3 localPosition =
            PlaybackArea.transform.InverseTransformPoint(player.GetPosition()) - PlaybackArea.center;
        Vector3 halfSize = PlaybackArea.size * 0.5f +
                           Vector3.one * PlaybackAreaTolerance;

        return Mathf.Abs(localPosition.x) <= halfSize.x &&
               Mathf.Abs(localPosition.y) <= halfSize.y &&
               Mathf.Abs(localPosition.z) <= halfSize.z;
    }

    private void SetLocalPlayerInPlaybackArea(bool isInside)
    {
        if (_isLocalPlayerInPlaybackArea == isInside)
        {
            return;
        }

        _isLocalPlayerInPlaybackArea = isInside;

        if (isInside)
        {
            ApplySharedState();
            return;
        }

        if (_syncedPlaying && _hasLoadedUrl)
        {
            VideoPlayer.Pause();
        }

        SetStatus(_syncedPlaying ? "Paused (outside range)" : "Stopped");
    }

    private string GetUrlText(VRCUrl url)
    {
        return url == null || url.Get() == null ? "" : url.Get();
    }

    private void ClearUrlInput()
    {
        if (UrlInput != null)
        {
            UrlInput.SetUrl(_emptyUrl);
        }
    }

    public int GetSelectedPlaylistItem(HamaStreamPlaylistController playlist)
    {
        int slot = FindPlaylistSlot(playlist);
        return slot >= 0 && _syncedPlaylistSlot == slot ? _syncedPlaylistIndex : -1;
    }

    private int FindPlaylistSlot(HamaStreamPlaylistController playlist)
    {
        if (playlist == null || PlaylistControllers == null)
        {
            return -1;
        }

        for (int index = 0; index < PlaylistControllers.Length; index++)
        {
            if (PlaylistControllers[index] == playlist)
            {
                return index;
            }
        }

        return -1;
    }

    private bool ClearPlaylistSelection()
    {
        if (_syncedPlaylistSlot < 0 && _syncedPlaylistIndex < 0)
        {
            return false;
        }

        _syncedPlaylistSlot = -1;
        _syncedPlaylistIndex = -1;
        RefreshPlaylistUi();
        return true;
    }

    private void RefreshPlaylistUi()
    {
        if (PlaylistControllers == null)
        {
            return;
        }

        for (int index = 0; index < PlaylistControllers.Length; index++)
        {
            HamaStreamPlaylistController playlist = PlaylistControllers[index];
            if (playlist != null)
            {
                playlist.RefreshSelection();
            }
        }
    }

    private void SetStatus(string message)
    {
        if (StatusText != null)
        {
            StatusText.text = message;
        }

        Debug.Log("[HamaStream] " + message);
    }
}
