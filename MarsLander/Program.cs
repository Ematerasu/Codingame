
using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
public static class Globals
{
    public static (int x1, int y1, int x2, int y2) LandingSegment = (0, 0, 0, 0);
    public static (int x1, int y1, int x2, int y2)[] SurfaceSegments = [(0, 0, 0, 0)];
    public static (int x, int y) middleOfLandingSpot = (0, 0);
}

class Player
{
    public const long NOGC_SIZE = 67_108_864;
    private const bool DEBUG_MODE = true;
    static void Main(string[] args)
    {
        //GC.TryStartNoGCRegion(NOGC_SIZE);
        string[] inputs;
        RHEA rhea = new RHEA(40, 20, 20, 2000);
        TextReader inputReader = DEBUG_MODE ? new StreamReader("input.txt") : Console.In;
        int N = int.Parse(inputReader.ReadLine()); // the number of points used to draw the surface of Mars.
        var surfaceSegments = new (int x1, int y1, int x2, int y2)[N];
        int prevX = 0, prevY = 0;
        for (int i = 0; i < N; i++)
        {
            inputs = inputReader.ReadLine().Split(' ');
            int landX = int.Parse(inputs[0]); // X coordinate of a surface point. (0 to 6999)
            int landY = int.Parse(inputs[1]); // Y coordinate of a surface point. By linking all the points together in a sequential fashion, you form the surface of Mars.
            if (i > 0)
            {
                surfaceSegments[i] = (prevX, prevY, landX, landY);
                Console.Error.WriteLine($"Segment: {(prevX, prevY, landX, landY)}");
            }

            prevX = landX;
            prevY = landY;
        }

        foreach (var segment in surfaceSegments)
        {
            int length = Math.Abs(segment.x2 - segment.x1);
            bool isFlat = segment.y1 == segment.y2;

            if (isFlat && length >= 1000)
            {
                Globals.LandingSegment = segment;
            }
        }
        Globals.SurfaceSegments = surfaceSegments;
        Globals.middleOfLandingSpot = ((Globals.LandingSegment.x2 + Globals.LandingSegment.x1)/2, Globals.LandingSegment.y1);
        // Read it once
        inputs = inputReader.ReadLine().Split(' ');
        double X = int.Parse(inputs[0]);
        double Y = int.Parse(inputs[1]);
        double hSpeed = int.Parse(inputs[2]); // the horizontal speed (in m/s), can be negative.
        double vSpeed = int.Parse(inputs[3]); // the vertical speed (in m/s), can be negative.
        int fuel = int.Parse(inputs[4]); // the quantity of remaining fuel in liters.
        int rotation = int.Parse(inputs[5]); // the rotation angle in degrees (-90 to 90).
        int thrustPower = int.Parse(inputs[6]); // the thrust power (0 to 4).\
        var gameState = new GameState(
            X, Y, hSpeed, vSpeed, fuel, rotation, thrustPower
        );
        Console.Error.WriteLine(gameState);
        (rotation, thrustPower) = rhea.FindBestMove(gameState);
        Console.WriteLine($"{rotation} {thrustPower}");
        while (true)
        {
            var _ = inputReader.ReadLine();
            gameState = gameState.ForwardModel(rotation, thrustPower);
            (rotation, thrustPower) = rhea.FindBestMove(gameState);
            Console.Error.WriteLine(gameState);
            //Console.Error.WriteLine(gameState.ForwardModel(rotation, thrustPower));
            Console.WriteLine($"{rotation} {thrustPower}");
            if (DEBUG_MODE)
            {
                if (gameState.Crashed || gameState.Success) break;
            }
        }
        Console.Error.WriteLine($"{((Globals.LandingSegment.x2 + Globals.LandingSegment.x1)/2, Globals.LandingSegment.y1)}");
    }
}
