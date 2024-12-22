# Gra Programistyczna: Tworzenie Bota

### Zasady

Gra odbywa się na siatce.

---

### 🔵🔴 Organizm

- Organizm składa się z organów, z których każdy zajmuje jedno pole na siatce gry.
- Każdy gracz zaczyna z organem typu ROOT. Organizm może rozrastać się, tworząc nowy organ co turę, aby zajmować większy obszar.
- Nowy organ może rosnąć z istniejącego organu na sąsiednie, puste pole.
- Aby rosnąć, organizm potrzebuje białek. 1 podstawowy organ wymaga 1 białka typu A.
- Białka można zdobyć, rosnąc na polu z źródłem białka, co daje 3 białka danego typu.

#### Komenda GROW:
- `GROW id x y type` – tworzy nowy organ typu `type` w lokalizacji x, y z organu o id `id`.

---

### HARVESTER

- Organy mogą mieć przypisaną orientację (np. kierunek).
- Komenda tworzy nowy organ HARVESTER skierowany na północ (N).
- HARVESTER zbiera 1 białko z pola z białkiem na koniec każdej tury, jeśli jest skierowany na to pole.
- Zbiór białek z tego samego źródła jest ograniczony do 1 na turę.

#### Wymagania:
- 1 białko typu C i 1 białko typu D.

---

### TENTACLE

- Organy TENTACLE atakują wrogie organy, zabijając je i ich dzieci.
- Ataki odbywają się równocześnie.
- TENTACLE blokuje możliwość wzrostu na polu, na które jest skierowany.

#### Wymagania:
- 1 białko typu B i 1 białko typu C.

---

### SPORER

- Organ SPORER tworzy nowy organ ROOT w prostoliniowym kierunku.
- ROOT, stworzony przez SPORER, nie ma rodzica.

#### Wymagania:
- 1 białko typu B i 1 białko typu D do wzrostu SPORERA.
- 1 białko każdego typu do stworzenia nowego ROOT.

---

### Tabela Kosztów Organów

| Organ      | A | B | C | D |
|------------|---|---|---|---|
| BASIC      | 1 | 0 | 0 | 0 |
| HARVESTER  | 0 | 0 | 1 | 1 |
| TENTACLE   | 0 | 1 | 1 | 0 |
| SPORER     | 0 | 1 | 0 | 1 |
| ROOT       | 1 | 1 | 1 | 1 |

---

### ⛔ Koniec Gry

Gra kończy się, gdy nie można już wykonać żadnych ruchów lub po 100 turach.

---

### 🎬 Kolejność Akcji w Turze

1. Wzrost i spory.
2. Tworzenie ścian w wyniku kolizji wzrostu.
3. Zbieranie białek.
4. Ataki tentaklami.
5. Sprawdzanie warunków końca gry.

---

### Protokół Gry

#### Inicjalizacja
Pierwsza linia: dwa liczby - szerokość i wysokość siatki.

#### Wejście na Turę
Pierwsza linia: liczba entityCount (liczba bytów na siatce).
Każdy byt zawiera:
- współrzędne (x, y),
- typ (WALL, ROOT, BASIC, HARVESTER, TENTACLE, SPORER, białka A, B, C, D),
- właściciela (1 dla gracza, 0 dla przeciwnika, -1 jeśli to nie organ),
- identyfikator organu i inne dane pomocnicze.

#### Wyjście
Wynik zawiera komendy dla każdego organizmu:
- `GROW id x y type direction` – rośnij w określoną lokalizację.
- `SPORE id x y` – stwórz ROOT.
- `WAIT` – nic nie rób.

---

### Ograniczenia
- Czas reakcji na turę ≤ 50ms.
- Czas reakcji na pierwszą turę ≤ 1000ms.
- Szerokość: 16–24, wysokość: 8–12.
