using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KingsCourt.EditorTools
{
    /// <summary>Builds the WebGL version (the phone-in-a-browser build). Menu: King's Court / Build WebGL.</summary>
    public static class WebBuild
    {
        [MenuItem("King's Court/Build WebGL")]
        public static void Build()
        {
            string output = "Builds/WebGL";
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-buildOutput") output = args[i + 1];
            Directory.CreateDirectory(output);

            PlayerSettings.productName = "Kings Court";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;   // plain files, works on any simple web server
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            // Assets/WebGLTemplates/KingsCourt: fills the whole browser viewport on every device instead of
            // Unity's stock template's small fixed-size box on desktop, which rendered card text (and
            // everything else) at a much lower effective pixel density there than on a phone filling its
            // real screen — that's what made text look sharp on a phone but blurry on a laptop/PC.
            PlayerSettings.WebGL.template = "PROJECT:KingsCourt";

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/KingsCourtTable.unity" },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("WEBBUILD result=" + report.summary.result + " size=" + report.summary.totalSize + " errors=" + report.summary.totalErrors + " time=" + report.summary.totalTime);
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }
    }
}
