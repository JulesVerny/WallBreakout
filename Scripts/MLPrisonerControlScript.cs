using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;

public class MLPrisonerControlScript : Agent
{
    // ======================================================================================================================
    // =========================================================================================================
    public enum PrisonerState { Idle, Walking, PushingCrate, KneelingDown, JumpingUp, PullingUpCollegue, ClimbingUp };
    public enum PrisonerActions { None, RotateLeft, RotateRight, WalkForward, PushCrate, KneelDown, StandUpIdle, JumpUp, ClimbUp, PullUp };

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

    private float CoPrisonerProximatoryThreshold = 1.5f;

    private float CratePushProximatoryThreshold = 2.0f;

    private CrateControl TheCrateController;
    private MLPrisonerControlScript TheCoPrisonerController;

    // Supporting Assessment Indicators 

    // Experiment Management
    public bool UnderManualControl;
    private PrisonerActions PrevAction;
    private ExperimentControl TheExperimentController;

    // ======================================================================================================================
    #region Awake, Initialisation  Start And Reset Processing
    private void Awake()
    {
        TheCharController = GetComponent<CharacterController>();
        ThePrisonerAnimator = GetComponentInChildren<Animator>();
        TheCrateController = TheCrateObject.GetComponent<CrateControl>();
        KneelingBoxJumpCollider = GetComponent<BoxCollider>();
        TheExperimentController = TheExperimentManager.GetComponent<ExperimentControl>();
        TheCoPrisonerController = CoPrisoner.GetComponent<MLPrisonerControlScript>();

    } // Awake
      // ======================================================================================================================

    public override void Initialize()
    {
        ResetEscape(); 
    }  //  Initialize

    // ======================================================================================================

    public override void OnEpisodeBegin()
    {
        ResetEscape();

    } // OnEpisodeBegin
    // ======================================================================================================================
    public void ResetEscape()
    {
        // Reset the Prisoner Back near the Start
        UnderManualControl = false;
        TheCharController.enabled = false;
        float RandomX = Random.Range(-3.0f, -2.0f);
        float RandomZ = 0.0f;
        if (OwnIdentity==1) RandomZ = Random.Range(-1.25f, -0.5f);
        else RandomZ = Random.Range(0.5f, 1.25f); 
        transform.localPosition = new Vector3(RandomX, 0.2f, RandomZ);     // The Initial Position Relative to the Parent Experiment Environment 
        transform.localRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f);
        TheCharController.enabled = true;
        KneelingBoxJumpCollider.enabled = false;

        ProposedAction = PrisonerActions.None;
        PrevAction = PrisonerActions.None;

        SetPrisonerIdle();

    } // ResetEscape
      // =====================================================================================================================
    #endregion
    // ==========================================================================================================================
    #region Observations and Action Masking
    // ======================================================================================================================
    public override void CollectObservations(VectorSensor sensor)
    {
        // Collect the Player Observations 
        // n.b Two Ray Cast Sensors { Crate, Prisoner, Wall, ExitGate, Enclosure }  
        // One at Prioner Level and the Other above Prsioner Head 

        // 5x Spatial Awareness Observations:
        sensor.AddObservation(transform.localPosition.x / 7.5f);
        sensor.AddObservation(transform.localPosition.z / 5.0f);

        float RotY = transform.localEulerAngles.y - 90.0f;
        if (RotY > 180.0f) RotY = RotY - 360.0f;
        if (RotY < -180.0f) RotY = RotY + 360.0f;
        sensor.AddObservation(RotY / 180.0f);

        sensor.AddObservation(1.75f - TheCrateObject.transform.localPosition.x / 5.0f);
        sensor.AddObservation(TheCrateObject.transform.localPosition.z / 5.0f);

        // 7x Proximal Observations
        sensor.AddObservation(PrisonersFacingEachOther());
        sensor.AddObservation(IsFacingFoward());
        sensor.AddObservation(IsOnLowerFloor());
        sensor.AddObservation(IsOnTheCrate());
        sensor.AddObservation(IsOnTheUpperLevel());
        sensor.AddObservation(InProximatoryToCoPrisoner());
        sensor.AddObservation(IsWithinCratePushingZone());

        // 6x Hot Encode The Current Player State
        sensor.AddObservation((ThePrisonerState == PrisonerState.Walking));
        sensor.AddObservation((ThePrisonerState == PrisonerState.PushingCrate));
        sensor.AddObservation((ThePrisonerState == PrisonerState.KneelingDown));
        sensor.AddObservation((ThePrisonerState == PrisonerState.JumpingUp));
        sensor.AddObservation((ThePrisonerState == PrisonerState.PullingUpCollegue));
        sensor.AddObservation((ThePrisonerState == PrisonerState.ClimbingUp));

        // 6x Action Conditionals 
        sensor.AddObservation(ActionIsAllowed(PrisonerActions.WalkForward));
        sensor.AddObservation(ActionIsAllowed(PrisonerActions.RotateLeft));
        sensor.AddObservation((ActionIsAllowed(PrisonerActions.PushCrate)));
        sensor.AddObservation(ActionIsAllowed(PrisonerActions.KneelDown));
        sensor.AddObservation(CanJumpUpOnCrate() || CanJumpUpToWall());
        sensor.AddObservation(CanClimbOntoCrate() || CanClimbOntoWall());

        // A Total of 24 Observations 

    }   // CollectObservations
        // ======================================================================================================================
        // Attempting to Mask Some of the Actions
        // As explained at  https://github.com/Unity-Technologies/ml-agents/blob/main/docs/Learning-Environment-Design-Agents.md#masking-discrete-actions
        
    //PrisonerActions { 0:None, 1:WalkForward,2:RotateLeft, 3: RotateRight, 4:PushCrate, 5:KneelDown, 6:StandUpIdle, 7:JumpUp, 8:ClimbUp, xxx: PullUp};
    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        CurrentAnimationName = ThePrisonerAnimator.GetCurrentAnimatorClipInfo(0)[0].clip.name;

        if ((CurrentAnimationName == "PullUp") || (CurrentAnimationName == "ClimbUp") || (CurrentAnimationName == "Jump"))
        {
            // Cannot Request or Perform any of the General Movement Actions: 1:WalkForward,2:RotateLeft, 3: RotateRight, 4:PushCrate, 5:KneelDown, 6:StandUpIdle
            actionMask.SetActionEnabled(0, 1, false);
            actionMask.SetActionEnabled(0, 2, false);
            actionMask.SetActionEnabled(0, 3, false);
            actionMask.SetActionEnabled(0, 4, false);
            actionMask.SetActionEnabled(0, 5, false);
            actionMask.SetActionEnabled(0, 6, false);
        }

        if(!ActionIsAllowed(PrisonerActions.WalkForward)) actionMask.SetActionEnabled(0, 1, false); // Mask Out Walk
        if (!ActionIsAllowed(PrisonerActions.RotateLeft)) actionMask.SetActionEnabled(0, 2, false); // Mask Out Rotate Left
        if (!ActionIsAllowed(PrisonerActions.RotateRight)) actionMask.SetActionEnabled(0, 3, false); // Mask Out Rotate Right

        if (!(ActionIsAllowed(PrisonerActions.PushCrate)) || !IsWithinCratePushingZone()) actionMask.SetActionEnabled(0, 4, false); // Mask Out Push Crate
        if (!ActionIsAllowed(PrisonerActions.KneelDown)) actionMask.SetActionEnabled(0, 5, false); // Mask Out Kneeling Down;

        if (!CanJumpUpOnCrate() && !CanJumpUpToWall()) actionMask.SetActionEnabled(0, 7, false);   // Mask out any Jumping
        if (!CanClimbOntoCrate() && !CanClimbOntoWall()) actionMask.SetActionEnabled(0, 8, false); // Mask out Any Climbing 

        // Both on Upper level so need positive Walking, Rotation moevments only
        if (IsOnTheUpperLevel() && transform.localPosition.x > 4.0f && TheCoPrisonerController.IsOnTheUpperLevel() &&  CoPrisoner.transform.localPosition.x > 4.0f)
        {
            // Both on Upper level, so limit to Walking, Rotation, Idle, and mask out 4:Push, 5:Kneel, 7:Jumping and 8: Climbing
            actionMask.SetActionEnabled(0, 4, false);
            actionMask.SetActionEnabled(0, 5, false);
            actionMask.SetActionEnabled(0, 7, false);
            actionMask.SetActionEnabled(0, 8, false);
        }
    }  // WriteDiscreteActionMask
       // ===========================================================================================================
    #endregion
    // ===========================================================================================================
    #region Main Action Processing
    // ===========================================================================================================
    // Main Action Processing 
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {

        // Get the Proposed Action From the Nueral Network
        ProposedAction = MappedPrisonerAction(actionBuffers.DiscreteActions[0]);

        // Get the Current Animation Name  and Progress, as this Conditions the Ability to Execute the Proposed Actions
        CurrentAnimationName = ThePrisonerAnimator.GetCurrentAnimatorClipInfo(0)[0].clip.name;
        AnnimationProgress = ThePrisonerAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime;

        // Should Always be able to Return to Idle
        if ((ProposedAction == PrisonerActions.StandUpIdle) && (ActionIsAllowed(ProposedAction))) SetPrisonerIdle();

        // Can Only Execute a New Proposed Action IF not Already within An Animation that requires Completion  
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
            if ((ThePrisonerState == PrisonerState.PushingCrate) && IsWithinCratePushingZone() && IsFacingFoward())
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
                TheCharController.stepOffset = 0.6f;   // was 1.0f
                PerformDeltaJumpMovement(JumpSpeed);
            }
            // Move Foward Across Prisoner
            if ((AnnimationProgress >= 0.3f) && (AnnimationProgress < 0.5f)) PerformForwardDeltaMovement(WalkSpeed * 5.0f);

            // Jump Again Onto Crate or Wall
            if ((AnnimationProgress >= 0.5f) && (AnnimationProgress < 0.7f)) PerformDeltaJumpMovement(JumpSpeed);

            // Final Foward Movement on top of Carte or Wall
            if ((AnnimationProgress > 0.7f) && (AnnimationProgress < 0.9)) PerformForwardDeltaMovement(WalkSpeed * 5.0f);

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
            // Note the 
            if (AnnimationProgress < 0.75f)
            {
                TheCharController.stepOffset = 1.75f;
                PerformDeltaClimbMovement(ClimbSpeed);
            }
            // Final Foward Movement on top of Crate or Wall
            if ((AnnimationProgress > 0.75f) && (AnnimationProgress < 0.95)) PerformForwardDeltaMovement(WalkSpeed * 2.5f);

            // Move Back Into and Idle Standing State and Ask the Co prisoner to do Likewise
            if ((AnnimationProgress >= 0.95f) && ActionIsAllowed(PrisonerActions.StandUpIdle))
            {
                SetPrisonerIdle();
                TheCoPrisonerController.RequestStandup();
            }
        } // ClimbUp Up Animation 
        // ===================================================================
        // If Not Jumping or Climbing Need to Ensure that the Character is Grounded
        if (!((CurrentAnimationName == "ClimbUp") || (CurrentAnimationName == "Jump"))) EnsureGrounded();

    } // OnActionReceived
      // ======================================================================================================
    public void SetUnderManualControl()
    {
        UnderManualControl = true;
    }
    public void ClearUnderManualControl()
    {
        UnderManualControl = false;
    }
    // ======================================================================================================
    PrisonerActions MappedPrisonerAction(int NNAction)
    {
        //PrisonerActions { 0:None, 1:WalkForward,2:RotateLeft, 3: RotateRight, 4:PushCrate, 5:KneelDown, 6:StandUpIdle, 7:JumpUp, 8:ClimbUp, xxx: PullUp};

        PrisonerActions RtnAction = PrisonerActions.None;
        if (NNAction == 1) RtnAction = PrisonerActions.WalkForward;
        if (NNAction == 2) RtnAction = PrisonerActions.RotateLeft;
        if (NNAction == 3) RtnAction = PrisonerActions.RotateRight;
        if (NNAction == 4) RtnAction = PrisonerActions.PushCrate;
        if (NNAction == 5) RtnAction = PrisonerActions.KneelDown;
        if (NNAction == 6) RtnAction = PrisonerActions.StandUpIdle;
        if (NNAction == 7) RtnAction = PrisonerActions.JumpUp;
        if (NNAction == 8) RtnAction = PrisonerActions.ClimbUp;

        return RtnAction;
    } // MappedPrisonerAction
    // =====================================================================================================
    public override void Heuristic(in ActionBuffers actionsOut)
    // Hueristic Manual Actions
    {
        //PrisonerActions { 0:None, 1:WalkForward,2:RotateLeft, 3: RotateRight, 4:PushCrate, 5:KneelDown, 6:StandUpIdle, 7:JumpUp, 8:ClimbUp, xxx: PullUp};

        var discreteActionsOut = actionsOut.DiscreteActions;
        discreteActionsOut[0] = 0;     // Default to None Action

        if (UnderManualControl)
        {
            if (Input.GetKey(KeyCode.UpArrow)) discreteActionsOut[0] = 1;     // Walk Foward
            if (Input.GetKey(KeyCode.LeftArrow)) discreteActionsOut[0] = 2;   // Rotate Right
            if (Input.GetKey(KeyCode.RightArrow)) discreteActionsOut[0] = 3;  // Rotate Left

            if (Input.GetKey(KeyCode.Space)) discreteActionsOut[0] = 4;         // Push Crate
            if (Input.GetKey(KeyCode.K)) discreteActionsOut[0] = 5;             // Kneel Down
            if (Input.GetKey(KeyCode.S)) discreteActionsOut[0] = 6;             // Stand Up
            if (Input.GetKey(KeyCode.J)) discreteActionsOut[0] = 7;             // Jump Up
            if (Input.GetKey(KeyCode.C)) discreteActionsOut[0] = 8;             // Climb Up
        }

        // Manual prisoner Selection
        if (Input.GetKey(KeyCode.Alpha1) && OwnIdentity == 1) UnderManualControl = true;
        if (Input.GetKey(KeyCode.Alpha1) && OwnIdentity == 2) UnderManualControl = false;

        if (Input.GetKey(KeyCode.Alpha2) && OwnIdentity == 2) UnderManualControl = true;
        if (Input.GetKey(KeyCode.Alpha2) && OwnIdentity == 1) UnderManualControl = false;

    } // Heuristic Controller
    // ==========================================================================
  
// =====================================================================================================================
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
                    if (!(CurrentAnimationName == "PullUp")) ActionIsAllowed = true;

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
                    if ((CurrentAnimationName == "Kneeling") && (AnnimationProgress > 0.975f)) ActionIsAllowed = true;
                    if (CurrentAnimationName == "PullUp") ActionIsAllowed = true;
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
    } // ActionIsAllowed  Check 
    // =====================================================================================================================
    #endregion
    // ================================================================================================================
    #region Basic Movement Stuff
    void PerformForwardDeltaMovement(float ReqFwdSpeed)
    {
        // Perfrom Normal (Forward Z Direction) Movement
        Vector3 TheDeltaMovement = transform.forward;
        TheDeltaMovement.y = 0.0f;    // To Ensure No Sky Walking !
        TheDeltaMovement = TheDeltaMovement * Time.deltaTime * ReqFwdSpeed;

        // Need to ensure that Do Not Ask for a Move whilst still Transit from Walking to Kneeling
        if(ThePrisonerState!= PrisonerState.KneelingDown) TheCharController.Move(TheDeltaMovement);
    }// PerformDeltaMovement
     // =========================================================================================
    void PerformDeltaJumpMovement(float ReqJumpSpeed)
    {
        // Jump Forward and Up  ~ 45 degree Jump Motion  
        Vector3 TheDeltaMovement = Vector3.zero; 
        if (TheCoPrisonerController.IsOnTheCrate()) TheDeltaMovement = transform.up * 3.5f + transform.forward;
        if (TheCoPrisonerController.IsOnLowerFloor()) TheDeltaMovement = transform.up * 3.0f + transform.forward;
        TheDeltaMovement = TheDeltaMovement * Time.deltaTime * ReqJumpSpeed;
        TheCharController.Move(TheDeltaMovement);
    }// PerformDeltaJumpMovement
    // =========================================================================================
    void PerformDeltaClimbMovement(float ReqClimbSpeed)
    {
        // Perfom a Climb Upwards and to the Side to try and Avoid the Other Prisoner 

        Vector3 TheDeltaMovement = Vector3.zero;
        if (TheCoPrisonerController.IsOnTheCrate()) TheDeltaMovement = 0.666f*transform.up + 1.25f * transform.right;
        if (TheCoPrisonerController.IsOnTheUpperLevel()) TheDeltaMovement = transform.up + 1.25f * transform.right;

         
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
        if (TheCharController.enabled) TheCharController.Move(GroundedMovement);
    }// EnsureGrounded
    // =========================================================================================
    #endregion
   
    // =====================================================================================================================
    #region Tactical Checks
    // ===================================================================================================
  /*  public bool InProximatoryToCrate()
    {
        bool RtnProximatory = false;
        float XDistanceToCrate = Mathf.Abs(transform.localPosition.x - TheCrateObject.transform.localPosition.x);
        if (XDistanceToCrate < CratePushProximatoryThreshold) RtnProximatory = true;
        return RtnProximatory;
    } // InProximatryToCrate
  */
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

        if ((Mathf.Abs(DeltaX) < 1.25f) && (Mathf.Abs(DeltaZ) < 1.2f) && (DeltaHeight > 0.4f) && (DeltaHeight < 1.5f))
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

        if (transform.localPosition.y > 3.0f)
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
                if ((TheCoPrisonerController.ThePrisonerState == PrisonerState.KneelingDown) && (TheCoPrisonerController.IsOnLowerFloor()))
                {
                    // Prisoner Direction to Crate and Other Prisoner Needs to be Aligned and Also In Close proximatory to other Prisoner
                    if (OtherPrisonerAlignedWithCrate() && InProximatoryToCoPrisoner()) RtnCanJumpOnCrate = true;

                }  // Direction and Proximarty Checks
            } // Current State Checks

        } // Currently On Lower Floor
        return RtnCanJumpOnCrate;
    } // CanClimOnCrate
    // ======================================================================================================
    public bool CanClimbOntoCrate()
    {
        bool RtnCanClimbOnCrate = false;

        if (IsOnLowerFloor() && IsFacingFoward())
        {
            // First Check in an Appropriate State
            if ((CurrentAnimationName == "Idle") || (CurrentAnimationName == "ClimbUp"))
            {
                // Co Prisoner Also needs to Be Kneeling Down and Can Accept the Pull Up Request (Includes Alignment and Proximatry Checks
                if ((TheCoPrisonerController.ThePrisonerState == PrisonerState.KneelingDown) && (TheCoPrisonerController.CheckRequestPullUp()))
                {
                    RtnCanClimbOnCrate = true;
                }  // Direction and Proximarty Checks
            } // Current State Checks
        } // Currently On Lower Floor

        // Debug.Log("CanClimbCrate() Check: " + RtnCanClimbOnCrate.ToString());

        return RtnCanClimbOnCrate;
    } // CanClimbOntoCrate
    // ==========================================================================================================
    private bool OtherPrisonerAlignedWithCrate()
    {
        bool AllAligned = false;

        Vector3 DirectionToOtherPrisoner = (CoPrisoner.transform.localPosition - transform.localPosition).normalized;
        Vector3 DirectionToCrate = (TheCrateObject.transform.localPosition - transform.localPosition).normalized;
        float RelativeDirection = Vector3.Dot(DirectionToOtherPrisoner, DirectionToCrate);
        if (RelativeDirection > 0.35f) AllAligned = true;

        return AllAligned;
    } // OtherPrisonerAlignedCrate
    // =========================================================================================================
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
                if ((TheCoPrisonerController.ThePrisonerState == PrisonerState.KneelingDown) && (CoPrisoner.transform.localPosition.x > 2.0f) && TheCoPrisonerController.IsOnTheCrate())
                {
                    // Prisoner Need to be facing Foward and close to each other
                    if (IsFacingFoward() && InProximatoryToCoPrisoner()) RtnCanJumpOnWall = true;

                }  // Direction and Proximarty Checks
            } // Current State Checks
        } // Is On the Crate

        return RtnCanJumpOnWall;
    } // CanJumpUpToWall
    // =====================================================================================
    public bool CanClimbOntoWall()
    {
        bool RtnCanClimbOnWall = false;

        if (IsOnTheCrate()  && IsFacingFoward())
        {
            // First Check in an Appropriate State
            if ((CurrentAnimationName == "Idle") || (CurrentAnimationName == "ClimbUp"))
            {
                // Co Prisoner Also needs to Be Kneeling Down  and Able to Pull up (n.b. inlcudes directional and proximary checks
                if ((TheCoPrisonerController.ThePrisonerState == PrisonerState.KneelingDown) && (TheCoPrisonerController.CheckRequestPullUp()))
                {
                     RtnCanClimbOnWall = true;
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
            // Can Pull Up if Close to Each Other and Facing Each other
            if (InProximatoryToCoPrisoner() && PrisonersFacingEachOther())
            {
                // Check in Reasonable Position 
                if (IsOnTheCrate()) PullUpCheckRequest = true;

                // If on Upper level, Also need to be at Wall Edge (Which should already be likley if in Proximatry to other Priosner) 
                if (IsOnTheUpperLevel() && (transform.localPosition.x > 3.25f) && (transform.localPosition.x < 5.0f)) PullUpCheckRequest = true;
            }
        }  //  Animation State check

        return PullUpCheckRequest;
    }  // CheckRequestPullUp
    // ==========================================================================================================
    public bool IsWithinCratePushingZone()
    {
        bool RtnInPushingZone = false;

        Vector3 DeltaCrateVector = TheCrateObject.transform.position - transform.position;
        float DeltaX = DeltaCrateVector.x;
        float DeltaZ = DeltaCrateVector.z;

        if ((DeltaX>1.0f) && (Mathf.Abs(DeltaZ) < 1.0f) )
        {
            RtnInPushingZone = true;
        }
        return RtnInPushingZone;
    }  //  IsWithinCratePushingZone
    // ==========================================================================================================

    public bool IsFacingFoward()
    {
        bool RtnFacingFoward = false;
        float DirectionToPositiveXAxis = Vector3.Dot(transform.forward, Vector3.right);
        if (DirectionToPositiveXAxis > 0.35f) RtnFacingFoward = true;
        return RtnFacingFoward;
    }  //  IsFacingFoward
    // ==========================================================================================================
   
    public bool PrisonersFacingEachOther()
    {
        bool RtnFacingEachOther = false;

        float RelativeDirection = Vector3.Dot(transform.forward, -CoPrisoner.transform.forward); 
        if (RelativeDirection > 0.35f) RtnFacingEachOther = true;

        return RtnFacingEachOther;
    }  //  PrisonersFacingEachOther
    // ==========================================================================================================
    public void PerformThePullUp()
    {
        if (ActionIsAllowed(PrisonerActions.PullUp)) SetPrisonerPullingUpCollegue();

    } // RequestPullUp
    // ===========================================================================================================
    public void RequestStandup()
    {
        if ((CurrentAnimationName == "Kneeling") && ActionIsAllowed(PrisonerActions.StandUpIdle))
        {
            SetPrisonerIdle();
        }
    }  // RequestStandup
    // =========================================================================================================
    #endregion
    // ======================================================================================================================
    #region Main Prisoner States and Animation Controls
    // ============================================================================
    // Animation Controller States
    // PrisonerState { Idle, Walking, PushingCrate, KneelingDown, ClimbingUp, PullingUpCollegue };
    void SetPrisonerIdle()
    {
        ThePrisonerState = PrisonerState.Idle;

        // Switch into Default Character Controller
        KneelingBoxJumpCollider.enabled = false;
        TheCharController.enabled = true;
        TheCharController.stepOffset = 0.25f;
        TheCharController.radius = 0.3f;
        TheCrateController.ApplyPush(Vector3.zero, OwnIdentity);  // Clear Down any Push Vector

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
        TheCharController.radius = 0.3f;
        TheCrateController.ApplyPush(Vector3.zero, OwnIdentity);  // Clear Down any Push Vector

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
        TheCharController.radius = 0.3f;

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
        TheCrateController.ApplyPush(Vector3.zero, OwnIdentity);  // Clear Down any Push Vector

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
        ThePrisonerAnimator.SetBool("IsPulling", true);
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

    #endregion 
    // ======================================================================================================================
}  // MLPrisonerControlScript
