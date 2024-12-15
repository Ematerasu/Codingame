using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

/**
 * Kieruj swoimi gargulacami aby złapać więcej prezentów i zdobyć więcej punktów niż drużyna przeciwnika.
 **/


class Gargoyle
{
    public int x;
    public int y;
    public int cooldown;

    public int previousX;
    public int previousY;

    public Gargoyle(int x=0, int y=0, int cooldown=0)
    {
        this.x = x;
        this.y = y;
        this.cooldown = cooldown;
        this.previousX = 0;
        this.previousY = 0;
    }

    public void update(int x, int y, int cooldown)
    {
        this.previousX = this.x;
        this.previousY = this.y;
        this.x = x;
        this.y = y;
        this.cooldown = cooldown;
    }

}
class Player
{
    static int FIREBALL_RANGE = 305;
    static void fly((int x, int y) target, string message="")
    {
        Console.WriteLine($"FLY {target.x} {target.y} {message}");
    }

    static void fireball(int targetId, string message="")
    {
        Console.WriteLine($"FIREBALL {targetId} {message}");
    }

    static void Debug(string message)
    {
        Console.Error.Write(message);
    }

    static void Main(string[] args)
    {
        string[] inputs;
        int gargoylesPerPlayer = int.Parse(Console.ReadLine()); // liczba gargulców w drużynie
        Gargoyle[] myGargoyles = new Gargoyle[gargoylesPerPlayer];
        Gargoyle[] enemyGargoyles = new Gargoyle[gargoylesPerPlayer];
        for (int i = 0; i < gargoylesPerPlayer; i++)
        {
            myGargoyles[i] = new Gargoyle();
            enemyGargoyles[i] = new Gargoyle();
        }
        // game loop
        while (true)
        {
            int missedPresentsToEnd = int.Parse(Console.ReadLine()); // ile prezentów musi jeszcze spaść na ziemię aby gra się zakończyła
            int myScore = int.Parse(Console.ReadLine()); // mój wynik
            for (int i = 0; i < gargoylesPerPlayer; i++)
            {
                inputs = Console.ReadLine().Split(' ');
                int x = int.Parse(inputs[0]); // pozycja na współrzędnej x
                int y = int.Parse(inputs[1]); // pozycja na współrzędnej y
                int cooldown = int.Parse(inputs[2]); // liczba tur aż będzie dostępna kula ognia, 0 oznacza możliwość rzucenia czaru
                myGargoyles[i].update(x, y, cooldown);
            }
            int foeScore = int.Parse(Console.ReadLine()); // wynik oponenta
            for (int i = 0; i < gargoylesPerPlayer; i++)
            {
                inputs = Console.ReadLine().Split(' ');
                int x = int.Parse(inputs[0]);
                int y = int.Parse(inputs[1]);
                int cooldown = int.Parse(inputs[2]);
                enemyGargoyles[i].update(x, y, cooldown);
            }
            int entityCount = int.Parse(Console.ReadLine()); // liczba spadających obiektów
            var presents = new List<(int id, int value, int x, int y, int vy)>(entityCount);
            for (int i = 0; i < entityCount; i++)
            {
                inputs = Console.ReadLine().Split(' ');
                int id = int.Parse(inputs[0]); // unikalny identyfikator spadającego obiektu
                int value = int.Parse(inputs[1]); // wartość prezentu
                int x = int.Parse(inputs[2]);
                int y = int.Parse(inputs[3]);
                int vy = Math.Abs(int.Parse(inputs[4])); // szybkość spadania obiektu w pionie
                presents.Add((id, value, x, y, vy));
            }
            (int id, int value, int x, int y, int vy) defaultPresent = (-1, 0, 0, 0, 0);
            var centerPresentsCnt = presents.Where(p => 200 < p.x && p.x < 1720).ToList().Count;
            var targets = new HashSet<int>();
            (int a, int b)[] intervals = {(0, 640), (640, 1280), (1280, 1920)};
            for (int i = 0; i < gargoylesPerPlayer; i++)
            {
                var gargoyle = myGargoyles[i];
                var enemyGargoyle = enemyGargoyles[i];

                if (gargoyle.cooldown == 0)
                {
                    var presentToShoot = presents.Where(p => !targets.Contains(p.id)).Where(p => {
                        double distance = euclideanDistance((gargoyle.x, gargoyle.y), (p.x, p.y));
                        int enemyPredictedY = CalculateOptimalY((enemyGargoyle.x, enemyGargoyle.y), 150, (p.x, p.y), p.vy);
                        var opponentFaster = IsOpponentTargetingPresent(
                            ((double)enemyGargoyle.previousX, (double)enemyGargoyle.previousY),
                            ((double)enemyGargoyle.x, (double)enemyGargoyle.y),
                            ((double)p.x, (double)enemyPredictedY),
                            ((double)gargoyle.x, (double)gargoyle.y),
                            gargoyle.cooldown == 0
                        );
                        int predictedY = CalculateOptimalY((gargoyle.x, gargoyle.y), 150, (p.x, p.y), p.vy);
                        if (!(distance < FIREBALL_RANGE && (opponentFaster || predictedY <= 50)))
                        {
                            Debug($"[{i}] Can't shoot {p.id} {distance} {opponentFaster} {predictedY <= 50}\n");
                        }
                        return distance < FIREBALL_RANGE && (opponentFaster || predictedY <= 50);
                    }).OrderByDescending(p => p.value)
                    .DefaultIfEmpty(defaultPresent)
                    .First();

                    if (presentToShoot.id != -1)
                    {
                        Debug($"[{i}] Throw fireball at {presentToShoot.id}");
                        fireball(presentToShoot.id);
                        continue;
                    }
                }

                var bestPresent = presents
                    .Where(p => (intervals[i].a < p.x && p.x < intervals[i].b))
                    .Where(p => !targets.Contains(p.id))
                    .Where(p => {
                        double distance = euclideanDistance((gargoyle.x, gargoyle.y), (p.x, p.y));
                        double timeToReach = distance / 150.0;

                        int predictedY = CalculateOptimalY((gargoyle.x, gargoyle.y), 150, (p.x, p.y), p.vy);
                        if (predictedY <= 50) {
                            Debug($"[{i}] {p.id} will be too low: {predictedY}\n");
                            return false;
                        }
                        int enemyPredictedY = CalculateOptimalY((enemyGargoyle.x, enemyGargoyle.y), 150, (p.x, p.y), p.vy);
                        var opponentFaster = IsOpponentTargetingPresent(
                            ((double)enemyGargoyle.previousX, (double)enemyGargoyle.previousY),
                            ((double)enemyGargoyle.x, (double)enemyGargoyle.y),
                            ((double)p.x, (double)enemyPredictedY),
                            ((double)gargoyle.x, (double)gargoyle.y),
                            gargoyle.cooldown == 0
                        );
                        if (opponentFaster) Debug($"[{i}] {p.id} opponent will be faster\n");
                        return !opponentFaster;
                    })
                    .OrderByDescending(p =>
                    {
                        int predictedY = CalculateOptimalY((gargoyle.x, gargoyle.y), 150, (p.x, p.y), p.vy);
                        return presentValue(p.value, euclideanDistance((gargoyle.x, gargoyle.y), (p.x, predictedY)), (p.x, p.y), p.id, i);
                    })
                    .ThenBy(p => p.y)
                    .DefaultIfEmpty(defaultPresent)
                    .First();
                if (bestPresent.id != -1)
                {
                    targets.Add(bestPresent.id);
                    int predictedX = bestPresent.x;
                    int predictedY = CalculateOptimalY((gargoyle.x, gargoyle.y), 150, (bestPresent.x, bestPresent.y), bestPresent.vy);

                    fly((predictedX, predictedY), $"{bestPresent.id} {predictedY}");
                }
                else
                {
                    fly(((intervals[i].a + intervals[i].b)/2, 500), "[{i}] Patrolling");
                }
            }
        }
    }
    static double euclideanDistance((int x, int y) a, (int x, int y) b)
    {
        return Math.Sqrt(Math.Pow(a.x - b.x, 2) + Math.Pow(a.y - b.y, 2));
    }

    static bool IsOpponentTargetingPresent(
        (double x, double y) prevPosition,
        (double x, double y) currentPosition,
        (double x, double y) presentPosition,
        (double x, double y) myGargoylePosition,
        bool canThrowFireball
    )
    {
        double opponentDx = currentPosition.x - prevPosition.x;
        double opponentDy = currentPosition.y - prevPosition.y;

        double toPresentDx = presentPosition.x - currentPosition.x;
        double toPresentDy = presentPosition.y - currentPosition.y;

        double opponentMagnitude = Math.Sqrt(opponentDx * opponentDx + opponentDy * opponentDy);
        double presentMagnitude = Math.Sqrt(toPresentDx * toPresentDx + toPresentDy * toPresentDy);

        if (opponentMagnitude == 0 || presentMagnitude == 0) return false;

        opponentDx /= opponentMagnitude;
        opponentDy /= opponentMagnitude;

        toPresentDx /= presentMagnitude;
        toPresentDy /= presentMagnitude;

        double dotProduct = opponentDx * toPresentDx + opponentDy * toPresentDy;
        double angle = Math.Acos(dotProduct); // W radianach

        if (angle > Math.PI / 6) return false;

        double opponentDistance = Math.Sqrt(
            Math.Pow(presentPosition.x - currentPosition.x, 2) +
            Math.Pow(presentPosition.y - currentPosition.y, 2)
        );

        double myDistance = Math.Sqrt(
            Math.Pow(presentPosition.x - myGargoylePosition.x, 2) +
            Math.Pow(presentPosition.y - myGargoylePosition.y, 2)
        );
        var myDistanceToleranced = myDistance - 60.0;
        if (canThrowFireball) {
            myDistanceToleranced += 60.0;
        }
        return opponentDistance < myDistanceToleranced;
    }

    static double presentValue(int value, double distance, (int x, int y) presentPos, int presentId, int gargoyleId)
    {
        double alpha = 1.2;
        double beta = 1.2;
        double valueFactor = 5;
        int centerX = 1920 / 2;
        double distanceFromCenterX = Math.Abs(presentPos.x - centerX);
        var giftValue =  Math.Pow(value*5, valueFactor)*10000 / (Math.Pow(distance+1, alpha) * Math.Pow(distanceFromCenterX + 1, beta));
        Debug($"[{gargoyleId}] {presentId}: {giftValue} (distanceFactor: {Math.Pow(distance, alpha)}, centerFactor: {Math.Pow(distanceFromCenterX + 1, beta)})\n");
        return giftValue;
    }

    static int CalculateOptimalY(
        (int x, int y) playerPosition, int playerVelocity,
        (int x, int y) objectPosition, int objectVelocityY)
    {
        int time = 1;

        while (true)
        {
            int currentY = objectPosition.y - objectVelocityY * time;

            if (currentY < 0)
            {
                return -1;
            }

            double distance = euclideanDistance(playerPosition, (objectPosition.x, currentY)) - 30.0;

            if (distance <= playerVelocity * time)
            {
                return currentY;
            }

            time++;
        }
    }
}