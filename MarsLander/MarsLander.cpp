#include <iostream>
#include <algorithm>
#include <cmath>
#include <cstdlib>
#include <ctime>
#include <iomanip>
#include <chrono>

using namespace std;
using namespace std::chrono;

# define M_PI 3.14159265358979323846
constexpr int CHROMOSOME_SIZE = 100;
constexpr int POPULATION_SIZE = 50;
constexpr double MUTATION_RATE = 0.02;
constexpr double GRAVITY = 3.711;
constexpr int MAX_SURFACE_POINTS = 30;
constexpr int ELITS = 10;
constexpr int TOURNAMENT_SIZE = 5;

double landingStartX, landingEndX, landingY;

class Gene {
public:
    int rotate;
    int power;

    Gene() : rotate(0), power(0) {}
    Gene(int r, int p) : rotate(r), power(p) {}
};

struct GameState {
    double x, y;          // Position
    double hSpeed, vSpeed; // Horizontal and vertical speed
    int fuel;             // Remaining fuel
    int rotate;           // Current rotation angle
    int power;            // Current thrust power
};

class Chromosome {
public:
    Gene genes[CHROMOSOME_SIZE];
    double fitness;

    Chromosome() : fitness(0) {}

    void initialize() {
        for (int i = 0; i < CHROMOSOME_SIZE; ++i) {
            genes[i] = Gene(rand() % 31 - 15, rand() % 3 - 1);
        }
    }

    void mutate() {
        for (int i = 0; i < CHROMOSOME_SIZE; ++i) {
            if ((double)rand() / RAND_MAX < MUTATION_RATE) {
                genes[i].rotate = (rand() % 31 - 15);
            }
            if ((double)rand() / RAND_MAX < MUTATION_RATE) {
                genes[i].power = (rand() % 3 - 1);
            }
        }
    }

    double calculateFitness(GameState state, const pair<int, int> surface[MAX_SURFACE_POINTS], int surfaceN) {
        for (int i = 1; i < surfaceN; ++i) {
            if (state.x >= surface[i - 1].first && state.x <= surface[i].first) {
                double surfaceY = surface[i - 1].second + (surface[i].second - surface[i - 1].second) * (state.x - surface[i - 1].first) / (surface[i].first - surface[i - 1].first);

                if (state.y <= surfaceY) {
                    if (i > 0 && surface[i].second == surface[i - 1].second && 
                        state.x >= landingStartX && state.x <= landingEndX &&
                        state.rotate == 0 && abs(state.hSpeed) <= 20 && abs(state.vSpeed) <= 40) {
                        return 10000 - state.fuel;
                    } else {
                        return -10000;
                    }
                }
                break;
            }
        }

        double distance = abs(state.x - (landingStartX + landingEndX) / 2);
        return -distance;
    }
};

double simulate(Chromosome& chromosome, GameState state, const pair<int, int> surface[MAX_SURFACE_POINTS], int surfaceN) {
    for (int i = 0; i < CHROMOSOME_SIZE; ++i) {
        int newRotate = max(-90, min(90, state.rotate + chromosome.genes[i].rotate));
        int newPower = max(0, min(4, state.power + chromosome.genes[i].power));

        double rad = newRotate * M_PI / 180.0;
        double hAcc = -newPower * sin(rad);
        double vAcc = newPower * cos(rad) - GRAVITY;

        state.x += state.hSpeed + 0.5 * hAcc;
        state.y += state.vSpeed + 0.5 * vAcc;
        state.hSpeed += hAcc;
        state.vSpeed += vAcc;

        state.fuel -= newPower;

        if (state.y <= 0) {
            break;
        }
    }
    return chromosome.calculateFitness(state, surface, surfaceN);
}

class GeneticPopulation {
public:
    Chromosome population[POPULATION_SIZE];
    Chromosome newPopulation[POPULATION_SIZE];

    GeneticPopulation() {
        for (int i = 0; i < POPULATION_SIZE; ++i) {
            population[i].initialize();
        }
    }

    Chromosome tournamentSelection() {
        Chromosome best = population[rand() % POPULATION_SIZE]; // Initialize with a random chromosome

        for (int i = 1; i < TOURNAMENT_SIZE; ++i) {
            int index = rand() % POPULATION_SIZE;
            if (population[index].fitness > best.fitness) {
                best = population[index];
            }
        }
        return best;
    }

    void evolve(const GameState& state, const pair<int, int> surface[MAX_SURFACE_POINTS], int surfaceN) {
        sort(population, population + POPULATION_SIZE, [](const Chromosome& a, const Chromosome& b) {
            return a.fitness > b.fitness;
        });

        for (int i = 0; i < ELITS; ++i) {
            newPopulation[i] = population[i];
        }

        for (int i = ELITS; i < POPULATION_SIZE; ++i) {
            Chromosome parent1 = tournamentSelection();
            Chromosome parent2 = tournamentSelection();
            Chromosome child = crossover(parent1, parent2);
            child.mutate();
            newPopulation[i] = child;
        }

        for (int i = 0; i < POPULATION_SIZE; ++i) {
            population[i] = newPopulation[i];
        }

        for (int i = 0; i < POPULATION_SIZE; ++i) {
            population[i].fitness = simulate(population[i], state, surface, surfaceN);
        }
    }

    Chromosome crossover(const Chromosome& parent1, const Chromosome& parent2) {
        Chromosome child;
        double random = (double)rand() / RAND_MAX;

        for (int i = 0; i < CHROMOSOME_SIZE; ++i) {
            child.genes[i].rotate = random * parent1.genes[i].rotate + (1 - random) * parent2.genes[i].rotate;
            child.genes[i].power = random * parent1.genes[i].power + (1 - random) * parent2.genes[i].power;
            child.genes[i].rotate = max(-90, min(90, child.genes[i].rotate));
            child.genes[i].power = max(-1, min(1, child.genes[i].power));
        }
        return child;
    }

    Chromosome getBestChromosome() {
        Chromosome best = population[0];
        for (int i = 1; i < POPULATION_SIZE; ++i) {
            if (population[i].fitness > best.fitness) {
                best = population[i];
            }
        }
        return best;
    }
};



void printGameState(const GameState& state) {
    cerr << fixed << setprecision(2); // Print with 2 decimal places
    cerr << "Calculated GameState:" << endl;
    cerr << "X: " << state.x << " Y: " << state.y << endl;
    cerr << "HSpeed: " << state.hSpeed << " VSpeed: " << state.vSpeed << endl;
    cerr << "Fuel: " << state.fuel << endl;
    cerr << "Rotate: " << state.rotate << " Power: " << state.power << endl;
    cerr << "-------------------------" << endl;
}

int main() {
    srand(time(0));

    int surfaceN;
    cin >> surfaceN;
    pair<int, int> surface[MAX_SURFACE_POINTS];
    for (int i = 0; i < surfaceN; ++i) {
        cin >> surface[i].first >> surface[i].second;
        if (i > 0 && surface[i].second == surface[i - 1].second) {
            landingStartX = surface[i - 1].first;
            landingEndX = surface[i].first;
            landingY = surface[i].second;
        }
    }

    GeneticPopulation gp;

    GameState state;
    cin >> state.x >> state.y >> state.hSpeed >> state.vSpeed >> state.fuel >> state.rotate >> state.power;

    for (int i = 0; i < POPULATION_SIZE; ++i) {
        gp.population[i].fitness = simulate(gp.population[i], state, surface, surfaceN);
    }

    auto start = high_resolution_clock::now();
    while (duration_cast<milliseconds>(high_resolution_clock::now() - start).count() < 99) {
        gp.evolve(state, surface, surfaceN);
    }

    Chromosome best = gp.getBestChromosome();
    int newRotate = max(-90, min(90, state.rotate + best.genes[0].rotate));
    int newPower = max(0, min(4, state.power + best.genes[0].power));
    cout << newRotate << " " << newPower << endl;

    double rad = newRotate * M_PI / 180.0;
    double hAcc = -newPower * sin(rad);
    double vAcc = newPower * cos(rad) - GRAVITY;

    state.x += state.hSpeed + 0.5 * hAcc;
    state.y += state.vSpeed + 0.5 * vAcc;
    state.hSpeed += hAcc;
    state.vSpeed += vAcc;

    state.fuel -= newPower;
    state.rotate = newRotate;
    state.power = newPower;
    printGameState(state);

    while (true) {
        int dummyX, dummyY, dummyHSpeed, dummyVSpeed, dummyFuel, dummyRotate, dummyPower;
        cin >> dummyX >> dummyY >> dummyHSpeed >> dummyVSpeed >> dummyFuel >> dummyRotate >> dummyPower;

        for (int i = 0; i < POPULATION_SIZE; ++i) {
            gp.population[i].initialize();
        }

        for (int i = 0; i < POPULATION_SIZE; ++i) {
            gp.population[i].fitness = simulate(gp.population[i], state, surface, surfaceN);
        }

        auto start = high_resolution_clock::now();
        while (duration_cast<milliseconds>(high_resolution_clock::now() - start).count() < 99) {
            gp.evolve(state, surface, surfaceN);
        }

        Chromosome best = gp.getBestChromosome();
        int newRotate = max(-90, min(90, state.rotate + best.genes[0].rotate));
        int newPower = max(0, min(4, state.power + best.genes[0].power));
        cout << newRotate << " " << newPower << endl;
        double rad = newRotate * M_PI / 180.0;
        double hAcc = -newPower * sin(rad);
        double vAcc = newPower * cos(rad) - GRAVITY;

        state.x += state.hSpeed + 0.5 * hAcc;
        state.y += state.vSpeed + 0.5 * vAcc;
        state.hSpeed += hAcc;
        state.vSpeed += vAcc;

        state.fuel -= newPower;
        state.rotate = newRotate;
        state.power = newPower;
        printGameState(state);
    }

    return 0;
}