
package com.codingame.game;

import java.util.ArrayList;
import java.util.Collections;
import java.util.HashSet;
import java.util.LinkedList;
import java.util.List;
import java.util.Optional;
import java.util.Queue;
import java.util.Random;
import java.util.Set;
import java.util.stream.Collectors;

import com.codingame.game.grid.Coord;
import com.codingame.game.grid.Grid;
import com.codingame.game.grid.Tile;
import com.google.inject.Singleton;

@Singleton
public class GridMaker {
    private static final int GRID_W_RATIO = 2;
    public static final int MAX_SPAWN_DIST_FROM_CORNER = 3;

    public static Grid initGrid(Random random) {
        int h = random.nextInt(8, 13);
        int w = h * GRID_W_RATIO;

        boolean ySym = random.nextBoolean() || random.nextBoolean();
        Grid grid = new Grid(w, h, ySym);

        double maxProteinRatio = 0.25;
        double maxObstacleRatio = 0.5;

        int proteinCount = (int) (random.nextDouble(maxProteinRatio) * h * w);
        int obstacleCount = (int) (random.nextDouble(maxObstacleRatio) * h * w);

        List<Coord> allCoords = new ArrayList<>(grid.getCoords());

        // Obstacles
        Set<Coord> obstacleCoords = new HashSet<>();
        while (obstacleCoords.size() < obstacleCount && !allCoords.isEmpty()) {
            int randIndex = random.nextInt(allCoords.size());
            Coord randCoord = allCoords.remove(randIndex);
            Coord symCoord = grid.opposite(randCoord);
            obstacleCoords.add(randCoord);
            obstacleCoords.add(symCoord);
        }
        for (Coord c : obstacleCoords) {
            grid.get(c).setObstacle();
        }

        // Proteins
        int proteinIdx = 0;
        Set<Coord> proteinCoords = new HashSet<>();
        while (proteinCoords.size() < proteinCount && !allCoords.isEmpty() || proteinIdx < Game.PROTEIN_COUNT * 2) {
            int randIndex = random.nextInt(allCoords.size());
            Coord randCoord = allCoords.remove(randIndex);
            Coord symCoord = grid.opposite(randCoord);
            proteinCoords.add(randCoord);
            proteinCoords.add(symCoord);

            grid.get(randCoord).setProtein(proteinIdx % Game.PROTEIN_COUNT);
            grid.get(symCoord).setProtein(proteinIdx % Game.PROTEIN_COUNT);
            proteinIdx++;
        }

        // Spawns
        Coord spawn = new Coord(
            random.nextInt(0, MAX_SPAWN_DIST_FROM_CORNER + 1),
            random.nextInt(0, MAX_SPAWN_DIST_FROM_CORNER + 1)
        );
        Tile t = grid.get(spawn);
        Tile tOpp = grid.get(grid.opposite(spawn));
        t.clear();
        tOpp.clear();

        List<Coord> wallCoords = grid.getCoords().stream().filter(c -> grid.get(c).isObstacle()).toList();

        fixIslands(grid, new ArrayList<>(wallCoords), random);

        grid.spawn = spawn;

        return grid;
    }

    private static Optional<Set<Coord>> getIslandFrom(List<Set<Coord>> islands, Coord coord) {
        return islands.stream()
            .filter(set -> set.contains(coord))
            .findFirst();
    }

    private static boolean closeIslandGap(Grid grid, List<Coord> wallCoords, List<Set<Coord>> islands) {
        List<Set<Coord>> connectingIslands = null;
        Coord bridge = null;

        for (Coord coord : wallCoords) {
            List<Coord> neighs = grid.getNeighbours(coord);
            connectingIslands = neighs.stream()
                .map(n -> getIslandFrom(islands, n))
                .filter(opt -> opt.isPresent())
                .map(opt -> opt.get())
                .distinct()
                .collect(Collectors.toList());
            if (connectingIslands.size() > 1) {
                bridge = coord;
                break;
            }
        }

        if (bridge != null) {
            final List<Set<Coord>> bridging = connectingIslands;
            Coord coord = bridge;
            Coord opposite = grid.opposite(coord);

            grid.get(coord).unsetObstacle();
            grid.get(opposite).unsetObstacle();

            wallCoords.remove(coord);
            wallCoords.remove(opposite);

            List<Set<Coord>> newIslands = islands.stream()
                .filter(set -> !bridging.contains(set))
                .collect(Collectors.toList());

            Set<Coord> newIsland = new HashSet<>();
            bridging.forEach(set -> newIsland.addAll(set));

            islands.clear();
            islands.addAll(newIslands);
            islands.add(newIsland);
            return true;
        }
        return false;
    }

    private static List<Set<Coord>> detectIslands(Grid grid) {
        List<Set<Coord>> islands = new ArrayList<>();
        Set<Coord> computed = new HashSet<>();
        Set<Coord> current = new HashSet<>();

        for (Coord p : grid.getCoords()) {
            if (grid.get(p).isObstacle()) {
                continue;
            }
            if (!computed.contains(p)) {
                Queue<Coord> fifo = new LinkedList<>();
                fifo.add(p);
                computed.add(p);

                while (!fifo.isEmpty()) {
                    Coord e = fifo.poll();
                    for (Coord delta : Grid.ADJACENCY) {
                        Coord n = e.add(delta);
                        Tile cell = grid.get(n);
                        if (cell.isValid() && !cell.isObstacle() && !computed.contains(n)) {
                            fifo.add(n);
                            computed.add(n);
                        }
                    }
                    current.add(e);
                }
                islands.add(new HashSet<>(current));
                current.clear();
            }
        }

        return islands;
    }

    private static void fixIslands(Grid grid, List<Coord> wallCoords, Random random) {
        Collections.shuffle(wallCoords, random);
        List<Set<Coord>> islands = detectIslands(grid);

        while (islands.size() > 1) {
            boolean closed = closeIslandGap(grid, wallCoords, islands);

            if (!closed) {
                Coord aWallAdjToFree = findWallAdjecentToFreeSpace(wallCoords, grid);
                if (aWallAdjToFree != null) {
                    Coord coord = aWallAdjToFree;
                    Coord opposite = grid.opposite(coord);

                    grid.get(coord).unsetObstacle();
                    grid.get(opposite).unsetObstacle();

                    wallCoords.remove(coord);
                    wallCoords.remove(opposite);
                }
                islands = detectIslands(grid);
            }
        }

    }

    private static Coord findWallAdjecentToFreeSpace(List<Coord> wallCoords, Grid grid) {
        for (Coord c : wallCoords) {
            List<Coord> neighs = grid.getNeighbours(c);
            Optional<Coord> free = neighs.stream().filter(n -> !grid.get(n).isObstacle()).findAny();
            if (free.isPresent()) {
                return c;
            }
        }
        return null;
    }

    public static Grid initTutorialGrid(int leagueLevel, Random random) {
        int w = 18;
        int h = leagueLevel == 3 ? 8 : 9;
        Grid grid = new Grid(w, h, true);
        grid.wallUp();

        for (int pIdx = 0; pIdx < 2; pIdx++) {
            for (int x = 1; x < w - 1; ++x) {
                if (leagueLevel == 3) {
                    for (int y = 2; y < 6; ++y) {
                        grid.get(x, y).unsetObstacle();
                    }
                } else {
                    for (int y = pIdx * 4 + 1; y < pIdx * 4 + 4; ++y) {
                        grid.get(x, y).unsetObstacle();
                    }
                }
            }
        }

        if (leagueLevel != 3) {
            grid.get(16, 4).unsetObstacle();
        }

        grid.spawn = new Coord(1, 2);

        Coord cur = grid.spawn;
        if (leagueLevel == 3) {
        } else if (leagueLevel == 4) {
            int dist = random.nextInt(15, 17);

            List<Coord> coords = getFreeCoordsToTheRightAtDistanceAndAbove(grid, cur, dist, 4);
            Collections.shuffle(coords, random);
            cur = coords.get(0);
            grid.get(cur).setProtein(0);
            grid.get(cur.add(0, 4)).setProtein(0);
        } else {
            while (cur.getX() < w - 3) {
                int dist = leagueLevel == 2 ? random.nextInt(4, 6) : 3;

                List<Coord> coords = getFreeCoordsToTheRightAtDistanceAndAbove(grid, cur, dist, 4);
                Collections.shuffle(coords, random);
                cur = coords.get(0);
                grid.get(cur).setProtein(0);
                grid.get(cur.add(0, 4)).setProtein(0);
                if (leagueLevel == 2) {
                    break;
                }
            }
        }

        return grid;
    }

    private static List<Coord> getFreeCoordsToTheRightAtDistanceAndAbove(Grid grid, Coord from, int dist, int above) {
        ArrayList<Coord> result = new ArrayList<>();
        grid.getCoords().stream()
            .filter(c -> c.getX() > from.getX())
            .filter(c -> c.getY() < above)
            .filter(c -> c.manhattanTo(from) == dist)
            .filter(c -> !grid.get(c).isObstacle())
            .forEach(result::add);
        return result;

    }

}
