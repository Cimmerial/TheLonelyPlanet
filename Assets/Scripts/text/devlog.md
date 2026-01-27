## TUE 1-20-26
- The Lonely Planet / The Dark Planet
- MAIN PLANET:
    - MOVEMENT:
        - ROTATION: 
        - VEHICLES: Objects can be in the front or back, and vehicles exist in the fg.
    - GENERATION:
        - NATURAL LANDMARKS:
            - LAKES/PONDS:
                - TIDES: Pulled in the direction of the moons/rings, just up/down, 3 levels of tide (high/mid/low).
            - MOUNTAINS/ROCKS:
    - ADDITIONS:
        - RINGS:
            - GRAVITY:
        - MOONS:
            - UTILITIES:
                - SKYHOOK: Pick up 
                - GRAVITY:
        - FOREGROUND: For vehicles and buildings.
        - BACKGROUND: Natural landmarks.
    - OBJECTS:
        - PLACEMENT: drawn at differnt angles, and then placed with the footprint in mind, making sure it overlaps the entire floor of the given planet surface.
    - VEHICLES:
        - FLIERS: Planes, Copters, Spaceships
        - CRAWLERS: Cars, Trucks, Sleds, Treaded Vehicles
        - DIVERS: Submarines
        - FLOATERS: Satillites, Probes
        - DIGGERS: Drilling Devices/Vehicles
- GALAXY:
    - PLANET:
        - MOON: orbits planets
        - BLACK HOLE: anything that touches it gets deleted
- LOCATIONS
    - BLACK HOLES:
        - THE DRAIN: very large
        - THE MARBLE: very small 
    - PLANETS:
        - COLOSSUS: Largest planet in the known universe.
        - PIEX: Mountain planet, jagged ranges. Good for mining.
        - FORMIN: 
- GAMEPLAY:
    - CORE LOOP:
        - CRAFT COMMAND: Making little crafts and commanding them to do your bidding.
        - MINING SEQUENCE: Drive digger to fallen asteroid, mine for resources.
        - RESOURCE COLLECTION: Final blow from mining tool = more mass retained on fracture than usual.
    - ASTEROIDS:
        - SPAWNING: Always falling onto planets, quite random.
        - COLOR CODING: Lighter shade = more premium resource.
        - RARITY RATINGS: Super rare = mostly white. Each color/shade/tint has certain weight (different resources).
        - RESOURCE TYPES: Start with nightstone, graystone, whitestone, ghoststone, arcium.
        - BASIC RESOURCE: Black rock (lowest tier).
    - AUTOMATION:
        - MINE AND PATROL MODE: Diggers drive around planet, find fallen asteroids, dig/collect, bring resources back to home base.
    - PROGRESSION:
        - SKILL TREE: Get more efficient, better, and different tools.
        - MINING EFFICIENCY: More efficient mining = more good stuff from certain mass percentage of bad.
        - BRANCH REQUIREMENTS: Exploring new branches requires progress in other branches (connected gameplay where skills/activities tie into one another).
        - RESOURCE USAGE: Different resources needed for different aspects of the game.
    - STARTING CONDITIONS:
        - HOME PLANET: Medium sized, has one moon, randomly generated terrain.

- Incremental game where you have to travel to where you want to go in order to upgrade anything there. You have all these vehicles which you can assign tasks to, and buildings where you can click on them to start tasks (Observatory to scope out new systems and planets and debris...) (long pipe thing to detect far away things) (Recycler where you can convert your things into more things, perhaps you are limited by money and resources, or maybe space?). You can travel between planets but to do so you must actually board a ship and go there. Should be randomly generated asteroids... Hmm. color code them? 

### FRI 1.23.26
- need to add functions to calculate the speed needed to keep a satilite in orbit...
- colossus

### SAT 1.24.26
- Should be able to break asteroids. 
- Have the planet orbit a black hole but if you can go and find the corresponding white hole then you can go through the black hole to travel fast if in right ship.
- Fishing somehow? water planets? where they are literally just like a ball of water?
- Should try and add vehicles next? just a basic rover first, then something. which can exit the atmosphere.
- Need to fix it where when fragmenting asteroids sometimes, it lags the game tf out. Need to find out why and remedy it.

- The Drain

### SUN 1.25.26
- Ok need to make it so skiff must be upright in order to properly function.ie, down matches down to at least a 45deg. Otherwise you need to use stabilizers or smth to make it work.
- Piexe

- Need to plot out how asteroid belts are going to work. like we need things to stay in orbit, or in a path like we need to lock them there... hmm.

- Need an asteroid river, ie constant stream of them in and out of the galaxy, like a U around the main star. hmm.

- So the main gameplay is making little crafts and commanding them to do your bidding. So you start and drive a digger over to a fallen asteroid and mine it to get resources from it. When the final blow of an asteroid is from a mining tool, more of its mass is loss on fracture than usual. Asteroids are always falling onto planets for you to collect. Its quite random. You can command diggers on the ground to be on 'mine and patrol' mode where they just drive around the planet until they find an asteroid which has fallen, they dig and collect and bring resources back to home base. There is a skill tree, where you can get more efficient and better and different tools. (more efficent mining means more good stuff from a certain mass percentage of bad). Going to have asteroids be colored, where the lighter the shade, the more premium the resource. rarity ratings of asteroids will be created where a super rare one would be mostly white, and each color/shade/tint has a certain weight, as they are different resources. With the lowest being basic black rock. Different resources are needed for different aspects of the game. Start with nightstone, graystone, whitestone, ghoststone, arcium. All that jazz. Exploring new branches on the tree requires you to progress in other branches (want game to be connected where skills/activities tie into one another). 
    - Player spawns on home planet which is medium sized, has one moon, and the terrain will be randomly generated.

- I want to create a website (not hosted but just running on my computer on ports) called MyArchive, where i can basically create my own wiki for different things and this way i can efficiently store my ideas for a given game in a place which is connective. The idea: We have a tree of ideas right? Like this:
    - The Lonely Planet:
        - LOCATIONS: descirption of black holes
            - BLACK HOLES: descirption of black holes
                - THE DRAIN: very large
                - THE MARBLE: very small 
            - PLANETS:
                - COLOSSUS: Largest planet in the known universe.
                - PIEX: Mountain planet, jagged ranges. Good for mining.
                - FORMIN: 
        - GAMEPLAY:
            - CORE LOOP:
                - CRAFT COMMAND: Making little crafts and commanding them to do your bidding.
                - MINING SEQUENCE: Drive digger to fallen asteroid, mine for resources.
                - RESOURCE COLLECTION: Final blow from mining tool = more mass retained on fracture than usual.
- What i want to do is turn the above into a wiki which works like this: we have the home page for the lonely planet, and then on that we have a search bar to look for an article, a summary of the lonely planet, and all the big tree articles (locations and gameplay in this example), then i can click on the locations item and it sends me to a page like the original lonely planet page, but this one is about locations with black holes and planets being the sub articles. and so on. have all the sub articles in a list on the left fourth of the screen, in alphabetical order (and have options to go back within the structure of articles), and then have the main page contents in the right side. it should look clean (black white and yellow aestetic) but also always be in edit mode, where i can much like an interactive jupyterhub notebook, create cells which i can write in, and can choose between text, header, or subheader cells. each page when created auto has a 'Summary' header as the top cell and a blank one below that. and the real kicker is just like how wikapedia has links to many words on each page, i should be able to create links to other articles easily. I want it to happen near automatic, where if i want to link 'Planets' article soemwhere, all i need type is the word PLANETS in all caps, and it auto turns that into a linked word and now can be left clicked once to go there. Additionally, if at some point in an article i wrote the word 'planet' then i can right click that word and it will pop up with a modal of page names with the highest similarity ratings to the word right clicked (or words, as i can highlight multiple then right click for same effect) or i can choose to create a new page and if i do then i can choose the path and where it is stored in the system. I want to be able to switch between projects too. When i make any edits on the frontend, it should immediately propagate and update the backend db of all the pages and projects and everything. I dont care how they are stored, just whatever is fast and works. The link matching should only happen is right click and specified or if all caps typed out, dont link Planets if 'Planets' types as sometimes that could result in incorrect things. Allow for ctr B ctr I when editied cells to bold and italic words repsectively. cells should be draggable/reorderable. store each projects files in different folder (under parent folder) so i can move them about. the search functionality should be for titles, but also content, though put title results above content results. have the header be of MyArchive - [name] - [first depth article] - [second depth article] ... etc. with the articles and my archive and name being links to repsective sources. clicking my arhcive brings you to page with all the projects availible. searching only works within a project, not all projects at once. 

- Figuring a lot out rn, main things i need to work on rn are resource design and automation. Dont want to be like that old space miner LEGO game. That sucked.

- Tool edits: need to make changing tab names possilbe, need to make changing tab names propagate through all instances of it.

- Dropping asteroids into a fuel

- Ley Liner

### MON 1.26.26

- Custom calendar? Have a Galaxy absolute calendar where there are galaxy wide events happening at all times?
- Have something where solar systems order a central point 

- Atmospheric winds
- Seb

- Ok how is the component system going to work? So I think that vehicles should be able to have component slots. These slots differ vehicle to vehicle, as most of the time you only need/want one drill bit as two doesnt make any sense. Then we can based on the current configuration of the given vehicle and its travel capabilities, command it to do something. So say we have a ROX class warship with a drill on it, 3 carriers and a voider (void everything besides blackstone and whitestone), then we can command it to collect blackstone until it has max capacity of the ship, then should deposit resource evenly among the planets nearest to Hades Horizon. We are going to have some sort of simple way to command them but being able to put many stipulations on how and where they should get and deposit and stop and all that. When components are used, have them flash white and gray. 
- Components dont have a hitbox. 
    - Types: Diggers/Borers, Grabbers, Pushers, Carriers (storage), Grapplers, Sensors (detect objects/changes near/far from vehicle), Spotters (detect ley lines), Shooters, Voider (ejects unwanted resources from vehicle), Booster (engines), Sailor (light sails), Loader (unloading and loading resources near instantly), Collector (collects vapors/gasses/liquids from atmospheres), Charger (solar panel), Shielder, 
- Can have capacity of ships being full inc the mass.
- Some vehicles have built in components like floaters usually have sensors on them already as well as chargers.
