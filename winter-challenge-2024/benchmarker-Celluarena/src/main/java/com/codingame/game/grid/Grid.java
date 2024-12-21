package com.codingame.game.grid;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;

public class Grid {
    public static final Coord[] ADJACENCY = new Coord[] { Direction.NORTH.coord, Direction.EAST.coord, Direction.SOUTH.coord, Direction.WEST.coord };

    public int width, height;
    public LinkedHashMap<Coord, Tile> cells;
    boolean ySymetry;
    public Coord spawn;

    public Grid(int width, int height) {
        this(width, height, false);
    }

    public Grid(int width, int height, boolean ySymetry) {
        this.width = width;
        this.height = height;
        this.ySymetry = ySymetry;
        spawn = new Coord(0, 0);

        cells = new LinkedHashMap<>();

        for (int y = 0; y < height; ++y) {
            for (int x = 0; x < width; ++x) {
                Coord coord = new Coord(x, y);
                Tile cell = new Tile(coord);
                cells.put(coord, cell);
            }
        }
    }

    public Tile get(int x, int y) {
        return cells.getOrDefault(new Coord(x, y), Tile.NO_TILE);
    }

    public List<Coord> getNeighbours(Coord pos) {
        List<Coord> neighs = new ArrayList<>();
        for (Coord delta : ADJACENCY) {
            Coord n = new Coord(pos.getX() + delta.getX(), pos.getY() + delta.getY());
            if (get(n) != Tile.NO_TILE) {
                neighs.add(n);
            }
        }
        return neighs;
    }

    public Tile get(Coord n) {
        return get(n.getX(), n.getY());
    }

    public List<Coord> getClosestTarget(Coord from, List<Coord> targets) {
        List<Coord> closest = new ArrayList<>();
        int closestBy = 0;
        for (Coord neigh : targets) {
            int distance = from.manhattanTo(neigh);
            if (closest.isEmpty() || closestBy > distance) {
                closest.clear();
                closest.add(neigh);
                closestBy = distance;
            } else if (!closest.isEmpty() && closestBy == distance) {
                closest.add(neigh);
            }
        }
        return closest;
    }

    public List<Coord> getCoords() {
        return cells.keySet().stream().toList();
    }

    public Coord opposite(Coord c) {
        return new Coord(width - c.x - 1, ySymetry ? (height - c.y - 1) : c.y);
    }

    public boolean isYSymetric() {
        return ySymetry;
    }

    public void wallUp() {
        for (Tile t : cells.values()) {
            t.setObstacle();
        }
    }
}
