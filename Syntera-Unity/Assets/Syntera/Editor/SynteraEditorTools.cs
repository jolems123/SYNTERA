#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Syntera.Editor
{
    public static class SynteraEditorTools
    {
        private const string ScenePath="Assets/Syntera/Scenes/City.unity";
        [MenuItem("SYNTERA/Create Starter Scene")]
        public static void CreateScene()
        {
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
                Debug.Log("Opened existing SYNTERA scene");
                return;
            }
            Directory.CreateDirectory("Assets/Syntera/Scenes");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("SYNTERA Boot").AddComponent<Syntera.SynteraGame>();
            EditorSceneManager.SaveScene(scene,ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            AssetDatabase.SaveAssets();
            Debug.Log("SYNTERA starter scene generated. Press Play.");
        }
        [MenuItem("SYNTERA/Build Windows Player Locally")]
        public static void BuildWindows()
        {
            SynteraReleaseGate.Run();
            CreateScene();
            EnsureStandardShaderIncluded();
            Directory.CreateDirectory("Builds/Windows");
            var options=new BuildPlayerOptions {
                scenes=new[]{ScenePath},
                locationPathName="Builds/Windows/SYNTERA.exe",
                target=BuildTarget.StandaloneWindows64,
                options=BuildOptions.None
            };
            var report=BuildPipeline.BuildPlayer(options);
            if (report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception("SYNTERA Windows build failed: "+report.summary.result);
            Debug.Log("Windows player built locally: Builds/Windows/SYNTERA.exe");
        }
        // Materials are created at runtime with the Standard shader; without this, the player
        // build strips it and Shader.Find returns null (blank screen).
        private static void EnsureStandardShaderIncluded()
        {
            var shader = Shader.Find("Standard");
            if (shader == null) throw new System.Exception("Standard shader not found in the editor.");
            var so = new SerializedObject(UnityEngine.Rendering.GraphicsSettings.GetGraphicsSettings());
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) return;
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("Added Standard shader to Always Included Shaders.");
        }

        public static void BatchBuildWindows()
        {
            BuildWindows();
        }
    }
}
#endif
