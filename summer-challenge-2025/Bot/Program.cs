using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

namespace SummerChallenge2025.Bot;

#if CODINGAME
class Player
{
    static void Main(string[] args)
    {

        BotSetup.Apply();
        var bot  = new Esdeath();
        bool init = false;   
        while (true)
        {
            var state = GameStateReader.ReadFromInput(Console.In);
            if (!init)
            {
                bot.Initialize(state.MyId);
                init = true;
            }
            TurnCommand cmd = bot.GetMove(state);
            foreach(var line in cmd.ToLines())
            {
                Console.WriteLine(line);
            }
        }
    }
}
#else
public class Program
{
    public static void Main()
    {
        Console.WriteLine("[Bot] Local entrypoint");
    }
}
#endif