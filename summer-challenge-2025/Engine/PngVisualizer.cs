using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using SixLabors.Fonts;

using SummerChallenge2025.Bot;

namespace SummerChallenge2025.Engine;

public sealed class PngVisualizer : IVisualizer
{
    private readonly string dir;
    private readonly int tile = 48;           // px
    private int side = 320;
    private readonly int margin = 32;
    private readonly Font font;

    private readonly Dictionary<int, AgentOrder> last = new();
    private GameState? prevState;

    public PngVisualizer(string gameId)
    {
        dir = Path.Combine("Games", gameId);
        Directory.CreateDirectory(dir);

        // domyślna czcionka ImageSharp
        var coll = new FontCollection();
        font = coll.AddSystemFonts().Get("Courier New").CreateFont(18);

        var sample = "id: (00,00)  W:100  CD:10  B:3  MOVE 19 9;SHOOT 9";
        side = (int)Math.Ceiling(
            TextMeasurer.MeasureSize(sample, new TextOptions(font)).Width) + 20;
    }

    /*------------- IVisualizer -------------*/
    public void UpdateOrders(TurnCommand p0, TurnCommand p1)
    {
        last.Clear();
        foreach (int id in p0.EnumerateActive()) last[id] = p0.Get(id);
        foreach (int id in p1.EnumerateActive()) last[id] = p1.Get(id);
    }

    public void Render(GameState st)
    {
        int gridW = GameState.MaxW * tile;
        int gridH = GameState.MaxH * tile;

        int imgW = margin + gridW + side;
        int imgH = margin + gridH;

        using var img = new Image<Rgba32>(imgW, imgH, Color.White);

        var panel = new Rectangle(margin + gridW, 0, side, imgH);
        img.Mutate(c => c.Fill(Color.WhiteSmoke, panel)
                        .Draw(Color.Black, 1, panel));

        DrawGrid(img, st);
        DrawCoordinates(img);
        DrawMoves(img, st);    // niebieskie strzałki
        DrawShots(img, st);
        DrawThrows(img, st);
        DrawAgents(img, st);
        DrawSidebar(img, st);

        string path = Path.Combine(dir, $"turn_{st.Turn:D3}.png");
        img.Save(path);
        prevState = st.FastClone();
    }

    void DrawCoordinates(Image<Rgba32> img)
    {
        var opt = new TextOptions(font);
        for (int x = 0; x < GameState.MaxW; x++)
        {
            string s = x.ToString();
            var rect = TextMeasurer.MeasureSize(s, opt);
            float px = margin + x * tile + tile / 2f - rect.Width / 2f;
            img.Mutate(c => c.DrawText(s, font, Color.Black, new PointF(px, 4)));
        }
        for (int y = 0; y < GameState.MaxH; y++)
        {
            string s = y.ToString();
            var rect = TextMeasurer.MeasureSize(s, opt);
            float py = margin + y * tile + tile / 2f - rect.Height / 2f;
            img.Mutate(c => c.DrawText(s, font, Color.Black, new PointF(4, py)));
        }
    }

    private void DrawGrid(Image<Rgba32> img, GameState st)
    {
        int w = st.W, h = st.H;

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Rectangle r = new(margin + x * tile, margin + y * tile, tile, tile);
                Color fill = GameState.Tiles[GameState.ToIndex((byte)x, (byte)y)] switch
                {
                    TileType.LowCover => Color.LightGray,
                    TileType.HighCover => Color.Gray,
                    _ => Color.White
                };
                img.Mutate(c => c.Fill(fill, r).Draw(Color.Black, 1, r));
            }
    }

    private void DrawMoves(Image<Rgba32> img, GameState st)
    {
        if (prevState is null) return;

        for (int id = 0; id < GameState.MaxAgents; ++id)
        {
            var now = st.Agents[id];
            var prev = prevState.Agents[id];

            if (!now.Alive || !prev.Alive) continue;
            if (now.X == prev.X && now.Y == prev.Y) continue; // nie ruszył się

            img.Mutate(c => c.DrawLine(
                Color.DeepSkyBlue, 3,
                PxPy(prev.X, prev.Y),
                PxPy(now.X, now.Y)
            ));
        }
    }

    private void DrawShots(Image<Rgba32> img, GameState st)
    {
        foreach (var pair in last)
        {
            int id = pair.Key; var ord = pair.Value;
            if (ord.Combat.Type != CombatType.Shoot) continue;

            var src = st.Agents[id];
            var dst = st.Agents[ord.Combat.Arg1];
            img.Mutate(c => c.DrawLine(Color.Red, 3,
                PxPy(src.X, src.Y),
                PxPy(dst.X, dst.Y)));
        }
    }
    private void DrawThrows(Image<Rgba32> img, GameState st)
    {
        foreach (var pair in last)
        {
            var ord = pair.Value;
            if (ord.Combat.Type != CombatType.Throw) continue;

            int cx = ord.Combat.Arg1, cy = ord.Combat.Arg2;
            var rect = new Rectangle(margin + cx * tile + tile / 4, margin + cy * tile + tile / 4, tile / 2, tile / 2);
            img.Mutate(c => c.Draw(Color.Gold, 3, rect));
        }
    }

    private void DrawAgents(Image<Rgba32> img, GameState st)
    {
        for (int id = 0; id < GameState.MaxAgents; ++id)
        {
            ref readonly var ag = ref st.Agents[id];
            if (!ag.Alive) continue;

            int px = margin + ag.X * tile;
            int py = margin + ag.Y * tile;
            var rect = new Rectangle(px + 4, py + 4, tile - 8, tile - 8);

            Color body = ag.playerId == 0 ? Color.Orange : Color.MediumPurple;
            img.Mutate(c => c.Fill(body, rect)
                            .Draw(Color.Black, 2, rect));

            // ───── ID na środku kwadratu ─────────────────────────────
            string label = id.ToString();

            // TextMeasurer w v3.x: Measure(string, TextOptions)
            var textOpts = new TextOptions(font);
            FontRectangle bounds = TextMeasurer.MeasureSize(label, textOpts);
            float txtW = bounds.Width;
            float txtH = bounds.Height;

            PointF pos = new(px + tile / 2f - txtW / 2f,
                            py + tile / 2f - txtH / 2f);

            img.Mutate(c => c.DrawText(label, font, Color.Black, pos));

            // ───── hunker – cyjanowa otoczka ─────────────────────────
            if (ag.Hunkering)
                img.Mutate(c => c.Draw(Color.Cyan.WithAlpha(0.6f), 4, rect));
        }
    }

    private void DrawSidebar(Image<Rgba32> img, GameState st)
    {
        int startX = margin + GameState.MaxW * tile + 10;
        int y = 10;

        string scoreLine = $"P0: {st.Score0}   P1: {st.Score1}";
        img.Mutate(c => c.DrawText(scoreLine, font, Color.Black, new PointF(startX, y)));
        y += 32;

        foreach (int id in Enumerable.Range(0, GameState.MaxAgents))
        {
            ref readonly var ag = ref st.Agents[id];
            if (!ag.Alive) continue;

            string line = $"{id}: ({ag.X},{ag.Y})  W:{ag.Wetness}  CD:{ag.Cooldown}  B:{ag.SplashBombs}";
            if (ag.Hunkering) line += "  HUNKER";

            if (last.TryGetValue(id, out var ord))
                line += " ▶ " + FormatOrder(ord);

            img.Mutate(c => c.DrawText(line, font, Color.Black, new PointF(startX, y)));
            y += 24;
        }
    }

    private string FormatOrder(AgentOrder o)
    {
        var sb = new System.Text.StringBuilder();
        if (o.Move.Type == MoveType.Step && !(o.Move.X == 0 && o.Move.Y == 0))
            sb.Append($"MOVE {o.Move.X} {o.Move.Y};");
        switch (o.Combat.Type)
        {
            case CombatType.Shoot: sb.Append($"SHOOT {o.Combat.Arg1}"); break;
            case CombatType.Throw: sb.Append($"THROW {o.Combat.Arg1} {o.Combat.Arg2}"); break;
            case CombatType.Hunker: sb.Append("HUNKER_DOWN"); break;
        }
        return sb.ToString();
    }
    
    private PointF PxPy(byte gridX, byte gridY)
        => new(margin + gridX * tile + tile / 2f,
            margin + gridY * tile + tile / 2f);
}
