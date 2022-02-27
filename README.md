# Unity ML Agent: Prisoner Break out

A review of Unity ML Agents to train Agents work in a collaborative beahviours. The objective is for the two prisoner agents to work together to escape.    

Please see the results of the Trained Prisoners Agents playing reasonably well on You Tube at << Your Tube Link   >> 

The Agents can be pursuaded through a sequence of reward objectives to perform an escape sequence of moving a crate, jumping and climbing onto teh crate, jumping and climbing onto the wall, and then to teh exit gate. The jumping and climbing actions require the Prisoner agents to collaborate and synchronise their actions together.    



![ScreenShot](sc2.PNG)

### Break out Overview 

The Environment consists of two Prisoner Agents. 

The Prisoner is encoded to be in one of the following states: { StandingIdle, Walking, PushingCrate, KneelingDown, JumpingUp, PullingUpCollegue, ClimbingUp }

The Prisoner Agents have possible Actions:    { None, RotateLeft, RotateRight, WalkForward, PushCrate, KneelDown, StandUpIdle, JumpUp, ClimbUp, PullUp }

These Actions are Masked, such that cannot PushCrate, if the Prisoner is not in the proximatory to the Crate. The JumpUp and ClimbUp actions cannot be accomplished unless the Prisoner, and the Co Prisoner are in compatible states. In order to Jump Up onto the crate or Wall, the other Prisoner needs to kneeling in front, on the glround or on the crate to support the jump. And in order to climb up the co prisoner needs to to be above, either on the crate or on the wall to help pull the prisoner up.  

The scenario is based upon the use of significant number of spatial and state checks, which affects what prisoners are allowed to perform. These are reflecetd within the Observations space, to help influence the Agent training. 
 
### Observaiton Space 
The Observation Space consists of:

3D Spatial Awareness:
- 3D Ray Sensor at the Prisoner Hip level, to detect (Crate, Co-Prisoner, Walls and Exit Gate) on the same level as the Prisoner) on same spatial level as prisoner
- 3D Ray Sensor above the Prisoner to detect (Crate, Co-Prisoner, Walls and Exit Gate) on the spatial level above the Prisoner

5x Explicit Spatial Awareness:
- X Local Position
- Z Local Position
- Y Local Rotation  (c.f. Prisoner heading)
- Crate.X Local Position relative to Wall
- crate.z Local Position
Where +x is Forwards towrads the Wall and The Exit Gate from the Originla Positions  and Z is depth side to side wrt to the Wall and Crate. +y is Up

7x Proximal Spatial and State Check Observations
- bool: PrisonersAreFacingEachOther()
- bool: InProximatoryToCoPrisoner()
- bool: IsFacingFoward()
- bool: IsOnLowerFloor()
- bool: IsOnTheCrate()
- bool: IsOnTheUpperLevel()
- bool: IsWithinCratePushingZone() 

6 x Hot encoded Player State: (Walking, PushingCrate, KneelingDown, JumpingUp, PullingUpCollegue, ClimbingUp)

6 x Action Conditionals:
- bool: WalkingIsPossible()
- bool: RotatingIsPossible()
- bool: PushingIsPossible()
- bool: KneelingIsPossible()
- bool: JumpingIsPossible()
- bool: ClimbingIsPossible()


### Extensive Reward Shaping

Collaborative Unity Group Training is employed . The rewards are Allocated on a Group basis.  The ExperimentControl.cs script manages the registration of both Prionsers into a Groups and the assignment of Group Rewards via RegisterAgent() calls.  

A Significant amount of Reward Shaping is required to get any Agent Discovery, Exploitation to make any Progress in this scenario. It being extreemly unlikely that the Agents would discover the means to capture overall Objective of both reaching the Exit Gate, from their initial positions. There are a number of collaborative steps required to get anywhere. So the scenario has had to be partitioned into Sub Objectives, with Partial rewards Assigned: 

| Level |Level Objective                    | Reward   |
|:-----:|:---------------------------------:|:------:|
| 1  | Both Prisoners At Crate | 0.2|
| 2  | Both Prisoners Pushing | 0.4|
| 3  | Pushed To Crate the Wall | 1.4|
| 4  | Prisoner Kneeling At Crate | 1.6|
| 5  | Prisoner Able to Jump Onto Crate | 1.8|
| 6  | A Prisoners On the Crate | 2.0|
| 7  | Prisoner Kneeling At Crate | 2.2|
| 8  | Prisoner Able to Climb Onto Crate | 2.4|
| 9  | Both Prisoners On the Crate | 2.6|
| 10  | Prisoner Kneeling On Crate | 2.8|
| 11  | Prisoner Able to Jump Onto Wall | 3.0|
| 12  | A Prisoner On the Upper Level | 3.2|
| 13  | Prisoner Kneeling On Wall | 3.4|
| 14  | Prisoner Able to Climb Onto Wall| 3.6|
| 15  | Both Prisoners On Upper level | 3.8|
| 16  | Both Prisoners Facing Exit Gate  | 4.0|
| 17  | Both Prisoners Escaped | 5.0|
| 18  | Both Prisoners Escaped (Perpetual) | 5.0|

Th ExperimentControl.cs progresses the Training through the Levels. The Ageng has to achieve 10 consecutive successfull objectives ina row, in order to be promoted to the next level. If the Episode times out, then the promotion count is decremented. And if there are more than 50 Level Objective failures, the Level will be demoted to a lower Training Level.   

The WallBreakout.yml configuration is set up to use the Unity ML POCA Multi-Agent POsthumous Credit Assignment (MA-POCA)  algorithm, which I believe is based around PPO. 

The rest of the environment is a lot of Prisoner statement management and animation control stuff, with the players transisting through their own states machines. 

The Prisoner Arena, Prisoners and Crate Game Objects are all captured within a BOEnvironment Unity Prefab Asset. So any changes to the Agents should be done within this Prefab. This Prefab is then used as a basis for replicating 12x Break Out Environment Game Objects within the Training Scene, to speed up Training. 

### Training Experience and Hyper Parameter Tuning

The Esential POCA Configuration requries a significant Time Horizon and a High Gamma, to ensure a long Trajectory of State,Action,Reward are considered in the Advanatge calculation. Large Batch Size and Buffer sizes are also required.  So Policy collapses were experienced, and so epsilon was reduced to 0.1, to clip more severely changes in policy and the learning rate down to a very conservative 0.00001, for smoother more robust, but slow, training. So the essential Configuration:      

 - batch_size: 4096
 - buffer_size:  40960
 - learning_rate: 0.0001
 - beta: 0.005
 - epsilon: 0.1
 - hidden_units: 512
 - num_layers: 2
 - gamma: 0.997
 - time_horizon: 512
 

## Conclusions

See the eventual Trained Prisoners Escape  on You Tube at  <<  You Tube Ref  >>

This experiment would be better classed as Reinforcement Training, ratherthan  Machine Learning. A Significant amount of reward Shaping and Sub Objectives were required to get the Agents to discover and advance through the each of scenario steps of the scenario.  
The level of Reward and Objective Shaping could well have been applied to similar level of programmed logic, with mroe robust results.
The Agents did however discover some weaknesses in the original environment, and exploited these to acheive none perceived Prisoner breakouts. (These exploits have been removed, for a more robust, and obvious breakout sequence)    
This probably illustrates the limits of Unity ML Agents, and Reinforcement Learning in such tight collaborative sequence environments. 

Happy for Any Discusssion, Comments and Improvements.

## Download and Use

This project has been exported as a Unity Package into the Unity WallBreakout folder containing the Breakout Scene, Scripts, Models etc. I am not so familiar with Unity Package export/ imports, so hopefully this is the most convenient way to import into your Unity Projects.   This can be downladed and imported into Unity, or possibly via the Unity Git import directly by reference to the .json file from the Unity Package Manager.  You will also need to import the Unity ML Agents run time package (Note this project was developed and Tested using Unity ML Agents Release 19)


The Trained Brains .onxx files for the eventual Prisoner ML Agents are in the Brains Folder. These can be copied into the ML Agents behaviour components to observe the trained behaviours. 


## Acknowledgements  

- Unity ML Agents at:  https://github.com/Unity-Technologies/ml-agents
- Jason Weimann: Unity and Game Development: https://www.youtube.com/c/Unity3dCollege
- Immersive Limit: Unity Machine Learning Agents: https://www.youtube.com/c/ImmersiveLimit
- Imphenzia:  3D Blender Modelling : https://www.youtube.com/c/Imphenzia
