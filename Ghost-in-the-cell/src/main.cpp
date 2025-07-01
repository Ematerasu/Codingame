// Uncomment to enable local testing
//#define MODE_LOCAL
#define MODE_LIVE

#include "02_game_state.hpp"
#include "03_ai.hpp"
#include "01_utils.hpp"
#include "ai_greedy.cpp"
#include <iostream>
#include <string>

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