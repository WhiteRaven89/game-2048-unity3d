using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace mrathod
{
    public static class CIBuildCreator
    {
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
    }
}
