using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
#if BW_URP
using UnityEngine.Rendering.Universal;
#endif

namespace Bubblewright.EditorTools
{
    /// <summary>
    /// One-click project setup, run automatically the first time the project is opened
    /// (and any time from the menu: Bubblewright > Run Project Setup). Safe to run again.
    ///
    /// 1. URP asset with the 2D Renderer, assigned in Graphics and every Quality level.
    /// 2. Tuning asset (all the sheet's numbers) and an unlit sprite material in Resources.
    /// 3. Player settings: name, landscape only on phones, bundle id.
    /// 4. Prototype scene with the Game object and camera, added to Build Settings.
    /// Sorting layers come from ProjectSettings/TagManager.asset in the repo.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        const string Root = "Assets/Bubblewright";
        const string SettingsFolder = Root + "/Settings";
        const string PipelinePath = SettingsFolder + "/URP-2D.asset";
        const string RendererPath = SettingsFolder + "/URP-2D-Renderer.asset";
        const string ResourcesFolder = Root + "/Resources";
        const string TuningPath = ResourcesFolder + "/Tuning.asset";
        const string MaterialPath = ResourcesFolder + "/SpriteUnlit.mat";
        const string ScenePath = "Assets/Scenes/Prototype.unity";

        static ProjectSetup()
        {
            EditorApplication.delayCall += AutoRun;
        }

        static void AutoRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!NeedsSetup()) return;
            Run(false);
        }

        static bool NeedsSetup() =>
            !File.Exists(TuningPath) || !File.Exists(ScenePath)
#if BW_URP
            || !File.Exists(PipelinePath) || !File.Exists(MaterialPath) || GraphicsSettings.defaultRenderPipeline == null
#endif
            ;

        [MenuItem("Bubblewright/Run Project Setup")]
        static void RunFromMenu() => Run(true);

        [MenuItem("Bubblewright/Open Prototype Scene")]
        static void OpenScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        static void Run(bool fromMenu)
        {
            EnsureFolder(Root, "Settings");
            EnsureFolder(Root, "Resources");
            EnsureFolder("Assets", "Scenes");

            SetupPipeline();
            SetupTuning();
            SetupSpriteMaterial();
            SetupPlayer();
            SetupScene();

            AssetDatabase.SaveAssets();
            Debug.Log("[Bubblewright] Project setup done. Open Assets/Scenes/Prototype.unity and press Play.");
            if (fromMenu)
                EditorUtility.DisplayDialog("Bubblewright", "Project setup done.\n\nOpen Assets/Scenes/Prototype.unity and press Play.", "OK");
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }

        static void SetupPipeline()
        {
#if BW_URP
            var rendererData = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<Renderer2DData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            pipeline.supportsHDR = false;     // flat pixel art, cheaper on phones
            pipeline.msaaSampleCount = 1;     // no anti-aliasing on pixel art
            EditorUtility.SetDirty(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);
#else
            Debug.LogWarning("[Bubblewright] URP package not found. Install com.unity.render-pipelines.universal, then run Bubblewright > Run Project Setup.");
#endif
        }

        static void SetupTuning()
        {
            if (AssetDatabase.LoadAssetAtPath<Tuning>(TuningPath) != null) return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<Tuning>(), TuningPath);
        }

        static void SetupSpriteMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null) return;
            var shader = Shader.Find(Game.SpriteUnlitShader);
            if (shader == null) return; // URP not active yet; the game falls back to the default sprite material
            AssetDatabase.CreateAsset(new Material(shader) { name = "SpriteUnlit" }, MaterialPath);
        }

        static void SetupPlayer()
        {
            PlayerSettings.companyName = "Kokan";
            PlayerSettings.productName = "Bubblewright";
            // Phones: landscape only (portrait shows a "turn your phone" prompt if it ever happens).
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.kokan.bubblewright");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.kokan.bubblewright");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.kokan.bubblewright");
        }

        static void SetupScene()
        {
            if (!File.Exists(ScenePath))
            {
                var active = SceneManager.GetActiveScene();
                bool replaceUntitled = string.IsNullOrEmpty(active.path) && !active.isDirty;

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                    replaceUntitled ? NewSceneMode.Single : NewSceneMode.Additive);

                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                var cam = camGo.AddComponent<Camera>();
                Game.ConfigureCamera(cam);
                SceneManager.MoveGameObjectToScene(camGo, scene);

                var game = new GameObject("Game");
                game.AddComponent<Game>();
                SceneManager.MoveGameObjectToScene(game, scene);

                EditorSceneManager.SaveScene(scene, ScenePath);
                if (!replaceUntitled) EditorSceneManager.CloseScene(scene, true);
            }

            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes) if (s.path == ScenePath) return;
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            list.AddRange(scenes);
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
