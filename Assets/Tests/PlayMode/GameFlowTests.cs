using System.Collections;
using System.Linq;
using KingsCourt.UnityLayer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KingsCourt.Tests
{
    /// <summary>
    /// Plays a whole game in Play mode by "clicking" the real UI (start screen, lobby, hand, decision panel).
    /// The UnityTest runner also fails the test if anything logs an error or exception.
    /// </summary>
    public class GameFlowTests
    {
        [SetUp]
        public void SetUp() { GameSettings.PaceMultiplier = 0f; GameSettings.FillWithBots = true; }

        [TearDown]
        public void TearDown() { GameSettings.PaceMultiplier = 1f; }

        [UnityTest]
        public IEnumerator FullGame_BuiltInCode() { yield return PlayWholeGame(null); }

        [UnityTest]
        public IEnumerator FullGame_ShippedScene() { yield return PlayWholeGame("KingsCourtTable"); }

        // ------------------------------------------------------------------

        private IEnumerator PlayWholeGame(string sceneName)
        {
            if (sceneName != null)
            {
                SceneManager.LoadScene(sceneName);
                yield return null;
            }
            else
            {
                new GameObject("Controller").AddComponent<GameController>();
                yield return null;
            }
            yield return null;
            Assert.IsNotNull(Object.FindAnyObjectByType<GameController>(), "GameController missing");
            if (sceneName != null) Assert.IsNotNull(CardView.Prefab, "scene did not wire the card prefab");

            // Start screen -> lobby -> game.
            Click(ButtonNamed("HOST GAME"));
            yield return null;
            Assert.IsTrue(Active("LobbyScreen"), "lobby did not open");
            Click(ButtonNamed("START GAME"));
            yield return null;
            yield return null;
            Assert.IsTrue(Active("GameScreen"), "game screen did not open");

            bool checkedRefusal = false;
            int decisions = 0;
            float deadline = Time.realtimeSinceStartup + 120f;

            while (!Active("GameOver"))
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "game did not finish in time (decisions answered: " + decisions + ")");
                var panel = Find("DecisionPanel");
                if (panel != null && panel.gameObject.activeInHierarchy)
                {
                    if (Act(panel)) decisions++;
                }
                else if (!checkedRefusal && Active("GameScreen"))
                {
                    // Not our turn: clicking a hand card must be refused with a message.
                    var card = HandCards().FirstOrDefault();
                    if (card != null)
                    {
                        Click(card.gameObject);
                        yield return null;
                        var toast = Find("Toast");
                        Assert.IsTrue(toast != null && toast.gameObject.activeInHierarchy, "clicking a card out of turn showed no refusal");
                        Assert.IsNotEmpty(toast.GetComponentInChildren<Text>().text);
                        checkedRefusal = true;
                    }
                }
                yield return null;
            }

            Assert.Greater(decisions, 0, "the human never had a decision to make");
            var over = Find("GameOver");
            Assert.IsNotEmpty(over.GetComponentsInChildren<Text>().First().text, "game over panel is empty");
        }

        /// <summary>One UI action per frame. Returns true when a choice was actually sent.</summary>
        private bool Act(Transform panel)
        {
            var confirm = panel.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == "Button CONFIRM" && b.gameObject.activeInHierarchy);
            var body = panel.Find("Body");
            var panelCards = body != null ? body.GetComponentsInChildren<CardView>().ToArray() : new CardView[0];

            if (confirm != null)
            {
                if (confirm.interactable) { Click(confirm.gameObject); return true; }
                var pick = panelCards.Length > 0 ? panelCards[0] : HandCards().FirstOrDefault();
                if (pick != null) Click(pick.gameObject);
                return false;
            }
            var grid = body != null ? body.Find("Grid") : null;
            if (grid != null)
            {
                var b = grid.GetComponentsInChildren<Button>().FirstOrDefault();
                if (b != null) { Click(b.gameObject); return true; }
            }
            var yesNo = body != null ? body.Find("YesNo") : null;
            if (yesNo != null)
            {
                var buttons = yesNo.GetComponentsInChildren<Button>();
                if (buttons.Length > 0) { Click(buttons[buttons.Length - 1].gameObject); return true; }   // "NOT NOW"
            }
            return false;
        }

        private static System.Collections.Generic.IEnumerable<CardView> HandCards()
        {
            var hand = Find("Hand");
            return hand == null ? new CardView[0] : hand.GetComponentsInChildren<CardView>();
        }

        private static void Click(GameObject go)
        {
            Assert.IsNotNull(go, "nothing to click");
            var data = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(go, data, ExecuteEvents.pointerClickHandler);
        }

        private static GameObject ButtonNamed(string label)
        {
            var b = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude).FirstOrDefault(x => x.name == "Button " + label);
            Assert.IsNotNull(b, "button not found: " + label);
            return b.gameObject;
        }

        private static Transform Find(string name)
        {
            return Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).FirstOrDefault(t => t.name == name);
        }

        private static bool Active(string name)
        {
            var t = Find(name);
            return t != null && t.gameObject.activeInHierarchy;
        }
    }
}
