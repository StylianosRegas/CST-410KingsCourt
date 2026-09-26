using System;
using System.Collections.Generic;
using KingsCourt.Core;
using UnityEngine;

namespace KingsCourt.UnityLayer
{
    /// <summary>
    /// Generates every sprite in code (no imported art needed) so the table works out of the box.
    /// Swap these for real art later; the rest of the view only asks for sprites by role.
    /// </summary>
    public static class SpriteFactory
    {
        private const int Size = 160;
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        private delegate bool Shape(float x, float y);

        private struct Layer
        {
            public Color Color;
            public Shape Shape;
            public Layer(Color c, Shape s) { Color = c; Shape = s; }
        }

        // ------------------------------------------------------------ public sprites

        public static Sprite White { get { return Get("white", () => Draw(4, new[] { new Layer(Color.white, (x, y) => true) }, 4)); } }
        public static Sprite Circle { get { return Get("circle", () => Draw(Size, new[] { new Layer(Color.white, Disc(.5f, .5f, .5f)) }, Size)); } }
        public static Sprite Ring { get { return Get("ring", () => Draw(Size, new[] { new Layer(Color.white, Annulus(.5f, .5f, .5f, .4f)) }, Size)); } }

        public static Sprite Pawn
        {
            get
            {
                return Get("pawn", () => Draw(Size, new[]
                {
                    new Layer(Color.white, Disc(.5f, .72f, .17f)),
                    new Layer(Color.white, Poly(.30f, .12f, .70f, .12f, .60f, .5f, .40f, .5f)),
                    new Layer(Color.white, Rect(.24f, .08f, .76f, .16f)),
                }, Size));
            }
        }

        public static Sprite CardBack
        {
            get
            {
                return Get("cardback", () => Draw(Size, new[]
                {
                    new Layer(new Color(.96f, .95f, .9f), Rect(.12f, .05f, .88f, .95f)),
                    new Layer(new Color(.55f, .12f, .16f), Rect(.2f, .13f, .80f, .87f)),
                    new Layer(new Color(.9f, .75f, .3f), Poly(.5f, .3f, .64f, .5f, .5f, .7f, .36f, .5f)),
                }, Size));
            }
        }

        public static Sprite Icon(Role role)
        {
            return Get("icon_" + role, () => Draw(Size, IconLayers(role), Size));
        }

        // ------------------------------------------------------------ role pieces

        private static Layer[] IconLayers(Role role)
        {
            var gold = new Color(.96f, .78f, .18f);
            var goldDark = new Color(.72f, .5f, .08f);
            var steel = new Color(.82f, .86f, .92f);
            switch (role)
            {
                case Role.King: // crown
                    return new[]
                    {
                        new Layer(gold, Poly(.16f, .28f, .16f, .74f, .34f, .54f, .5f, .8f, .66f, .54f, .84f, .74f, .84f, .28f)),
                        new Layer(goldDark, Rect(.16f, .2f, .84f, .34f)),
                        new Layer(new Color(.85f, .1f, .15f), Disc(.5f, .27f, .045f)),
                        new Layer(new Color(.2f, .45f, .95f), Disc(.3f, .27f, .035f)),
                        new Layer(new Color(.2f, .45f, .95f), Disc(.7f, .27f, .035f)),
                        new Layer(gold, Disc(.16f, .77f, .05f)),
                        new Layer(gold, Disc(.5f, .83f, .05f)),
                        new Layer(gold, Disc(.84f, .77f, .05f)),
                    };
                case Role.Queen: // silver tiara with a gem
                    return new[]
                    {
                        new Layer(new Color(.9f, .9f, .97f), Poly(.18f, .3f, .27f, .62f, .38f, .46f, .5f, .74f, .62f, .46f, .73f, .62f, .82f, .3f)),
                        new Layer(new Color(.62f, .62f, .78f), Rect(.18f, .22f, .82f, .32f)),
                        new Layer(new Color(.75f, .2f, .75f), Disc(.5f, .5f, .07f)),
                        new Layer(new Color(.95f, .5f, .8f), Disc(.27f, .64f, .03f)),
                        new Layer(new Color(.95f, .5f, .8f), Disc(.73f, .64f, .03f)),
                        new Layer(new Color(.95f, .5f, .8f), Disc(.5f, .77f, .035f)),
                    };
                case Role.Knight: // sword
                    return new[]
                    {
                        new Layer(steel, Poly(.45f, .3f, .55f, .3f, .55f, .78f, .5f, .9f, .45f, .78f)),
                        new Layer(new Color(.62f, .68f, .76f), Rect(.49f, .3f, .51f, .82f)),
                        new Layer(goldDark, Rect(.3f, .26f, .7f, .33f)),
                        new Layer(new Color(.45f, .28f, .14f), Rect(.465f, .1f, .535f, .27f)),
                        new Layer(goldDark, Disc(.5f, .09f, .045f)),
                    };
                case Role.Elder: // hourglass
                    return new[]
                    {
                        new Layer(new Color(.95f, .9f, .75f), Poly(.32f, .8f, .68f, .8f, .52f, .5f, .48f, .5f)),
                        new Layer(new Color(.95f, .9f, .75f), Poly(.48f, .5f, .52f, .5f, .68f, .2f, .32f, .2f)),
                        new Layer(new Color(.85f, .62f, .25f), Poly(.4f, .74f, .6f, .74f, .5f, .56f)),
                        new Layer(new Color(.85f, .62f, .25f), Poly(.5f, .5f, .62f, .2f, .38f, .2f)),
                        new Layer(new Color(.5f, .32f, .16f), Rect(.26f, .8f, .74f, .87f)),
                        new Layer(new Color(.5f, .32f, .16f), Rect(.26f, .13f, .74f, .2f)),
                    };
                case Role.CourtWizard: // pointed hat with star
                    return new[]
                    {
                        new Layer(new Color(.32f, .3f, .82f), Poly(.5f, .92f, .62f, .5f, .7f, .3f, .3f, .3f, .38f, .55f)),
                        new Layer(new Color(.24f, .22f, .66f), Ellipse(.5f, .28f, .36f, .07f)),
                        new Layer(gold, Rect(.31f, .3f, .69f, .38f)),
                        new Layer(gold, Star(.5f, .6f, .1f, .04f, 5)),
                    };
                case Role.Prince: // signet ring with a red gem
                    return new[]
                    {
                        new Layer(gold, Annulus(.5f, .36f, .27f, .17f)),
                        new Layer(goldDark, Rect(.4f, .58f, .6f, .66f)),
                        new Layer(new Color(.9f, .1f, .2f), Poly(.5f, .9f, .62f, .76f, .5f, .6f, .38f, .76f)),
                        new Layer(new Color(1f, .6f, .65f), Poly(.5f, .86f, .55f, .79f, .5f, .72f, .45f, .79f)),
                    };
                case Role.Nobleman: // money bag
                    return new[]
                    {
                        new Layer(new Color(.62f, .42f, .2f), Disc(.5f, .36f, .28f)),
                        new Layer(new Color(.62f, .42f, .2f), Poly(.4f, .58f, .6f, .58f, .58f, .72f, .42f, .72f)),
                        new Layer(new Color(.46f, .3f, .14f), Rect(.36f, .66f, .64f, .72f)),
                        new Layer(new Color(.62f, .42f, .2f), Poly(.36f, .72f, .64f, .72f, .7f, .84f, .3f, .84f)),
                        new Layer(gold, Disc(.5f, .34f, .13f)),
                        new Layer(goldDark, Rect(.485f, .26f, .515f, .42f)),
                    };
                case Role.Jester: // three-pronged hat with bells
                    return new[]
                    {
                        new Layer(new Color(.86f, .16f, .2f), Poly(.26f, .4f, .1f, .74f, .46f, .4f)),
                        new Layer(new Color(.2f, .7f, .35f), Poly(.36f, .4f, .5f, .84f, .64f, .4f)),
                        new Layer(new Color(.95f, .8f, .15f), Poly(.54f, .4f, .9f, .74f, .74f, .4f)),
                        new Layer(new Color(.35f, .2f, .6f), Rect(.24f, .26f, .76f, .42f)),
                        new Layer(gold, Disc(.1f, .76f, .06f)),
                        new Layer(gold, Disc(.5f, .86f, .06f)),
                        new Layer(gold, Disc(.9f, .76f, .06f)),
                    };
                case Role.Beggar: // bowl with coins
                    return new[]
                    {
                        new Layer(new Color(.58f, .38f, .22f), HalfDisc(.5f, .58f, .32f)),
                        new Layer(new Color(.42f, .27f, .14f), Rect(.16f, .56f, .84f, .64f)),
                        new Layer(new Color(.42f, .27f, .14f), Rect(.38f, .22f, .62f, .28f)),
                        new Layer(gold, Disc(.38f, .7f, .07f)),
                        new Layer(gold, Disc(.55f, .73f, .07f)),
                        new Layer(gold, Disc(.5f, .66f, .06f)),
                        new Layer(goldDark, Disc(.62f, .68f, .04f)),
                    };
            }
            return new Layer[0];
        }

        // ------------------------------------------------------------ shape helpers (unit square, y up)

        private static Shape Disc(float cx, float cy, float r) { return (x, y) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r; }
        private static Shape Annulus(float cx, float cy, float ro, float ri) { return (x, y) => { float d = (x - cx) * (x - cx) + (y - cy) * (y - cy); return d <= ro * ro && d >= ri * ri; }; }
        private static Shape Rect(float x0, float y0, float x1, float y1) { return (x, y) => x >= x0 && x <= x1 && y >= y0 && y <= y1; }
        private static Shape Ellipse(float cx, float cy, float rx, float ry) { return (x, y) => { float a = (x - cx) / rx, b = (y - cy) / ry; return a * a + b * b <= 1f; }; }
        private static Shape HalfDisc(float cx, float cy, float r) { return (x, y) => y <= cy && (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r; }

        private static Shape Poly(params float[] xy)
        {
            return (x, y) =>
            {
                bool inside = false;
                int n = xy.Length / 2;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    float xi = xy[i * 2], yi = xy[i * 2 + 1], xj = xy[j * 2], yj = xy[j * 2 + 1];
                    if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
                }
                return inside;
            };
        }

        private static Shape Star(float cx, float cy, float outer, float inner, int points)
        {
            var pts = new float[points * 4];
            for (int i = 0; i < points * 2; i++)
            {
                float a = Mathf.PI / 2f + i * Mathf.PI / points;
                float r = i % 2 == 0 ? outer : inner;
                pts[i * 2] = cx + Mathf.Cos(a) * r;
                pts[i * 2 + 1] = cy + Mathf.Sin(a) * r;
            }
            return Poly(pts);
        }

        // ------------------------------------------------------------ rasteriser

        private static Sprite Draw(int px, Layer[] layers, float pixelsPerUnit)
        {
            var tex = new Texture2D(px, px, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[px * px];
            const int ss = 3; // 3x3 supersampling for smooth edges
            for (int j = 0; j < px; j++)
            {
                for (int i = 0; i < px; i++)
                {
                    Color result = new Color(0, 0, 0, 0);
                    foreach (var layer in layers)
                    {
                        int hit = 0;
                        for (int sy = 0; sy < ss; sy++)
                            for (int sx = 0; sx < ss; sx++)
                                if (layer.Shape((i + (sx + .5f) / ss) / px, (j + (sy + .5f) / ss) / px)) hit++;
                        if (hit == 0) continue;
                        float a = layer.Color.a * hit / (ss * ss);
                        float outA = a + result.a * (1 - a);
                        Color rgb = outA <= 0 ? Color.clear : (new Color(layer.Color.r, layer.Color.g, layer.Color.b) * a + new Color(result.r, result.g, result.b) * result.a * (1 - a)) / outA;
                        result = new Color(rgb.r, rgb.g, rgb.b, outA);
                    }
                    pixels[j * px + i] = result;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(.5f, .5f), pixelsPerUnit);
        }

        private static Sprite Get(string key, Func<Sprite> make)
        {
            Sprite s;
            if (cache.TryGetValue(key, out s) && s != null) return s;
            s = make();
            cache[key] = s;
            return s;
        }
    }
}
