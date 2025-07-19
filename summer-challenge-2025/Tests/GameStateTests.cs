using FluentAssertions;
using Xunit;
using SummerChallenge2025.Bot;

namespace SummerChallenge2025.Tests;

public static class TestFactory
{
    public static GameState EmptyState()
    {
        var gs = new GameState(GameState.MaxW, GameState.MaxH);
        gs.ClearAgents();
        for (int y = 0; y < gs.H; y++)
            for (int x = 0; x < gs.W; x++)
                GameState.Tiles[GameState.ToIndex(x, y)] = TileType.Empty;
        for (int id = 0; id < GameState.MaxAgents; id++)
        {
            gs.Agents[id] = new AgentState { Alive = false };
        }
        return gs;
    }

    public static GameState WithAgents(params (int id, byte x, byte y)[] layout)
    {
        var gs = EmptyState();
        foreach (var (id, x, y) in layout)
        {
            gs.Agents[id] = new AgentState { X = x, Y = y, Alive = true };
            gs.Occup.Set(GameState.ToIndex(x, y));
        }
        return gs;
    }

    public static GameState WithAgents(
        (int id, byte x, byte y, int player, bool alive, int bombs, int cd)[] layout,
        params (byte x, byte y, TileType tile)[] covers)
    {
        var gs = EmptyState();
        foreach (var (x, y, tile) in covers)
            GameState.Tiles[GameState.ToIndex(x, y)] = tile;

        foreach (var (id, x, y, player, alive, bombs, cd) in layout)
        {
            gs.Agents[id] = new AgentState {
                X = x, Y = y, Alive = alive,
                playerId = player, SplashBombs = bombs, Cooldown = cd
            };
            if (alive) gs.Occup.Set(GameState.ToIndex(x, y));
        }

        for (int i = 0; i < GameState.MaxAgents; ++i)
            GameState.AgentClasses[0] = AgentClass.Gunner;
        return gs;
    }
}

public class MovePhaseTests
{
    [Fact(DisplayName = "Pusty krok: (0,0) ➜ (1,0)")]
    public void Move_OneStep_UpdatesPosition()
    {
        var gs = TestFactory.WithAgents((0, 0, 0));
        var cmd = new TurnCommand(GameState.MaxAgents);
        cmd.SetMove(0, new MoveAction(MoveType.Step, 1, 0));

        gs.ApplyInPlace(cmd, new TurnCommand(GameState.MaxAgents));

        gs.Agents[0].X.Should().Be(1);
        gs.Agents[0].Y.Should().Be(0);
        gs.Occup.Test(GameState.ToIndex(1, 0)).Should().Be(true);
        gs.Occup.Test(GameState.ToIndex(0, 0)).Should().Be(false);
    }

    [Fact(DisplayName = "Pusty krok: (0,0) ➜ (5,0) ale stoimy na (1,0)")]
    public void Move_BigStep_UpdatesPosition()
    {
        var gs = TestFactory.WithAgents((0, 0, 0));
        var cmd = new TurnCommand(GameState.MaxAgents);
        cmd.SetMove(0, new MoveAction(MoveType.Step, 5, 0));

        gs.ApplyInPlace(cmd, new TurnCommand(GameState.MaxAgents));

        gs.Agents[0].X.Should().Be(1);
        gs.Agents[0].Y.Should().Be(0);
        gs.Occup.Test(GameState.ToIndex(1, 0)).Should().Be(true);
        gs.Occup.Test(GameState.ToIndex(0, 0)).Should().Be(false);
    }

    [Fact(DisplayName = "Cel zajęty przez cover ➜ wybiera innego sąsiada")]
    public void Move_IntoCover_ChoosesAlternateStep()
    {
        var gs = TestFactory.WithAgents((0, 0, 0));
        GameState.Tiles[GameState.ToIndex(1, 0)] = TileType.LowCover;   // blokada E

        var cmd = new TurnCommand(GameState.MaxAgents);
        cmd.SetMove(0, new MoveAction(MoveType.Step, 1, 0));    // cel = (1,0)

        gs.ApplyInPlace(cmd, new TurnCommand(GameState.MaxAgents));

        gs.Agents[0].X.Should().Be(1);
        gs.Agents[0].Y.Should().Be(0);
    }

    [Fact]
    public void Move_IntoStaticAgent_IsCancelled()
    {
        var gs = TestFactory.WithAgents((0, 0, 0), (1, 1, 0));

        var cmd = new TurnCommand(GameState.MaxAgents);
        cmd.SetMove(0, new MoveAction(MoveType.Step, 1, 0));

        gs.ApplyInPlace(cmd, new TurnCommand(GameState.MaxAgents));

        gs.Agents[0].X.Should().Be(0);
        gs.Agents[0].Y.Should().Be(0);
        gs.Agents[1].X.Should().Be(1);
        gs.Agents[1].Y.Should().Be(0);
    }


    [Fact]
    public void Move_TwoAgentsSameTile_BothCancelled()
    {
        var gs = TestFactory.WithAgents((0, 0, 0), (1, 2, 0));

        var a = new TurnCommand(GameState.MaxAgents);
        var b = new TurnCommand(GameState.MaxAgents);
        a.SetMove(0, new MoveAction(MoveType.Step, 1, 0));
        b.SetMove(1, new MoveAction(MoveType.Step, 1, 0));

        gs.ApplyInPlace(a, b);

        gs.Agents[0].X.Should().Be(0);
        gs.Agents[0].Y.Should().Be(0);
        gs.Agents[1].X.Should().Be(2);
        gs.Agents[1].Y.Should().Be(0);
    }

    [Fact]
    public void Move_SwapPositions_BothCancelled()
    {
        var gs = TestFactory.WithAgents((0, 0, 0), (1, 1, 0));

        var a = new TurnCommand(GameState.MaxAgents);
        var b = new TurnCommand(GameState.MaxAgents);
        a.SetMove(0, new MoveAction(MoveType.Step, 1, 0));
        b.SetMove(1, new MoveAction(MoveType.Step, 0, 0));

        gs.ApplyInPlace(a, b);

        gs.Agents[0].X.Should().Be(0);
        gs.Agents[0].Y.Should().Be(0);
        gs.Agents[1].X.Should().Be(1);
        gs.Agents[1].Y.Should().Be(0);
    }
}

public class CombatPhaseTests
{
    [Fact]
    public void Shoot_DealsCorrectDamageAndSetsCooldown()
    {
        var gs = TestFactory.WithAgents(new[] {
            (0, (byte)0, (byte)0, /*P*/0, true, 0, 0),
            (1, (byte)1, (byte)0, /*P*/1, true, 0, 0)
        });
        GameState.AgentClasses[0] = AgentClass.Gunner;

        var cmd = new TurnCommand(GameState.MaxAgents);
        cmd.SetCombat(0, new CombatAction(CombatType.Shoot, 1));

        gs.ApplyInPlace(cmd, new TurnCommand(GameState.MaxAgents));

        gs.Agents[1].Wetness.Should().Be(16); // Gunner deals 16 damage
        gs.Agents[0].Cooldown.Should().Be(1); // Gunner has 1 turn cooldown
    }

    [Fact]
    public void Shoot_TooFar_NoDamage()
    {
        var gs = TestFactory.WithAgents((0, 0, 0), (1, 5, 0));
        GameState.AgentClasses[0] = AgentClass.Gunner;

        var cmd = new TurnCommand(GameState.MaxAgents);
        cmd.SetCombat(0, new CombatAction(CombatType.Shoot, 1));

        gs.ApplyInPlace(cmd, new TurnCommand(GameState.MaxAgents));

        gs.Agents[1].Wetness.Should().Be(0);
    }

    [Fact]
    public void Throw_SoaksAgentsIn3x3Area()
    {
        var gs = TestFactory.WithAgents((0, 2, 2), (1, 3, 3), (2, 5, 5));
        GameState.AgentClasses[0] = AgentClass.Gunner;
        gs.Agents[0].SplashBombs = 1;

        var cmd = new TurnCommand(GameState.MaxAgents);
        cmd.SetCombat(0, new CombatAction(CombatType.Throw, 3, 3));

        gs.ApplyInPlace(cmd, new TurnCommand(GameState.MaxAgents));

        gs.Agents[1].Wetness.Should().Be(30);
        gs.Agents[2].Wetness.Should().Be(0);
        gs.Agents[0].SplashBombs.Should().Be(0);
    }
}

public class PossibleMovesTests
{
    [Fact(DisplayName = "Stay + 4 kroki / brak broni")]
    public void OnlyMovesAndHunker()
    {
        var gs = TestFactory.WithAgents(new[]
        {
            (0, (byte)1, (byte)1, 0, true, 0, 0)   // mój agent
        });

        GameState.Tiles[GameState.ToIndex(0, 1)] = TileType.Empty;
        GameState.Tiles[GameState.ToIndex(2, 1)] = TileType.Empty;
        GameState.Tiles[GameState.ToIndex(1, 0)] = TileType.Empty;
        GameState.Tiles[GameState.ToIndex(1, 2)] = TileType.Empty;

        Span<AgentOrder> buf = stackalloc AgentOrder[32];
        int n = gs.GetLegalOrders(0, buf);

        // 5 MOVE × 2 COMBAT (None, Hunker) = 10 wariantów
        n.Should().Be(10);
        for (int i = 0; i < n; ++i)
            buf[i].Combat.Type.Should().BeOneOf(CombatType.None, CombatType.Hunker);
    }

    [Fact(DisplayName = "Shoot generuje się tylko przy cooldown == 0 i w zasięgu")]
    public void ShootOnlyIfReadyAndInRange()
    {
        var gs = TestFactory.WithAgents(new[]
        {
            (0,(byte)0,(byte)0,0,true,0,0),   // mój agent, cd=0
            (1,(byte)1,(byte)0,1,true,0,0)    // przeciwnik w odl. 1
        });

        Span<AgentOrder> buf = stackalloc AgentOrder[32];
        int n = gs.GetLegalOrders(0, buf);

        bool hasShoot = false;
        for (int i = 0; i < n; ++i)
            if (buf[i].Combat.Type == CombatType.Shoot) hasShoot = true;
        hasShoot.Should().BeTrue();

        gs.Agents[0].Cooldown = 1;
        n = gs.GetLegalOrders(0, buf);
        for (int i = 0; i < n; ++i)
            buf[i].Combat.Type.Should().NotBe(CombatType.Shoot);
    }

    [Fact(DisplayName = "Rzuty bomb ograniczone zasięgiem Manhattan ≤ 4")]
    public void ThrowDistanceLimited()
    {
        var gs = TestFactory.WithAgents(new[]
        {
            (0,(byte)5,(byte)5,0,true,1,0)
        });

        Span<AgentOrder> buf = stackalloc AgentOrder[512]; // większy, bo 5×40=200
        int n = gs.GetLegalOrders(0, buf);

        var targets = new HashSet<(ushort,byte)>();
        for (int i=0;i<n;i++)
            if (buf[i].Combat.Type == CombatType.Throw)
                targets.Add((buf[i].Combat.Arg1, buf[i].Combat.Arg2));

        targets.Count.Should().Be(40);               // 40 pól w promieniu ≤4

        // każda współrzędna spełnia |dx|+|dy| ≤ 4
        foreach (var (tx,ty) in targets)
            (Math.Abs(tx-5) + Math.Abs(ty-5)).Should().BeLessThanOrEqualTo(4);
    }

    [Fact(DisplayName = "Move nie wchodzi na cover ani agenta")]
    public void MoveSkipsBlockedTiles()
    {
        var gs = TestFactory.WithAgents(
            new[]
            {
                (0,(byte)0,(byte)0,0,true,0,0),
                (1,(byte)1,(byte)0,1,true,0,0)  // przeciwnik blokuje (1,0)
            },
            (1,1,TileType.LowCover)            // cover na (1,1)
        );

        Span<AgentOrder> buf = stackalloc AgentOrder[32];
        int n = gs.GetLegalOrders(0, buf);

        var targets = new HashSet<(byte,byte)>();
        for (int i = 0; i < n; ++i)
            targets.Add((buf[i].Move.X, buf[i].Move.Y));

        targets.Should().BeEquivalentTo(new[] { ((byte)0,(byte)0), ((byte)0,(byte)1) });
    }
}