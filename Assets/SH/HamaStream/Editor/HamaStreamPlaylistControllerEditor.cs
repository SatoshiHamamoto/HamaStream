using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HamaStreamPlaylistController))]
public class HamaStreamPlaylistControllerEditor : Editor
{
    private SerializedProperty _mainController;
    private SerializedProperty _playlistButtons;
    private SerializedProperty _playlistDisplayNameLabels;
    private SerializedProperty _playlistUrlLabels;
    private SerializedProperty _playlistDisplayNames;
    private SerializedProperty _playlistUrls;
    private SerializedProperty _selectedButtonColor;

    private void OnEnable()
    {
        _mainController = serializedObject.FindProperty("MainController");
        _playlistButtons = serializedObject.FindProperty("PlaylistButtons");
        _playlistDisplayNameLabels = serializedObject.FindProperty("PlaylistDisplayNameLabels");
        _playlistUrlLabels = serializedObject.FindProperty("PlaylistUrlLabels");
        _playlistDisplayNames = serializedObject.FindProperty("PlaylistDisplayNames");
        _playlistUrls = serializedObject.FindProperty("PlaylistUrls");
        _selectedButtonColor = serializedObject.FindProperty("SelectedButtonColor");
    }

    public override void OnInspectorGUI()
    {
        if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target))
        {
            return;
        }

        serializedObject.Update();

        EditorGUILayout.LabelField("Player Reference", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_mainController);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Generated UI References", EditorStyles.boldLabel);
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.PropertyField(_playlistButtons, true);
        EditorGUILayout.PropertyField(_playlistDisplayNameLabels, true);
        EditorGUILayout.PropertyField(_playlistUrlLabels, true);
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space();
        HamaStreamPlaylistController controller = (HamaStreamPlaylistController)target;
        EditorGUILayout.LabelField("Playlist " + controller.PlaylistNumber, EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Configure up to 10 items. Items without a URL stay hidden at runtime.",
            MessageType.Info);

        int currentSize = Mathf.Max(_playlistDisplayNames.arraySize, _playlistUrls.arraySize);
        int requestedSize = EditorGUILayout.IntSlider("Item Count", currentSize, 0, 10);
        if (requestedSize != currentSize ||
            _playlistDisplayNames.arraySize != _playlistUrls.arraySize)
        {
            _playlistDisplayNames.arraySize = requestedSize;
            _playlistUrls.arraySize = requestedSize;
        }

        for (int index = 0; index < requestedSize; index++)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Item " + (index + 1), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                _playlistDisplayNames.GetArrayElementAtIndex(index),
                new GUIContent("Display Name"));
            EditorGUILayout.PropertyField(
                _playlistUrls.GetArrayElementAtIndex(index),
                new GUIContent("URL"));
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Appearance", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_selectedButtonColor);

        if (!serializedObject.ApplyModifiedProperties())
        {
            return;
        }

        UdonSharpEditorUtility.CopyProxyToUdon(controller, ProxySerializationPolicy.All);
        EditorUtility.SetDirty(controller);
    }
}
