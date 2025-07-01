#include <string>

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