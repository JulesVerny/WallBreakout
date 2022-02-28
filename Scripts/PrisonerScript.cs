using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PrisonerScript : MonoBehaviour
{
    // =========================================================================================================
    public enum PrisonerState { Idle, Walking, PushingCrate, KneelingDown, JumpingUp, PullingUpCollegue, ClimbingUp};
    public enum PrisonerActions {None, RotateLeft, RotateRight, WalkForward, PushCrate, KneelDown, StandUpIdle, JumpUp, ClimbUp, PullUp };

    // =========================================================================================================
    public GameObject TheCrateObject;
    public GameObject TheExperimentManager;
    public GameObject CoPrisoner;
    public int OwnIdentity; 

    public PrisonerState ThePrisonerState = PrisonerState.Idle;
    private Animator ThePrisonerAnimator;
    public string CurrentAnimationName;
    private float AnnimationProgress; 
    public PrisonerActions ProposedAction;
 
    private CharacterController TheCharController;
    private BoxCollider KneelingBoxJumpCollider; 

    private float WalkSpeed = 1.0f;
    private float gravity = -20.0f;
    private float RotationRate = 120.0f;
    private float ClimbSpeed = 0.66f;
    private float JumpSpeed = 0.75f;

    private float CoPrisonerProximatoryThreshold = 1.5f;  // was 1.25

    private float CratePushProximatoryThreshold = 1.75f;

    private CrateControl TheCrateController;
    private PrisonerScript TheCoPrisonerController; 

    // Supporting Assessment Indicators 
    
    // Experiment Management
    public int DifficultyLevel;
    public bool UnderManualControl;
    private PrisonerActions PrevAction;
    private ExperimentControl TheExperimentController; 

    // =========================================================================================================
    private void Awake()
    {
        TheCharController = GetComponent<CharacterController>();
        ThePrisonerAnimator = GetComponentInChildren<Animator>();
        TheCrateController = TheCrateObject.GetComponent<CrateControl>();
        KneelingBoxJumpCollider = GetComponent<BoxCollider>();
        TheExperimentController = TheExperimentManager.GetComponent<ExperimentControl>(); 
        TheCoPrisonerController = CoPrisoner.GetComponent<PrisonerScript>(); 
    }  // Awake

    // =========================================================================================================
    void Start()
    {
        ResetEscape();

    } // Start
    // =========================================================================================================
    public void ResetEscape()
    {
        // Reset the Prisoner Back near the Start
        UnderManualControl = false; 
        TheCharController.enabled = false;
        float RandomX = Random.Range(-4.0f, -2.0f);
        float RandomZ = Random.Range(-2.0f, 2.0f);
        transform.localPosition = new Vector3(RandomX, 0.2f, RandomZ);     // The Initial Position Relative to the Parent Experiment Environment 
        transform.localRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f);
        TheCharController.enabled = true;
        KneelingBoxJumpCollider.enabled = false;
       

        ProposedAction = PrisonerActions.None;
        PrevAction = PrisonerActions.None;

        SetPrisonerIdle(); 
        

    } // ResetEscape
    // =========================================================================================================
    // Collect the Observations - Per Prisoner 
    void DummyCollectObservations()
    {

        // n.b Two Ray Cast Sensors { Crate, Prisoner, Wall, ExitGate, Enclosure }  
        // One at Prioner Level and the Other above Prsioner Head 

        // Spatial Awareness Observations:
        float Ob1 = transform.localPosition.x / 7.5f;
        float Ob2 = transform.localPosition.z / 5.0f;

        float Ob3 = transform.localEulerAngles.y-90.0f;
        if (Ob3 > 180.0f) Ob3 = Ob3 - 360.0f;
        if (Ob3 < -180.0f) Ob3 = Ob3 + 360.0f;
        Ob3 = Ob3 / 180.0f;

        float Ob4 = 2.5f-TheCrateObject.transform.localPosition.x / 5.0f;
        float Ob5 = TheCrateObject.transform.localPosition.z / 5.0f;
        // Orienation and Level Observations
        bool Ob6 = IsFacingBackward();
        bool Ob7 = IsFacingFoward();
        bool Ob8 = IsOnLowerFloor();
        bool Ob9 = IsOnTheCrate();
        bool ob10 = IsOnTheUpperLevel();

        // Hot Enciode The Current Player State
        bool ob11 = (ThePrisonerState == PrisonerState.Walking);
        bool ob12 = (ThePrisonerState == PrisonerState.PushingCrate);
        bool ob13 = (ThePrisonerState == PrisonerState.KneelingDown);
        bool ob14 = (ThePrisonerState == PrisonerState.JumpingUp);
        bool ob15 = (ThePrisonerState == PrisonerState.PullingUpCollegue);
        bool ob16 = (ThePrisonerState == PrisonerState.ClimbingUp);

        // Action Conditionals 
        bool ob17 = ActionIsAllowed(PrisonerActions.WalkForward); 
        bool ob18 = ActionIsAllowed(PrisonerActions.RotateLeft);
        bool ob19 = (ActionIsAllowed(PrisonerActions.PushCrate) && (InProximatoryToCrate()));
        bool ob20 = ActionIsAllowed(PrisonerActions.KneelDown);
        bool ob21 = ActionIsAllowed(PrisonerActions.JumpUp) && (CanJumpUpOnCrate() || (CanJumpUpToWall()));
        bool ob22 = ActionIsAllowed(PrisonerActions.ClimbUp) && (CanClimbOntoCrate() || (CanClimbOntoWall()));

        // A Total of 20 Explicit Observations 
        
    }  // DummyCollectObservations
    // ==========================================================================================================
    // Update is called once per UI frame
    void Update()
    {

        // Manual Actions Control :  {None, RotateLeft, RotateRight, WalkForward, PushCrate, KneelDown, StandUpIdle, ClimbUp, PullUp};
        ProposedAction = PrisonerActions.None;

        if (UnderManualControl)
        {
            if (Input.GetKey(KeyCode.UpArrow)) ProposedAction = PrisonerActions.WalkForward;
            if (Input.GetKey(KeyCode.RightArrow)) ProposedAction = PrisonerActions.RotateRight;
            if (Input.GetKey(KeyCode.LeftArrow)) ProposedAction = PrisonerActions.RotateLeft;

            if (Input.GetKey(KeyCode.Space)) ProposedAction = PrisonerActions.PushCrate;
            if (Input.GetKey(KeyCode.J)) ProposedAction = PrisonerActions.JumpUp;
            if (Input.GetKey(KeyCode.C)) ProposedAction = PrisonerActions.ClimbUp;
            if (Input.GetKey(KeyCode.K)) ProposedAction = PrisonerActions.KneelDown;
            if (Input.GetKey(KeyCode.P)) ProposedAction = PrisonerActions.PullUp;
            if (Input.GetKey(KeyCode.S)) ProposedAction = PrisonerActions.StandUpIdle;
        }
        

    }  // UI Update
    // =========================================================================================================
    void FixedUpdate()
    {

        // Get the Animation name - To Check progress
        CurrentAnimationName = ThePrisonerAnimator.GetCurrentAnimatorClipInfo(0)[0].clip.name;
        AnnimationProgress = ThePrisonerAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime;

        // Review and Porgress the Proposed Actions

        // Should Always be able to Retunr to Idle
        if ((ProposedAction == PrisonerActions.StandUpIdle) && (ActionIsAllowed(ProposedAction))) SetPrisonerIdle();
       
        // Can Only Propose and Action new Proposod Action IF not in A completion Action Animation 
        if (!(CurrentAnimationName == "PullUp") || (CurrentAnimationName == "ClimbUp") || (CurrentAnimationName == "Jump")) 
        {
            if ((ProposedAction == PrisonerActions.WalkForward) && (ActionIsAllowed(ProposedAction))) SetPrisonerWalking();
            if ((ProposedAction == PrisonerActions.PushCrate) && (ActionIsAllowed(ProposedAction))) SetPrisonerPushing();
            if ((ProposedAction == PrisonerActions.KneelDown) && (ActionIsAllowed(ProposedAction))) SetPrisonerKneeling();

            // ===================================================================
            // Perform Basic Motion Controls
            // Implement the Rotation Actions
            if ((ProposedAction == PrisonerActions.RotateLeft) && (ActionIsAllowed(ProposedAction))) transform.Rotate(new Vector3(0.0f, -RotationRate * Time.deltaTime, 0.0f), Space.Self);  // Rotate Negative Action
            if ((ProposedAction == PrisonerActions.RotateRight) && (ActionIsAllowed(ProposedAction))) transform.Rotate(new Vector3(0.0f, RotationRate * Time.deltaTime, 0.0f), Space.Self); // Rotate Positive Action

            // Implement the Walk Ahead Action
            if ((ProposedAction == PrisonerActions.WalkForward) && (ActionIsAllowed(ProposedAction)))
            {
                // Move Charcater Forward Walking Speed
                PerformForwardDeltaMovement(WalkSpeed);
            }
     
            // Check Push Action on the Crate
            if ((ThePrisonerState == PrisonerState.PushingCrate) && InProximatoryToCrate())
            {
                // Apply Some Force to the Crate in Player Forward Direction
                TheCrateController.ApplyPush(transform.forward, OwnIdentity);
                // Move Charcater Forward Pushing Speed
                PerformForwardDeltaMovement(WalkSpeed * 0.75f);
            }

            // Check Can Jump up Onto Crate
            if ((ProposedAction == PrisonerActions.JumpUp) && ActionIsAllowed(ProposedAction) && CanJumpUpOnCrate())
            {
                SetPrisonerJumpingUp();
            }

            // Check CanJump up Wall
            if ((ProposedAction == PrisonerActions.JumpUp) && ActionIsAllowed(ProposedAction) && CanJumpUpToWall())
            {
                SetPrisonerJumpingUp();
            }

            // Check Climb up  Onto Crate
            if ((ProposedAction == PrisonerActions.ClimbUp) && ActionIsAllowed(ProposedAction) && CanClimbOntoCrate())
            {
                SetPrisonerClimbingUp();
                // Request the Co prisoner to Start Pulling Up 
                TheCoPrisonerController.PerformThePullUp(); 
            }

            // Check Climb up  Onto Wall 
            if ((ProposedAction == PrisonerActions.ClimbUp) && ActionIsAllowed(ProposedAction) && CanClimbOntoWall())
            {
                SetPrisonerClimbingUp();
                // Request the Co prisoner to Start Pulling Up 
                TheCoPrisonerController.PerformThePullUp();
            }
         // ===================================================
        }  // If Not performing a Completion Action

        // =========================================================
        // Update the Decision Counts
        if (ProposedAction != PrevAction) TheExperimentController.UpdateDecisionCount();  
        PrevAction = ProposedAction;


        // ================================================================================================
        // Jumping Animation Progress  
        if (CurrentAnimationName == "Jump")
        {
            // Note the Two Steps Movement 1st Jump Motion: Prisoner 2nd Jump Motion: Crate Or Wall 
            if (AnnimationProgress < 0.3f)
            {
                TheCharController.stepOffset = 1.0f;
                PerformDeltaJumpMovement(JumpSpeed);
            }
            // Move Foward Across Prisoner
            if ((AnnimationProgress >= 0.3f) && (AnnimationProgress < 0.5f)) PerformForwardDeltaMovement(WalkSpeed*5.0f);
            
            // Jump Again Onto Crate or Wall
            if ((AnnimationProgress >= 0.5f) && (AnnimationProgress < 0.7f)) PerformDeltaJumpMovement(JumpSpeed);
            
            // Final Foward Movement on top of Carte or Wall
            if ((AnnimationProgress> 0.7f) && (AnnimationProgress < 0.9)) PerformForwardDeltaMovement(WalkSpeed*5.0f);
            
            // Move Back Into and Idle Standing Stte 
            if ((AnnimationProgress >= 0.9f) && ActionIsAllowed(PrisonerActions.StandUpIdle)) SetPrisonerIdle();  
        } // Jumping Up Animation 
        
        // =======================================
        // Pull Up Animation Progress  
        if (CurrentAnimationName == "PullUp")
        {
            // Need to Return to Kneeling after Pull Up is Complete  - So Only One Pull Up Animation Cylce is Presumed  
            if ((AnnimationProgress >= 0.975f) && ActionIsAllowed(PrisonerActions.KneelDown)) SetPrisonerKneeling();
        } // PullUp  Animation 
        // ========================================
        // Climb Up Animation Progress
        if (CurrentAnimationName == "ClimbUp")
        {
            // Note the Two Steps Movement 1st Jump Motion: Prisoner 2nd Jump Motion: Crate Or Wall 
            if (AnnimationProgress < 0.75f)
            {
                TheCharController.stepOffset = 1.5f;
                PerformDeltaClimbMovement(ClimbSpeed);
            }
            // Final Foward Movement on top of Carte or Wall
            if ((AnnimationProgress > 0.75f) && (AnnimationProgress < 0.95)) PerformForwardDeltaMovement(WalkSpeed * 2.5f);

            // Move Back Into and Idle Standing State and Ask the Co prisoner to do Likewise
            if ((AnnimationProgress >= 0.95f) && ActionIsAllowed(PrisonerActions.StandUpIdle))
            {
                SetPrisonerIdle();
                TheCoPrisonerController.RequestStandup();
            }
        } // ClimbUp Up Animation 
        // ===================================================================

        // ===========================================================================
        // If Not Jumping or Climbing Need to Ensure that the Character is Grounded
        if (!((CurrentAnimationName == "ClimbUp") || (CurrentAnimationName == "Jump"))) EnsureGrounded();
       

        // ========================================================
    } // FixedUpdate
    // =========================================================================================================
    bool ActionIsAllowed(PrisonerActions TheProposedAction)
    {
        // Need to check and Correct the Propsoed Actions
        bool ActionIsAllowed = false;

        // *** MAY ALSO NEED To CONDITION By Annimation Completions
        switch (TheProposedAction)
        {
            case PrisonerActions.None:
                {
                    // Allowed to Return to Idle from ALL States Except   Pulling Up States 
                    if (! (CurrentAnimationName == "PullUp")) ActionIsAllowed = true; 

                    break;
                }  // Requested None Action  
            case PrisonerActions.StandUpIdle:
                {
                    // Allowed to Return to Idle from ALL States Except   Pulling Up States 
                    if (!(CurrentAnimationName == "PullUp")) ActionIsAllowed = true;

                    break;
                }  // Requested Stand Up Action 

            case PrisonerActions.WalkForward:
                {
                    //Can ONLY Go Into a Walk State From an Idle State
                    if ((CurrentAnimationName == "Idle") || (CurrentAnimationName == "Walking") || (CurrentAnimationName == "Push")) ActionIsAllowed = true;
                    break;
                }  // Requested Walk Action

            case PrisonerActions.PushCrate:
                {        
                    //Can ONLY Go Into a Push State From an Idle State OR Walking States
                    if ((CurrentAnimationName == "Idle") || (CurrentAnimationName == "Walking") || (CurrentAnimationName == "Push")) ActionIsAllowed = true;
                    break;
                }  // Requested Push Action

            case PrisonerActions.JumpUp:
                {
                    //Can ONLY Go Into a Climb State From an Idle Or Push States (> Competion) 
                    if ((CurrentAnimationName == "Idle") || (CurrentAnimationName == "Walking") || (CurrentAnimationName == "Jump")) ActionIsAllowed = true;
                    break;
                }  // Requested Climb Action


            case PrisonerActions.KneelDown:
                {
                    //Can ONLY Go Into a Kneel State From an Idle   Or Pull Up State (>Completion) 
                    if ((CurrentAnimationName == "Idle") || (CurrentAnimationName == "Kneeling") || (CurrentAnimationName == "Walking")) ActionIsAllowed = true;
                    if ((CurrentAnimationName == "PullUp") && (AnnimationProgress > 0.975f)) ActionIsAllowed = true;
                    if ((CurrentAnimationName == "Push")) ActionIsAllowed = true;
                    break;
                }  // Requested Kneel Action

            case PrisonerActions.PullUp:
                {
                    //Can ONLY Go Into a Pulling Up State From a kneeling (> Competion) 
                    if ((CurrentAnimationName == "Kneeling") && (AnnimationProgress > 0.975f))  ActionIsAllowed = true;
                    if(CurrentAnimationName == "PullUp") ActionIsAllowed = true;
                    break;
                }  // Requested Pull Up  Action

            case PrisonerActions.ClimbUp:
                {
                    //Can ONLY Go Into a Climbing State from Idle  
                    if ((CurrentAnimationName == "Idle") || (CurrentAnimationName == "ClimbUp")) ActionIsAllowed = true;
                    break;
                }  // Requested Pull Up  Action


            case PrisonerActions.RotateLeft:
                {
                    //Can ONLY Rotate When Walking or Idle 
                    if ((CurrentAnimationName == "Idle") || (CurrentAnimationName == "Walking") || (CurrentAnimationName == "Kneeling")) ActionIsAllowed = true;
                    break;
                }  // Requested Rotate Left

            case PrisonerActions.RotateRight:
                {
                    //Can ONLY Rotate When Walking or Idle 
                    if ((CurrentAnimationName == "Idle") || (CurrentAnimationName == "Walking") || (CurrentAnimationName == "Kneeling")) ActionIsAllowed = true;
                    break;
                }  // Requested Roate Right

        } // Switch Selector accross Actions

        return ActionIsAllowed;
    } // ConfirmProposedAction
    // ===========================================================================================================
    #region Basic Movement Stuff
    void PerformForwardDeltaMovement(float ReqFwdSpeed)
    {
        // Perfrom Normal (Forward Z Direction) Movement
        Vector3 TheDeltaMovement = transform.forward;
        TheDeltaMovement.y = 0.0f;    // To Ensure No Sky Walking !
        TheDeltaMovement = TheDeltaMovement * Time.deltaTime * ReqFwdSpeed;
        TheCharController.Move(TheDeltaMovement);
    }// PerformDeltaMovement
     // =========================================================================================
    void PerformDeltaJumpMovement(float ReqJumpSpeed)
    {
        // Jump Forward and Up  ~ 45 degree Jump Motion  
        Vector3 TheDeltaMovement = transform.up*3.5f + transform.forward;
        TheDeltaMovement = TheDeltaMovement * Time.deltaTime * ReqJumpSpeed;
        TheCharController.Move(TheDeltaMovement);
    }// PerformDeltaJumpMovement
    // =========================================================================================
    void PerformDeltaClimbMovement(float ReqClimbSpeed)
    {
        // Perfom a Climb Upwards and to the Side to try and Avoid the Other Prisoner 
        Vector3 TheDeltaMovement = transform.up + 1.25f*transform.right;
        TheDeltaMovement = TheDeltaMovement * Time.deltaTime * ReqClimbSpeed;
        TheCharController.Move(TheDeltaMovement);
    }// PerformDeltaClimbMovement
     // =========================================================================================
    void EnsureGrounded()
    {
        Vector3 GroundedMovement = Vector3.zero;
        if (!TheCharController.isGrounded)
        {
            GroundedMovement.y = 100.0f * gravity;
        }
        GroundedMovement = GroundedMovement * Time.deltaTime * 1.0f;
        if(TheCharController.enabled)  TheCharController.Move(GroundedMovement);
    }// EnsureGrounded
    // =========================================================================================
    #endregion
    // =========================================================================================
    #region Tactical Checks
    // ===================================================================================================
    public bool InProximatoryToCrate()
    {
        bool RtnProximatory = false;
        float XDistanceToCrate = Mathf.Abs(transform.localPosition.x- TheCrateObject.transform.localPosition.x);
        if(XDistanceToCrate< CratePushProximatoryThreshold) RtnProximatory = true;
        return RtnProximatory; 
    } // InProximatryToCrate
    // =================================================================================================
    public bool InProximatoryToCoPrisoner()
    {
        bool RtnProximatory = false;
        Vector3 DeltaVectorToCoPrisoner = CoPrisoner.transform.localPosition - transform.localPosition;
        DeltaVectorToCoPrisoner.y = 0.0f;  // Ignore Differences in height
        float DistanceToCoPrisoner = Vector3.Magnitude(DeltaVectorToCoPrisoner); 
        if (DistanceToCoPrisoner < CoPrisonerProximatoryThreshold) RtnProximatory = true;
        return RtnProximatory;
    } // InProximatoryToCoPrisoner
    // =========================================================================================================
    public bool IsOnLowerFloor()
    {
        bool RtnOnLowerFloor = false;

        if ((transform.localPosition.x < 3.25f) && (transform.localPosition.y < 0.5f))
        {
            RtnOnLowerFloor = true;
        }
        return RtnOnLowerFloor;
    } // IsOnLowerFloor

    // =========================================================================================================
    public bool IsOnTheCrate()
    {
        bool RtnOnCrate = false;

        Vector3 DeltaCrateVector = transform.position - TheCrateObject.transform.position;
        float DeltaX = DeltaCrateVector.x;
        float DeltaHeight = DeltaCrateVector.y; 
        float DeltaZ = DeltaCrateVector.z;

        if((Mathf.Abs(DeltaX)< 1.0f) && (Mathf.Abs(DeltaZ) < 1.25f) && (DeltaHeight>0.4f) && (DeltaHeight < 1.5f))
        {
            RtnOnCrate = true;
            //Debug.Log("Prisoner is Now on the Crate"); 
        }
        return RtnOnCrate; 
    } // IsOnTheCrate
    // ======================================================================================================
    public bool IsOnTheUpperLevel()
    {
        bool RtnOnUpperLevel = false;

        if(transform.localPosition.y>3.0f)
        {
            RtnOnUpperLevel = true;
            //Debug.Log("Prisoner is Now on the Upper Level");
        }
        return RtnOnUpperLevel;
    } // IsOnTheUpperLevel
    // ======================================================================================================
    public bool CanJumpUpOnCrate()
    {
        bool RtnCanJumpOnCrate = false;

        // *** TODO Will need to modify to Be in Proximatry to Collegue Kneeling 

        // Currently on Lower Floor
        // Needs to facing + X Direciton 
        // Needs to be in Proximatory to Co Prisoner (Rather than Crate) 
        // Need to be in Idle/Push/ClimbUp States
        if (IsOnLowerFloor())
        {
            // First Check in an Appropriate State
            if ((CurrentAnimationName == "Idle") || (CurrentAnimationName == "Push") || (CurrentAnimationName == "Jump") || (CurrentAnimationName == "Walking"))
            {
                // Co Prisoner Also needs to Be Kneeling Down
                if (TheCoPrisonerController.ThePrisonerState == PrisonerState.KneelingDown)
                {
                    // This Prisoner Needs to be facing Foward and close to  other prisoner
                    if (IsFacingFoward() && InProximatoryToCoPrisoner()) RtnCanJumpOnCrate = true;

                    Debug.Log(" CanJumpOnCrate Check: Facing Fwd:" + IsFacingFoward().ToString() + "  Is in Proximatry Prisoner: " + InProximatoryToCoPrisoner().ToString());

                }  // Direction and Proximarty Checks
            } // Current State Checks

        } // Currently On Lower Floor
        return RtnCanJumpOnCrate;
    } // CanClimOnCrate
    // ======================================================================================================
    public bool CanJumpUpToWall()
    {
        bool RtnCanJumpOnWall = false;

        // Needs to facing + X Direciton 
        // Needs within 1.0f of Wall.x local Position 3.5f 
        // Need to be in Idle/Push/ClimbUp States
        // And is standing on the Crate

        // First Check in an Appropriate State
        if (IsOnTheCrate())
        {
            if ((CurrentAnimationName == "Idle") || (CurrentAnimationName == "Jump") || (CurrentAnimationName == "Walking"))
            {
                // Co Prisoner Also needs to Be Kneeling Down
                if ((TheCoPrisonerController.ThePrisonerState == PrisonerState.KneelingDown) && (CoPrisoner.transform.localPosition.x>2.25f) && (TheCoPrisonerController.IsOnTheCrate()))
                {
                    // This Prisoner Need to be facing Foward and close to other prisoner
                    if (IsFacingFoward() && InProximatoryToCoPrisoner()) RtnCanJumpOnWall = true;

                }  // Direction and Proximarty Checks
            } // Current State Checks
        } // Is On the Crate

        return RtnCanJumpOnWall;
    } // CanJumpUpToWall
    // ===================================================================================================
    public bool CanClimbOntoCrate()
    {
        bool RtnCanClimbOnCrate = false;

        if (IsOnLowerFloor())
        {
            // First Check in an Appropriate State
            if ((CurrentAnimationName == "Idle") || (CurrentAnimationName == "ClimbUp"))
            {
                // Co Prisoner Also needs to Be Kneeling Down
                if ((TheCoPrisonerController.ThePrisonerState == PrisonerState.KneelingDown)  && (TheCoPrisonerController.CheckRequestPullUp()))
                {
                    // This prisoner Need to be facing Foward But Co prisoner Facing Backwards and In proximatory
                    if (IsFacingFoward() && TheCoPrisonerController.IsFacingBackward() && InProximatoryToCoPrisoner()) RtnCanClimbOnCrate = true;
                    
                }  // Direction and Proximarty Checks
            } // Current State Checks
        } // Currently On Lower Floor

       // Debug.Log("CanClimbCrate() Check: " + RtnCanClimbOnCrate.ToString());

        return RtnCanClimbOnCrate;
    } // CanClimbOntoCrate
    // =====================================================================================
    public bool CanClimbOntoWall()
    {
        bool RtnCanClimbOnWall = false;

        if (IsOnTheCrate())
        {
            // First Check in an Appropriate State
            if ((CurrentAnimationName == "Idle") || (CurrentAnimationName == "ClimbUp"))
            {
                // Co Prisoner Also needs to Be Kneeling Down
                if ((TheCoPrisonerController.ThePrisonerState == PrisonerState.KneelingDown) && (TheCoPrisonerController.CheckRequestPullUp()))
                {
                    // This prisoner Need to be facing Foward But Co prisoner Facing Backwards and In proximatory
                    if (IsFacingFoward() && TheCoPrisonerController.IsFacingBackward() && InProximatoryToCoPrisoner()) RtnCanClimbOnWall = true;

                }  // Direction and Proximarty Checks
            } // Current State Checks
        } // Currently On The Crate 
        return RtnCanClimbOnWall;
    } // CanClimbOntoWall
    // =========================================================================================================
    public bool CheckRequestPullUp()
    {
        bool PullUpCheckRequest = false;
        // Is Already Kneelling Down
        // is either on the Crate or Wall
        // Is facing backward
        if ((CurrentAnimationName == "Kneeling") || (CurrentAnimationName == "PullUp") && (ActionIsAllowed(PrisonerActions.PullUp)))
        {
            // Check in Reasonable Position 
            if(IsOnTheCrate() && IsFacingBackward()) PullUpCheckRequest = true;

            // If on Upper level, needs to be at Wall Edge
            if(IsOnTheUpperLevel() && IsFacingBackward() && (transform.localPosition.x > 3.5f) && (transform.localPosition.x < 5.0f)) PullUpCheckRequest = true;
        }  //  Animation State check

        return PullUpCheckRequest;
    }  // CheckRequestPullUp
    // ==========================================================================================================
    public bool IsFacingFoward()
    {
        bool RtnFacingFoward = false;
        float DirectionToPositiveXAxis = Vector3.Dot(transform.forward, Vector3.right);
        if (DirectionToPositiveXAxis > 0.5f)  RtnFacingFoward = true; 
        return RtnFacingFoward; 
    }  //  IsFacingFoward
    // ==========================================================================================================
    public bool IsFacingBackward()
    {
        bool RtnFacingBackward = false;
        float DirectionToNegativeXAxis = Vector3.Dot(transform.forward, -Vector3.right);
        if (DirectionToNegativeXAxis > 0.5f) RtnFacingBackward = true;
        return RtnFacingBackward;
    }  //  IsFacingBackward
    // ==========================================================================================================
    public void PerformThePullUp()
    {
        if (ActionIsAllowed(PrisonerActions.PullUp)) SetPrisonerPullingUpCollegue(); 

    } // RequestPullUp
    // ===========================================================================================================
    public void RequestStandup()
    {
        if((CurrentAnimationName == "Kneeling") && ActionIsAllowed(PrisonerActions.StandUpIdle))
        {
            SetPrisonerIdle(); 
        }
    }  // RequestStandup
    // =========================================================================================================
    #endregion
    // =========================================================================================================
    public void SetUnderManualControl()
    {
        UnderManualControl = true;
    }
    public void ClearUnderManualControl()
    {
        UnderManualControl = false;
    }
    // ==================================================================================

    // =========================================================================================================
    // Animation Controller States
    // PrisonerState { Idle, Walking, PushingCrate, KneelingDown, ClimbingUp, PullingUpCollegue };
    void SetPrisonerIdle()
    {
        ThePrisonerState = PrisonerState.Idle;

        // Switch into Default Character Controller
        KneelingBoxJumpCollider.enabled = false;
        TheCharController.enabled = true;
        TheCharController.stepOffset = 0.25f;
        TheCharController.radius = 0.4f;
        TheCrateController.ApplyPush(Vector3.zero, OwnIdentity);  // Clear Down Push Vector

        ThePrisonerAnimator.SetBool("IsWalking", false);
        ThePrisonerAnimator.SetBool("IsPushing", false);
        ThePrisonerAnimator.SetBool("IsJumping", false);
        ThePrisonerAnimator.SetBool("IsKneeling", false);
        ThePrisonerAnimator.SetBool("IsPulling", false);
        ThePrisonerAnimator.SetBool("IsClimbing", false);

    } // SetPlayerIdle
      // ==================================================
    void SetPrisonerWalking()
    {
        ThePrisonerState = PrisonerState.Walking;

        TheCharController.stepOffset = 0.25f;
        TheCharController.radius = 0.4f;

        ThePrisonerAnimator.SetBool("IsWalking", true);
        ThePrisonerAnimator.SetBool("IsPushing", false);
        ThePrisonerAnimator.SetBool("IsJumping", false);
        ThePrisonerAnimator.SetBool("IsKneeling", false);
        ThePrisonerAnimator.SetBool("IsPulling", false);
        ThePrisonerAnimator.SetBool("IsClimbing", false);
    } // PrisonerState.Walking
      // ==================================================
    void SetPrisonerPushing()
    {
        ThePrisonerState = PrisonerState.PushingCrate;
        TheCharController.radius = 0.4f;

        ThePrisonerAnimator.SetBool("IsWalking", false);
        ThePrisonerAnimator.SetBool("IsPushing", true);
        ThePrisonerAnimator.SetBool("IsJumping", false);
        ThePrisonerAnimator.SetBool("IsKneeling", false);
        ThePrisonerAnimator.SetBool("IsPulling", false);
        ThePrisonerAnimator.SetBool("IsClimbing", false);
    } // PrisonerState.PushingCrate
      // ==================================================
    void SetPrisonerJumpingUp()
    {
        ThePrisonerState = PrisonerState.JumpingUp;

        TheCharController.stepOffset = 1.0f;
        TheCrateController.ApplyPush(Vector3.zero, OwnIdentity);  // Clear Down Push Vector

        ThePrisonerAnimator.SetBool("IsWalking", false);
        ThePrisonerAnimator.SetBool("IsPushing", false);
        ThePrisonerAnimator.SetBool("IsJumping", true);
        ThePrisonerAnimator.SetBool("IsKneeling", false);
        ThePrisonerAnimator.SetBool("IsPulling", false);
        ThePrisonerAnimator.SetBool("IsClimbing", false);
    } // PrisonerState.ClimbingUp
      // ==================================================
    void SetPrisonerKneeling()
    {
        ThePrisonerState = PrisonerState.KneelingDown;

        // Switch over to Box Kneeling Collider 
        KneelingBoxJumpCollider.enabled = true;
        TheCharController.enabled = false;
        TheCharController.radius = 0.3f;

        ThePrisonerAnimator.SetBool("IsWalking", false);
        ThePrisonerAnimator.SetBool("IsPushing", false);
        ThePrisonerAnimator.SetBool("IsJumping", false);
        ThePrisonerAnimator.SetBool("IsKneeling", true);
        ThePrisonerAnimator.SetBool("IsPulling", false);
        ThePrisonerAnimator.SetBool("IsClimbing", false);
    } // PrisonerState.KneelingDown
    // ==================================================
    void SetPrisonerPullingUpCollegue()
    {
        ThePrisonerState = PrisonerState.PullingUpCollegue;
        TheCharController.radius = 0.3f;

        ThePrisonerAnimator.SetBool("IsWalking", false);
        ThePrisonerAnimator.SetBool("IsPushing", false);
        ThePrisonerAnimator.SetBool("IsJumping", false);
        ThePrisonerAnimator.SetBool("IsKneeling", false);
        ThePrisonerAnimator.SetBool("IsPulling",true);
        ThePrisonerAnimator.SetBool("IsClimbing", false);
    } // PrisonerState.PullingUpCollegue
    // ==================================================
    void SetPrisonerClimbingUp()
    {
        ThePrisonerState = PrisonerState.ClimbingUp;

        TheCharController.radius = 0.3f; 

        ThePrisonerAnimator.SetBool("IsWalking", false);
        ThePrisonerAnimator.SetBool("IsPushing", false);
        ThePrisonerAnimator.SetBool("IsJumping", false);
        ThePrisonerAnimator.SetBool("IsKneeling", false);
        ThePrisonerAnimator.SetBool("IsPulling", false);
        ThePrisonerAnimator.SetBool("IsClimbing", true);
    } // PrisonerState.PullingUpCollegue
    // ==================================================


    // =========================================================================================================
}
