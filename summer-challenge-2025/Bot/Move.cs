using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace SummerChallenge2025.Bot;

public enum MoveType : byte   { None = 0, Step }
public enum CombatType : byte { None = 0, Shoot, Throw, Hunker }

public readonly struct MoveAction
{
    public readonly MoveType Type;
    public readonly byte X;
    public readonly byte Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MoveAction(MoveType type, byte x = 0, byte y = 0)
        => (Type, X, Y) = (type, x, y);
}

public readonly struct CombatAction
{
    public readonly CombatType Type;
    public readonly ushort Arg1;   // X or enemyId
    public readonly byte Arg2;   // Y (THROW)

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CombatAction(CombatType type, ushort a1 = 0, byte a2 = 0)
        => (Type, Arg1, Arg2) = (type, a1, a2);
}

public struct AgentOrder  // 8 bytes
{
    public MoveAction Move;
    public CombatAction Combat;
}

public struct TurnCommand
{
    // (slot = agentId => O(1) lookup)
    public AgentOrder[] Orders;          // length = GameState.MaxAgents

    public ulong ActiveMask;             // bit i = agent i has command

    public TurnCommand(int maxAgents)
    {
        Orders = new AgentOrder[maxAgents]; // pre-allocation ≈ 8 × 10 = 80 B
        ActiveMask = 0UL;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly AgentOrder Get(int id) => Orders[id];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetMove(int id, MoveAction mv)
    {
        ref var o = ref Orders[id];
        o.Move = mv;
        ActiveMask |= 1UL << id;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetCombat(int id, CombatAction cb)
    {
        ref var o = ref Orders[id];
        o.Combat = cb;
        ActiveMask |= 1UL << id;
    }

    public readonly IEnumerable<int> EnumerateActive()
    {
        ulong mask = ActiveMask;
        while (mask != 0)
        {
            int id = BitOperations.TrailingZeroCount(mask);
            yield return id;
            mask &= mask - 1;
        }
    }
    
    public IEnumerable<string> ToLines(GameState st, int myId)
    {
        var sb = new StringBuilder(32);

        for (int id = 0; id < GameState.MaxAgents; ++id)
        {   
            if (!st.Agents[id].Alive) continue;
            if (st.Agents[id].playerId != myId) continue;
            var ord = Orders[id];
            sb.Clear();
            sb.Append(id + 1).Append(';');

            // MOVE
            if (ord.Move.Type == MoveType.Step)
                sb.Append("MOVE ").Append(ord.Move.X).Append(' ').Append(ord.Move.Y).Append(';');

            // COMBAT
            switch (ord.Combat.Type)
            {
                case CombatType.Shoot:
                    sb.Append("SHOOT ").Append(ord.Combat.Arg1 + 1);
                    break;
                case CombatType.Throw:
                    sb.Append("THROW ").Append(ord.Combat.Arg1).Append(' ').Append(ord.Combat.Arg2);
                    break;
                case CombatType.Hunker:
                    sb.Append("HUNKER_DOWN");
                    break;
                case CombatType.None:
                    sb.Append("HUNKER_DOWN");
                    break;
            }
            yield return sb.ToString();
        }
    }
}