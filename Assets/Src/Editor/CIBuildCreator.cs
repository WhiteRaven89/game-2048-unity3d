using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace mrathod
{
    public static class CIBuildCreator
    {
        /*
        public static void BuildPlayer(string path, BuildTarget buildTarget)
        {
            // Get the list of scene names that are added to the build list
            List<string> activeScenes = new List<string>();
            foreach (var buildSettingScene in EditorBuildSettings.scenes)
            {
                if (buildSettingScene.enabled)
                {
                    activeScenes.Add(buildSettingScene.path);
                }
            }
            AssetDatabase.Refresh();

            //Setup the rest and build based on the given platform
            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = activeScenes.ToArray(),
                target = buildTarget,
                locationPathName = path,
            };

            BuildPipeline.BuildPlayer(buildPlayerOptions);
        }

        public static void BuildWindowsPlayer()
        {
            BuildPlayer("output/2048Test.exe", BuildTarget.StandaloneWindows);
        }
        */
        public static void BuildGame()
        {
            string platform = GetCommandLineParameter("platform");

            if (platform.Equals(""))
            {
                throw new Exception("Platform information is missing");
            }

            //string debug = GetCommandLineParameter("debug");

            // Get the list of scene names that are added to the build list
            List<string> activeScenes = new List<string>();
            foreach (var buildSettingScene in EditorBuildSettings.scenes)
            {
                if (buildSettingScene.enabled)
                {
                    activeScenes.Add(buildSettingScene.path);
                }
            }
            AssetDatabase.Refresh();

            BuildTarget buildTarget = (BuildTarget)Enum.Parse(typeof(BuildTarget), platform);
            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = activeScenes.ToArray(),
                target = buildTarget,
            };
            //Setup the rest and build based on the given platform

            switch (buildTarget)
            {
                case BuildTarget.StandaloneWindows:
                    buildPlayerOptions.locationPathName = GetBuildPath(buildTarget);
                    break;
                case BuildTarget.iOS:
                    buildPlayerOptions.locationPathName = GetBuildPath(buildTarget);
                    break;
                case BuildTarget.Android:
                    buildPlayerOptions.locationPathName = GetBuildPath(buildTarget);
                    break;
                default:
                    throw new Exception("Platform is not implemented");
            }
            var buildReport = BuildPipeline.BuildPlayer(buildPlayerOptions);
            if (buildReport.summary.result != BuildResult.Succeeded)
            {
                throw new Exception("BuildPlayer failure: " + buildReport.summary.result);
            }
        }
        // Helper function for getting the command line arguments
        private static string GetCommandLineParameter(string parameterName)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int iParam = 0;
            string targetParam = "-" + parameterName;
            foreach (string argument in args)
            {
                iParam++;
                if (argument.Equals(targetParam))
                {
                    return args[iParam];
                }
            }
            return "";
        }

        private static string GetBuildPath(BuildTarget target)
        {
            string buildPath = $"/output/{PlayerSettings.bundleVersion}/";

            switch (target)
            {
                case BuildTarget.StandaloneWindows:
                    return buildPath + target + "/Game2048.exe";
                case BuildTarget.iOS:
                    return buildPath + target + "/" + PlayerSettings.iOS.buildNumber+"/";
                case BuildTarget.Android:
                    return buildPath + target + "/" + "Game2048_" + PlayerSettings.Android.bundleVersionCode + ".apk";
            }
            throw new Exception($"Platform {target} is not implemented");
        }
    }
}
