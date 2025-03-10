// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using UnityEngine;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

namespace UnityEditor
{
    [System.Serializable]
    internal class AvatarSubEditor : ScriptableObject
    {
        /*
        // Will be used to patch animation when handiness changes.
        public class AvatarSetter : AssetPostprocessor
        {
            public void OnPostprocessModel(GameObject go)
            {
                ModelImporter modelImporter = (ModelImporter)assetImporter;
                ModelImporterEditor inspector = ActiveEditorTracker.MakeCustomEditor(modelImporter) as ModelImporterEditor;

                SerializedProperty humanDescription = inspector.serializedObject.FindProperty("m_HumanDescription");

                Avatar avatar = AssetDatabase.LoadAssetAtPath("Assets/1_Characters/Dude/Dude.fbx", typeof(UnityEngine.Avatar)) as Avatar;
                if (avatar == null)
                    Debug.Log("Could not find avatar when importing : " + modelImporter.assetPath);

                if (avatar != null && modelImporter != null)
                    modelImporter.UpdateHumanDescription(avatar, humanDescription);

                EditorUtility.SetDirty(inspector);
                EditorUtility.SetDirty(modelImporter);
            }
        }
        */

        /*[MenuItem ("Mecanim/Write All Assets")]
        static void DoWriteAllAssets()
        {
            UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll(typeof(UnityEngine.Object));
            foreach (UnityEngine.Object asset in objects)
            {
                if (AssetDatabase.Contains(asset))
                    EditorUtility.SetDirty(asset);
            }
            AssetDatabase.SaveAssets();
        }*/

        protected AvatarEditor m_Inspector;
        protected GameObject gameObject { get { return m_Inspector.m_GameObject; } }
        protected GameObject prefab { get { return m_Inspector.prefab; } }
        protected Dictionary<Transform, bool> modelBones { get { return m_Inspector.m_ModelBones; } }
        protected Transform root { get { return gameObject == null ? null : gameObject.transform; } }
        protected SerializedObject serializedObject { get { return m_Inspector.serializedAssetImporter; } }
        protected Avatar avatarAsset { get { return m_Inspector.avatar; } }

        public virtual void Enable(AvatarEditor inspector)
        {
            this.m_Inspector = inspector;
        }

        public virtual void Disable()
        {
        }

        public virtual void OnDestroy()
        {
            if (HasModified())
            {
                AssetImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(avatarAsset));
                if (importer)
                {
                    if (EditorUtility.DisplayDialog("Unapplied import settings", "Unapplied import settings for \'" + importer.assetPath + "\'", "Apply", "Revert"))
                        ApplyAndImport();
                    else
                        ResetValues();
                }
            }
        }

        public virtual void OnInspectorGUI()
        {
        }

        public virtual void OnSceneGUI()
        {
        }

        protected bool HasModified()
        {
            if (!m_Inspector)
                return false;
            if (serializedObject != null && serializedObject.hasModifiedProperties)
                return true;

            return false;
        }

        protected virtual void ResetValues()
        {
            serializedObject.Update();
        }

        protected void Apply()
        {
            serializedObject.ApplyModifiedProperties();
        }

        public void ApplyAndImport()
        {
            Apply();

            string assetPath = AssetDatabase.GetAssetPath(avatarAsset);
            AssetDatabase.ImportAsset(assetPath);

            ResetValues();
        }

        protected void ApplyRevertGUI()
        {
            EditorGUILayout.Space();

            using (new GUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!HasModified()))
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Revert"))
                    {
                        ResetValues();
                        System.Diagnostics.Debug.Assert(!HasModified(), "Avatar settings are marked as modified after calling Reset.");
                    }

                    if (GUILayout.Button("Apply"))
                    {
                        ApplyAndImport();
                    }
                }

                if (GUILayout.Button("Done"))
                {
                    m_Inspector.SwitchToAssetMode();
                    GUIUtility.ExitGUI();
                }
            }
        }
    }

    [CustomEditor(typeof(Avatar))]
    internal class AvatarEditor : Editor
    {
        private class Styles
        {
            public GUIContent[] tabs =
            {
                EditorGUIUtility.TrTextContent("Mapping"),
                EditorGUIUtility.TrTextContent("Muscles & Settings"),
                //EditorGUIUtility.TrTextContent ("Handle"),
                //EditorGUIUtility.TrTextContent ("Collider")
            };

            public GUIContent editCharacter = EditorGUIUtility.TrTextContent("Configure Avatar");

            public GUIContent reset = EditorGUIUtility.TrTextContent("Reset");
        }

        static Styles styles { get { if (s_Styles == null) s_Styles = new Styles(); return s_Styles; } }
        static Styles s_Styles;

        enum EditMode
        {
            NotEditing,
            Starting,
            Editing,
            Stopping
        }

        protected int m_TabIndex;
        internal GameObject m_GameObject;
        internal Dictionary<Transform, bool> m_ModelBones = null;
        private EditMode m_EditMode = EditMode.NotEditing;
        internal bool m_CameFromImportSettings = false;
        private bool m_SwitchToEditMode = false;
        internal static bool s_EditImmediatelyOnNextOpen = false;

        // These member are used when the avatar is part of an asset

        // This is used as a backend by the AvatarSubEditors to serialize
        // only the FBXImporter part and not mess up with the Editor's serializedObject.
        internal SerializedObject m_SerializedAssetImporter = null;
        public SerializedObject serializedAssetImporter
        {
            get
            {
                // TODO find a better sync for that and be in better control of the lifetime
                if (m_SerializedAssetImporter == null)
                {
                    m_SerializedAssetImporter = CreateSerializedImporterForTarget(target);
                }
                return m_SerializedAssetImporter;
            }
        }

        internal Avatar avatar
        {
            get
            {
                var r = target as Avatar;
                return r;
            }
        }

        protected bool m_InspectorLocked;

        // We'd prefer to have just one AvatarEditor member but due to Serialization issues,
        // we need to keep one of each type. It should still be treated as a single one though.
        // I.e. only one should be used at a time; the rest should be null.
        protected AvatarSubEditor editor
        {
            get
            {
                switch (m_TabIndex)
                {
                    case sMuscleTab: return m_MuscleEditor;
                    default:
                    case sMappingTab: return m_MappingEditor;
                }
            }
            set
            {
                switch (m_TabIndex)
                {
                    case sMuscleTab: m_MuscleEditor = value as AvatarMuscleEditor; break;
                    default:
                    case sMappingTab: m_MappingEditor = value as AvatarMappingEditor; break;
                }
            }
        }
        private AvatarMuscleEditor m_MuscleEditor;
        private AvatarMappingEditor m_MappingEditor;

        const int sMappingTab = 0;
        const int sMuscleTab = 1;

        public GameObject prefab
        {
            get
            {
                string path = AssetDatabase.GetAssetPath(target);
                return AssetDatabase.LoadMainAssetAtPath(path) as GameObject;
            }
        }

        static private SerializedObject CreateSerializedImporterForTarget(UnityEngine.Object target)
        {
            SerializedObject so = null;

            AssetImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(target));
            if (importer != null)
            {
                so = new SerializedObject(importer);
            }
            return so;
        }

        void OnEnable()
        {
            EditorApplication.update += Update;
            m_SwitchToEditMode = false;
            if (m_EditMode == EditMode.Editing)
            {
                m_ModelBones = AvatarSetupTool.GetModelBones(m_GameObject.transform, false, null);
                editor.Enable(this);
            }
            else if (m_EditMode == EditMode.NotEditing)
            {
                editor = null;

                if (s_EditImmediatelyOnNextOpen)
                {
                    m_CameFromImportSettings = true;
                    s_EditImmediatelyOnNextOpen = false;
                }
            }
        }

        void OnDisable()
        {
            if (m_EditMode == EditMode.Editing)
                editor.Disable();

            EditorApplication.update -= Update;

            if (m_SerializedAssetImporter != null)
            {
                m_SerializedAssetImporter.Cache(GetInstanceID());
                m_SerializedAssetImporter = null;
            }
        }

        void OnDestroy()
        {
            // If editor is destroyed for some other reason than exiting
            // avatar configuration explicitly - this can for example happen
            // when entering or exiting Play Mode, since this rebuilds all
            // editors in the Inspector - ensure we exit the
            // Avatar Configuration Stage if it's still open.
            // This will also trigger cleanup of the editor.
            if (StageNavigationManager.instance.currentStage is AvatarConfigurationStage)
                StageUtility.GoToMainStage();
        }

        void SelectAsset()
        {
            UnityEngine.Object obj;
            if (m_CameFromImportSettings)
            {
                string path = AssetDatabase.GetAssetPath(target);
                obj = AssetDatabase.LoadMainAssetAtPath(path);
            }
            else
                obj = target;

            Selection.activeObject = obj;
        }

        protected void CreateEditor()
        {
            switch (m_TabIndex)
            {
                case sMuscleTab: editor = ScriptableObject.CreateInstance<AvatarMuscleEditor>(); break;
                default:
                case sMappingTab: editor = ScriptableObject.CreateInstance<AvatarMappingEditor>(); break;
            }

            editor.hideFlags = HideFlags.HideAndDontSave;
            editor.Enable(this);
        }

        protected void DestroyEditor()
        {
            editor.OnDestroy();
            editor = null;
        }

        // @TODO@MECANIM: Implement context Reset - probably best in C++
        /*[MenuItem ("CONTEXT/Avatar/Reset")]
        static void ResetValues (MenuCommand command)
        {
            Debug.Log ("Reset");

            AssetImporter importer = AssetImporter.GetAtPath (AssetDatabase.GetAssetPath (command.context));
            SerializedObject serializedObject = new SerializedObject (importer);

            if (importer && serializedObject != null)
            {
                string sHuman = "m_HumanDescription.m_Human";
                string sSkeleton = "m_HumanDescription.m_Skeleton";
                SerializedProperty human = serializedObject.FindProperty (sHuman);
                if (human != null && human.isArray)
                    human.ClearArray ();

                SerializedProperty skeleton = serializedObject.FindProperty (sSkeleton);
                if (skeleton != null && skeleton.isArray)
                    skeleton.ClearArray ();

                if (GetCurrentEditor () != null)
                {
                    GetCurrentEditor ().ApplyAndImport ();
                    GetCurrentEditor ().OnEnable (this);
                }
            }
        }*/

        public override bool UseDefaultMargins() { return false; }

        public override void OnInspectorGUI()
        {
            GUI.enabled = true;

            using (new EditorGUILayout.VerticalScope(EditorStyles.inspectorFullWidthMargins))
            {
                if (m_EditMode == EditMode.Editing)
                {
                    EditingGUI();
                }
                else if (!m_CameFromImportSettings)
                {
                    EditButtonGUI();
                }
                else
                {
                    if (m_EditMode == EditMode.NotEditing && Event.current.type == EventType.Repaint)
                    {
                        m_SwitchToEditMode = true;
                    }
                }
            }
        }

        void EditButtonGUI()
        {
            if (avatar == null || !avatar.isHuman)
                return;

            // Can only edit avatar from a model importer
            string assetPath = AssetDatabase.GetAssetPath(avatar);
            ModelImporter modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (modelImporter == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(styles.editCharacter, GUILayout.Width(120)))
                {
                    SwitchToEditMode();
                    GUIUtility.ExitGUI();
                }

                GUILayout.FlexibleSpace();
            }
        }

        void EditingGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                {
                    int tabIndex = m_TabIndex;
                    bool wasEnable = GUI.enabled;
                    GUI.enabled = !(avatar == null || !avatar.isHuman);
                    tabIndex = GUILayout.Toolbar(tabIndex, styles.tabs, "LargeButton",
                        GUI.ToolbarButtonSize.FitToContents);
                    GUI.enabled = wasEnable;
                    if (tabIndex != m_TabIndex)
                    {
                        DestroyEditor();
                        if (avatar != null && avatar.isHuman)
                            m_TabIndex = tabIndex;

                        CreateEditor();
                    }
                }
                GUILayout.FlexibleSpace();
            }

            editor.OnInspectorGUI();
        }

        public void OnSceneGUI()
        {
            if (m_EditMode == EditMode.Editing)
                editor.OnSceneGUI();
        }

        internal void SwitchToEditMode()
        {
            // Lock inspector
            ChangeInspectorLock(true);

            string assetPath = AssetDatabase.GetAssetPath(target);
            AvatarConfigurationStage stage = AvatarConfigurationStage.CreateStage(assetPath, this);
            StageUtility.GoToStage(stage, true);

            m_EditMode = EditMode.Starting;

            // Instantiate character
            m_GameObject = stage.gameObject;
            if (serializedAssetImporter.FindProperty("m_OptimizeGameObjects").boolValue)
                AnimatorUtility.DeoptimizeTransformHierarchy(m_GameObject);

            SerializedProperty humanBoneArray = serializedAssetImporter.FindProperty("m_HumanDescription.m_Human");

            // First get all available modelBones
            Dictionary<Transform, bool> modelBones = AvatarSetupTool.GetModelBones(m_GameObject.transform, true, null);
            AvatarSetupTool.BoneWrapper[] humanBones = AvatarSetupTool.GetHumanBones(humanBoneArray, modelBones);

            m_ModelBones = AvatarSetupTool.GetModelBones(m_GameObject.transform, false, humanBones);

            // Unfold all nodes in hierarchy
            // TODO@MECANIM: Only expand actual bones
            foreach (SceneHierarchyWindow shw in Resources.FindObjectsOfTypeAll(typeof(SceneHierarchyWindow)))
                shw.SetExpandedRecursive(m_GameObject.GetInstanceID(), true);

            CreateEditor();

            m_EditMode = EditMode.Editing;
        }

        internal void SwitchToAssetMode()
        {
            StageUtility.GoToMainStage();
        }

        internal void CleanupEditor()
        {
            bool selectAvatarAsset = StageUtility.GetCurrentStageHandle() == StageUtility.GetMainStageHandle();

            m_EditMode = EditMode.Stopping;

            DestroyEditor();

            ChangeInspectorLock(m_InspectorLocked);

            EditorApplication.CallbackFunction CleanUpOnDestroy = null;
            CleanUpOnDestroy = () =>
            {
                // Make sure that we restore the "original" selection if we exit Avatar Editing mode
                // from the avatar tooling itself (e.g by clicking done).
                if (selectAvatarAsset)
                    SelectAsset();

                if (!m_CameFromImportSettings)
                    m_EditMode = EditMode.NotEditing;

                EditorApplication.update -= CleanUpOnDestroy;
            };

            EditorApplication.update += CleanUpOnDestroy;

            // Reset back the Edit Mode specific states (they probably should be better encapsulated)
            m_GameObject = null;
            m_ModelBones = null;
        }

        void ChangeInspectorLock(bool locked)
        {
            foreach (InspectorWindow i in InspectorWindow.GetAllInspectorWindows())
            {
                ActiveEditorTracker activeEditor = i.tracker;
                foreach (Editor e in activeEditor.activeEditors)
                {
                    if (e == this)
                    {
                        m_InspectorLocked = i.isLocked;
                        i.isLocked = locked;
                    }
                }
            }
        }

        public void Update()
        {
            if (m_SwitchToEditMode)
            {
                m_SwitchToEditMode = false;
                SwitchToEditMode();

                Repaint();
            }

            if (m_EditMode == EditMode.Editing)
            {
                if (m_GameObject == null || m_ModelBones == null)
                    SwitchToAssetMode();

                else if (m_ModelBones != null)
                {
                    foreach (KeyValuePair<Transform, bool> pair in m_ModelBones)
                    {
                        if (pair.Key == null)
                        {
                            SwitchToAssetMode();
                            return;
                        }
                    }
                }
            }
        }

        public bool HasFrameBounds()
        {
            if (m_ModelBones != null)
            {
                foreach (KeyValuePair<Transform, bool> pair in m_ModelBones)
                {
                    if (pair.Key == Selection.activeTransform)
                        return true;
                }
            }

            return false;
        }

        public Bounds OnGetFrameBounds()
        {
            Transform bone = Selection.activeTransform;
            Bounds bounds = new Bounds(bone.position, new Vector3(0, 0, 0));
            foreach (Transform child in bone)
                bounds.Encapsulate(child.position);

            if (bone.parent) bounds.Encapsulate(bone.parent.position);

            return bounds;
        }
    }
}
