using SummerChallenge2025.Engine;

public class SimulationParams
{
    public int MaxTurns { get; set; } = 100;
    public int Seed { get; set; }
    public IVisualizer? Visualizer { get; set; } = null;
    public int Games { get; set; } = 1;
}