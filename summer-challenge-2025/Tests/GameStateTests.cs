using FluentAssertions;
using Xunit;
using SummerChallenge2025.Bot;

namespace SummerChallenge2025.Tests;

public static class TestFactory
{
    public static GameState EmptyState()
    {
        var gs = new GameState(GameState.MaxW, GameState.MaxH);
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
            gs.Agents[id] = new AgentState
            {
                X = x,
                Y = y,
                Alive = alive,
                playerId = player,
                SplashBombs = bombs,
                Cooldown = cd
            };
            if (alive) gs.Occup.Set(GameState.ToIndex(x, y));
        }

        for (int i = 0; i < GameState.MaxAgents; ++i)
            GameState.AgentClasses[0] = AgentClass.Gunner;
        return gs;
    }
    
    public static void ResetStatics()
    {
        var tiles = new TileType[GameState.Cells];
        var classes = new AgentClass[GameState.MaxAgents];
        Array.Fill(classes, AgentClass.Gunner);
        GameState.InitStatic(tiles, classes);
    }
}

public class MovePhaseTests
{
    public MovePhaseTests() => TestFactory.ResetStatics();

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

    [Fact(DisplayName = "Cel zajęty przez cover ➜ stoi w miejscu")]
    public void Move_IntoCover_ChoosesAlternateStep()
    {
        var gs = TestFactory.WithAgents(
            new[] {
                (0, (byte)5, (byte)5, 0, true, 0, 0),
            },
            (5, 6, TileType.HighCover)
        );
        var cmd = new TurnCommand(GameState.MaxAgents);
        cmd.SetMove(0, new MoveAction(MoveType.Step, 5, 6));

        gs.ApplyInPlace(cmd, new TurnCommand(GameState.MaxAgents));

        gs.Agents[0].X.Should().Be(5);
        gs.Agents[0].Y.Should().Be(5);
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
    public CombatPhaseTests() => TestFactory.ResetStatics();
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
    public PossibleMovesTests() => TestFactory.ResetStatics();
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

        var targets = new HashSet<(ushort, byte)>();
        for (int i = 0; i < n; i++)
            if (buf[i].Combat.Type == CombatType.Throw)
                targets.Add((buf[i].Combat.Arg1, buf[i].Combat.Arg2));

        targets.Count.Should().Be(40);               // 40 pól w promieniu ≤4

        // każda współrzędna spełnia |dx|+|dy| ≤ 4
        foreach (var (tx, ty) in targets)
            (Math.Abs(tx - 5) + Math.Abs(ty - 5)).Should().BeLessThanOrEqualTo(4);
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
            (1, 1, TileType.LowCover)            // cover na (1,1)
        );

        Span<AgentOrder> buf = stackalloc AgentOrder[32];
        int n = gs.GetLegalOrders(0, buf);

        var targets = new HashSet<(byte, byte)>();
        for (int i = 0; i < n; ++i)
            targets.Add((buf[i].Move.X, buf[i].Move.Y));

        targets.Should().BeEquivalentTo(new[] { ((byte)0, (byte)0), ((byte)0, (byte)1) });
    }
}

public class GameStateCombatTests
{
    public GameStateCombatTests() => TestFactory.ResetStatics();

    [Fact(DisplayName = "HighCover zmniejsza obrażenia od Shoot (75%)")]
    public void Shoot_HighCover_ReducesDamage()
    {
        var gs = TestFactory.WithAgents(
            new[] {
                (0, (byte)0, (byte)0, 0, true, 0, 0),
                (1, (byte)4, (byte)0, 1, true, 0, 0)
            },
            (3, 0, TileType.HighCover) // cover między nimi
        );
        GameState.AgentClasses[0] = AgentClass.Gunner;

        var cmd = new TurnCommand(GameState.MaxAgents);
        cmd.SetCombat(0, new CombatAction(CombatType.Shoot, 1));

        gs.ApplyInPlace(cmd, new TurnCommand(GameState.MaxAgents));
        gs.Agents[1].Wetness.Should().Be(4); // 16 * 0.75
    }

    [Fact(DisplayName = "LowCover zmniejsza obrażenia od Shoot (50%)")]
    public void Shoot_LowCover_ReducesDamage()
    {
        var gs = TestFactory.WithAgents(
            new[] {
                (0, (byte)0, (byte)1, 0, true, 0, 0),
                (1, (byte)4, (byte)1, 1, true, 0, 0)
            },
            (3, 1, TileType.LowCover)
        );
        GameState.AgentClasses[0] = AgentClass.Gunner;

        var cmd = new TurnCommand(GameState.MaxAgents);
        cmd.SetCombat(0, new CombatAction(CombatType.Shoot, 1));

        gs.ApplyInPlace(cmd, new TurnCommand(GameState.MaxAgents));
        gs.Agents[1].Wetness.Should().Be(8); // 16 * 0.5
    }


    [Fact(DisplayName = "HighCover does not block from same side")]
    public void HighCoverNotBlockSameSide()
    {
        var gs = TestFactory.WithAgents(
            new[] {
                (0, (byte)0, (byte)0, 0, true, 0, 0),
                (1, (byte)2, (byte)0, 1, true, 0, 0)
            },
            (3, 1, TileType.HighCover)
        );
        GameState.AgentClasses[0] = AgentClass.Gunner;
        var cmd = new TurnCommand(GameState.MaxAgents);
        cmd.SetCombat(0, new CombatAction(CombatType.Shoot, 1));
        gs.ApplyInPlace(cmd, new TurnCommand(GameState.MaxAgents));
        gs.Agents[1].Wetness.Should().Be(16);
    }

    [Fact(DisplayName = "Hunkering reduces damage")]
    public void HunkeringReducesDamage()
    {
        var gs = TestFactory.WithAgents(
            new[] {
                (0, (byte)0, (byte)0, 0, true, 0, 0),
                (1, (byte)1, (byte)0, 1, true, 0, 0)
            }
        );
        GameState.AgentClasses[0] = AgentClass.Gunner;
        var c0 = new TurnCommand(GameState.MaxAgents);
        var c1 = new TurnCommand(GameState.MaxAgents);
        c0.SetCombat(0, new CombatAction(CombatType.Shoot, 1));
        c1.SetCombat(1, new CombatAction(CombatType.Hunker));
        gs.ApplyInPlace(c0, c1);
        gs.Agents[1].Wetness.Should().Be(12);
    }

    [Fact(DisplayName = "Throw hits 2 agents in 3x3")]
    public void ThrowHitsMultipleAgents()
    {
        var gs = TestFactory.WithAgents(
            new[] {
                (0, (byte)1, (byte)1, 0, true, 1, 0),
                (1, (byte)2, (byte)2, 1, true, 0, 0),
                (2, (byte)3, (byte)2, 1, true, 0, 0)
            }
        );
        var cmd = new TurnCommand(GameState.MaxAgents);
        cmd.SetCombat(0, new CombatAction(CombatType.Throw, 2, 2));
        gs.ApplyInPlace(cmd, new TurnCommand(GameState.MaxAgents));
        gs.Agents[1].Wetness.Should().Be(30);
        gs.Agents[2].Wetness.Should().Be(30);
        gs.Agents[0].SplashBombs.Should().Be(0);
    }

    [Fact(DisplayName = "Agent dies when wetness >= 100")]
    public void AgentDiesWhenWet()
    {
        var gs = TestFactory.WithAgents(
            new[] {
                (0, (byte)0, (byte)0, 0, true, 0, 0),
                (1, (byte)1, (byte)0, 1, true, 0, 0)
            }
        );
        GameState.AgentClasses[0] = AgentClass.Gunner;
        gs.Agents[1].Wetness = 90;
        for (int i = 0; i < 2; ++i)
        {
            var cmd = new TurnCommand(GameState.MaxAgents);
            cmd.SetCombat(0, new CombatAction(CombatType.Shoot, 1));
            gs.ApplyInPlace(cmd, new TurnCommand(GameState.MaxAgents));
        }
        gs.Agents[1].Alive.Should().BeFalse();
        gs.Occup.Test(GameState.ToIndex(1, 0)).Should().BeFalse();
    }

    [Fact(DisplayName = "Game ends on full team wipe")]
    public void GameEndsOnKillAll()
    {
        var gs = TestFactory.WithAgents(
            new[] {
                (0, (byte)0, (byte)0, 0, true, 0, 0),
                (1, (byte)1, (byte)0, 1, true, 0, 0)
            }
        );
        GameState.AgentClasses[0] = AgentClass.Gunner;
        gs.Agents[1].Wetness = 95;
        var cmd = new TurnCommand(GameState.MaxAgents);
        cmd.SetCombat(0, new CombatAction(CombatType.Shoot, 1));
        gs.ApplyInPlace(cmd, new TurnCommand(GameState.MaxAgents));
        gs.IsGameOver.Should().BeTrue();
        gs.Winner.Should().Be(0);
    }

    [Fact(DisplayName = "Game ends after 100 turns")]
    public void GameEndsAfter100Turns()
    {
        var gs = TestFactory.WithAgents((0, 0, 0), (1, 5, 5));
        for (int i = 0; i < 100; ++i)
        {
            gs.ApplyInPlace(new TurnCommand(GameState.MaxAgents), new TurnCommand(GameState.MaxAgents));
        }
        gs.IsGameOver.Should().BeTrue();
    }

    [Fact(DisplayName = "Punktacja: ręczne liczenie pól kontrolowanych")]
    public void Scoring_CorrectlyCountsControlledTiles()
    {
        // Plansza 5x5, dwóch agentów – gracz 0 w (0,0), gracz 1 w (4,4)
        var gs = new GameState(5, 5);
        GameState.InitStatic(
            Enumerable.Repeat(TileType.Empty, GameState.Cells).ToArray(),
            Enumerable.Repeat(AgentClass.Gunner, GameState.MaxAgents).ToArray()
        );

        gs.ClearAgents();
        gs.Agents[0] = new AgentState { X = 0, Y = 0, Alive = true, playerId = 0 };
        gs.Agents[1] = new AgentState { X = 4, Y = 4, Alive = true, playerId = 1 };
        gs.Occup.Set(GameState.ToIndex(0, 0));
        gs.Occup.Set(GameState.ToIndex(4, 4));

        // Przed ruchem (po 1. Apply) – punktacja
        gs.ApplyInPlace(new TurnCommand(GameState.MaxAgents), new TurnCommand(GameState.MaxAgents));

        int tiles0 = 0, tiles1 = 0;

        for (int y = 0; y < 5; y++)
        for (int x = 0; x < 5; x++)
        {
            int idx = GameState.ToIndex(x, y);
            if (GameState.Tiles[idx] != TileType.Empty) continue;

            int d0 = Math.Abs(x - gs.Agents[0].X) + Math.Abs(y - gs.Agents[0].Y);
            int d1 = Math.Abs(x - gs.Agents[1].X) + Math.Abs(y - gs.Agents[1].Y);

            if (gs.Agents[0].Wetness >= 50) d0 *= 2;
            if (gs.Agents[1].Wetness >= 50) d1 *= 2;

            if (d0 < d1) tiles0++;
            else if (d1 < d0) tiles1++;
            // jeśli równe → nikt nie kontroluje
        }

        int expectedDiff = Math.Abs(tiles0 - tiles1);

        if (tiles0 > tiles1)
        {
            gs.Score0.Should().Be(expectedDiff);
            gs.Score1.Should().Be(0);
        }
        else if (tiles1 > tiles0)
        {
            gs.Score1.Should().Be(expectedDiff);
            gs.Score0.Should().Be(0);
        }
        else
        {
            gs.Score0.Should().Be(0);
            gs.Score1.Should().Be(0);
        }
    }
}

public class GameStateCombatTests_MultiAgent
{
    public GameStateCombatTests_MultiAgent() => TestFactory.ResetStatics();

    [Fact(DisplayName = "Dwóch agentów dostaje obrażenia od bomby")]
    public void ThrowHitsMultipleEnemies()
    {
        var gs = TestFactory.WithAgents(
            new[] {
                (0, (byte)5, (byte)5, 0, true, 1, 0),
                (1, (byte)6, (byte)5, 1, true, 0, 0),
                (2, (byte)4, (byte)6, 1, true, 0, 0)
            });
        var c0 = new TurnCommand(GameState.MaxAgents);
        c0.SetCombat(0, new CombatAction(CombatType.Throw, 5, 5));
        gs.ApplyInPlace(c0, new TurnCommand(GameState.MaxAgents));
        gs.Agents[1].Wetness.Should().Be(30);
        gs.Agents[2].Wetness.Should().Be(30);
    }

    [Fact(DisplayName = "Agent nie porusza się na pole z coverem")]
    public void MoveBlockedByCover()
    {
        var gs = TestFactory.WithAgents((0, 1, 1));
        GameState.Tiles[GameState.ToIndex(2, 1)] = TileType.HighCover;
        GameState.InitStatic(GameState.Tiles, GameState.AgentClasses);

        var cmd = new TurnCommand(GameState.MaxAgents);
        cmd.SetMove(0, new MoveAction(MoveType.Step, 2, 1));
        gs.ApplyInPlace(cmd, new TurnCommand(GameState.MaxAgents));
        gs.Agents[0].X.Should().Be(1);
    }

    [Fact(DisplayName = "Dwóch agentów chce wejść na to samo pole – konflikt, obaj stoją")]
    public void MoveConflictCancelsBoth()
    {
        var gs = TestFactory.WithAgents((0, 1, 1), (1, 3, 1));
        var c0 = new TurnCommand(GameState.MaxAgents);
        var c1 = new TurnCommand(GameState.MaxAgents);
        c0.SetMove(0, new MoveAction(MoveType.Step, 2, 1));
        c1.SetMove(1, new MoveAction(MoveType.Step, 2, 1));
        gs.ApplyInPlace(c0, c1);
        gs.Agents[0].X.Should().Be(1);
        gs.Agents[1].X.Should().Be(3);
    }

    [Fact(DisplayName = "Nie można wejść poza mapę")]
    public void MoveOutOfBoundsIgnored()
    {
        var gs = TestFactory.WithAgents((0, 19, 0));
        var cmd = new TurnCommand(GameState.MaxAgents);
        cmd.SetMove(0, new MoveAction(MoveType.Step, 20, 0));
        gs.ApplyInPlace(cmd, new TurnCommand(GameState.MaxAgents));
        gs.Agents[0].X.Should().Be(19);
    }

    [Fact(DisplayName = "Cooldown zmniejsza się o 1")]
    public void CooldownDecreasesEachTurn()
    {
        var gs = TestFactory.WithAgents(
            new[] {
                (0, (byte)5, (byte)5, 0, true, 0, 0),
                (1, (byte)6, (byte)5, 1, true, 0, 0)
            });
        GameState.AgentClasses[0] = AgentClass.Sniper;
        var atk = new TurnCommand(GameState.MaxAgents);
        atk.SetCombat(0, new CombatAction(CombatType.Shoot, 1));
        gs.ApplyInPlace(atk, new TurnCommand(GameState.MaxAgents));
        gs.Agents[0].Cooldown.Should().Be(5);

        gs.ApplyInPlace(new TurnCommand(GameState.MaxAgents), new TurnCommand(GameState.MaxAgents));
        gs.Agents[0].Cooldown.Should().Be(4);
    }

    [Fact(DisplayName = "Rzucenie bomby poza zasięg 4 nie działa")]
    public void ThrowTooFarIgnored()
    {
        var gs = TestFactory.WithAgents(new[] { (0, (byte)1, (byte)1, 0, true, 1, 0) });
        var cmd = new TurnCommand(GameState.MaxAgents);
        cmd.SetCombat(0, new CombatAction(CombatType.Throw, 10, 10));
        gs.ApplyInPlace(cmd, new TurnCommand(GameState.MaxAgents));
        gs.Agents[0].SplashBombs.Should().Be(1);
    }

    [Fact(DisplayName = "Agent ginie od splash bomby gdy ma 99 wetness")]
    public void AgentDiesFromThrow()
    {
        var gs = TestFactory.WithAgents(
            new[] {
                (0, (byte)5, (byte)5, 0, true, 1, 0),
                (1, (byte)6, (byte)5, 1, true, 0, 0)
            });
        gs.Agents[1].Wetness = 99;
        var c0 = new TurnCommand(GameState.MaxAgents);
        c0.SetCombat(0, new CombatAction(CombatType.Throw, 6, 5));
        gs.ApplyInPlace(c0, new TurnCommand(GameState.MaxAgents));
        gs.Agents[1].Alive.Should().BeFalse();
    }
}

public class GameStateScoreTests
{
    public GameStateScoreTests() => TestFactory.ResetStatics();

    private GameState CreateCustomState(int w, int h, params (int id, int x, int y, int player, int wet)[] agents)
    {
        var gs = new GameState((byte)w, (byte)h);
        GameState.InitStatic(
            Enumerable.Repeat(TileType.Empty, GameState.Cells).ToArray(),
            Enumerable.Repeat(AgentClass.Gunner, GameState.MaxAgents).ToArray()
        );

        gs.ClearAgents();
        foreach (var (id, x, y, player, wet) in agents)
        {
            gs.Agents[id] = new AgentState
            {
                X = (byte)x,
                Y = (byte)y,
                Alive = true,
                playerId = player,
                Wetness = wet
            };
            gs.Occup.Set(GameState.ToIndex(x, y));
        }
        return gs;
    }

    [Theory(DisplayName = "Statyczna punktacja na mapie 5x5")]
    [InlineData(0, 0, 0, 4, 4, 1, 0, 0)]   // gracz 0 i 1 symetrycznie
    [InlineData(0, 0, 0, 2, 2, 1, 0, 12)]  // gracz 1 bliżej środka
    [InlineData(2, 2, 0, 4, 4, 1, 12, 0)]   // gracz 0 dominuje
    public void Score_Static5x5(int x0, int y0, int wet0, int x1, int y1, int wet1, int expected0, int expected1)
    {
        var gs = CreateCustomState(5, 5,
            (0, x0, y0, 0, wet0),
            (1, x1, y1, 1, wet1));

        gs.ApplyInPlace(new TurnCommand(GameState.MaxAgents), new TurnCommand(GameState.MaxAgents));
        gs.Score0.Should().Be(expected0);
        gs.Score1.Should().Be(expected1);
    }

    [Fact(DisplayName = "Wetness ≥ 50 zmniejsza zasięg logiczny")]
    public void Score_WetnessPenalty()
    {
        var gs = CreateCustomState(5, 5,
            (0, 0, 0, 0, 60),  // z karą (wetness ≥ 50)
            (1, 4, 4, 1, 0));

        gs.ApplyInPlace(new TurnCommand(GameState.MaxAgents), new TurnCommand(GameState.MaxAgents));
        gs.Score0.Should().Be(0);
        gs.Score1.Should().BePositive();
    }

    [Fact(DisplayName = "Punkty przyrastają z każdą turą, gdy jeden gracz się porusza")]
    public void Score_Progression()
    {
        var gs = CreateCustomState(5, 5,
            (0, 0, 2, 0, 0),  // agent 0 w lewo
            (1, 4, 2, 1, 0)); // agent 1 stoi

        int prevScore0 = 0;
        for (int t = 0; t < 3; ++t)
        {
            var cmd = new TurnCommand(GameState.MaxAgents);
            cmd.SetMove(0, new MoveAction(MoveType.Step, (byte)(gs.Agents[0].X + 1), gs.Agents[0].Y));
            gs.ApplyInPlace(cmd, new TurnCommand(GameState.MaxAgents));
            gs.Score0.Should().BeGreaterThanOrEqualTo(prevScore0);
            prevScore0 = gs.Score0;
        }
        gs.Score0.Should().BePositive();
        gs.Score1.Should().Be(0);
    }

    [Fact(DisplayName = "Punktacja w przypadku 2v2 agentów")]
    public void Score_TwoVsTwo_RegularAndWetness()
    {
        var gs = CreateCustomState(5, 5,
            (0, 0, 0, 0, 0),   // team 0
            (1, 0, 4, 0, 0),   // team 0
            (2, 4, 0, 1, 0),   // team 1
            (3, 4, 4, 1, 0));  // team 1

        // 1. Początkowo powinno być równo
        gs.ApplyInPlace(new TurnCommand(GameState.MaxAgents), new TurnCommand(GameState.MaxAgents));
        gs.Score0.Should().Be(0);
        gs.Score1.Should().Be(0);

        // 2. Osłab team 1 → jego zasięg logiczny się pogorszy
        gs.Agents[2].Wetness = 60;
        gs.Agents[3].Wetness = 60;

        gs.ApplyInPlace(new TurnCommand(GameState.MaxAgents), new TurnCommand(GameState.MaxAgents));
        gs.Score0.Should().BePositive(); // powinien zyskać przewagę
        gs.Score1.Should().Be(0);
    }
}