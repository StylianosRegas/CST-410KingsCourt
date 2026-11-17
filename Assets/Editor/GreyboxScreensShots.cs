using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace KingsCourt.EditorTools
{
    /// <summary>One-off: renders each of the 5 Greybox_Screens panels to a PNG without entering Play
    /// mode, same headless RenderTexture approach as ScreenshotTool. Not part of the deliverable -
    /// delete before merging anything, if this ever gets merged at all.
    /// Unity.exe -batchmode -quit -executeMethod KingsCourt.EditorTools.GreyboxScreensShots.Capture -shots &lt;dir&gt;</summary>
    public static class GreyboxScreensShots
    {
        private const int W = 1920, H = 1080;

        [MenuItem("King's Court/Capture Greybox Screenshots")]
        public static void Capture()
        {
            string dir = "Docs/Screenshots/Greybox";
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-shots") dir = args[i + 1];
            Directory.CreateDirectory(dir);

            EditorSceneManager.OpenScene("Assets/Scenes/Greybox_Screens.unity", OpenSceneMode.Single);
            var canvasGO = GameObject.Find("Canvas");
            var canvas = canvasGO.GetComponent<Canvas>();

            var camGO = new GameObject("ShotCam", typeof(Camera));
            var cam = camGO.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            cam.orthographicSize = H / 2f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            camGO.transform.position = new Vector3(0, 0, -10);

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5f;

            string[] names = { "01_MainMenu", "02_Setup", "03_MainPlay", "04_CardZoom", "05_End" };
            for (int i = 0; i < names.Length; i++)
            {
                for (int j = 0; j < names.Length; j++)
                {
                    var t = canvasGO.transform.Find(names[j]);
                    if (t != null) t.gameObject.SetActive(j == i);
                }
                Shot(cam, canvas, Path.Combine(dir, names[i] + ".png"));
            }

            Debug.Log("GREYBOX_SHOTS_DONE dir=" + dir);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void Shot(Camera cam, Canvas canvas, string path)
        {
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            for (int i = 0; i < 3; i++)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(canvas.GetComponent<RectTransform>());
            }
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Debug.Log("Saved " + path);
        }
    }
}
