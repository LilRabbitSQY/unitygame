using System;
using System.IO;
using IOPath = System.IO.Path;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FinalDefense.Tests
{
    // Run only on an isolated project copy. This builds the normal player with
    // the production scene list; TestAssemblies are excluded from the player.
    public static class ReleasePlayerBuilder
    {
        [Serializable]
        private sealed class BuildEvidence
        {
            public string unityVersion, result, target, builtAtUtc, appPath;
            public string[] scenes;
            public int errors, warnings;
            public long bytes;
            public double seconds;
        }

        public static void BuildMacPlayer()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX))
                throw new InvalidOperationException("Installed Unity does not include macOS player support.");
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0 || IOPath.GetFileNameWithoutExtension(scenes[0]) != "MainMenu")
                throw new InvalidOperationException("The normal player must start at the registered MainMenu scene.");
            string appPath = Environment.GetEnvironmentVariable("FINAL_DEFENSE_MAC_BUILD_PATH");
            if (string.IsNullOrEmpty(appPath)) appPath = "/tmp/FinalDefense.app";
            appPath = IOPath.GetFullPath(appPath);
            Directory.CreateDirectory(IOPath.GetDirectoryName(appPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = appPath,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            });
            var summary = report.summary;
            var evidence = new BuildEvidence
            {
                unityVersion = Application.unityVersion, result = summary.result.ToString(), target = summary.platform.ToString(),
                builtAtUtc = DateTime.UtcNow.ToString("O"), appPath = appPath, scenes = scenes,
                errors = (int)summary.totalErrors, warnings = (int)summary.totalWarnings,
                bytes = (long)summary.totalSize, seconds = summary.totalTime.TotalSeconds
            };
            File.WriteAllText(IOPath.Combine(IOPath.GetDirectoryName(appPath), "mac-build-summary.json"), JsonUtility.ToJson(evidence, true));
            if (summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("macOS player build failed: " + summary.result);
            Debug.Log("RELEASE_MAC_BUILD_SUCCESS " + appPath);
        }
    }
}
