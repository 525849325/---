using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ImmortalLoot.Editor
{
    public static class PlayerBuildTools
    {
        [MenuItem("ImmortalLoot/Build/Windows Development Player")]
        public static void BuildWindowsDevelopmentPlayer()
        {
            const string output = "Build/Windows/ImmortalLoot.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Game/Scenes/Main.unity" },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Windows Player build failed: {report.summary.result}, {report.summary.totalErrors} errors.");
            Debug.Log($"ImmortalLoot Windows Player built: {report.summary.totalSize} bytes in {report.summary.totalTime}.");
        }

        [MenuItem("ImmortalLoot/Build/WebGL Development Player")]
        public static void BuildWebGlDevelopmentPlayer()
        {
            const string output = "Build/WebGL";
            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Game/Scenes/Main.unity" },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"WebGL Player build failed: {report.summary.result}, {report.summary.totalErrors} errors.");
            Debug.Log($"ImmortalLoot WebGL Player built: {report.summary.totalSize} bytes in {report.summary.totalTime}.");
        }

        [MenuItem("ImmortalLoot/Build/Android Development APK")]
        public static void BuildAndroidDevelopmentApk() => BuildAndroid(AndroidBuildFlavor.DevelopmentApk);

        [MenuItem("ImmortalLoot/Build/Android Release Candidate APK")]
        public static void BuildAndroidReleaseCandidateApk() => BuildAndroid(AndroidBuildFlavor.ReleaseCandidateApk);

        [MenuItem("ImmortalLoot/Build/Android Release Candidate AAB")]
        public static void BuildAndroidReleaseCandidateAab() => BuildAndroid(AndroidBuildFlavor.ReleaseCandidateAab);

        private static void BuildAndroid(AndroidBuildFlavor flavor)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Android Build Support is not installed. Add android, android-sdk-ndk-tools, and android-open-jdk for Unity 6000.5.10f1.");
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Unable to resolve the Unity project root.");
            var spec = AndroidBuildSpec.Create(flavor, PlayerSettings.bundleVersion);
            var output = Path.Combine(projectRoot, "Build", "Android", spec.OutputFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var stagingOutput = Path.Combine(Path.GetTempPath(), "ImmortalLootBuild", spec.OutputFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(stagingOutput));
            if (File.Exists(stagingOutput)) File.Delete(stagingOutput);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.immortalloot.prototype");
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            EnsureRuntimeShadersIncluded();
            if (flavor != AndroidBuildFlavor.DevelopmentApk && !PlayerSettings.Android.useCustomKeystore)
                Debug.LogWarning("RC uses Unity's default test signing because no custom Android keystore is configured. It is installable for validation but not store-ready.");
            var previousAppBundle = EditorUserBuildSettings.buildAppBundle;
            try
            {
                EditorUserBuildSettings.buildAppBundle = spec.BuildAppBundle;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Game/Scenes/Main.unity" },
                    locationPathName = stagingOutput,
                    target = BuildTarget.Android,
                    options = spec.Options
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException($"Android build failed: {report.summary.result}, {report.summary.totalErrors} errors.");
                if (!File.Exists(stagingOutput))
                    throw new FileNotFoundException("Unity reported success but did not create the Android artifact.", stagingOutput);
                File.Copy(stagingOutput, output, true);
                Debug.Log($"Android artifact built at '{output}': {report.summary.totalSize} bytes in {report.summary.totalTime}.");
            }
            finally { EditorUserBuildSettings.buildAppBundle = previousAppBundle; }
        }

        private static void EnsureRuntimeShadersIncluded()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0)
                throw new InvalidOperationException("Unable to load ProjectSettings/GraphicsSettings.asset.");

            var settings = new SerializedObject(assets[0]);
            var shaders = settings.FindProperty("m_AlwaysIncludedShaders")
                ?? throw new InvalidOperationException("Unable to find m_AlwaysIncludedShaders.");
            foreach (var shaderName in new[] { "UI/Default", "UI/Default Font", "Sprites/Default" })
            {
                var shader = Shader.Find(shaderName);
                if (shader == null)
                    continue;

                var alreadyIncluded = false;
                for (var i = 0; i < shaders.arraySize; i++)
                {
                    if (shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    {
                        alreadyIncluded = true;
                        break;
                    }
                }

                if (!alreadyIncluded)
                {
                    shaders.InsertArrayElementAtIndex(shaders.arraySize);
                    shaders.GetArrayElementAtIndex(shaders.arraySize - 1).objectReferenceValue = shader;
                }
            }

            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }
    }
}
