import matplotlib.pyplot as plt
import numpy as np
import sys

def visualize_board_to_image(file_path, output_path):
    # Wczytaj planszę z pliku
    with open(file_path, "r") as f:
        board = [list(line.strip()) for line in f.readlines()]
    
    # Zmapuj znaki na kolory
    color_map = {
        '0': 'red',    # Gracz 0
        '1': 'blue',   # Gracz 1
        '-': 'gray',   # Puste pole
        'x': 'black'   # Dziura
    }

    # Stwórz tablicę kolorów
    colors = np.array([[color_map[cell] for cell in row] for row in board])
    
    # Rysowanie planszy
    fig, ax = plt.subplots()
    for i, row in enumerate(colors):
        for j, color in enumerate(row):
            rect = plt.Rectangle((j, len(colors) - i - 1), 1, 1, color=color)
            ax.add_patch(rect)

    # Ustawienia osi
    ax.set_xlim(0, len(board[0]))
    ax.set_ylim(0, len(board))
    ax.set_xticks([])
    ax.set_yticks([])
    ax.set_aspect('equal')

    # Zapis obrazu
    plt.savefig(output_path)
    plt.close()

if __name__ == "__main__":
    board_file = sys.argv[1]
    output_image = sys.argv[2]
    visualize_board_to_image(board_file, output_image)
