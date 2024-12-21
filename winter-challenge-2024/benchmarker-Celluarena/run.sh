# BEFORE USE: Substitute GAMENAME with the name of the game: content of <artifactId> from pom.xml file

# run from the referee main directory; agent executables should be in agents/ dir
# Arguments (not provided use default values):
#   $1 $2 - agent executables
#   $3    - nuber of games to run
#   $4    - nuber of threads to run
java -cp target/winter-2024-sprawl-1.0-SNAPSHOT.jar:target/dependency/* benchmarker.Benchmarker $1 $2 $3 $4