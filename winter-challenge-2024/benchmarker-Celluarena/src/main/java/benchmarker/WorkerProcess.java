package benchmarker;

import com.codingame.gameengine.runner.MultiplayerGameRunner;
import com.codingame.gameengine.runner.simulate.GameResult;

import java.io.*;
import java.util.StringTokenizer;

/**
 * By MSz 2023
 */
public class WorkerProcess extends Thread {

  private static GameResult runOnePlay(String[] agents, long seed) {
    MultiplayerGameRunner gameRunner = new MultiplayerGameRunner();
    gameRunner.setLeagueLevel(5);
    gameRunner.setSeed(seed);
    for (String agent: agents) gameRunner.addAgent(agent);
    return gameRunner.simulate();
  }

  /**
   * In a separate process
   */
  public static void main(String[] params) throws Exception {
    if (params.length != 1 || !params[0].equals("worker"))
      throw new Exception("This is not for manual run!");
    try {
      BufferedReader input = new BufferedReader(new InputStreamReader(System.in));
      String line;
      while ((line = input.readLine()) != null) {
        if (line.equals("q")) return;
        StringTokenizer args = new StringTokenizer(line, "|");

        int numAgents = (args.countTokens() - 2)/2;
        String[] agents = new String[numAgents];
        boolean[] agentsLogOpt = new boolean[numAgents];
        long seed = Long.parseLong(args.nextToken());
        for (int p = 0; p < numAgents; p++) {
          agents[p] = args.nextToken();
          agentsLogOpt[p] = "1".equals(args.nextToken());
        }
        boolean sumLogOpt = "1".equals(args.nextToken());

        try {
          GameResult gameResult = runOnePlay(agents, seed);
          StringBuilder s = new StringBuilder();
          for (int p = 0; p < numAgents; p++) {
            s.append(gameResult.scores.get(p)).append("|");
            if (agentsLogOpt[p]) s.append(Utils.encodeToOneLine(Utils.concatenateListOfStrings(gameResult.errors.get(Integer.toString(p)))));
            s.append("|");
          }
          if (sumLogOpt) s.append(Utils.encodeToOneLine(Utils.concatenateListOfStrings(gameResult.summaries)));
          System.out.println(s);
        } catch (Exception e) {
          System.out.println(e);
          System.out.flush();
          continue;
        }
        System.out.flush();
      }
    } catch (Exception e) {
      System.out.println(e);
      System.out.flush();
    }
  }

  // ******************************************************************************************************************

  static final String JAVA_BIN = System.getProperty("java.home") + File.separator + "bin" + File.separator + "java";
  public static final String[] MAIN_CMD = new String[]{
    JAVA_BIN,
    //"--add-opens", "java.base/java.lang=ALL-UNNAMED",// Seems unnecessary now
    "-cp", System.getProperty("java.class.path"),
    WorkerProcess.class.getName(),
    "worker"
  };

  public static Process exec() throws IOException {
    return Runtime.getRuntime().exec(MAIN_CMD);
  }

  // ******************************************************************************************************************

  final Benchmarker main;
  Process workerProcess;
  BufferedWriter processInput;
  BufferedReader processOutput;

  WorkerProcess(Benchmarker main) {
    this.main = main;
  }

  void startProcess() throws IOException {
    workerProcess = WorkerProcess.exec();
    processInput = new BufferedWriter(new OutputStreamWriter(workerProcess.getOutputStream()));
    processOutput = new BufferedReader(new InputStreamReader(workerProcess.getInputStream()));
  }

  void closeProcess() throws IOException, InterruptedException {
    if (workerProcess == null) return;
    try {
      processInput.write("q\n");
      processInput.close();
      workerProcess.waitFor();
    } finally {
      workerProcess = null;
    }
  }

  @Override public void run() {
    try {
      startProcess();
      while (true) {
        Benchmarker.Task task = main.taskQueue.take();
        int numAgents = task.perm.length;
        if (numAgents == 0) break;

        StringBuilder s = new StringBuilder();
        s.append(task.seed);
        for (int p = 0; p < numAgents; p++) {
          s.append("|").append(Benchmarker.AGENTS_PATH.resolve(Benchmarker.AGENTS[task.perm[p]]));
          s.append("|").append(Benchmarker.AGENTS_LOG[task.perm[p]] ? "1" : "0");
        }
        s.append("|").append(Benchmarker.SUMMARIES_LOG ? "1" : "0").append("\n");
        processInput.write(s.toString());
        processInput.flush();

        String outStr = processOutput.readLine();
        String[] outTokens = outStr.split("\\|",-1);
        if (outTokens.length != 2*numAgents + 1) throw new Exception("Invalid token counts: " + outTokens.length + " in: " + outStr);
        int[] scores = new int[numAgents];
        String[] logs = new String[numAgents];
        for (int p = 0; p < numAgents; p++) {
          scores[p] = Integer.parseInt(outTokens[2*p]);
          logs[p] = Utils.decodeFromOneLine(outTokens[2*p+1]);
        }
        String summaries = Utils.decodeFromOneLine(outTokens[2*numAgents]);
        main.reportResult(task, scores, logs, summaries);
      }
    } catch (Exception e) {
      e.printStackTrace();
    }
    try {
      closeProcess();
    } catch (Exception ignored) {}
  }

}
