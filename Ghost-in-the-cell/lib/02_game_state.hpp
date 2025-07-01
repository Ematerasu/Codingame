#pragma once

#include <iostream>
#include <vector>
#include <map>
#include <string>
#include <algorithm>
#include <limits>
#include <cmath>

enum Owner { ENEMY = -1, NEUTRAL = 0, ME = 1 };

struct Factory {
    uint8_t id;
    Owner owner;
    uint8_t cyborgs;
    uint8_t production;
    uint8_t disabledTurns;
};

struct Troop {
    uint8_t id;
    Owner owner;
    uint8_t from, to;
    uint8_t cyborgs;
    uint8_t turnsLeft;
};

struct Bomb {
    uint8_t id;
    Owner owner;
    uint8_t from, to;
    uint8_t turnsLeft;
};

class GameState {
public:
    static const int MAX_FACTORIES = 15;
    static const int INF = 1e9;
    int factoryCount;
    int linkCount;

    int distances[MAX_FACTORIES][MAX_FACTORIES];
    int next[MAX_FACTORIES][MAX_FACTORIES];

    Factory factories[MAX_FACTORIES];
    std::vector<Troop> troops;
    std::vector<Bomb> bombs;

    std::vector<int> bestPaths[MAX_FACTORIES][MAX_FACTORIES];

    GameState() {
        troops.reserve(100);
        bombs.reserve(4);
        for (int i = 0; i < MAX_FACTORIES; ++i)
            for (int j = 0; j < MAX_FACTORIES; ++j)
            {
                distances[i][j] = -1;
                next[i][j] = -1;
            }
                
    }

    void readMap() {
        std::cin >> factoryCount >> linkCount; std::cin.ignore();
        for (int i = 0; i < linkCount; ++i) {
            int f1, f2, dist;
            std::cin >> f1 >> f2 >> dist; std::cin.ignore();
            distances[f1][f2] = dist;
            distances[f2][f1] = dist;
        }

        for (int i = 0; i < factoryCount; ++i) {
            for (int j = 0; j < factoryCount; ++j) {
                if (i == j) {
                    distances[i][j] = 0;
                    next[i][j] = -1;
                } else if (distances[i][j] > 0) {
                    next[i][j] = j;
                } else {
                    distances[i][j] = INF;
                    next[i][j] = -1;
                }
            }
        }

        for (int k = 0; k < factoryCount; ++k) {
            for (int i = 0; i < factoryCount; ++i) {
                for (int j = 0; j < factoryCount; ++j) {
                    if (distances[i][k] + distances[k][j] < distances[i][j]) {
                        distances[i][j] = distances[i][k] + distances[k][j];
                        next[i][j] = next[i][k];
                    }
                }
            }
        }

        for (int i = 0; i < factoryCount; ++i) {
            for (int j = 0; j < factoryCount; ++j) {
                if (next[i][j] == -1 || i == j) continue;

                std::vector<int> path;
                int u = i;
                while (u != j) {
                    u = next[u][j];
                    path.push_back(u);
                }
                bestPaths[i][j] = path;
            }
        }

    }

    void updateFromTurnInput() {
        troops.clear();
        bombs.clear();

        int entityCount; std::cin >> entityCount; std::cin.ignore();
        for (int i = 0; i < entityCount; ++i) {
            int id; std::string type;
            int a1, a2, a3, a4, a5;
            std::cin >> id >> type >> a1 >> a2 >> a3 >> a4 >> a5; std::cin.ignore();

            if (type == "FACTORY") {
                factories[id] = Factory{
                    static_cast<uint8_t>(id),
                    static_cast<Owner>(a1),
                    static_cast<uint8_t>(a2),
                    static_cast<uint8_t>(a3),
                    static_cast<uint8_t>(a4)
                };
            }
            else if (type == "TROOP") {
                troops.push_back(Troop{
                    static_cast<uint8_t>(id),
                    static_cast<Owner>(a1),
                    static_cast<uint8_t>(a2),
                    static_cast<uint8_t>(a3),
                    static_cast<uint8_t>(a4),
                    static_cast<uint8_t>(a5)
                });
            }
            else if (type == "BOMB") {
                bombs.push_back(Bomb{
                    static_cast<uint8_t>(id),
                    static_cast<Owner>(a1),
                    static_cast<uint8_t>(a2),
                    static_cast<uint8_t>(a3),
                    static_cast<uint8_t>(a4)
                });
            }
        }
    }

    Factory& getFactory(int id) {
        return factories[id];
    }

    int getDistance(int from, int to) const {
        return distances[from][to];
    }

    std::vector<const Factory*> getMyFactories() const {
        std::vector<const Factory*> res;
        for (const auto& f : factories)
            if (f.owner == ME) res.push_back(&f);
        return res;
    }

    std::vector<const Factory*> getNeutralFactories() const {
        std::vector<const Factory*> res;
        for (const auto& f : factories)
            if (f.owner == NEUTRAL) res.push_back(&f);
        return res;
    }

    std::vector<const Factory*> getEnemyFactories() const {
        std::vector<const Factory*> res;
        for (const auto& f : factories)
            if (f.owner == ENEMY) res.push_back(&f);
        return res;
    }

    std::vector<int> getBestPath(int from, int to) const {
        return bestPaths[from][to];
    }

    std::vector<const Troop*> getEnemyTroops() const {
        std::vector<const Troop*> res;
        for (const auto& t : troops)
            if (t.owner == ENEMY) res.push_back(&t);
        return res;
    }

    void debugPrint() const {
        std::cerr << "=== GAME STATE ===" << std::endl;
        std::cerr << "Factories: " << factoryCount << ", Links: " << linkCount << std::endl;

        for (int i = 0; i < factoryCount; ++i) {
            const Factory& f = factories[i];
            std::cerr << "Factory " << (int)f.id
                << " | Owner: " << (int)f.owner
                << " | Cyborgs: " << (int)f.cyborgs
                << " | Prod: " << (int)f.production
                << " | Disabled: " << (int)f.disabledTurns << std::endl;
        }

        std::cerr << "--- Troops ---" << std::endl;
        for (const Troop& t : troops) {
            std::cerr << "Troop " << (int)t.id
                << " | Owner: " << (int)t.owner
                << " | " << (int)t.from << " -> " << (int)t.to
                << " | Count: " << (int)t.cyborgs
                << " | ETA: " << (int)t.turnsLeft << std::endl;
        }

        std::cerr << "--- Bombs ---" << std::endl;
        for (const Bomb& b : bombs) {
            std::cerr << "Bomb " << (int)b.id
                << " | Owner: " << (int)b.owner
                << " | " << (int)b.from << " -> " << (int)b.to
                << " | ETA: " << (int)b.turnsLeft << std::endl;
        }

        std::cerr << "===============" << std::endl;
    }
};