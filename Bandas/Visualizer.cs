using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace bandas;
public class GameVisualizer
{
    public static void SaveBoardToFile(Board board, string filePath)
    {
        using (StreamWriter writer = new StreamWriter(filePath))
        {
            for (int i = 0; i < board.Height; i++)
            {
                for (int j = 0; j < board.Width; j++)
                {
                    writer.Write(board._board[i, j]);
                }
                writer.WriteLine();
            }
        }
    }

    public static string SerializeTreeToJson(FullMCTS.MCTSTree tree)
    {
        var nodes = tree.tree
            .Take((int)tree.size)
            .Select((node, index) => new
            { 
                Id = index,
                Parent = node.Parent,
                Wins = node.Wins,
                Visits = node.Visits,
                PlayerId = node.PlayerId,
                Children = node.ChildrenId.Where(id => id != 0).ToArray()
            });

        return JsonSerializer.Serialize(nodes, new JsonSerializerOptions { WriteIndented = true });
    }

    public static void GenerateImage(string pythonPath, string scriptPath, string boardFile, string outputImage)
    {
        ProcessStartInfo start = new ProcessStartInfo
        {
            FileName = pythonPath,
            Arguments = $"{scriptPath} {boardFile} {outputImage}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using (Process process = Process.Start(start))
        {
            // using (StreamReader reader = process.StandardOutput)
            // {
            //     string output = reader.ReadToEnd();
            //     Console.WriteLine($"Output: {output}");
            // }

            // using (StreamReader errorReader = process.StandardError)
            // {
            //     string errors = errorReader.ReadToEnd();
            //     Console.WriteLine($"Errors: {errors}");
            // }
            process.WaitForExit();
        }
    }

    public static void VisualizeBoard(Board board, string boardFile="Board", string outputDir="imgs")
    {
        string pythonPath = "python";
        string scriptPath = "visualizer.py";

        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }
        string tempTxtFile = $"{boardFile}.txt";
        string outputImage = $"{outputDir}/{boardFile}.png";
        SaveBoardToFile(board, tempTxtFile);
        GenerateImage(pythonPath, scriptPath, tempTxtFile, outputImage);

        if (File.Exists(tempTxtFile))
        {
            File.Delete(tempTxtFile);
        }
    }

    public static void VisualizeMCTSTree(FullMCTS.MCTSTree currentTree)
    {
        string fileName = "mcts_tree.json";
        File.WriteAllText(fileName, SerializeTreeToJson(currentTree));
        ProcessStartInfo start = new ProcessStartInfo
        {
            FileName = "python3",
            Arguments = $"mcts_visualizer.py",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using (Process process = Process.Start(start))
        {
            using (StreamReader reader = process.StandardOutput)
            {
                string output = reader.ReadToEnd();
                Console.WriteLine($"Output: {output}");
            }

            using (StreamReader errorReader = process.StandardError)
            {
                string errors = errorReader.ReadToEnd();
                Console.WriteLine($"Errors: {errors}");
            }
            process.WaitForExit();
        }

        if (File.Exists(fileName))
        {
            //File.Delete(fileName);
        }
    }

    public static void ClearFolder(string folderPath)
    {
        if (Directory.Exists(folderPath))
        {
            try
            {
                string[] files = Directory.GetFiles(folderPath);

                foreach (string file in files)
                {
                    File.Delete(file);
                    //Console.WriteLine($"Deleted: {file}");
                }

                //Console.WriteLine($"All files in '{folderPath}' have been deleted.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while clearing the folder: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine($"The folder '{folderPath}' does not exist.");
        }
    }
}
