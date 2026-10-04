## MS-VRCSA Billiards  
  
A VRChat Billiards table by Neko Mabel and Sacchan  
  
This table is a fork of the Pool Parlor table by metaphira and Toasterly  
Originally coded by Harry-T  
  
The aim of this table is provide an updated and improved Pool table that is as realistic* as possible for cue sports enthusiasts, while also providing quality of life enhancements and useful features that everyone can enjoy.  
See it in action in the VRChat world 'Sacc's Snooker Club'  
  
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
		- Place balls on top of the rail in practice mode by clicking while holding them to change placement mode  
- Snooker rule set:  
	- Complete rule set for 6-red Snooker has been implemented using (WPBSA - rule set) https://wpbsa.com/wp-content/uploads/Rulebook-Website-Updated-May-2022-2.pdf [Page 34]  
- Greater table customization:  
	- Almost every vertex of the collision model is adjustable, and shown by the in-editor visualizer script for easy setup  
	- Each table has it's own physics settings  
- Misc:  
	- Local build and test works fine with two clients for testing (Now syncing player ID instead of player name)  
	- 3D pockets - balls actually have to fall down a bit before they count as pocketed  
	- Balls don't freeze the instant they're pocketed  
	- LoD system to disable simulation and visuals when far away  
- 9 Tables included  
	- The original prefab table  
	- Accurate to real 7-8-9ft Pool tables  
	- Accurate to real 10 and 12ft Snooker tables  
	- 3 Tables from the VRC Billiards Community Edition package are included  
  
### Setup:  
Remove any other pool table packages (Some files may have the same IDs, causing conflicts)  
Import this package  
Click MS-VRCSA->Set Up Pool Table Layers  
	- this names layer 22 to 'BilliardsModule' and sets the collision matrix so that it only collides with itself  
Place one or more Prefab/MS-VRCA Table prefabs into your scene  
You can freely add/remove tables from the tables list under the hierarchy at BilliardsModule/intl.table/  
	- The table prefabs are in the folder Modules/BilliardsModule/Prefabs  
Because tables can be swapped out they can't easily be lightmapped. Use just one table if you wish to have light mapping  
	- Remove the unused tables from the hierarchy under BilliardsModule/intl.table/  
  
### Table Creation  
Duplicate an existing table prefab and replace the mesh ('table' object) with your own.  
Place the table prefab on its own in to the scene and select and enable gizmos it to display its physical setup, adjust ModelData settings to match your new mesh.  
Copy the hierarchy of the existing tables and you should be fine, objects whose name begin with a period are used in the code, so don't change their names.  
The TableSurface shader's Metallic/Smoothness texture can be exported as a 2 channel png with photoshop's 'Export As' and ticking the '[x]Smaller File (8-bit)' option (not required)  
  
### Future  
There's still a lot that can be done to improve things - I don't intend to do any more major work on this myself - Sacchan  
- Stuff that could be useful for worlds like support for custom ball and table skins  
- A system to export shots from an entire match for preservation  
- Snooker with 15 red balls:  
	- Requires more synced data and simulation, might be best to implement as a fork  
		- see https://github.com/cheesestudio/VRChat-Pool-table-with-15-red-snooker-Pyramid-Chinese-8-ball-based-on-MS-VRCSA-Billiards  
- 9Ball Rules:  
	- 9Ball push out rule  
- Further improvements to physics  
- Misscues  
- New sound effects  
- Optimization  
- 10Ball mode  
- Other standards for 8/9ball (WPA ..)  
- Quest works but there are no quest specific optimizations.  
- I have completely removed the referee stuff, there's probably a simpler way to handle it now, and 99% of people wont use it  
- Message me if you're a programmer and need help working something out  
  
### Credits  
Neko Mabel:  
- Ball-cushion collision function, ball-to-ball collision, rolling, and bounce functions  
- Deep knowledge about all things billiards related  
  
Sacchan:  
- Everything else  
  
### Major changes in version 1.15  
New version of the physics script:  
	- new cushion model (MAT10 replacing HAN05)  
	- See commits by NMabel for more info: 2dcabab, 2a325d2  
Added null checks on the debugger, so you can simply delete it if you don't want it visible.  
TableSurface shader rewritten by Claude as a vert/frag shader to support light volumes properly and only require one shader.  
	known issue: If light volumes is not installed, ticking 'Integrate VRC Light Volumes' may make it disappear from the selectable shaders list. Untick it and reimport the shader file to fix.  

### Major changes in version 1.14  
Added a VRC Light Volumes version of the custom table shader. Change if yourself if using.  
Added 3 tables from the VRCBCE package (metal, scifi, main)  
Added two variant prefabs, (All tables, VRCBCE tables only)  
Pocket shape is now defined by the intersection of two circles, allowing for more accurate pocket shapes  
Fix LoD issue with pocket blockers state not changing if match start was not witnessed  
Fix a bug where teams could be inverted when switching to snooker after playing 4Ball  
Optimize cue's FixedUpdate() a bit, running correct code for owner/non-owner, this may fix a rare bug where cue becomes un-grabbable  
  