using System;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Collections;
using System.Collections.Generic;

static class PermRepair
{
    public static void Repair1ToNminus1(int[] child, int n)
    {
        int m = child.Length;
        var count = new int[n];
        var missing = new List<int>(m);

        for (int i = 0; i < m; i++)
        {
            int v = child[i];
            if (v > 0 && v < n) count[v]++;
        }
        for (int v = 1; v < n; v++)
            if (count[v] == 0) missing.Add(v);

        if (missing.Count == 0) return;

        int missIdx = 0;
        Array.Clear(count, 0, count.Length);
        for (int i = 0; i < m; i++)
        {
            int v = child[i];
            bool okFirst = (v > 0 && v < n && count[v] == 0);
            if (okFirst)
            {
                count[v] = 1;
            }
            else
            {
                if (missIdx < missing.Count)
                {
                    child[i] = missing[missIdx++];
                }
                else
                {
                    child[i] = 1;
                }
            }
        }
    }
}

public sealed class ProblemInstance
{
    public int N;
    public int M;
    public int Capacity;
    public (int x, int y)[] points;
    public int[] Demand;
    public int[] Dist;
    public int[] Dist0;       // Dist0[v] = dist(0,v)
    public int[] DistTo0;     // DistTo0[v] = dist(v,0)

    public ProblemInstance(int n, int capacity)
    {
        N = n;
        M = n - 1;
        Capacity = capacity;
        points = new (int x, int y)[N];
        Demand = new int[N];
        Dist = new int[N * N];
        Dist0 = new int[N];
        DistTo0 = new int[N];
    }

    public void CalculateDists()
    {
        for (int i = 0; i < N; ++i)
        {
            for (int j = 0; j < N; ++j)
            {
                if (i == j) continue;
                var a = points[i];
                var b = points[j];
                double dist = Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y));
                Dist[i * N + j] = (int)Math.Round(dist);
            }
        }
        for (int v = 0; v < N; v++) { Dist0[v] = Dist[0*N + v]; DistTo0[v] = Dist[v*N + 0]; }
    }

    public int D(int i, int j) => Dist[i * N + j];
}

public sealed class Individual
{
    public int[] Perm;
    public int Fitness;
    public int[] Parent;
    public Individual(int m)
    {
        Perm = new int[m];
        Parent = new int[m + 1];
    }
}

public static class SplitDecoder
{
    public static int Evaluate(
        ProblemInstance P, int[] perm, int[] parent,
        int[] dp, int[] loadPrefix, int[] runPrefix)
    {
        int m = perm.Length;
        var demand = P.Demand;
        var dist = P.Dist;
        var dist0 = P.Dist0;
        var distTo0 = P.DistTo0;
        int N = P.N, cap = P.Capacity;

        // 1) prefixy
        loadPrefix[0] = 0;
        for (int t = 1; t <= m; t++)
            loadPrefix[t] = loadPrefix[t - 1] + demand[perm[t - 1]];

        if (m >= 1)
        {
            runPrefix[0] = 0;
            for (int t = 1; t <= m - 1; t++)
            {
                int a = perm[t - 1];
                int b = perm[t];
                runPrefix[t] = runPrefix[t - 1] + dist[a * N + b];
            }
        }

        const int INF = int.MaxValue / 4;
        dp[0] = 0; parent[0] = -1;
        for (int j = 1; j <= m; j++) dp[j] = INF;

        for (int j = 1; j <= m; j++)
        {
            int bestCost = INF, bestI = -1;

            int load = 0;
            for (int i = j - 1; i >= 0; i--)
            {
                load += demand[perm[i]];
                if (load > cap) break;

                int first = perm[i];
                int last = perm[j - 1];
                int inner = (j - i >= 2) ? (runPrefix[j - 1] - runPrefix[i]) : 0;

                int routeCost = dist0[first] + inner + distTo0[last];
                int cand = dp[i] + routeCost;
                if (cand < bestCost) { bestCost = cand; bestI = i; }
            }

            dp[j] = bestCost;
            parent[j] = bestI;
        }
        return dp[m];
    }

    public static List<(int L, int R)> ReconstructCuts(int m, int[] parent)
    {
        var segs = new List<(int L, int R)>();
        int j = m;
        while (j > 0)
        {
            int i = parent[j];
            if (i < 0) { segs.Clear(); break; }
            segs.Add((i, j - 1));
            j = i;
        }
        segs.Reverse();
        return segs;
    }
}

static class Seeders
{
    public static void Sweep((int x, int y)[] pts, int N, int[] outPerm)
    {
        var tmp = new List<(int id, double ang)>(N-1);
        for (int id = 1; id < N; id++)
        {
            var p = pts[id];
            double ang = Math.Atan2(p.y - pts[0].y, p.x - pts[0].x);
            tmp.Add((id, ang));
        }
        tmp.Sort((a,b) => a.ang.CompareTo(b.ang));
        for (int i = 0; i < tmp.Count; i++) outPerm[i] = tmp[i].id;
    }

    public static void NearestNeighbor(ProblemInstance P, int[] outPerm)
    {
        int N = P.N;
        var used = new bool[N];
        int best = int.MaxValue;
        int pick = 1;
        for (int id = 1; id < N; id++)
        {
            int d = P.Dist0[id];
            if (d < best) { best = d; pick = id; }
        }

        int cur = pick;
        int idx = 0;
        outPerm[idx++] = cur;
        used[cur] = true;

        while (idx < P.M)
        {
            int nxt = -1, bestD = int.MaxValue;
            for (int v = 1; v < N; v++)
            {
                if (used[v]) continue;
                int d = P.Dist[cur * N + v];
                if (d < bestD) { bestD = d; nxt = v; }
            }
            if (nxt == -1) break;
            outPerm[idx++] = nxt;
            used[nxt] = true;
            cur = nxt;
        }

        for (int v = 1; v < N && idx < P.M; v++)
            if (!used[v]) outPerm[idx++] = v;
    }
}

public static class Operators
{
    private static readonly Random Rng = new Random(1337);
    public static readonly Random RngForLS = new Random(777);

    public static void Shuffle(int[] a)
    {
        for (int i = a.Length - 1; i > 0; i--)
        {
            int j = Rng.Next(i + 1);
            (a[i], a[j]) = (a[j], a[i]);
        }
    }

    public static void OX(int[] p1, int[] p2, int[] child, int m, int[] buf, bool[] seen)
    {
        int L = Rng.Next(m), R = Rng.Next(m);
        if (L > R) (L, R) = (R, L);

        Array.Clear(seen, 0, seen.Length);
        Array.Copy(p2, buf, m);

        for (int i = L; i <= R; i++)
        {
            int val = p1[i];
            child[i] = val;
            if (val >= 0 && val < seen.Length) seen[val] = true;
        }

        int idx = (R + 1) % m;
        int p2idx = (R + 1) % m;
        int guard = 0;

        while (idx != L)
        {
            int cand = buf[p2idx];
            if (cand < 0 || cand >= seen.Length || !seen[cand])
            {
                child[idx] = cand;
                if (cand >= 0 && cand < seen.Length) seen[cand] = true;
                idx = (idx + 1) % m;
            }
            p2idx = (p2idx + 1) % m;

            if (++guard > 2 * m)
            {
                for (int t = 0; idx != L && t < m; t++)
                {
                    int x = buf[t];
                    if (x >= 0 && x < seen.Length && !seen[x])
                    {
                        child[idx] = x;
                        seen[x] = true;
                        idx = (idx + 1) % m;
                    }
                }
                break;
            }
        }
        PermRepair.Repair1ToNminus1(child, seen.Length);
    }

    public static void MutateSwap(int[] a)
    {
        if (a.Length < 2) return;
        int i = Rng.Next(a.Length);
        int j = Rng.Next(a.Length);
        (a[i], a[j]) = (a[j], a[i]);
    }

    public static void MutateInsert(int[] a)
    {
        if (a.Length < 2) return;
        int from = Rng.Next(a.Length);
        int to = Rng.Next(a.Length);
        if (from == to) return;

        int val = a[from];
        if (from < to)
        {
            Array.Copy(a, from + 1, a, from, to - from);
            a[to] = val;
        }
        else
        {
            Array.Copy(a, to, a, to + 1, from - to);
            a[to] = val;
        }
    }

    public static void MutateReverseSmall(int[] a)
    {
        if (a.Length < 2) return;
        int maxLen = Math.Min(8, a.Length);
        int len = 1 + Rng.Next(maxLen);
        int i = Rng.Next(a.Length - len + 1);
        int j = i + len - 1;
        while (i < j) (a[i++], a[j--]) = (a[j], a[i]);
    }
}

static class LocalSearch
{
    public static bool TwoOptOnRoute(ProblemInstance P, int[] perm, int L, int R)
    {
        if (R - L + 1 < 4) return false;
        int N = P.N;
        var dist = P.Dist;
        var dist0 = P.Dist0; var distTo0 = P.DistTo0;

        const int tries = 12;
        var rng = Operators.RngForLS;

        for (int t = 0; t < tries; t++)
        {
            int a = rng.Next(L, R - 1);
            int b = rng.Next(a + 2, R + 1);

            int pa = (a == L) ? 0 : perm[a - 1];
            int qa = perm[a];
            int qb = perm[b - 1];
            int pb = (b == R + 1) ? 0 : perm[b];

            int before = 0, after = 0;

            before += (a == L) ? dist0[qa] : dist[pa * N + qa];
            before += (b == R + 1) ? distTo0[qb] : dist[qb * N + pb];

            after  += (a == L) ? dist0[qb] : dist[pa * N + qb];
            after  += (b == R + 1) ? distTo0[qa] : dist[qa * N + pb];

            int delta = after - before;
            if (delta < 0)
            {
                for (int i = a, j = b - 1; i < j; i++, j--)
                    (perm[i], perm[j]) = (perm[j], perm[i]);
                return true;
            }
        }
        return false;
    }

    public static bool ImproveEliteInRoutes(ProblemInstance P, Individual elite, int maxRoutes, int maxPasses)
    {
        var segs = SplitDecoder.ReconstructCuts(P.M, elite.Parent);
        bool improved = false;
        int routes = Math.Min(maxRoutes, segs.Count);

        for (int r = 0; r < routes; r++)
        {
            var (L, R) = segs[r];
            int passes = maxPasses;
            while (passes-- > 0)
            {
                if (TwoOptOnRoute(P, elite.Perm, L, R)) { improved = true; }
                else break;
            }
        }
        return improved;
    }
}

public sealed class GeneticEA
{
    private readonly ProblemInstance P;
    private readonly int PopSize;
    private readonly Individual[] Pop;
    private readonly Individual[] Next;
    private readonly int[] Buf;
    private readonly bool[] Seen;
    private readonly Random Rng = new Random(42);

    private readonly int TournamentK = 3;
    private readonly double Pm = 0.2;
    private readonly int MaxGen = 5000;
    private double PmCur;
    private int noImpr;
    private readonly int[] ScratchDp;
    private readonly int[] ScratchLoad;
    private readonly int[] ScratchRun;

    public GeneticEA(ProblemInstance p, int popSize)
    {
        P = p;
        PopSize = popSize;
        Pop = new Individual[PopSize];
        Next = new Individual[PopSize];
        for (int i = 0; i < PopSize; i++)
        {
            Pop[i] = new Individual(P.M);
            Next[i] = new Individual(P.M);
        }
        Buf = new int[P.M];
        Seen = new bool[P.N];
        ScratchDp   = new int[P.M + 1];
        ScratchLoad = new int[P.M + 1];
        ScratchRun  = new int[P.M];
        PmCur = Pm;
        noImpr = 0;
    }

    public void InitPopulation()
    {
        var basePerm = new int[P.M];
        for (int i = 0; i < P.M; i++) basePerm[i] = i + 1;

        var dp = ScratchDp; var load = ScratchLoad; var run = ScratchRun;

        Seeders.Sweep(P.points, P.N, Pop[0].Perm);
        Pop[0].Fitness = SplitDecoder.Evaluate(P, Pop[0].Perm, Pop[0].Parent, dp, load, run);

        Seeders.NearestNeighbor(P, Pop[1].Perm);
        Pop[1].Fitness = SplitDecoder.Evaluate(P, Pop[1].Perm, Pop[1].Parent, dp, load, run);

        for (int i = 2; i < PopSize; i++)
        {
            Array.Copy(basePerm, Pop[i].Perm, P.M);
            Operators.Shuffle(Pop[i].Perm);
            Pop[i].Fitness = SplitDecoder.Evaluate(P, Pop[i].Perm, Pop[i].Parent, dp, load, run);
        }
    }

    private Individual Tournament(Individual[] pool, int k)
    {
        var best = pool[Rng.Next(PopSize)];
        for (int i = 1; i < k; i++)
        {
            var cand = pool[Rng.Next(PopSize)];
            if (cand.Fitness < best.Fitness) best = cand;
        }
        return best;
    }

    public Individual Solve(TimeSpan budget)
    {
        var sw = Stopwatch.StartNew();
        InitPopulation();
        for (int i = 0; i < PopSize; i++)
            PermRepair.Repair1ToNminus1(Pop[i].Perm, P.N);

        var best = Pop[0];
        for (int i = 1; i < PopSize; i++) if (Pop[i].Fitness < best.Fitness) best = Pop[i];

        int gen = 0;
        while (sw.Elapsed < budget && gen < MaxGen)
        {
            CopyInto(best, Next[0]);

            for (int i = 1; i < PopSize; i++)
            {
                var p1 = Tournament(Pop, TournamentK);
                var p2 = Tournament(Pop, TournamentK);

                Operators.OX(p1.Perm, p2.Perm, Next[i].Perm, P.M, Buf, Seen);

                if (Rng.NextDouble() < PmCur)
                {
                    int which = Rng.Next(3);
                    if (which == 0) Operators.MutateSwap(Next[i].Perm);
                    else if (which == 1) Operators.MutateInsert(Next[i].Perm);
                    else Operators.MutateReverseSmall(Next[i].Perm);
                    PermRepair.Repair1ToNminus1(Next[i].Perm, P.N);
                }

                Next[i].Fitness = SplitDecoder.Evaluate(P, Next[i].Perm, Next[i].Parent, ScratchDp, ScratchLoad, ScratchRun);
            }

            if ((gen % 100) == 99)
            {
                int k = Math.Max(1, PopSize / 10);
                ReplaceWorstWithImmigrants(k);
            }

            for (int i = 0; i < PopSize; i++) { var t = Pop[i]; Pop[i] = Next[i]; Next[i] = t; }

            var cur = Pop[0];
            for (int i = 1; i < PopSize; i++) if (Pop[i].Fitness < cur.Fitness) cur = Pop[i];

            if (cur.Fitness < best.Fitness) { best = cur; noImpr = 0; }
            else noImpr++;

            if (noImpr > 120) PmCur = Math.Min(0.35, PmCur + 0.05);
            else if (noImpr == 0 && PmCur > Pm) PmCur = Math.Max(Pm, PmCur - 0.05);

            gen++;
            if ((gen % 5) == 0)
            {
                if (LocalSearch.ImproveEliteInRoutes(P, best, maxRoutes: 2, maxPasses: 2))
                {
                    best.Fitness = SplitDecoder.Evaluate(P, best.Perm, best.Parent, ScratchDp, ScratchLoad, ScratchRun);
                }
            }
        }
        Console.Error.WriteLine($"[GA] Performed {gen} generations in {sw.ElapsedMilliseconds}ms time");
        return best;
    }

    private static void CopyInto(Individual src, Individual dst)
    {
        Array.Copy(src.Perm, dst.Perm, src.Perm.Length);
        Array.Copy(src.Parent, dst.Parent, src.Parent.Length);
        dst.Fitness = src.Fitness;
    }

    private void ReplaceWorstWithImmigrants(int k)
    {
        var idx = new List<int>(PopSize);
        for (int i = 0; i < PopSize; i++) idx.Add(i);
        idx.Sort((a,b) => Pop[b].Fitness.CompareTo(Pop[a].Fitness));

        for (int t = 0; t < k && t < PopSize; t++)
        {
            int i = idx[t];
            if ((t & 1) == 0) Seeders.Sweep(P.points, P.N, Pop[i].Perm);
            else              Seeders.NearestNeighbor(P, Pop[i].Perm);

            Pop[i].Fitness = SplitDecoder.Evaluate(P, Pop[i].Perm, Pop[i].Parent, ScratchDp, ScratchLoad, ScratchRun);
        }
    }

    public static string BuildOutput(ProblemInstance P, Individual best)
    {
        var segs = SplitDecoder.ReconstructCuts(P.M, best.Parent);
        var sb = new StringBuilder();

        for (int s = 0; s < segs.Count; s++)
        {
            var (L, R) = segs[s];
            for (int i = L; i <= R; i++)
            {
                if (sb.Length > 0 && sb[sb.Length - 1] != ';') sb.Append(' ');
                sb.Append(best.Perm[i]);
            }
            if (s != segs.Count - 1) sb.Append(';');
        }
        return sb.ToString();
    }
}

class Player
{
    static void Main(string[] args)
    {
        int n = int.Parse(Console.ReadLine()!); // The number of customers
        int c = int.Parse(Console.ReadLine()!); // The capacity of the vehicles
        var problemInstance = new ProblemInstance(n, c);
        for (int i = 0; i < n; i++)
        {
            string[] inputs = Console.ReadLine()!.Split(' ');
            int index = int.Parse(inputs[0]); // The index of the customer (0 is the depot)
            int x = int.Parse(inputs[1]); // The x coordinate of the customer
            int y = int.Parse(inputs[2]); // The y coordinate of the customer
            int demand = int.Parse(inputs[3]); // The demand
            problemInstance.points[index] = (x, y);
            problemInstance.Demand[index] = demand;
        }
        problemInstance.CalculateDists();

        var solver = new GeneticEA(problemInstance, 40);
        var best = solver.Solve(TimeSpan.FromMilliseconds(9700.0));
        Console.WriteLine(GeneticEA.BuildOutput(problemInstance, best));

    }
}