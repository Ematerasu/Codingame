import json
import networkx as nx
import matplotlib.pyplot as plt

# Wczytanie drzewa
with open("mcts_tree.json", "r") as file:
    nodes = json.load(file)

# Budowanie drzewa w networkx
tree = nx.DiGraph()

for node in nodes:
    node_id = node["Id"]
    tree.add_node(node_id, visits=node["Visits"], wins=node["Wins"], player=node["PlayerId"])
    for child_id in node["Children"]:
        tree.add_edge(node_id, child_id)

def visualize_tree(tree):
    # Pozycje węzłów jako drzewo
    pos = nx.drawing.nx_agraph.graphviz_layout(tree, prog="dot")
    
    # Rysowanie węzłów
    node_labels = {node: f"{node}\nW: {tree.nodes[node]['wins']}, V: {tree.nodes[node]['visits']}" for node in tree.nodes}
    nx.draw(tree, pos, with_labels=True, labels=node_labels, node_size=2000, node_color="lightblue", font_size=8)
    
    # Wyświetlenie
    plt.savefig("trees\tree1.png")
    plt.close()

visualize_tree(tree)

# from pyvis.network import Network

# def interactive_visualize_tree(tree):
#     net = Network(directed=True, notebook=True)
    
#     for node, data in tree.nodes(data=True):
#         net.add_node(node, label=f"{node}\nW:{data['wins']}, V:{data['visits']}", title=f"Player: {data['player']}")
    
#     for src, dst in tree.edges:
#         net.add_edge(src, dst)
#     net.show_buttons(filter_=['physics'])
#     net.show("mcts_tree.html")

# interactive_visualize_tree(tree)
