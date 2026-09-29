using System.Collections.Generic;
using System.Linq;
using KingsCourt.Core;
using UnityEngine;

namespace KingsCourt.UnityLayer
{
    /// <summary>
    /// The top-down court every player sees: a long table, the throne at the head, a chair per seat and one
    /// piece per player. Hidden players are grey pawns; once revealed they get the piece for their role.
    /// It draws from a GameSnapshot only, so a player can never see anything the rules hide from them.
    /// </summary>
    public sealed class TableView : MonoBehaviour
    {
        public static readonly Color LoyalistColor = new Color(.2f, .45f, .9f);
        public static readonly Color TraitorColor = new Color(.85f, .2f, .2f);
        public static readonly Color ChaosColor = new Color(.62f, .3f, .85f);

        // Eight physical spots around the table: 0 = throne (head, left end), then clockwise.
        private static readonly Vector2[] Slots =
        {
            new Vector2(-7.6f, 0.4f),
            new Vector2(-3.6f, 3.65f), new Vector2(0f, 3.65f), new Vector2(3.6f, 3.65f),
            new Vector2(7.6f, 0.4f),
            new Vector2(3.6f, -2.85f), new Vector2(0f, -2.85f), new Vector2(-3.6f, -2.85f),
        };

        // Which slots are used at each player count (kept in clockwise order).
        private static readonly int[][] SlotsFor =
        {
            null, null, null, null, null,
            new[] { 0, 1, 3, 5, 7 },
            new[] { 0, 1, 3, 4, 5, 7 },
            new[] { 0, 1, 2, 3, 5, 6, 7 },
            new[] { 0, 1, 2, 3, 4, 5, 6, 7 },
        };

        private sealed class PlayerVisual
        {
            public Transform Root;
            public SpriteRenderer Glow, Candidate, Base, Pawn, Icon, Badge, Cross;
            public TextMesh Name, Tag;
            public readonly List<SpriteRenderer> Cards = new List<SpriteRenderer>();
        }

        private GameSnapshot snap;
        private readonly Dictionary<int, PlayerVisual> visuals = new Dictionary<int, PlayerVisual>();
        private readonly Dictionary<int, Vector3> targets = new Dictionary<int, Vector3>();
        private HashSet<int> candidates = new HashSet<int>();
        private TextMesh drawCount, discardCount, graveLabel;
        private Font font;

        // ------------------------------------------------------------ build

        public void Build(GameSnapshot s)
        {
            snap = s;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int i = transform.childCount - 1; i >= 0; i--) DestroyNow(transform.GetChild(i).gameObject);
            visuals.Clear();
            targets.Clear();

            BuildTable();
            var slots = SlotsFor[s.SeatCount];
            for (int seat = 0; seat < slots.Length; seat++) BuildChair(seat, Slots[slots[seat]]);
            foreach (var p in s.Players) BuildPlayer(p);

            graveLabel = MakeText("graveLabel", new Vector2(8.7f, 4.85f), 0.045f, new Color(.9f, .5f, .5f), TextAnchor.MiddleCenter, 14);
            Refresh(s);
        }

        private static void DestroyNow(Object o)
        {
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        private void BuildTable()
        {
            var wood = new Color(.36f, .21f, .1f);
            Sprite(transform, "tableEdge", SpriteFactory.White, new Vector2(0, 0.4f), new Vector2(12.8f, 4.5f), wood * .75f, 0);
            Sprite(transform, "tableTop", SpriteFactory.White, new Vector2(0, 0.4f), new Vector2(12.4f, 4.1f), wood, 1);
            Sprite(transform, "felt", SpriteFactory.White, new Vector2(0, 0.4f), new Vector2(11.2f, 3.1f), new Color(.1f, .32f, .22f), 2);

            // Draw pile above the throne end, discard pile across from it (GDD table topology).
            for (int i = 0; i < 4; i++)
                Sprite(transform, "draw" + i, SpriteFactory.CardBack, new Vector2(-4.8f + i * 0.03f, 0.4f + i * 0.03f), Vector2.one * 1.5f, Color.white, 3 + i);
            for (int i = 0; i < 3; i++)
                Sprite(transform, "discard" + i, SpriteFactory.CardBack, new Vector2(4.8f - i * 0.05f, 0.4f + i * 0.02f), Vector2.one * 1.5f, new Color(.7f, .7f, .7f), 3 + i, i * 9f - 9f);
            drawCount = MakeText("drawCount", new Vector2(-4.8f, -0.75f), 0.06f, Color.white, TextAnchor.MiddleCenter, 12);
            discardCount = MakeText("discardCount", new Vector2(4.8f, -0.75f), 0.06f, Color.white, TextAnchor.MiddleCenter, 12);
            MakeText("drawLabel", new Vector2(-4.8f, 1.55f), 0.05f, new Color(.8f, .9f, .8f), TextAnchor.MiddleCenter, 12).text = "DRAW";
            MakeText("discardLabel", new Vector2(4.8f, 1.55f), 0.05f, new Color(.8f, .9f, .8f), TextAnchor.MiddleCenter, 12).text = "DISCARD";
        }

        private void BuildChair(int seat, Vector2 pos)
        {
            bool throne = seat == 0;
            var chairColor = throne ? new Color(.75f, .14f, .2f) : new Color(.55f, .36f, .18f);
            float s = throne ? 1.5f : 1.3f;
            var root = new GameObject("chair" + seat).transform;
            root.SetParent(transform, false);
            root.localPosition = pos;
            Sprite(root, "seat", SpriteFactory.White, Vector2.zero, new Vector2(s, s), chairColor, 1);
            // Chair back sits on the side away from the table.
            Vector2 back = throne ? new Vector2(-s * .55f, 0) : (pos.y > 0.4f ? new Vector2(0, s * .55f) : (pos.y < 0.4f ? new Vector2(0, -s * .55f) : new Vector2(s * .55f, 0)));
            bool horizontalBack = Mathf.Abs(back.y) > Mathf.Abs(back.x);
            Sprite(root, "back", SpriteFactory.White, back, horizontalBack ? new Vector2(s, s * .2f) : new Vector2(s * .2f, throne ? s * 1.35f : s), chairColor * .7f, 0);
            if (throne)
            {
                Sprite(root, "throneTrim", SpriteFactory.White, back, new Vector2(s * .1f, s * 1.1f), new Color(.95f, .78f, .2f), 1);
                Sprite(root, "throneCrown", SpriteFactory.Icon(Role.King), back + new Vector2(-s * .1f, s * .75f), Vector2.one * .5f, Color.white, 2);
            }
        }

        private void BuildPlayer(PlayerInfo p)
        {
            var v = new PlayerVisual();
            var root = new GameObject("player" + p.Id).transform;
            root.SetParent(transform, false);
            v.Root = root;

            v.Candidate = Sprite(root, "candidate", SpriteFactory.Ring, Vector2.zero, Vector2.one * 1.5f, new Color(.3f, .9f, 1f, 0f), 5);
            v.Glow = Sprite(root, "glow", SpriteFactory.Circle, Vector2.zero, Vector2.one * 1.25f, new Color(1f, .85f, .2f, 0f), 4);
            v.Pawn = Sprite(root, "pawn", SpriteFactory.Pawn, Vector2.zero, Vector2.one, new Color(.72f, .72f, .74f), 6);
            v.Base = Sprite(root, "base", SpriteFactory.Circle, Vector2.zero, Vector2.one * 0.98f, Color.white, 6);
            v.Icon = Sprite(root, "icon", SpriteFactory.Icon(p.Role ?? Role.King), Vector2.zero, Vector2.one * 0.86f, Color.white, 8);
            v.Badge = Sprite(root, "badge", SpriteFactory.Icon(p.Role ?? Role.King), new Vector2(.42f, .42f), Vector2.one * .5f, new Color(1, 1, 1, .9f), 9);
            v.Cross = Sprite(root, "cross", SpriteFactory.White, Vector2.zero, new Vector2(1.1f, .1f), new Color(.9f, .1f, .1f, 0), 12);
            v.Name = MakeText("name", Vector2.zero, 0.05f, Color.white, TextAnchor.MiddleCenter, 14);
            v.Name.transform.SetParent(root, false);
            v.Tag = MakeText("tag", Vector2.zero, 0.04f, new Color(1f, .9f, .4f), TextAnchor.MiddleCenter, 14);
            v.Tag.transform.SetParent(root, false);
            for (int i = 0; i < 6; i++)
                v.Cards.Add(Sprite(root, "card" + i, SpriteFactory.CardBack, Vector2.zero, Vector2.one * .42f, Color.white, 5 + i));
            visuals[p.Id] = v;
        }

        // ------------------------------------------------------------ refresh

        /// <summary>Highlight the players the local player is being asked to pick from.</summary>
        public void SetCandidates(IEnumerable<int> ids)
        {
            candidates = new HashSet<int>(ids ?? new int[0]);
        }

        public void Refresh(GameSnapshot s)
        {
            snap = s;
            var slots = SlotsFor[s.SeatCount];
            int deadIndex = 0;
            graveLabel.text = s.Players.Any(x => !x.Alive) ? "EXECUTED" : "";

            foreach (var p in s.Players)
            {
                PlayerVisual v;
                if (!visuals.TryGetValue(p.Id, out v)) continue;
                Vector2 pos = Slots[slots[Mathf.Clamp(p.Seat, 0, slots.Length - 1)]];
                if (!p.Alive) pos = new Vector2(9.2f, 3.9f - deadIndex++ * 1.05f);
                targets[p.Id] = new Vector3(pos.x, pos.y, 0);
                if (!Application.isPlaying) v.Root.localPosition = targets[p.Id];
                v.Root.localScale = Vector3.one * (p.Alive ? 1f : 0.6f);

                bool spectator = s.ViewerId < 0;
                bool full = p.Revealed || ((spectator || s.Result != null) && p.Role.HasValue);
                Color team = p.Role.HasValue ? TeamColor(p) : Color.gray;
                v.Pawn.enabled = !full;
                v.Base.enabled = full;
                v.Icon.enabled = full;
                v.Badge.enabled = !full && p.IsYou && p.Role.HasValue;
                if (p.Role.HasValue)
                {
                    v.Icon.sprite = SpriteFactory.Icon(p.Role.Value);
                    v.Badge.sprite = SpriteFactory.Icon(p.Role.Value);
                }

                float dim = p.Alive ? 1f : 0.35f;
                var baseColor = Color.Lerp(team, Color.black, .35f);
                v.Base.color = new Color(baseColor.r * dim, baseColor.g * dim, baseColor.b * dim, 1);
                v.Pawn.color = p.IsYou ? new Color(.95f * dim, .85f * dim, .4f * dim) : new Color(.72f * dim, .72f * dim, .74f * dim, 1);
                v.Icon.color = new Color(dim, dim, dim, 1);
                v.Cross.color = new Color(.9f, .1f, .1f, p.Alive ? 0f : 1f);
                v.Cross.transform.localRotation = Quaternion.Euler(0, 0, 45);

                bool isTb = s.ThronebearerId == p.Id;
                v.Glow.color = new Color(1f, .85f, .2f, p.Alive && isTb ? .85f : 0f);
                v.Candidate.color = new Color(.3f, .9f, 1f, candidates.Contains(p.Id) ? 1f : 0f);

                string roleLine = "";
                if (full && p.Role.HasValue) roleLine = "\n" + p.Role.Value;
                else if (p.IsYou && p.Role.HasValue) roleLine = "\n(you are " + p.Role.Value + ")";
                v.Name.text = p.Name + (p.IsBot && !p.Name.StartsWith("Bot") ? " [bot]" : "") + roleLine;
                v.Name.color = !p.Alive ? new Color(.6f, .6f, .6f) : (p.IsYou ? UiKit.Gold : Color.white);
                v.Name.transform.localPosition = new Vector3(0, -1.0f, 0);
                v.Tag.transform.localPosition = new Vector3(0, .8f, 0);
                v.Tag.text = !p.Alive ? "" : isTb ? "THRONEBEARER" : (s.TrustedAdvisorId == p.Id ? "TRUSTED ADVISOR" : "");

                for (int i = 0; i < v.Cards.Count; i++)
                {
                    v.Cards[i].enabled = p.Alive && i < p.HandCount;
                    v.Cards[i].transform.localPosition = new Vector3(-0.8f + i * 0.13f, -0.05f, 0);
                    v.Cards[i].transform.localRotation = Quaternion.Euler(0, 0, 20f - i * 8f);
                }
            }

            drawCount.text = s.DeckCount.ToString();
            discardCount.text = s.DiscardCount.ToString();
        }

        private void Update()
        {
            // Pieces glide to their seats when someone swaps or rotates.
            foreach (var kv in targets)
            {
                var t = visuals[kv.Key].Root;
                t.localPosition = Vector3.Lerp(t.localPosition, kv.Value, 1f - Mathf.Exp(-9f * Time.deltaTime));
            }
            // Selectable players pulse.
            float pulse = .6f + .4f * Mathf.Sin(Time.time * 6f);
            foreach (var id in candidates)
            {
                PlayerVisual v;
                if (visuals.TryGetValue(id, out v)) v.Candidate.color = new Color(.3f, .9f, 1f, pulse);
            }
        }

        public static Color TeamColor(PlayerInfo p)
        {
            if (!p.Role.HasValue) return Color.gray;
            var f = RoleInfo.FactionOf(p.Role.Value);
            if (p.Role.Value == Role.Beggar && p.Revealed) f = Faction.Traitor;
            switch (f)
            {
                case Faction.Traitor: return TraitorColor;
                case Faction.Chaos: return ChaosColor;
                default: return LoyalistColor;
            }
        }

        public static Color TeamColor(Role role)
        {
            var f = RoleInfo.FactionOf(role);
            return f == Faction.Traitor ? TraitorColor : f == Faction.Chaos ? ChaosColor : LoyalistColor;
        }

        // ------------------------------------------------------------ helpers

        private SpriteRenderer Sprite(Transform parent, string name, Sprite s, Vector2 pos, Vector2 scale, Color color, int order, float rotZ = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(scale.x, scale.y, 1);
            go.transform.localRotation = Quaternion.Euler(0, 0, rotZ);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        private TextMesh MakeText(string name, Vector2 pos, float charSize, Color color, TextAnchor anchor, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 64;
            tm.characterSize = charSize;
            tm.anchor = anchor;
            tm.alignment = anchor == TextAnchor.UpperLeft ? TextAlignment.Left : TextAlignment.Center;
            tm.color = color;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingOrder = order;
            return tm;
        }
    }
}
