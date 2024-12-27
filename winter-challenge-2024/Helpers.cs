using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Linq;
using System.Text;
using System.IO;

namespace winter_challenge_2024;

class Helpers
{
    public static void PrintDebugState(GameState gameState)
    {
        Console.WriteLine($"Turn: {gameState.Turn}\n");
        for (int y = 0; y < gameState.Height; y++)
        {
            for (int x = 0; x < gameState.Width; x++)
            {
                var cell = gameState.Grid[x, y];
                string cellSign = cell.Type switch {
                    CellType.WALL => "W",
                    CellType.ROOT => "R",
                    CellType.BASIC => "B",
                    CellType.SPORER => "S",
                    CellType.TENTACLE => "T",
                    CellType.HARVESTER => "H",
                    CellType.PROTEIN_A => "A",
                    CellType.PROTEIN_B => "B",
                    CellType.PROTEIN_C => "C",
                    CellType.PROTEIN_D => "D",
                    _ => "."

                };
                Console.Write(cellSign);
            }
            Console.WriteLine();
        }

        Console.WriteLine($"Player 0 proteins: {gameState.Player0Proteins}\n");
        Console.WriteLine($"Player 1 proteins: {gameState.Player1Proteins}\n");

        Console.WriteLine("Player 0 Entities:");
        foreach (var entity in gameState.Player0Entities.Values)
        {
            Console.WriteLine($"Entity ID: {entity.Id}, Type: {entity.Type}, Position: {entity.Position}");
        }

        Console.WriteLine("Player 1 Entities:");
        foreach (var entity in gameState.Player1Entities.Values)
        {
            Console.WriteLine($"Entity ID: {entity.Id}, Type: {entity.Type}, Position: {entity.Position}");
        }

        Console.WriteLine("Player 0 possible Actions:");
        foreach (var rootActions in gameState.GetPossibleActions(0))
        {
            foreach(var action in rootActions)
            {
                Console.WriteLine(action.ToString());
            }
                
        }

        Console.WriteLine("Player 1 possible Actions:");
        foreach (var rootActions in gameState.GetPossibleActions(1))
        {
            foreach(var action in rootActions)
            {
                Console.WriteLine(action.ToString());
            }
                
        }
    }

    public static void VisualizeDebugState(GameState gameState, string title, string additionalMessage="")
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
                FileName = "python3",
                Arguments = $"{visualizerPath} -f {tempFilePath}",
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

    private static string GenerateVisualizationData(GameState gameState, string title, string additionalMessage)
    {
        StringBuilder builder = new StringBuilder();

        // Dodaj ramkę
        builder.AppendLine($"FRAME {title}");

        // Dodaj komendę INIT
        builder.AppendLine($"INIT {gameState.Width+2} {gameState.Height+2}");

        // Dodaj koordynaty
        builder.AppendLine("COORDS H");

        // Dodaj stan planszy (GRID)
        for (int y = 0; y < gameState.Height; y++)
        {
            for (int x = 0; x < gameState.Width; x++)
            {
                Entity entity = gameState.Grid[x, y];
                if (entity.Type == CellType.EMPTY)
                {
                    builder.AppendLine($"CELL {x} {y} E");
                }
                else if (entity.Type == CellType.WALL)
                {
                    builder.AppendLine($"CELL {x} {y} W");
                }
                else if (entity.Type == CellType.PROTEIN_A)
                {
                    builder.AppendLine($"CELL {x} {y} A");
                }
                else if (entity.Type == CellType.PROTEIN_B)
                {
                    builder.AppendLine($"CELL {x} {y} B");
                }
                else if (entity.Type == CellType.PROTEIN_C)
                {
                    builder.AppendLine($"CELL {x} {y} C");
                }
                else if (entity.Type == CellType.PROTEIN_D)
                {
                    builder.AppendLine($"CELL {x} {y} D");
                }
                else
                {
                    // Struktura: RBSHT,01,NSWE,NSWE
                    var sourceDir = entity.Dir == Direction.N ? 
                                    Direction.S : entity.Dir == Direction.E ? 
                                    Direction.W : entity.Dir == Direction.S ?
                                    Direction.N : Direction.E;
                    var dir = entity.Dir != Direction.X ? entity.Dir : Direction.N;
                    string code = $"{entity.Type.ToString()[0]}{entity.OwnerId}{dir}{sourceDir}";
                    builder.AppendLine($"CELL {x} {y} {code}");
                }
            }
        }

        // Dodaj zasoby graczy
        builder.AppendLine($"RES {gameState.Player0Proteins.A} {gameState.Player0Proteins.B} {gameState.Player0Proteins.C} {gameState.Player0Proteins.D} " +
                           $"{gameState.Player1Proteins.A} {gameState.Player1Proteins.B} {gameState.Player1Proteins.C} {gameState.Player1Proteins.D}");

        // Dodaj rozmiary (punkty + korzenie)
        builder.AppendLine($"SIZE {gameState.Player0Entities.Count} {gameState.Player0Entities.Where(kvp => kvp.Value.Type == CellType.ROOT).ToList().Count} " +
                           $"{gameState.Player1Entities.Count} {gameState.Player1Entities.Where(kvp => kvp.Value.Type == CellType.ROOT).ToList().Count}");

        // Dodaj tekst z numerem tury
        builder.AppendLine($"TEXTL Turn: {gameState.Turn}");
        builder.AppendLine($"TEXTR {additionalMessage}");

        // Dodaj koniec ramki
        builder.AppendLine("END");

        return builder.ToString();
    }
}
