package com.codingame.game;

import com.codingame.gameengine.core.AbstractPlayer.TimeoutException;
import com.codingame.gameengine.core.AbstractReferee;
import com.codingame.gameengine.core.MultiplayerGameManager;
import com.codingame.gameengine.module.endscreen.EndScreenModule;
import com.codingame.view.ViewModule;
import com.google.inject.Inject;
import com.google.inject.Singleton;

import java.util.List;

@Singleton
public class Referee extends AbstractReferee {
    public static boolean REVEAL = false;

    @Inject private MultiplayerGameManager<Player> gameManager;
    @Inject private CommandManager commandManager;
    @Inject private Game game;
    @Inject private ViewModule viewModule;
    @Inject private EndScreenModule endScreenModule;

    long[] executionTime;

    @Override
    public void init() {
        Organ.ENTITY_COUNT = 0;
        executionTime = new long[] { 0, 0 };

        try {
            game.init();
            sendGlobalInfo();

            gameManager.setFrameDuration(1000);
            gameManager.setMaxTurns(101);

            gameManager.setTurnMaxTime(5000);
            gameManager.setFirstTurnMaxTime(10000);
//            gameManager.setTurnMaxTime(50);
//            gameManager.setFirstTurnMaxTime(1000);
        } catch (Exception e) {
            e.printStackTrace();
            System.err.println("Referee failed to initialize");
            abort();
        }
    }

    private void abort() {
        gameManager.endGame();

    }

    private void sendGlobalInfo() {
        // Give input to players
        for (Player player : gameManager.getActivePlayers()) {
            for (String line : Serializer.serializeGlobalInfoFor(player, game)) {
                player.sendInputLine(line);
            }
        }
    }

    @Override
    public void gameTurn(int turn) {
        game.resetGameTurnData();

        // Give input to players
        for (Player player : gameManager.getActivePlayers()) {
            for (String line : Serializer.serializeFrameInfoFor(player, game)) {
                player.sendInputLine(line);
            }
            if (REVEAL) {
                for (Player other: gameManager.getPlayers())
                    if (other != player) {
                        if (other.prevOutputs == null) player.sendInputLine("0"); else {
                            player.sendInputLine(Integer.toString(other.prevOutputs.size()));
                            for (String act: other.prevOutputs) player.sendInputLine(act);
                        }
                        break;
                    }
            }
            long time = System.currentTimeMillis();
            player.execute();
            executionTime[player.getIndex()] += System.currentTimeMillis() - time;            
        }
        // Get output from players
        handlePlayerCommands();

        game.performGameUpdate(turn);

        if (gameManager.getActivePlayers().size() < 2) {
            abort();
        }
    }

    private void handlePlayerCommands() {

        for (Player player : gameManager.getActivePlayers()) {
            try {
                List<String> outputs = player.getOutputs();
                player.prevOutputs = outputs;
                commandManager.handleCommands(player, outputs);
            } catch (TimeoutException e) {
                player.deactivate("Timeout!");
                gameManager.addToGameSummary(player.getNicknameToken() + " has not provided " + player.getExpectedOutputLines() + " lines in time");
            }
        }

    }

    @Override
    public void onEnd() {
        gameManager.putMetadata("executionTime_0", executionTime[0]);
        gameManager.putMetadata("executionTime_1", executionTime[1]);
        game.onEnd();
    }
}
