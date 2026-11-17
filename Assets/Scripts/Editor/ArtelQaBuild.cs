using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public static class ArtelQaBuild
{
    public static void BuildWindows64()
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled)
                .Select(scene => scene.path).ToArray(),
            locationPathName = "Build/ArtelQa/Local/ArcaneCasters.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development,
            extraScriptingDefines = new[] { "ARCANECASTERS_LOCAL_QA_PLAYER" }
        });

        if (report == null || report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException($"ARTEL QA build failed: {report?.summary.result}");
    }
}
