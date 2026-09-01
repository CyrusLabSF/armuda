using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ArmudaBuildPipeline
{
    private const string ProductName = "Armuda";
    private const string CompanyName = "CyFi Network Corporation";
    private const string Version = "0.1.1";
    private const string AndroidIdentifier = "com.cyfinetwork.armuda";
    private const string StandaloneIdentifier = "com.cyfinetwork.armuda";
    private const string IconAssetPath = "Assets/Branding/Armuda-App-Icon.png";
    private const string SigningConfigEnvironmentVariable = "ARMUDA_ANDROID_SIGNING_CONFIG";
    private const string DefaultSigningConfigFileName = "android-signing.env";

    [MenuItem("Armuda/Build/Validate Packaging")]
    public static void ValidateProject()
    {
        ApplyPlayerSettings();
        string[] scenes = GetEnabledScenes();
        ValidateSceneFiles(scenes);
        AuditMissingScripts(scenes);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Armuda Build] Packaging validation passed. Scenes: {string.Join(", ", scenes)}");
    }

    [MenuItem("Armuda/Build/Audit Missing Scripts")]
    public static void AuditMissingScripts()
    {
        string[] scenes = GetEnabledScenes();
        ValidateSceneFiles(scenes);
        AuditMissingScripts(scenes);
        Debug.Log("[Armuda Build] Missing-script audit passed.");
    }

    [MenuItem("Armuda/Build/Windows Desktop")]
    public static void BuildWindows()
    {
        ApplyPlayerSettings();
        string root = ResolveOutputRoot();
        string outputPath = Path.Combine(root, "Windows", "Armuda.exe");
        Build(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64, outputPath);
    }

    [MenuItem("Armuda/Build/Android APK")]
    public static void BuildAndroid()
    {
        ApplyPlayerSettings();
        try
        {
            ApplyAndroidSigning();
            PrepareAndroidGradleEnvironment();
            EditorUserBuildSettings.buildAppBundle = false;
            string root = ResolveOutputRoot();
            string outputPath = Path.Combine(root, "Android", $"Armuda-{Version}.apk");
            Build(BuildTargetGroup.Android, BuildTarget.Android, outputPath);
        }
        finally
        {
            ClearAndroidSigning();
        }
    }

    private static void Build(BuildTargetGroup group, BuildTarget target, string outputPath)
    {
        string[] scenes = GetEnabledScenes();
        ValidateSceneFiles(scenes);
        AuditMissingScripts(scenes);

        string directory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("Armuda output directory could not be resolved.");
        }

        Directory.CreateDirectory(directory);

        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(group, target))
        {
            throw new InvalidOperationException($"Could not switch Unity to build target {target}.");
        }

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = target,
            targetGroup = group,
            options = BuildOptions.StrictMode | BuildOptions.CompressWithLz4HC
        };

        Debug.Log($"[Armuda Build] Starting {target} build at {outputPath}");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException(
                $"Armuda {target} build failed: {summary.totalErrors} errors, {summary.totalWarnings} warnings.");
        }

        Debug.Log(
            $"[Armuda Build] Succeeded: {outputPath} ({summary.totalSize} bytes, {summary.totalTime}).");
    }

    private static void ApplyPlayerSettings()
    {
        PlayerSettings.companyName = CompanyName;
        PlayerSettings.productName = ProductName;
        PlayerSettings.bundleVersion = Version;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidIdentifier);
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, StandaloneIdentifier);
        PlayerSettings.Android.bundleVersionCode = 2;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
        // Armuda's Android XR profile targets Magic Leap 2. Its Unity runtime
        // and native provider libraries require the Android x86-64 ABI.
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.X86_64;
        PlayerSettings.Android.renderOutsideSafeArea = false;
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.usePlayerLog = true;
        PlayerSettings.SplashScreen.backgroundColor = Color.black;
        ApplyBranding();
    }

    private static void ApplyBranding()
    {
        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconAssetPath);
        if (icon == null)
        {
            throw new BuildFailedException($"Armuda branding icon is missing: {IconAssetPath}");
        }

        ApplyIcon(NamedBuildTarget.Standalone, icon);
        ApplyIcon(NamedBuildTarget.Android, icon);
    }

    private static void ApplyIcon(NamedBuildTarget target, Texture2D icon)
    {
        int[] sizes = PlayerSettings.GetIconSizes(target, IconKind.Any);
        if (sizes.Length == 0)
        {
            return;
        }

        Texture2D[] icons = Enumerable.Repeat(icon, sizes.Length).ToArray();
        PlayerSettings.SetIcons(target, icons, IconKind.Any);
    }

    private static void ApplyAndroidSigning()
    {
        string configPath = Environment.GetEnvironmentVariable(SigningConfigEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configPath))
        {
            configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".armuda",
                "signing",
                DefaultSigningConfigFileName);
        }

        configPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(configPath));
        if (!File.Exists(configPath))
        {
            throw new BuildFailedException(
                $"Armuda Android signing configuration was not found at {configPath}. " +
                "Run Tools/Packaging/Initialize-ArmudaAndroidSigning.ps1 first.");
        }

        Dictionary<string, string> config = LoadSigningConfig(configPath);
        string keystorePath = ReadSigningSetting(config, "ARMUDA_ANDROID_KEYSTORE_PATH");
        if (!Path.IsPathRooted(keystorePath))
        {
            string configDirectory = Path.GetDirectoryName(configPath) ?? string.Empty;
            keystorePath = Path.Combine(configDirectory, keystorePath);
        }

        keystorePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(keystorePath));
        if (!File.Exists(keystorePath))
        {
            throw new BuildFailedException($"Armuda Android keystore was not found at {keystorePath}.");
        }

        string alias = ReadSigningSetting(config, "ARMUDA_ANDROID_KEY_ALIAS");
        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = keystorePath;
        PlayerSettings.Android.keystorePass = ReadSigningSetting(config, "ARMUDA_ANDROID_KEYSTORE_PASSWORD");
        PlayerSettings.Android.keyaliasName = alias;
        PlayerSettings.Android.keyaliasPass = ReadSigningSetting(config, "ARMUDA_ANDROID_KEY_PASSWORD");

        Debug.Log($"[Armuda Build] Production Android signing configured for alias '{alias}'.");
    }

    private static Dictionary<string, string> LoadSigningConfig(string configPath)
    {
        Dictionary<string, string> config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string rawLine in File.ReadAllLines(configPath))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            int separator = line.IndexOf('=');
            if (separator <= 0)
            {
                throw new BuildFailedException($"Invalid Armuda signing configuration line in {configPath}.");
            }

            string key = line.Substring(0, separator).Trim();
            string value = line.Substring(separator + 1).Trim();
            config[key] = value;
        }

        return config;
    }

    private static string ReadSigningSetting(Dictionary<string, string> config, string key)
    {
        string environmentValue = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return environmentValue.Trim();
        }

        if (config.TryGetValue(key, out string configValue) && !string.IsNullOrWhiteSpace(configValue))
        {
            return configValue.Trim();
        }

        throw new BuildFailedException($"Armuda Android signing setting '{key}' is missing.");
    }

    private static void ClearAndroidSigning()
    {
        PlayerSettings.Android.keystorePass = string.Empty;
        PlayerSettings.Android.keyaliasPass = string.Empty;
        PlayerSettings.Android.keyaliasName = string.Empty;
        PlayerSettings.Android.keystoreName = string.Empty;
        PlayerSettings.Android.useCustomKeystore = false;
        AssetDatabase.SaveAssets();
    }

    private static void PrepareAndroidGradleEnvironment()
    {
        // Java's Windows selector can fail to create its local Gradle socket when
        // the inherited temporary path is unsuitable for an AF_UNIX socket. Keep
        // Armuda's build temp path short and local so command-line builds remain
        // reproducible on the packaging workstation.
        string driveRoot = Path.GetPathRoot(Path.GetFullPath(Application.dataPath));
        string tempPath = Path.Combine(
            string.IsNullOrWhiteSpace(driveRoot) ? Path.GetTempPath() : driveRoot,
            "ArmudaGradleTemp");
        Directory.CreateDirectory(tempPath);

        Environment.SetEnvironmentVariable("TEMP", tempPath, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("TMP", tempPath, EnvironmentVariableTarget.Process);

        string javaToolOptions = Environment.GetEnvironmentVariable("JAVA_TOOL_OPTIONS") ?? string.Empty;
        if (javaToolOptions.IndexOf("-Djava.io.tmpdir=", StringComparison.OrdinalIgnoreCase) < 0)
        {
            string separator = string.IsNullOrWhiteSpace(javaToolOptions) ? string.Empty : " ";
            Environment.SetEnvironmentVariable(
                "JAVA_TOOL_OPTIONS",
                $"{javaToolOptions}{separator}-Djava.io.tmpdir={tempPath}",
                EnvironmentVariableTarget.Process);
        }
    }

    private static string[] GetEnabledScenes()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            throw new BuildFailedException("Armuda has no enabled scenes in Build Settings.");
        }

        return scenes;
    }

    private static void ValidateSceneFiles(string[] scenes)
    {
        foreach (string scene in scenes)
        {
            if (!File.Exists(scene))
            {
                throw new BuildFailedException($"Enabled scene does not exist: {scene}");
            }
        }
    }

    private static void AuditMissingScripts(string[] scenes)
    {
        int missingScriptCount = 0;

        foreach (string scenePath in scenes)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                    if (count <= 0)
                    {
                        continue;
                    }

                    missingScriptCount += count;
                    Debug.LogError(
                        $"[Armuda Build] {count} missing script(s): {GetHierarchyPath(transform)} in {scenePath}");
                }
            }
        }

        if (missingScriptCount > 0)
        {
            throw new BuildFailedException(
                $"Armuda packaging found {missingScriptCount} missing script reference(s). See the build log for object paths.");
        }
    }

    private static string GetHierarchyPath(Transform transform)
    {
        string path = transform.name;
        Transform parent = transform.parent;
        while (parent != null)
        {
            path = $"{parent.name}/{path}";
            parent = parent.parent;
        }

        return path;
    }

    private static string ResolveOutputRoot()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int index = 0; index < arguments.Length - 1; index++)
        {
            if (string.Equals(arguments[index], "-armudaOutputRoot", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(arguments[index + 1]);
            }
        }

        return Path.GetFullPath(Path.Combine("Releases", $"Armuda-{Version}"));
    }
}
