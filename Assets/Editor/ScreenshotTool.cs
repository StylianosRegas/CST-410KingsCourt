using System;
using System.IO;
using System.Linq;
using KingsCourt.Core;
using KingsCourt.UnityLayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace KingsCourt.EditorTools
{
    /// <summary>
    /// Renders every screen to a PNG without entering Play mode (nothing needs a window).
    /// Unity.exe -batchmode -projectPath . -executeMethod KingsCourt.EditorTools.ScreenshotTool.Capture -shots ./Docs/Screenshots
    /// </summary>
    public static class ScreenshotTool
    {
        private const int W = 1920, H = 1080;
        private static string dir;

        [MenuItem("King's Court/Capture Screenshots")]
        public static void Capture()
        {
            dir = "Docs/Screenshots";
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-shots") dir = args[i + 1];
            Directory.CreateDirectory(dir);
            foreach (var old in Directory.GetFiles(dir, "*.png")) File.Delete(old);

            // Menus.
            var c = NewController(); c.ShowStart(); Shot(c, "01_start_screen");
            c = NewController(); c.HostGame(); Shot(c, "02_waiting_lobby");
            c = NewController(); c.ShowStart(); c.ShowSettings(); Shot(c, "03_settings");
            c = NewController(); c.ShowStart(); c.ShowHelp(); Shot(c, "04_help_round");
            c = NewController(); c.ShowStart(); c.ShowHelp(); c.HelpPanel.ShowRoles(); Shot(c, "05_help_roles");
            c = NewController(); c.ShowStart(); c.ShowHelp(); c.HelpPanel.ShowCards(); Shot(c, "06_help_cards");

            // In game: waiting for others, and a refused move.
            c = NewController();
            var waiting = FindWaiting(c);
            if (waiting != null)
            {
                c.BeginWith(waiting);
                var refusal = waiting.Submit(-1, 0);
                c.GameScreen.DebugToast(refusal.Ok ? "" : refusal.Reason);
                Shot(c, "07_waiting_and_refused_move");
            }

            // In game: each kind of decision.
            Decide("08_your_turn_play_card", d => d.Kind == ChoiceKind.PlayCard, 1, -1);
            Decide("09_trusted_advisor_picks_cards", d => d.Kind == ChoiceKind.KeepCard && d.Cards.Count >= 3, -1, 1);
            Decide("10_king_enacts_card", d => d.Kind == ChoiceKind.EnactCard, -1, 0);
            Decide("11_choose_trusted_advisor", d => d.Kind == ChoiceKind.TrustedAdvisor, -1, -1);
            Decide("12_choose_a_player", d => d.Kind == ChoiceKind.Target && d.Type == OptionType.Player, -1, -1);
            Decide("13_use_ability", d => d.Kind == ChoiceKind.UseAbility, -1, -1);

            // Game over.
            c = NewController();
            var over = NewSession(c, 8, 21);
            over.Start();
            if (over.Seats[0].Pending != null) over.Seats[0].BecomeBot();
            c.BeginWith(over);
            Shot(c, "14_game_over");

            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>Opens the shipped scene and checks it is wired up. Unity.exe -batchmode -executeMethod KingsCourt.EditorTools.ScreenshotTool.ValidateScene</summary>
        public static void ValidateScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/KingsCourtTable.unity", OpenSceneMode.Single);
            var c = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var so = c != null ? new SerializedObject(c) : null;
            var prefab = so != null ? so.FindProperty("cardPrefab").objectReferenceValue : null;
            Debug.Log("VALIDATE scene=" + scene.name + " controller=" + (c != null) + " cardPrefab=" + (prefab != null ? prefab.name : "none"));
            if (c != null) { c.Init(); Debug.Log("VALIDATE init ok, prefab in use=" + (CardView.Prefab != null)); }
            if (Application.isBatchMode) EditorApplication.Exit(c != null ? 0 : 1);
        }

        // ------------------------------------------------------------------ scenes and sessions

        private static GameController NewController()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("King's Court");
            var c = go.AddComponent<GameController>();
            c.Init();
            c.UseCameraCanvas();
            c.Pacer.Instant = true;
            return c;
        }

        private static GameSession NewSession(GameController c, int players, int seed)
        {
            return new GameSession(players, 0, seed, c.Pacer);
        }

        /// <summary>Find a game where the human has a decision of the wanted kind, replaying random legal answers to get there.</summary>
        private static GameSession FindDecision(GameController c, Func<Decision, bool> want, int players = 8)
        {
            for (int seed = 1; seed < 500; seed++)
            {
                var s = NewSession(c, players, seed);
                s.Start();
                var rng = new System.Random(seed);
                for (int step = 0; step < 120; step++)
                {
                    if (s.Game.Result != null) break;
                    var d = s.Seats[0].Pending;
                    if (d == null) break;
                    if (want(d)) return s;
                    s.Submit(d.Id, rng.Next(d.OptionCount));
                }
            }
            return null;
        }

        /// <summary>A game paused while bots act (the real pacing waits, so the human has no decision).</summary>
        private static GameSession FindWaiting(GameController c)
        {
            for (int seed = 1; seed < 200; seed++)
            {
                var slow = new Pacer();                       // never advances in edit mode, so the game parks here
                var s = new GameSession(8, 0, seed, slow);
                s.Start();
                if (s.Seats[0].Pending == null && s.Game.Round >= 1) return s;
            }
            return null;
        }

        private static void Decide(string name, Func<Decision, bool> want, int selectHand, int selectPanel)
        {
            var c = NewController();
            var s = FindDecision(c, want);
            if (s == null) { Debug.LogWarning("No game found for " + name); return; }
            c.BeginWith(s);
            c.Tick();                                    // first build of the screen
            c.GameScreen.DebugSelect(selectHand, selectPanel);
            Shot(c, name);
        }

        // ------------------------------------------------------------------ rendering

        private static void Shot(GameController c, string name)
        {
            var cam = c.Cam;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            c.Tick();
            for (int i = 0; i < 3; i++)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(c.Canvas.GetComponent<RectTransform>());
            }
            c.Tick();
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            var path = Path.Combine(dir, name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);
            Debug.Log("Saved " + path);
        }
    }
}
