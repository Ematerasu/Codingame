
# visualizer_wc24

v1.0a_public

by Jakub Kowalski.

The package is confidential. Distribution outside of people affiliated with _University of Wrocław_ is strictly prohibited.


## Usage

Odpowiednio zakodowany opis sytuacji w grze (w domyśle generowany jako stderr przez nasz program) trzeba podać na input visualizera. Albo używając `Ctrl+V` albo przez plik `python3 visualizer_fc23.py < tmp.txt`
Można też przekazać plik opcją `-f` i wtedy czyta z pliku

_Warning_: Domyślnie program używa `UbuntuMono-R.ttf` (stała w programie koło linii 14)- jak będzie wybuchał z braku czcionki to trzeba ją zmienić na jakąś inną która jest w systemie.

### Flagi w kodzie

Początkowe linie kodu:

`PREFIX_REQUIRED =''` domyślnie każda komenda nie będąca komentarzem (patrz niżej) jest traktowana jako komenda wizualizacji. Można wymóc żeby jednak komendy wizualizacji wymagały jakiegoś ustalonego prefiksu.

`NOBORDER_MODE = False` Jeśli w `INIT` podajemy rozmiar bez żadnej dodatkowej ramki to polecam ustawić na True, bo się lepiej będzie wyświetlać

### Argumenty

`-h` pokazuje help

`-d dir` przekierowuje generowanie obrazków z `imgs` do innego folderu

`-f file` generuje obrazki z podanego pliku zamiast z inputa

`-o filterstr` zamiast generować wszystkie ramki tworzy tylko takie których tytuł zawiera substring `filterstr`


## Komendy

Każda komenda musi się mieścić w pojedynczej linii

### Komentarze

Obsługiwane są zwykłe line comments:

`// to jest komentarz`

`?> to test jest komentarz, żeby było kompatybilnie z CGButtons`

`## to też jest komentarz ` 


### FRAME

Wskazuje na początek rysowania ramki o zadanym tytule: `FRAME tytuł ramki`

Każda ramka rysowana jest od zera i nie powiązana z wcześniejszymi/późniejszymi. Wszystkie komendy po starcie ramki (oprócz rozpoczęcia następnej) są na niej rysowane.

Tytuł ramki jest równocześnie nazwą pliku w której będzie jej wygenerowany obraz.

`FRAME 019_sim // stworzy plik imgs/019_sim.png`

### END

Powinien być na końcu wejścia (inaczej może się nie rysować ostatnia ramka). Po prostu: `END`

Można też celowo drukować wcześniej (np jak któryś z graczy został skillowany) żeby kończyć input i nie drukowac potem zbędnych ramek


### INIT

Musi się znajdować na poczatku, inaczej może wybuchnąć. 
`INIT mapWidth mapHeight` - można podać albo prawdziwe wymiary planszy albo większe, z ramką dookoła (np. `width+2, height+2`).
Wszystkie kolejne komendy bazują na podanych wielkościach, więc musimy być z nimi zgodni przez cały czas.

### COORDS

`COORDS H` - wyświetla koordynaty na górze (domyślne)
`COORDS C` - wyświetla koordynaty na każdym cellu
`COORDS N` - nie wyświetla wcale

Flaga zmieniają wyświetlanie dla następnych framów. Można zmieniać kilka razy w trakcie działania programu. Jak chce się domyślne to nie trzeba ustawiać.

### CELL/BOARD

Przekazuje informacje o komórce

`CELL x y code` - gdzie `code` to: `E` empty,  `W` wall `A..D` resource, a dla struktury 4 literki: `"RBSHT,01,NSWE,NSWE"`, kolejno typ, id gracza, dir gdzie celuje, dir skąd. 
Przykłady `R1NN`, `H0WE`.

`BOARD W W W W W... D E E E W E C W E D B W C E E R0NN E W E W...` przekazuje od razu w jednej linii info o całej planszy, space-separated, kolejno rzędami oczekiwana wielkość `WIDTH*HEIGHT`


### Reszta stanu

`RES 8 3 3 8 8 3 3 8` - resource najpierw dla garcza 0, potem dla gracza 1, kolejno `A B C D`
`INC 0 0 0 0 0 0 0 0` - format taki sam jak dla res ale o ile nam przybywa co turę
`SIZE 1 1 1 1` - score (liczba zajętch celli) i liczba rootów, kolejno: `player0score player0roots player1score player1roots`

### TEXT

Komendy tekst w wydzielonych panelach tekstowych.  Są dwa panele, po lewej  i po prawej  
Kolejne komendy automatycznie przechodzą do następnych linii. Jeśli chce się dodać znak nowej linii bez wywoływania kolejnej komendy, w tekście wystarczy dać `\\n`. 

`TEXTL costam costam\\ncostam` - drukuje tekst na lewym panelu
`TEXTR costam costam\\ncostam` - drukuje tekst na prawym panelu

Wersja `C` drukuje tekst w zadanym kolorze.
Kolory podajemy albo jako `#rrggbb` albo takie podstawowe można jako text `black`, `red`, `white`

`TEXTLC orange Ten tekst będzie pomarańczowy na lewym panelu`
`TEXTLC #ff0000 A ten czerwony na prawym`

Dla uproszczenia są wersje `L0 R0 L1 R1` które drukuja tekst na zadanym panelu w kolorze zadanego gracza

`TEXTR1 GROW 17,6->16,6 SPORER W\\nGROW 22,9->23,9 HARVESTER E`



### MASK...

Komendy do wizualizacji masek, czyli pokazywania że jakiś cell jest zaznaczony.

Cell jest podzielony na 9 podkwadracików i można podawać pozycję gdzie chcemy dawać znacznik (kółko zadanego koloru, wielkości ~1/3 cella).
`TL` -- top left, `B` - bottom (center), `BR` - bottom right, `L` - left(center), `C` - center

Kolory podajemy albo jako `#rrggbb` albo takie podstawowe można jako text `black`, `red`, `white`

`MASKB!! pos col mask` - pokazuje zadaną bitmaskę - założenie, jest że bitmaska jest odwrócona, tak jak wychodzi z `bitset.to_string()`
`MASKB!! BL #df77a4 00100111001000000101000110000010011...`

`MASKB pos col mask` - pokazuje zadaną bitmaskę jak jest podana w odpowiedniej kolejności (najpierw rząd 0, od x'a 0)

`MASKC pos col x y` - pokazuje znacznik na zadanym `x y`

`MASKL pos col cellId0 cellId cellId2..` - pokazuje znaczniki na zadanych cellach przy formatowaniu `cellId=y*WIDTH+x`


### MASK TEXT

To samo co maski tylko zamiast znaczników wypisujemy tekst. Takst na każdym polu musi być niepusty i nie może mieć spacji / innych białych znaków (może mieć `\\n`)
Najpierw są drukowane znaczniki potem tekst, więc można mieć tekst na znacznikach.

`MASKTB pos col text00 text10 text20..`

`MASKTC pos col x y textXY`

opcji `TL` nie ma bo chyba nieprzydatna
opcji reverse z `!!` też nie bo nie będziemy tu mapowac bitsetów i tak..

