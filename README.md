## MS-VRCSA Billiards

A VRChat Billiards table by Neko Mabel and Sacchan

This table is a fork of the Pool Parlor table by metaphira and Toasterly
Originally coded by Harry-T

The aim of this table is provide an updated and improved pool table that is as realistic* as possible for cue sports enthusiasts, while also providing quality of life enhancements and useful features that everyone can enjoy.

### Features
- New game set up menu:
	- Choose table to play on (developers can make their own table models and distribute them!)
	- Choose physics script to use (developers can use this to experiment while not breaking functionality!)
- New extra menu:
	- Cue length and movement smoothing sliders
	- Undo, redo, and skip turn are always available during multiplayer matches:
		- Allows undoing of accidental hits
		- Allows viewing of previous shots
		- Needless to say, this change assumes greater trust between players but I think it'll be okay.
- Remade player join menu:
	- Players can leave and join the match at any time
	- No worries about disconnections - late joiners can join in
	- When there's only a single player, practice mode automatically activates, enabling shot-set up and multiplayer practice sessions
- Save/Load menu for shots (originally made by eijis-pan):
	- Save a ball layout or actual shot (by saving while the simulation is running)
	- Save the ball layout from someone else's game and try the shot yourself on another table!
	- Extremely useful for bug testing!
- Greatly improved ball physics:
	- Re-written all ball collision functions
	- All balls now use a collision prediction step to ensure accurate ball-to-ball collisions, and that they will never clip through the corners of cushions
	- Sub-step physics - All balls now run extra physics steps if they hit something to ensure they move the full distance they should have in the step time
	- Enhanced 3D physics:
		- All balls are equal - All balls can jump and bounce. Hitting cushions too fast can cause jumping
		- On rail physics - balls can roll around on top of the side rail, and fall in to pockets from it
- Snooker rule set:
	- Complete rule set for 6-red snooker has been implemented using (WPBSA - rule set) https://wpbsa.com/wp-content/uploads/Rulebook-Website-Updated-May-2022-2.pdf [Page 34]
	- A full size Snooker table model is included
	- May not be perfect, message me if you find a bug
- Greater table customization:
	- Almost every vertex of the collision model is adjustable, and shown by the in-editor visualizer script for easy setup
- Misc:
	- Local build and test works fine with two clients for testing (Now syncing player ID instead of player name)
	- 3D pockets - balls actually have to fall down a bit before they count as pocketed
	- Balls don't freeze the instant they're pocketed
- 'Bar Box' 7ft table model

### Setup:
Import the package
Click MS-VRCSA->Set Up Pool Table Layers
	- this names layer 22 to 'BilliardsModule' and sets the collision matrix so that it only collides with itself
Place one or more Prefab/MS-VRCA Table prefabs into your scene

### Table Creation
Copy the hierarchy of the existing tables and you should be fine, objects whose name begin with a period are used in the code, so don't change their names.
The shader for the table's Metallic/Smoothness texture can be exported as a 2 channel png with photoshop's Export As and ticking the '[x]Smaller Filer (8-bit)' option

### Known issues
- It's possible for a player to be unable to grab their cue when it's their turn, leaving and re-joining the match fixes this issue. (rare)
- Saving/loading shots isn't perfect due to some values getting compressed - this may be fixable

### Future
There's still a lot that can be done to improve things
- Stuff that could be useful for worlds like support for custom ball and table skins
- A system to export shots from an entire match for preservation
- Snooker with 15 red balls:
	- Requires more synced data and simulation, might be best to implement as a fork
- 9Ball Rules:
	- 9Ball push out rule
	- Break rules (4 balls must contact cushion on break)
- Further improvements to physics
- Optimization - may not be necessary with udon2 coming soon
- 10Ball mode
- pocket selection for 8Ball
- 9ft table
- 10ft table
- I have completely neglected the Quest setup stuff, idk if it still works, never used it
- I have completely neglected the referee stuff, idk if it still works, never used it
- Message me if you're a programmer and need help working something out

### Credits
Neko Mabel:
- Cushion collision, ball-to-ball collision, rolling, and bounce physics
- Deep knowledge about all things billiards related

Sacchan:
- Everything else