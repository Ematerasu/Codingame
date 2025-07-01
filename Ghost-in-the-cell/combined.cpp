// === STANDARD INCLUDES ===
#include <algorithm>
#include <cmath>
#include <iostream>
#include <limits>
#include <map>
#include <set>
#include <string>
#include <vector>
// === HEADER FILES ===

// --- 01_utils.hpp ---

enum ActionType { MOVE = 0, INC = 1, BOMB = 2, WAIT = 3, MSG = 4 };

struct Action {
    ActionType type;
    int src, dst, amount;
    std::string message;

    static Action Move(int from, int to, int amt) {
        return {ActionType::MOVE, from, to, amt, ""};
    }
    static Action Inc(int factory) {
        return {ActionType::INC, factory, -1, -1, ""};
    }
    static Action Bomb(int from, int to) {
        return {ActionType::BOMB, from, to, -1, ""};
    }
    static Action Wait() {
        return {ActionType::WAIT, -1, -1, -1, ""};
    }

    std::string toString() const {
        if (type == ActionType::MOVE) return "MOVE " + std::to_string(src) + " " + std::to_string(dst) + " " + std::to_string(amount);
        if (type == ActionType::INC) return "INC " + std::to_string(src);
        if (type == ActionType::WAIT) return "WAIT";
        if (type == ActionType::MSG) return "MSG " + message;
        return "WAIT";
    }
};
// --- 02_game_state.hpp ---


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
// --- 03_ai.hpp ---


class AI {
public:
    virtual ~AI() = default;
    virtual std::vector<Action> getActions(const GameState& state) = 0;
};


class GreedyAI : public AI {
public:
    std::vector<Action> getActions(const GameState& state) override {
        std::vector<Action> actions;
        std::map<int, int> incomingEnemyCyborgs;
        std::set<int> factoriesNeedingHelp;
        std::map<int, int> availableCyborgs;

        for (const auto& troop : state.troops) {
            if (troop.owner == ENEMY) {
                incomingEnemyCyborgs[troop.to] += troop.cyborgs;
            }
        }

        for(const auto& myF : state.getMyFactories())
        {
            int incoming = incomingEnemyCyborgs[myF->id];
            availableCyborgs[myF->id] = std::max(0, myF->cyborgs - (incoming + 1));

            if (myF->cyborgs < incoming + 1) {
                factoriesNeedingHelp.insert(myF->id);
            }
        }

        for (const auto& myF : state.getMyFactories()) {
            if (myF->production < 3 && myF->cyborgs > 20 && availableCyborgs[myF->id] >= 10) {
                actions.push_back(Action::Inc(myF->id));
                availableCyborgs[myF->id] -= 10;
            }
        }

        auto neutralFactories = state.getNeutralFactories();
        int bestScore = -1;
        const Factory* target = nullptr;

        for (const auto& myF : state.getMyFactories()) {
            int spare = availableCyborgs[myF->id];
            if (spare <= 0) continue;

            for (int needyId : factoriesNeedingHelp) {
                if (needyId == myF->id) continue;
                actions.push_back(Action::Move(myF->id, needyId, spare));
                availableCyborgs[myF->id] = 0;
                break;
            }

            if (availableCyborgs[myF->id] > 0 && !neutralFactories.empty()) {
                auto nearest = *std::min_element(neutralFactories.begin(), neutralFactories.end(), [&](const Factory* a, const Factory* b) {
                    return state.getDistance(myF->id, a->id) < state.getDistance(myF->id, b->id);
                });
                actions.push_back(Action::Move(myF->id, nearest->id, spare));
                availableCyborgs[myF->id] = 0;
            }
            for (const auto& enemyF : state.getEnemyFactories()) {
                int dist = state.getDistance(myF->id, enemyF->id);
                int score = enemyF->production * 10 + enemyF->cyborgs - dist;
                if (score > bestScore) {
                    bestScore = score;
                    target = enemyF;
                }
            }
            if (target) {
                actions.push_back(Action::Bomb(myF->id, target->id));
            }
        }

        if (actions.empty()) actions.push_back(Action::Wait());
        return actions;
    }
};// === CPP FILES ===

// --- ai_greedy.cpp ---

// --- game_state.cpp ---
// === MAIN FILE ===

// --- main.cpp ---
// Uncomment to enable local testing
//#define MODE_LOCAL
#define MODE_LIVE


#ifdef MODE_LOCAL
#endif

int main() {
#ifdef MODE_LIVE
    GameState state;
    state.readMap();
    GreedyAI ai;

    while (true) {
        state.updateFromTurnInput();
        auto actions = ai.getActions(state);

        for (size_t i = 0; i < actions.size(); ++i) {
            std::cout << actions[i].toString();
            if (i + 1 < actions.size()) std::cout << ";";
        }
        std::cout << std::endl;
    }

#else
    GreedyAI bot1;
    GreedyAI bot2;

    GameState state = GameState::fromInitialConfig();

    int turn = 0;
    while (!state.isGameOver() && turn++ < 200) {
        auto a1 = bot1.getActions(state);
        auto a2 = bot2.getActions(state);

        state.applyActions(a1, a2);
        state.step();
    }
#endif
}