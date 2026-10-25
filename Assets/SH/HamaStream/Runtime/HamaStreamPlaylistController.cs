using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class HamaStreamPlaylistController : UdonSharpBehaviour
{
    private const int MaxPlaylistItems = 10;

    [Header("Player Reference")]
    public HamaStreamController MainController;

    [HideInInspector]
    public int PlaylistNumber = 1;

    [Header("Generated UI References")]
    [HideInInspector]
    public Button[] PlaylistButtons;

    [HideInInspector]
    public Text[] PlaylistDisplayNameLabels;

    [HideInInspector]
    public Text[] PlaylistUrlLabels;

    [Header("Playlist (Maximum 10 Items)")]
    public string[] PlaylistDisplayNames = new string[0];
    public VRCUrl[] PlaylistUrls = new VRCUrl[0];

    [Header("Appearance")]
    public Color SelectedButtonColor = new Color(0.35f, 0.75f, 0.45f, 1f);

    private VRCUrl _emptyUrl = new VRCUrl("");
    private Color[] _defaultNormalColors = new Color[MaxPlaylistItems];
    private Color[] _defaultHighlightedColors = new Color[MaxPlaylistItems];
    private Color[] _defaultSelectedColors = new Color[MaxPlaylistItems];
    private bool _buttonColorsCached;

    private void Start()
    {
        CacheButtonColors();
        UpdatePlaylistUi();
    }

    public void PlayPlaylistItem0()
    {
        PlayPlaylistItem(0);
    }

    public void PlayPlaylistItem1()
    {
        PlayPlaylistItem(1);
    }

    public void PlayPlaylistItem2()
    {
        PlayPlaylistItem(2);
    }

    public void PlayPlaylistItem3()
    {
        PlayPlaylistItem(3);
    }

    public void PlayPlaylistItem4()
    {
        PlayPlaylistItem(4);
    }

    public void PlayPlaylistItem5()
    {
        PlayPlaylistItem(5);
    }

    public void PlayPlaylistItem6()
    {
        PlayPlaylistItem(6);
    }

    public void PlayPlaylistItem7()
    {
        PlayPlaylistItem(7);
    }

    public void PlayPlaylistItem8()
    {
        PlayPlaylistItem(8);
    }

    public void PlayPlaylistItem9()
    {
        PlayPlaylistItem(9);
    }

    public void RefreshSelection()
    {
        UpdatePlaylistUi();
    }

    private void PlayPlaylistItem(int index)
    {
        if (MainController == null)
        {
            Debug.LogError("[HamaStream] Playlist MainController is not configured");
            return;
        }

        if (index < 0 || index >= GetPlaylistItemCount())
        {
            return;
        }

        VRCUrl playlistUrl = GetPlaylistUrl(index);
        MainController.PlaylistRequestSource = this;
        MainController.PlaylistRequestIndex = index;
        if (string.IsNullOrEmpty(GetUrlText(playlistUrl)))
        {
            MainController.PlaylistRequestUrl = _emptyUrl;
            MainController.PlayFromPlaylistRequest();
            return;
        }

        MainController.PlaylistRequestUrl = playlistUrl;
        MainController.PlayFromPlaylistRequest();
    }

    private void UpdatePlaylistUi()
    {
        CacheButtonColors();
        int itemCount = GetPlaylistItemCount();
        int selectedIndex = MainController == null
            ? -1 : MainController.GetSelectedPlaylistItem(this);

        for (int index = 0; index < MaxPlaylistItems; index++)
        {
            bool isConfigured = index < itemCount;
            string urlText = isConfigured ? GetUrlText(GetPlaylistUrl(index)) : "";
            bool hasUrl = !string.IsNullOrEmpty(urlText);
            Button button = PlaylistButtons != null && index < PlaylistButtons.Length
                ? PlaylistButtons[index]
                : null;

            if (button != null)
            {
                button.gameObject.SetActive(isConfigured && hasUrl);
            }

            if (!isConfigured || !hasUrl)
            {
                continue;
            }

            string displayName = GetPlaylistDisplayName(index);

            if (button != null)
            {
                ApplyButtonColor(button, index, index == selectedIndex);
            }

            if (PlaylistDisplayNameLabels != null &&
                index < PlaylistDisplayNameLabels.Length &&
                PlaylistDisplayNameLabels[index] != null)
            {
                PlaylistDisplayNameLabels[index].text = displayName;
            }

            if (PlaylistUrlLabels != null && index < PlaylistUrlLabels.Length &&
                PlaylistUrlLabels[index] != null)
            {
                PlaylistUrlLabels[index].text = urlText;
            }
        }
    }

    private void CacheButtonColors()
    {
        if (_buttonColorsCached || PlaylistButtons == null)
        {
            return;
        }

        for (int index = 0; index < MaxPlaylistItems; index++)
        {
            if (index >= PlaylistButtons.Length || PlaylistButtons[index] == null)
            {
                continue;
            }

            ColorBlock colors = PlaylistButtons[index].colors;
            _defaultNormalColors[index] = colors.normalColor;
            _defaultHighlightedColors[index] = colors.highlightedColor;
            _defaultSelectedColors[index] = colors.selectedColor;
        }

        _buttonColorsCached = true;
    }

    private void ApplyButtonColor(Button button, int index, bool isSelected)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = isSelected ? SelectedButtonColor : _defaultNormalColors[index];
        colors.highlightedColor = isSelected
            ? SelectedButtonColor
            : _defaultHighlightedColors[index];
        colors.selectedColor = isSelected ? SelectedButtonColor : _defaultSelectedColors[index];
        button.colors = colors;
    }

    private int GetPlaylistItemCount()
    {
        int nameCount = PlaylistDisplayNames == null ? 0 : PlaylistDisplayNames.Length;
        int urlCount = PlaylistUrls == null ? 0 : PlaylistUrls.Length;
        return Mathf.Min(MaxPlaylistItems, Mathf.Max(nameCount, urlCount));
    }

    private string GetPlaylistDisplayName(int index)
    {
        if (PlaylistDisplayNames == null || index < 0 || index >= PlaylistDisplayNames.Length)
        {
            return "";
        }

        return PlaylistDisplayNames[index];
    }

    private VRCUrl GetPlaylistUrl(int index)
    {
        if (PlaylistUrls == null || index < 0 || index >= PlaylistUrls.Length)
        {
            return _emptyUrl;
        }

        return PlaylistUrls[index];
    }

    private string GetUrlText(VRCUrl url)
    {
        return url == null || url.Get() == null ? "" : url.Get();
    }
}
