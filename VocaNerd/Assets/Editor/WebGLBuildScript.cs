using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VocaNerd.EditorTools
{
    public static class WebGLBuildScript
    {
        private const string DefaultBuildPath = "Builds/WebGL";
        private const string VideoSourceDir = "Assets/Video";
        private const string StreamingAssetsDirName = "StreamingAssets";
        private static readonly string[] SafeBuildRootNames = { "Build", "Builds", "build", "builds" };

        [MenuItem("VocaNerd/Build/WebGL")]
        public static void Build()
        {
            var args = ParseCommandLineArgs();
            var buildPath = GetBuildPath(GetOption(args, "buildPath", DefaultBuildPath));
            var cleanBuild = HasFlag(args, "cleanBuild");
            var developmentBuild = HasFlag(args, "development");
            var scenes = GetEnabledScenes();
            var videoFileNames = GetReferencedVideoFileNames();

            if (scenes.Length == 0)
                throw new InvalidOperationException("[WebGLBuildScript] No enabled scenes found in EditorBuildSettings.");

            if (cleanBuild)
                CleanBuildPath(buildPath);

            Directory.CreateDirectory(buildPath);

            Debug.Log("[WebGLBuildScript] Switching active build target to WebGL.");
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new InvalidOperationException("[WebGLBuildScript] Failed to switch build target to WebGL. Make sure the WebGL build support module is installed.");

            var options = developmentBuild ? BuildOptions.Development : BuildOptions.None;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildPath,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = options,
            });

            var summary = report.summary;
            Debug.Log($"[WebGLBuildScript] Result: {summary.result}, size: {summary.totalSize} bytes, time: {summary.totalTime}.");

            if (summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"[WebGLBuildScript] WebGL build failed: {summary.result}");

            CopyVideoFiles(buildPath, videoFileNames);
        }

        private static string[] GetEnabledScenes()
        {
            return EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
        }

        private static string GetBuildPath(string buildPath)
        {
            if (Path.IsPathRooted(buildPath))
                return Path.GetFullPath(buildPath);

            return Path.GetFullPath(Path.Combine(GetProjectRoot().FullName, buildPath));
        }

        private static void CleanBuildPath(string buildPath)
        {
            if (!IsSafeBuildPath(buildPath))
                throw new InvalidOperationException($"[WebGLBuildScript] Refusing to delete unsafe build path: {buildPath}");

            if (!Directory.Exists(buildPath))
                return;

            Directory.Delete(buildPath, true);
        }

        private static void CopyVideoFiles(string buildPath, IReadOnlyCollection<string> videoFileNames)
        {
            if (videoFileNames.Count == 0)
            {
                Debug.LogWarning("[WebGLBuildScript] No MiniGameData video files were found.");
                return;
            }

            var projectRoot = GetProjectRoot();
            var sourceDir = Path.Combine(projectRoot.FullName, VideoSourceDir);

            var targetDir = Path.Combine(buildPath, StreamingAssetsDirName);
            Directory.CreateDirectory(targetDir);

            var copiedCount = 0;
            foreach (var fileName in videoFileNames)
            {
                var source = Path.Combine(sourceDir, fileName);
                var target = Path.Combine(targetDir, fileName);
                File.Copy(source, target, true);
                copiedCount++;
            }

            Debug.Log($"[WebGLBuildScript] Copied {copiedCount} video file(s) to {targetDir}.");
        }

        private static IReadOnlyCollection<string> GetReferencedVideoFileNames()
        {
            var projectRoot = GetProjectRoot();
            var sourceDir = Path.Combine(projectRoot.FullName, VideoSourceDir);
            var fileNames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var guid in AssetDatabase.FindAssets("t:MiniGameData"))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<MiniGameData>(assetPath);
                if (data == null)
                    continue;

                var fileName = NormalizeVideoFileName(data.VideoFileName, assetPath);
                var source = Path.Combine(sourceDir, fileName);
                if (!File.Exists(source))
                    throw new InvalidOperationException($"[WebGLBuildScript] Video file was not found for {assetPath}: {source}");

                fileNames.Add(fileName);
            }

            return fileNames.ToArray();
        }

        private static string NormalizeVideoFileName(string fileName, string assetPath)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new InvalidOperationException($"[WebGLBuildScript] Video file name is empty: {assetPath}");

            fileName = fileName.Trim();
            if (fileName.IndexOfAny(new[] { '/', '\\' }) >= 0)
                throw new InvalidOperationException($"[WebGLBuildScript] Video file name must not contain a path: {assetPath} ({fileName})");

            var safeFileName = Path.GetFileName(fileName);
            if (!string.Equals(fileName, safeFileName, StringComparison.Ordinal))
                throw new InvalidOperationException($"[WebGLBuildScript] Video file name must not contain a path: {assetPath} ({fileName})");

            if (!fileName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"[WebGLBuildScript] Video file must be an .mp4 file: {assetPath} ({fileName})");

            return fileName;
        }

        private static bool IsSafeBuildPath(string buildPath)
        {
            var projectRoot = GetProjectRoot();
            var repoRoot = projectRoot.Parent;
            var safeRoots = SafeBuildRootNames.SelectMany(rootName =>
            {
                var roots = new List<string> { Path.Combine(projectRoot.FullName, rootName) };
                if (repoRoot != null)
                    roots.Add(Path.Combine(repoRoot.FullName, rootName));
                return roots;
            });

            return safeRoots.Any(root => IsSameOrChildPath(buildPath, root));
        }

        private static bool IsSameOrChildPath(string path, string root)
        {
            var fullPath = TrimTrailingSeparators(Path.GetFullPath(path));
            var fullRoot = TrimTrailingSeparators(Path.GetFullPath(root));

            return string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static string TrimTrailingSeparators(string path)
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static DirectoryInfo GetProjectRoot()
        {
            var projectRoot = Directory.GetParent(Application.dataPath);
            if (projectRoot == null)
                throw new InvalidOperationException("[WebGLBuildScript] Could not resolve Unity project root.");

            return projectRoot;
        }

        private static Dictionary<string, string> ParseCommandLineArgs()
        {
            var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var args = Environment.GetCommandLineArgs();

            for (var i = 0; i < args.Length; i++)
            {
                var key = args[i];
                if (!key.StartsWith("-", StringComparison.Ordinal))
                    continue;

                key = key.TrimStart('-');
                var value = "true";
                if (i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal))
                    value = args[++i];

                parsed[key] = value;
            }

            return parsed;
        }

        private static string GetOption(IReadOnlyDictionary<string, string> args, string name, string defaultValue)
        {
            return args.TryGetValue(name, out var value) ? value : defaultValue;
        }

        private static bool HasFlag(IReadOnlyDictionary<string, string> args, string name)
        {
            return args.TryGetValue(name, out var value) && bool.TryParse(value, out var parsed) && parsed;
        }
    }
}
