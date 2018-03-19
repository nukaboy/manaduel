# manaduel
A simple 1 vs 1 multiplayer card game

Each player starts with the same deck of spells, then every round each player casts a spell, their Attack / Defense value are compared, and each player loses HP. Whoever loses 10 HP first loses the game.

Host a game with "host", connect with "connect [ip]".
Use "help" to get a list of all commands.

Its very easy to create your own deck of cards, just create a .deck file and load it with "deck [filename]".
Each spell is a line looking like "Name of the spell#Attack value#Defense value". Both players need the same deck to play.
