#pragma once
#include "02_game_state.hpp"
#include "01_utils.hpp"
#include <vector>
#include <set>


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
};