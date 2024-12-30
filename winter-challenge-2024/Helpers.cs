using System.Diagnostics;
using System.Text;

namespace winter_challenge_2024;

class Helpers
{
    public static void VisualizeDebugState(NewGameState gameState, string title, string dir="imgs", string additionalMessage="")
    {
        string tempFilePath = "tmp_visualization.txt";
        
        string visualizationData = GenerateVisualizationData(gameState, title, additionalMessage);
        //Console.WriteLine(visualizationData);
        File.WriteAllText(tempFilePath, visualizationData);

        string visualizerPath = "visualizer_wc24.py";

        Process process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"{visualizerPath} -f {tempFilePath} -d {dir}",
                UseShellExecute = false,
                RedirectStandardError = false,
                RedirectStandardOutput = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        process.WaitForExit();

        File.Delete(tempFilePath);
    }

    private static string GenerateVisualizationData(NewGameState gameState, string title, string additionalMessage)
    {
        StringBuilder builder = new StringBuilder();

        // Dodaj ramkę
        builder.AppendLine($"FRAME {title}");

        // Dodaj komendę INIT
        builder.AppendLine($"INIT {gameState.Width} {gameState.Height}");

        // Dodaj koordynaty
        builder.AppendLine("COORDS H");

        // Dodaj stan planszy (GRID)
        for (int y = 0; y < gameState.Height; y++)
        {
            for (int x = 0; x < gameState.Width; x++)
            {
                var entity = gameState.Board[x, y];
                if (entity.IsEmpty)
                {
                    builder.AppendLine($"CELL {x} {y} E");
                }
                else if (entity.IsWall)
                {
                    builder.AppendLine($"CELL {x} {y} W");
                }
                else if (entity.IsResource && entity.OrganOrResourceType == 0b00)
                {
                    builder.AppendLine($"CELL {x} {y} A");
                }
                else if (entity.IsResource && entity.OrganOrResourceType == 0b01)
                {
                    builder.AppendLine($"CELL {x} {y} B");
                }
                else if (entity.IsResource && entity.OrganOrResourceType == 0b10)
                {
                    builder.AppendLine($"CELL {x} {y} C");
                }
                else if (entity.IsResource && entity.OrganOrResourceType == 0b11)
                {
                    builder.AppendLine($"CELL {x} {y} D");
                }
                else
                {
                    // Struktura: RBSHT,01,NSWE,NSWE
                    string type = "";
                    if (entity.OrganOrResourceType == 0b00 && entity.Rotation == 0b00) type = "R";
                    else if (entity.OrganOrResourceType == 0b00 && entity.Rotation == 0b11) type = "B";
                    else if (entity.OrganOrResourceType == 0b01) type = "H";
                    else if (entity.OrganOrResourceType == 0b10) type = "T";
                    else if (entity.OrganOrResourceType == 0b11) type = "S";
                    string dir = "";
                    if (entity.OrganOrResourceType == 0b00 && entity.Rotation == 0b00) dir = "N";
                    else if (entity.OrganOrResourceType == 0b00 && entity.Rotation == 0b11) dir = "N";
                    else if (entity.Rotation == 0b00) dir = "N";
                    else if (entity.Rotation == 0b01) dir = "E";
                    else if (entity.Rotation == 0b10) dir = "S";
                    else if (entity.Rotation == 0b11) dir = "W";
                    
                    var parentPos = gameState.IdToPosition[entity.ParentId];
                    string dirFrom = Utils.DirToString(Utils.vectorToDir((parentPos.x-x, parentPos.y-y)));
                    string code = $"{type}{entity.Owner}{dir}{dirFrom}";
                    builder.AppendLine($"CELL {x} {y} {code}");
                }
            }
        }

        // Dodaj zasoby graczy
        builder.AppendLine($"RES {gameState.OpponentProteins[0]} {gameState.OpponentProteins[1]} {gameState.OpponentProteins[2]} {gameState.OpponentProteins[3]} " +
                           $"{gameState.PlayerProteins[0]} {gameState.PlayerProteins[1]} {gameState.PlayerProteins[2]} {gameState.PlayerProteins[3]}");

        // Dodaj rozmiary (punkty + korzenie)
        // builder.AppendLine($"SIZE {gameState.Player0Entities.Count} {gameState.Player0Entities.Where(kvp => kvp.Value.Type == CellType.ROOT).ToList().Count} " +
        //                    $"{gameState.Player1Entities.Count} {gameState.Player1Entities.Where(kvp => kvp.Value.Type == CellType.ROOT).ToList().Count}");

        // Dodaj tekst z numerem tury
        builder.AppendLine($"TEXTL Turn: {gameState.Turn}");
        builder.AppendLine($"TEXTR {additionalMessage}");

        // Dodaj koniec ramki
        builder.AppendLine("END");

        return builder.ToString();
    }
}
