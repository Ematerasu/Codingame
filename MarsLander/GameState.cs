using System;
using System.Collections.Generic;


public class GameState
{
    private const double Gravity = 3.711;
    private const int MaxRotationChange = 15;
    private const int MaxThrustChange = 1;

    public double X { get; private set; }
    public double Y { get; private set; }
    public double HorizontalSpeed { get; private set; }
    public double VerticalSpeed { get; private set; }
    public int Fuel { get; private set; }
    public int Rotation { get; private set; }
    public int ThrustPower { get; private set; }
    

    public bool Crashed { get; private set; }
    public bool Success { get; private set; }

    public GameState(
        double x, double y, double horizontalSpeed, double verticalSpeed,
        int fuel, int rotation, int thrustPower,
        bool crashed = false, bool success = false)
    {
        X = x;
        Y = y;
        HorizontalSpeed = horizontalSpeed;
        VerticalSpeed = verticalSpeed;
        Fuel = fuel;
        Rotation = rotation;
        ThrustPower = thrustPower;
        Crashed = crashed;
        Success = success;
    }

    public GameState ForwardModel(int desiredRotation, int desiredThrustPower)
    {
        int newRotation = Constrain(Rotation + Clamp(desiredRotation - Rotation, -MaxRotationChange, MaxRotationChange), -90, 90);
        int newThrustPower = Constrain(ThrustPower + Clamp(desiredThrustPower - ThrustPower, -MaxThrustChange, MaxThrustChange), 0, 4);
        newThrustPower = Math.Min(Fuel, newThrustPower);

        double angleRad = newRotation * Math.PI / 180.0;
        double thrustX = Math.Sin(angleRad) * newThrustPower;
        double thrustY = Math.Cos(angleRad) * newThrustPower;

        double accelerationX = -thrustX;
        double accelerationY = thrustY - Gravity;

        double newHorizontalSpeed = HorizontalSpeed + accelerationX; 
        double newVerticalSpeed = VerticalSpeed + accelerationY;

        double newX = X + HorizontalSpeed + 0.5 * accelerationX;
        double newY = Y + VerticalSpeed + 0.5 * accelerationY;

        int newFuel = Math.Max(0, Fuel - newThrustPower);

        bool isCrashed = false;
        bool isSuccessful = false;

        foreach (var segment in Globals.SurfaceSegments)
        {
            if (IntersectsLine((X, Y), (newX, newY), segment))
            {
                if (segment.y1 == segment.y2)
                {
                    bool validPosition = newRotation == 0;
                    bool validVerticalSpeed = Math.Abs(newVerticalSpeed) <= 40;
                    bool validHorizontalSpeed = Math.Abs(newHorizontalSpeed) <= 20;

                    if (validPosition && validVerticalSpeed && validHorizontalSpeed)
                    {
                        isSuccessful = true;
                    }
                    else
                    {
                        isCrashed = true;
                    }
                }
                else
                {
                    isCrashed = true;
                }
                break;
            }
        }

        return new GameState(newX, newY, newHorizontalSpeed, newVerticalSpeed, newFuel, newRotation, newThrustPower, isCrashed, isSuccessful);
    }

    public GameState ForwardModel((int Rotation, int ThrustPower)[] moves)
    {
        double currentX = X;
        double currentY = Y;
        double currentHorizontalSpeed = HorizontalSpeed;
        double currentVerticalSpeed = VerticalSpeed;
        int currentFuel = Fuel;
        int currentRotation = Rotation;
        int currentThrustPower = ThrustPower;

        bool isCrashed = false;
        bool isSuccessful = false;

        foreach (var move in moves)
        {
            if (isCrashed || isSuccessful) break;

            int desiredRotation = move.Rotation;
            int desiredThrustPower = move.ThrustPower;

            int newRotation = Constrain(
                currentRotation + Clamp(desiredRotation - currentRotation, -MaxRotationChange, MaxRotationChange), 
                -90, 
                90
            );

            int newThrustPower = Constrain(
                currentThrustPower + Clamp(desiredThrustPower - currentThrustPower, -MaxThrustChange, MaxThrustChange), 
                0, 
                4
            );
            newThrustPower = Math.Min(currentFuel, newThrustPower);

            double angleRad = newRotation * Math.PI / 180.0;
            double thrustX = Math.Sin(angleRad) * newThrustPower;
            double thrustY = Math.Cos(angleRad) * newThrustPower;

            double accelerationX = -thrustX;
            double accelerationY = thrustY - Gravity;

            double newHorizontalSpeed = currentHorizontalSpeed + accelerationX; 
            double newVerticalSpeed = currentVerticalSpeed + accelerationY;

            double newX = currentX + currentHorizontalSpeed + 0.5 * accelerationX;
            double newY = currentY + currentVerticalSpeed + 0.5 * accelerationY;

            int newFuel = Math.Max(0, currentFuel - newThrustPower);

            foreach (var segment in Globals.SurfaceSegments)
            {
                if (IntersectsLine((currentX, currentY), (newX, newY), segment))
                {
                    if (segment.y1 == segment.y2) // Flat surface
                    {
                        bool validPosition = newRotation == 0;
                        bool validVerticalSpeed = Math.Abs(newVerticalSpeed) <= 40;
                        bool validHorizontalSpeed = Math.Abs(newHorizontalSpeed) <= 20;

                        if (validPosition && validVerticalSpeed && validHorizontalSpeed)
                        {
                            isSuccessful = true;
                        }
                        else
                        {
                            isCrashed = true;
                        }
                    }
                    else
                    {
                        isCrashed = true;
                    }
                    break;
                }
            }

            // Update state variables for the next iteration
            currentX = newX;
            currentY = newY;
            currentHorizontalSpeed = newHorizontalSpeed;
            currentVerticalSpeed = newVerticalSpeed;
            currentFuel = newFuel;
            currentRotation = newRotation;
            currentThrustPower = newThrustPower;
        }

        return new GameState(currentX, currentY, currentHorizontalSpeed, currentVerticalSpeed, currentFuel, currentRotation, currentThrustPower, isCrashed, isSuccessful);
    }

    private static bool IntersectsLine((double x, double y) start, (double x, double y) end, (int x1, int y1, int x2, int y2) segment)
    {
        if (end.y > segment.y1 && end.y > segment.y2) return false;
        return LineSegmentsIntersect(start.x, start.y, end.x, end.y, segment.x1, segment.y1, segment.x2, segment.y2);
    }

    private static bool LineSegmentsIntersect(
        double x1, double y1, double x2, double y2,
        double x3, double y3, double x4, double y4)
    {
        double denominator = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);

        if (Math.Abs(denominator) < 1e-9) return false; // Równoległe

        double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denominator;
        double u = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / denominator;

        return t >= 0 && t <= 1 && u >= 0 && u <= 1;
    }

    public List<(int Rotation, int ThrustPower)> AvailableMoves()
    {
        var moves = new List<(int Rotation, int ThrustPower)>();

        for (int dRotation = -MaxRotationChange; dRotation <= MaxRotationChange; dRotation += 15)
        {
            int newRotation = Constrain(Rotation + dRotation, -90, 90);

            for (int dThrust = -MaxThrustChange; dThrust <= MaxThrustChange; dThrust++)
            {
                int newThrustPower = Constrain(ThrustPower + dThrust, 0, 4);

                if (Fuel >= newThrustPower) // Ensure move is valid given remaining fuel
                {
                    moves.Add((newRotation, newThrustPower));
                }
            }
        }

        return moves;
    }

    private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
    private static int Constrain(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

    public override string ToString()
    {
        return $"X: {X}, Y: {Y}, HS: {HorizontalSpeed}, VS: {VerticalSpeed}, Fuel: {Fuel}, Rotation: {Rotation}, Thrust: {ThrustPower}\nCrashed: {Crashed}, Success: {Success}";
    }
}
