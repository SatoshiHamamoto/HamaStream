using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HamaStreamController))]
public class HamaStreamControllerEditor : Editor
{
    private static bool _showInternal;
    private SerializedProperty _videoPlayer;
    private SerializedProperty _urlInput;
    private SerializedProperty _loopToggle;
    private SerializedProperty _defaultLoop;
    private SerializedProperty _statusText;
    private SerializedProperty _currentUrlText;
    private SerializedProperty _ownerText;
    private SerializedProperty _playlistControllers;
    private SerializedProperty _autoPlayOnStart;
    private SerializedProperty _autoPlayUrl;
    private SerializedProperty _enableRenderTextureOutput;
    private SerializedProperty _videoScreenRenderer;
    private SerializedProperty _displayScreenRenderer;
    private SerializedProperty _outputRenderTexture;
    private SerializedProperty _videoBlitMaterial;
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
        _autoPlayOnStart = serializedObject.FindProperty("AutoPlayOnStart");
        _autoPlayUrl = serializedObject.FindProperty("AutoPlayUrl");
        _enableRenderTextureOutput = serializedObject.FindProperty("EnableRenderTextureOutput");
        _videoScreenRenderer = serializedObject.FindProperty("VideoScreenRenderer");
        _displayScreenRenderer = serializedObject.FindProperty("DisplayScreenRenderer");
        _outputRenderTexture = serializedObject.FindProperty("OutputRenderTexture");
        _videoBlitMaterial = serializedObject.FindProperty("VideoBlitMaterial");
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

        EditorGUILayout.LabelField("Playback Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_defaultLoop, new GUIContent("Default Loop"));
        EditorGUILayout.PropertyField(_autoPlayOnStart, new GUIContent("Auto Play On Start"));
        EditorGUILayout.PropertyField(_autoPlayUrl, new GUIContent("Auto Play URL"));

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(_enableRenderTextureOutput, new GUIContent("Enable Output"));
        if (_enableRenderTextureOutput.boolValue)
        {
            EditorGUILayout.PropertyField(_outputRenderTexture, new GUIContent("Output Render Texture"));
            if (_outputRenderTexture.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Assign a Render Texture to receive the video.", MessageType.Warning);
            }
        }
        EditorGUILayout.PropertyField(_standbyTexture, new GUIContent("Standby Texture"));
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
        _showInternal = EditorGUILayout.Foldout(_showInternal, "Internal", true);
        if (_showInternal)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_videoPlayer);
            EditorGUILayout.PropertyField(_urlInput);
            EditorGUILayout.PropertyField(_loopToggle);
            EditorGUILayout.PropertyField(_statusText);
            EditorGUILayout.PropertyField(_currentUrlText);
            EditorGUILayout.PropertyField(_ownerText);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(_playlistControllers, new GUIContent("Playlist Controllers"), true);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.PropertyField(_videoScreenRenderer, new GUIContent("Video Source Renderer"));
            EditorGUILayout.PropertyField(_videoBlitMaterial, new GUIContent("Video Blit Material"));
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(_displayScreenRenderer, new GUIContent("Display Screen Renderer"));
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.PropertyField(_playbackArea);
            EditorGUILayout.PropertyField(_urlLoadCooldownSeconds);
            EditorGUILayout.PropertyField(_urlLoadPeers, true);
            EditorGUI.indentLevel--;
        }
        if (_videoBlitMaterial.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("Rebuild the HamaStream prefab to assign the ProTV blit material.", MessageType.Warning);
        }
        if (_displayScreenRenderer.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("Rebuild the HamaStream prefab to create the display screen.", MessageType.Warning);
        }

        if (!serializedObject.ApplyModifiedProperties())
        {
            return;
        }

        HamaStreamController controller = (HamaStreamController)target;
        UdonSharpEditorUtility.CopyProxyToUdon(controller, ProxySerializationPolicy.All);
        EditorUtility.SetDirty(controller);
    }
}
