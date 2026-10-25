using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HamaStreamController))]
public class HamaStreamControllerEditor : Editor
{
    private SerializedProperty _videoPlayer;
    private SerializedProperty _urlInput;
    private SerializedProperty _loopToggle;
    private SerializedProperty _defaultLoop;
    private SerializedProperty _statusText;
    private SerializedProperty _currentUrlText;
    private SerializedProperty _ownerText;
    private SerializedProperty _playlistControllers;
    private SerializedProperty _enableRenderTextureOutput;
    private SerializedProperty _videoScreenRenderer;
    private SerializedProperty _standbyScreenRenderer;
    private SerializedProperty _outputRenderTexture;
    private SerializedProperty _standbyTexture;
    private SerializedProperty _playbackArea;
    private SerializedProperty _urlLoadCooldownSeconds;
    private SerializedProperty _urlLoadPeers;

    private void OnEnable()
    {
        _videoPlayer = serializedObject.FindProperty("VideoPlayer");
        _urlInput = serializedObject.FindProperty("UrlInput");
        _loopToggle = serializedObject.FindProperty("LoopToggle");
        _defaultLoop = serializedObject.FindProperty("DefaultLoop");
        _statusText = serializedObject.FindProperty("StatusText");
        _currentUrlText = serializedObject.FindProperty("CurrentUrlText");
        _ownerText = serializedObject.FindProperty("OwnerText");
        _playlistControllers = serializedObject.FindProperty("PlaylistControllers");
        _enableRenderTextureOutput = serializedObject.FindProperty("EnableRenderTextureOutput");
        _videoScreenRenderer = serializedObject.FindProperty("VideoScreenRenderer");
        _standbyScreenRenderer = serializedObject.FindProperty("StandbyScreenRenderer");
        _outputRenderTexture = serializedObject.FindProperty("OutputRenderTexture");
        _standbyTexture = serializedObject.FindProperty("StandbyTexture");
        _playbackArea = serializedObject.FindProperty("PlaybackArea");
        _urlLoadCooldownSeconds = serializedObject.FindProperty("UrlLoadCooldownSeconds");
        _urlLoadPeers = serializedObject.FindProperty("UrlLoadPeers");
    }

    public override void OnInspectorGUI()
    {
        if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target))
        {
            return;
        }

        serializedObject.Update();

        EditorGUILayout.LabelField("Player References", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_videoPlayer);
        EditorGUILayout.PropertyField(_urlInput);
        EditorGUILayout.PropertyField(_loopToggle);
        EditorGUILayout.PropertyField(_defaultLoop, new GUIContent("Default Loop"));
        EditorGUILayout.PropertyField(_statusText);
        EditorGUILayout.PropertyField(_currentUrlText);
        EditorGUILayout.PropertyField(_ownerText);
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.PropertyField(_playlistControllers, new GUIContent("Playlist Controllers"), true);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.HelpBox(
            "Add a PlaylistControls module with Tools > HamaStream > Add Playlist To Selected Player. " +
            "Use Remove Selected Playlist to delete one. Save the scene afterward.",
            MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Render Texture Output", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_enableRenderTextureOutput, new GUIContent("Enable Output"));
        if (_enableRenderTextureOutput.boolValue)
        {
            EditorGUILayout.PropertyField(_videoScreenRenderer, new GUIContent("Video Screen Renderer"));
            EditorGUILayout.PropertyField(_outputRenderTexture, new GUIContent("Output Render Texture"));
            if (_outputRenderTexture.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Assign a Render Texture to receive the video.", MessageType.Warning);
            }
        }
        EditorGUILayout.PropertyField(_standbyTexture, new GUIContent("Standby Texture"));
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.PropertyField(_standbyScreenRenderer, new GUIContent("Standby Screen Renderer"));
        EditorGUI.EndDisabledGroup();
        if (_standbyTexture.objectReferenceValue != null &&
            _standbyScreenRenderer.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox(
                "Rebuild the HamaStream prefab to create the standby screen overlay.",
                MessageType.Warning);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Playback Area", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_playbackArea);

        BoxCollider playbackArea = _playbackArea.objectReferenceValue as BoxCollider;
        if (playbackArea == null)
        {
            EditorGUILayout.HelpBox("Playback Area is required.", MessageType.Error);
        }
        else if (!playbackArea.isTrigger)
        {
            EditorGUILayout.HelpBox("Playback Area must have Is Trigger enabled.", MessageType.Error);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Loading", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_urlLoadCooldownSeconds);
        EditorGUILayout.PropertyField(_urlLoadPeers, true);
        EditorGUILayout.HelpBox(
            "After adding, duplicating or removing players, run Tools > HamaStream > Configure Scene URL Load Peers, then save the scene. The minimum shared interval is 5 seconds.",
            MessageType.Info);

        if (!serializedObject.ApplyModifiedProperties())
        {
            return;
        }

        HamaStreamController controller = (HamaStreamController)target;
        UdonSharpEditorUtility.CopyProxyToUdon(controller, ProxySerializationPolicy.All);
        EditorUtility.SetDirty(controller);
    }
}
