using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.MLAgents;


public class ExperimentControl : MonoBehaviour
{
    public enum LevelObjectives { ProximalToCrate, PushingCrate, CrateMoved,CrateAtWall, KneelingAtCrate, AbleJumpOnCrate,OneOnCrate, KneelingOnCrate, AbleToClimbCrate, BothOnCrate, KneelingAtWall, AbleToJumpWall, OneOnUpperLevel, KneelingOnWall, AbleToClimbWall, BothOnUpper,BothFacingGate,  PrisonersEscaped }
    
    // ===================================================================================
    public int ExperimentLevel;
    public LevelObjectives LevelObjective;
    public GameObject TheCrateObject;
    public GameObject Prisoner1;
    public GameObject Prisoner2;

    public bool ManualTest;

    // Displays
    public Text LevelTextDisplay;
    public Text NarrativeDisplay;
    public Text LevelCountDisplay;
    public Text PartialRewardDisplay;

    private CrateControl TheCrateController;
    private MLPrisonerControlScript Prisoner1Controller;
    private MLPrisonerControlScript Prisoner2Controller;

    // ML Agents Groups
    private SimpleMultiAgentGroup PrisonerAgentsGroup; 

    private IDictionary<int, LevelObjectives> LevelToObjectiveMap;
    bool CurrentLevelAchieved;
    //float CurrentLevelReward;
    public int PromotionCount;
    public int DemotionCount; 
    public  int DecisionCounter;
    private int MaxNumberOfDecisions = 2000;    // Consider that a Normal Prison Escape is around 125 Decision Steps
    
    private bool [] SubObjectiveAchieved;
   
    public float PartialRewards;

    private int PromotionThreshold = 12;
    private int DemotionThreshold = 40;
    private int DisplayCountDown = 0; 

    // ==========================================================
    void Awake()
    {
        TheCrateController = TheCrateObject.GetComponent<CrateControl>();

        Prisoner1Controller = Prisoner1.GetComponent<MLPrisonerControlScript>();
        Prisoner2Controller = Prisoner2.GetComponent<MLPrisonerControlScript>();

        // Set Up Level Objectives
        LevelToObjectiveMap  = new Dictionary<int, LevelObjectives>();
        LevelToObjectiveMap.Add(1, LevelObjectives.ProximalToCrate);
        LevelToObjectiveMap.Add(2, LevelObjectives.PushingCrate);
        LevelToObjectiveMap.Add(3, LevelObjectives.CrateMoved);
        LevelToObjectiveMap.Add(4, LevelObjectives.CrateAtWall);
        LevelToObjectiveMap.Add(5, LevelObjectives.KneelingAtCrate);
        LevelToObjectiveMap.Add(6, LevelObjectives.AbleJumpOnCrate);
        LevelToObjectiveMap.Add(7, LevelObjectives.OneOnCrate);
        LevelToObjectiveMap.Add(8, LevelObjectives.KneelingOnCrate);
        LevelToObjectiveMap.Add(9, LevelObjectives.AbleToClimbCrate);
        LevelToObjectiveMap.Add(10, LevelObjectives.BothOnCrate);
        LevelToObjectiveMap.Add(11, LevelObjectives.KneelingAtWall);
        LevelToObjectiveMap.Add(12, LevelObjectives.AbleToJumpWall);
        LevelToObjectiveMap.Add(13, LevelObjectives.OneOnUpperLevel);
        LevelToObjectiveMap.Add(14, LevelObjectives.KneelingOnWall);
        LevelToObjectiveMap.Add(15, LevelObjectives.AbleToClimbWall);
        LevelToObjectiveMap.Add(16, LevelObjectives.BothOnUpper);
        LevelToObjectiveMap.Add(17, LevelObjectives.BothFacingGate);
        LevelToObjectiveMap.Add(18, LevelObjectives.PrisonersEscaped);
        LevelToObjectiveMap.Add(19, LevelObjectives.PrisonersEscaped);
        LevelToObjectiveMap.Add(20, LevelObjectives.PrisonersEscaped);

    }  // Awake
    // ===================================================================================
    void Start()
    {
        ExperimentLevel = 1;
        LevelObjective = LevelToObjectiveMap[1];

        // Set Up the Prisoner Agents Group
        PrisonerAgentsGroup = new SimpleMultiAgentGroup();

        //  Register Both Prisoner Agenta
        PrisonerAgentsGroup.RegisterAgent(Prisoner1Controller);
        PrisonerAgentsGroup.RegisterAgent(Prisoner2Controller);

        NarrativeDisplay.text = "Lets Try to Escape !";
        DisplayCountDown = 200;
        PromotionCount = 0;
        DemotionCount = 0;

        SubObjectiveAchieved = new bool[22];

        ResetScenario(); 

    } // Start
    // ===================================================================================
    private void ResetScenario()
    {
        // Reset the Prisonsers
        Prisoner1Controller.ResetEscape();
        Prisoner2Controller.ResetEscape();

        // Reset the Crate
        TheCrateController.ResetScenario();

        // Reset the Group Rewards
        CurrentLevelAchieved = false;
        for (int subobjI = 0; subobjI < SubObjectiveAchieved.Length; subobjI++) SubObjectiveAchieved[subobjI] = false; 
        PartialRewards = 0.0f; 

        DecisionCounter = 0; 

       LevelTextDisplay.text = "LEVEL: " + ExperimentLevel.ToString();
       LevelCountDisplay.text = "Objective Count: " + PromotionCount.ToString();
       PartialRewardDisplay.text = "No Reward yet";

    } // ResetScenario
    // ===================================================================================
    void FixedUpdate()
    {
        if (DisplayCountDown > 0) DisplayCountDown--; 
        if (DisplayCountDown == 0) NarrativeDisplay.text = " "; 
        //  Review All Partial Sub level level Objectives to assign Partial Rewards (0.2f for ALL Goals)
        if(ExperimentLevel>1)
        {
            // Review each Previous Sub objectives
            for (int subobjI = 1; subobjI < ExperimentLevel; subobjI++)
            {
                if(!SubObjectiveAchieved[subobjI])
                {
                    if(ReviewCurrentLevelObjective(subobjI))
                    {
                        PartialRewards = PartialRewards + 0.2f;
                        if (subobjI == 4) PartialRewards = PartialRewards + 1.0f;    // Increased Reward for having the Crate at the Wall

                        SubObjectiveAchieved[subobjI] = true;
                        PartialRewardDisplay.text = "Partial Rewards: " + PartialRewards.ToString();
                    }
                }
            }
        } // ExperimentLevel>1  

        // Review Objectives Progress
        CurrentLevelAchieved = ReviewCurrentLevelObjective(ExperimentLevel);
        // ====================================
        if (CurrentLevelAchieved)
        {
            NarrativeDisplay.text = LevelToObjectiveMap[ExperimentLevel].ToString() + " Was Achieved";
            DisplayCountDown = 200;
            PromotionCount++;
            if (DemotionCount > 0) DemotionCount--;

 
            // Assign Group Rewards and End Episode()
            //=============================================
   
            // Increase the Partial Reward for Progress towards the Gate on the Upper Level
            if ((ExperimentLevel >= 17) && (BothOnUpperCheck())) PartialRewards = PartialRewards + (Prisoner1.transform.localPosition.x - 4.0f) / 5.0f + (Prisoner2.transform.localPosition.x - 4.0f) / 5.0f;

            // If 4: Currently at CrateTaWall Assign a +1.0f additional Reward  and If 18: priosners Escaped Add a +5.0f Reward 
            if (ExperimentLevel == 4) PrisonerAgentsGroup.AddGroupReward(1.0f + PartialRewards - 0.1f * (float)DecisionCounter / MaxNumberOfDecisions);
            else if(ExperimentLevel == 18) PrisonerAgentsGroup.AddGroupReward(5.0f + PartialRewards - 0.1f * (float)DecisionCounter / MaxNumberOfDecisions);
            else if (ExperimentLevel > 18) PrisonerAgentsGroup.AddGroupReward(4.8f + PartialRewards - 0.1f * (float)DecisionCounter / MaxNumberOfDecisions);
            else PrisonerAgentsGroup.AddGroupReward(0.2f + PartialRewards - 0.1f * (float)DecisionCounter / MaxNumberOfDecisions);

            // ==============================
            // A Bit of Manual Objective & Episode Management if Under Manual Control Continue And don't End Episode or Reset 
            if (ManualTest)
            {
                if(ExperimentLevel < 19) ExperimentLevel = ExperimentLevel + 1;
                LevelTextDisplay.text = "LEVEL: " + ExperimentLevel.ToString();
            } 
            else
            {
                // Review the LevelObjectiveCount and Increment the Experiment Level
                if ((PromotionCount >= PromotionThreshold) && (ExperimentLevel<18))
                {
                    ExperimentLevel = ExperimentLevel + 1;
                    PromotionCount = 0;
                    DemotionCount = 0;
                    Debug.Log(" A Promotion to Level: " + ExperimentLevel.ToString() + " Occured at Training Step: " + Academy.Instance.TotalStepCount.ToString());
                }

                // Under Normal Training Need to End the Episode and Reset the Scenario
                PrisonerAgentsGroup.EndGroupEpisode();
                ResetScenario();
            }
            // =========================

        }  // Current Level Objective Achieved
        // ==================================================
        // Excessive Decision Count Checks
        if (DecisionCounter > MaxNumberOfDecisions)
        {
            // Exceeded the Tactical Decisions - so shoud abort the Training
            NarrativeDisplay.text = "  ";

            if (PromotionCount > 0) PromotionCount--;
            DemotionCount++; 
            // Need to Check for a possible Demotion
            if (DemotionCount >= DemotionThreshold)
            {
                ExperimentLevel = ExperimentLevel - 1;
                PromotionCount = 0;
                DemotionCount = 0;
                Debug.Log(" ** An Environment Has Just been Demoted Down To: " + ExperimentLevel.ToString()+  "  Occured at Training Step: " + Academy.Instance.TotalStepCount.ToString()); 
            }

            //PrisonerAgentsGroup.AddGroupReward(PartialRewards - 2.0f - 0.25f* ExperimentLevel);       // I think we should Explicitly Penalise Running Out of Time
            PrisonerAgentsGroup.GroupEpisodeInterrupted();
 
            ResetScenario();
        }  // Excessive Number of Decisions

    } // Fixed Update
    // =================================================================================
    public void UpdateDecisionCount()
    {
        DecisionCounter++;
    }
    // ===================================================================================

    public bool ReviewCurrentLevelObjective(int ExpObjLevel)
    {
        bool CurrentGameLevelObjectiveAchieved = false;
        
        switch (ExpObjLevel)
        {
            case 1:
                {
                    if ((PushProximalToCrateCheck(Prisoner2Controller)) && (PushProximalToCrateCheck(Prisoner1Controller))) CurrentGameLevelObjectiveAchieved = true;
                    break;
                }
            case 2:
                {
                    if ((PushingCrateCheck(Prisoner2Controller)) && (PushingCrateCheck(Prisoner1Controller))) CurrentGameLevelObjectiveAchieved = true;
                    break;
                }
            case 3:
                {
                    CurrentGameLevelObjectiveAchieved = CrateHasMovedCheck();
                    break;
                }

            case 4:
                {
                    CurrentGameLevelObjectiveAchieved = CrateAtWallCheck();
                    break;
                }

            case 5:
                {
                    CurrentGameLevelObjectiveAchieved = KneelingAtCrateCheck();
                    break;
                }

            case 6:
                {
                    CurrentGameLevelObjectiveAchieved = AbleJumpOnCrateCheck();
                    break;
                }

            case 7:
                {
                    CurrentGameLevelObjectiveAchieved = OneOnCrateCheck();
                    break;
                }

            case 8:
                {
                    CurrentGameLevelObjectiveAchieved = KneelingOnCrateCheck();
                    break;
                }

            case 9:
                {
                    CurrentGameLevelObjectiveAchieved = AbleToClimbCrateCheck();
                    break;
                }

            case 10:
                {
                    CurrentGameLevelObjectiveAchieved = BothOnCrateCheck();
                    break;
                }

            case 11:
                {
                    CurrentGameLevelObjectiveAchieved = KneelingAtWallCheck();
                    break;
                }
            case 12:
                {
                    CurrentGameLevelObjectiveAchieved = AbleToJumpWallCheck();
                    break;
                }
            case 13:
                {
                    CurrentGameLevelObjectiveAchieved = OneOnUpperLevelCheck();
                    break;
                }
            case 14:
                {
                    CurrentGameLevelObjectiveAchieved = KneelingOnWallCheck();
                    break;
                }
            case 15:
                {
                    CurrentGameLevelObjectiveAchieved = AbleToClimbWallCheck();
                    break;
                }
            case 16:
                {
                    CurrentGameLevelObjectiveAchieved = BothOnUpperCheck();
                    break;
                }
            case 17:
                {
                    CurrentGameLevelObjectiveAchieved = BothOnUpperFacingGateCheck();
                    break;
                }
            case 18:
                {
                    CurrentGameLevelObjectiveAchieved = PrisonersEscapedCheck();
                    break;
                }
            case 19:
                {
                    CurrentGameLevelObjectiveAchieved = PrisonersEscapedCheck();
                    break;
                }
            case 20:
                {
                    CurrentGameLevelObjectiveAchieved = PrisonersEscapedCheck();
                    break;
                }
        }  //switch on Game level
        return CurrentGameLevelObjectiveAchieved;
    } // ReviewCurrentLevelObjective
    // ===================================================================================

    // ===================================================================================
    // Objectives Checks
    //LevelObjectives { ProximalToCrate, PushingCrate, CrateAtWall, KneelingTowardsCrate, AbleJumpOnCrate,OneOnCrate, KneelingOnCrate, AbleToClimbCrate, BothOnCrate, KneelingTowardsWall, AbleToJumpWall, OneOnUpperLevel, KneelingOnWall, AbleToClimbWall, BothOnUpper, PrisonersEscaped}
    // ===================================================================================
    bool PushProximalToCrateCheck(MLPrisonerControlScript SpecificPrisoner)
    {
        bool ObjectiveMet = false;

        // Just Check for a Specific Prisoner
        if (SpecificPrisoner.IsWithinCratePushingZone() && SpecificPrisoner.IsFacingFoward()) ObjectiveMet = true;   
          
       return ObjectiveMet; 
    } // ProximalToCrateCheck
    // ==============================================================
    bool PushingCrateCheck(MLPrisonerControlScript SpecificPrisoner)
    {
        bool ObjectiveMet = false;

        if (SpecificPrisoner.ThePrisonerState== MLPrisonerControlScript.PrisonerState.PushingCrate && SpecificPrisoner.IsFacingFoward() && (SpecificPrisoner.IsWithinCratePushingZone())) ObjectiveMet = true;

        return ObjectiveMet;
    } // PushingCrateCheck
    // ==============================================================
    
    bool CrateHasMovedCheck()
    {
        bool ObjectiveMet = false;

        if (TheCrateObject.transform.localPosition.x > 0.5f) ObjectiveMet = true;

        return ObjectiveMet;
    } // CrateHasMovedCheck
    // ==============================================================
    bool CrateAtWallCheck()
    {
        bool ObjectiveMet = false;

        if (TheCrateObject.transform.localPosition.x > 1.75f) ObjectiveMet = true;

        return ObjectiveMet;
    } // CrateAtWallCheck
    // ==============================================================

    bool KneelingAtCrateCheck()
    {
        bool ObjectiveMet = false;

        // Need to check if either Prisoner is Kneeling In front of the crate
        if (Prisoner1Controller.ThePrisonerState == MLPrisonerControlScript.PrisonerState.KneelingDown && Prisoner1Controller.IsWithinCratePushingZone() && (TheCrateObject.transform.localPosition.x > 1.75f)) ObjectiveMet = true;
        if (Prisoner2Controller.ThePrisonerState == MLPrisonerControlScript.PrisonerState.KneelingDown && Prisoner2Controller.IsWithinCratePushingZone() && (TheCrateObject.transform.localPosition.x > 1.75f)) ObjectiveMet = true;

        return ObjectiveMet;
    } // KneelingTowardsCrateCheck
    // ==============================================================
    bool AbleJumpOnCrateCheck()
    {
        bool ObjectiveMet = false;

        // Need to Check If Either Prisoner Is Able to Jump Onto Crate
        if (((Prisoner1Controller.CanJumpUpOnCrate()) || (Prisoner2Controller.CanJumpUpOnCrate())) && (TheCrateObject.transform.localPosition.x > 1.75f)) ObjectiveMet = true; 
        
        return ObjectiveMet;
    } // AbleJumpOnCrateCheck
    // ==============================================================
    bool OneOnCrateCheck()
    {
        bool ObjectiveMet = false;

        // Need to Check if either Prisoner is on the Crate
        if (((Prisoner1Controller.IsOnTheCrate()) || (Prisoner2Controller.IsOnTheCrate())) && (TheCrateObject.transform.localPosition.x > 1.75f)) ObjectiveMet = true;

        return ObjectiveMet;
    } // OneOnCrateCheck
    // ==============================================================
    bool KneelingOnCrateCheck()
    {
        bool ObjectiveMet = false;

        // If Either Prisoner is on the Crate Kneelimng Backwards
        if (Prisoner1Controller.ThePrisonerState == MLPrisonerControlScript.PrisonerState.KneelingDown  && Prisoner1Controller.IsOnTheCrate() && (TheCrateObject.transform.localPosition.x > 1.75f)) ObjectiveMet = true;
        if (Prisoner2Controller.ThePrisonerState == MLPrisonerControlScript.PrisonerState.KneelingDown  && Prisoner2Controller.IsOnTheCrate() && (TheCrateObject.transform.localPosition.x > 1.75f)) ObjectiveMet = true;

        return ObjectiveMet;
    } // KneelingOnCrateCheck
    // ==============================================================
    bool AbleToClimbCrateCheck()
    {
        bool ObjectiveMet = false;

        if (((Prisoner1Controller.CanClimbOntoCrate()) || (Prisoner2Controller.CanClimbOntoCrate())) && (TheCrateObject.transform.localPosition.x > 1.75f)) ObjectiveMet = true;

        return ObjectiveMet;
    } // AbleToClimbCrateCheck
    // ==============================================================
    bool BothOnCrateCheck()
    {
        bool ObjectiveMet = false;
        if ((Prisoner1Controller.IsOnTheCrate()) && (Prisoner2Controller.IsOnTheCrate()) && (TheCrateObject.transform.localPosition.x > 1.75f)) ObjectiveMet = true;

        return ObjectiveMet;
    } // BothOnCrateCheck
    // ==============================================================
    bool KneelingAtWallCheck()
    {
        bool ObjectiveMet = false;

        // Check if Either Prisoner is Kneeling on the Crate and the other standing On the crate
        if (Prisoner1Controller.ThePrisonerState == MLPrisonerControlScript.PrisonerState.KneelingDown && Prisoner1Controller.IsOnTheCrate() && (Prisoner1.transform.localPosition.x > 2.25f) && (TheCrateObject.transform.localPosition.x > 1.75f) && (Prisoner2Controller.ThePrisonerState == MLPrisonerControlScript.PrisonerState.Idle)) ObjectiveMet = true;
        if (Prisoner2Controller.ThePrisonerState == MLPrisonerControlScript.PrisonerState.KneelingDown && Prisoner2Controller.IsOnTheCrate() && (Prisoner2.transform.localPosition.x > 2.25f) && (TheCrateObject.transform.localPosition.x > 1.75f) && (Prisoner1Controller.ThePrisonerState == MLPrisonerControlScript.PrisonerState.Idle)) ObjectiveMet = true;

        return ObjectiveMet;
    } // KneelingTowardsWallCheck
    // ==============================================================
    bool AbleToJumpWallCheck()
    {
        bool ObjectiveMet = false;
        // Check if Eotehr Prisoner Can Jump Up to Wall
        if (((Prisoner1Controller.CanJumpUpToWall()) || (Prisoner2Controller.CanJumpUpToWall())) && (TheCrateObject.transform.localPosition.x > 1.75f)) ObjectiveMet = true;
        return ObjectiveMet;
    } // AbleToJumpWallCheck
    // ==============================================================
    bool OneOnUpperLevelCheck()
    {
        bool ObjectiveMet = false;

        // Need to Check if either Prisoner is on the Uppper Level
        if (((Prisoner1Controller.IsOnTheUpperLevel()) || (Prisoner2Controller.IsOnTheUpperLevel())) && (TheCrateObject.transform.localPosition.x > 1.75f)) ObjectiveMet = true;

        return ObjectiveMet;
    } // OneOnUpperLevelCheck
    // ==============================================================
    bool KneelingOnWallCheck()
    {
        bool ObjectiveMet = false;

        // Check if either Prisoner is on the Wall kneeling down near edge of Wall
        if ((Prisoner1Controller.ThePrisonerState == MLPrisonerControlScript.PrisonerState.KneelingDown) && Prisoner1Controller.IsOnTheUpperLevel() && (TheCrateObject.transform.localPosition.x > 1.75f) && (Prisoner1.transform.localPosition.x > 3.5f) && (Prisoner1.transform.localPosition.x < 5.0f)) ObjectiveMet = true;
        if ((Prisoner2Controller.ThePrisonerState == MLPrisonerControlScript.PrisonerState.KneelingDown) && Prisoner2Controller.IsOnTheUpperLevel() && (TheCrateObject.transform.localPosition.x > 1.75f) && (Prisoner2.transform.localPosition.x > 3.5f) && (Prisoner2.transform.localPosition.x < 5.0f)) ObjectiveMet = true;

        return ObjectiveMet;
    } // KneelingOnWallCheck
    // ==============================================================
    bool AbleToClimbWallCheck()
    {
        bool ObjectiveMet = false;

        // Check if eitehr player Can Climb Up tWall
        if (((Prisoner1Controller.CanClimbOntoWall()) || (Prisoner2Controller.CanClimbOntoWall())) && (TheCrateObject.transform.localPosition.x > 1.75f)) ObjectiveMet = true;

        return ObjectiveMet;
    } // AbleToClimbWallCheck
      // ==============================================================
    bool BothOnUpperCheck()
    {
        bool ObjectiveMet = false;

        // Check if either player is on the Upper Level
        if ((Prisoner1Controller.IsOnTheUpperLevel()) && (Prisoner2Controller.IsOnTheUpperLevel()) && (TheCrateObject.transform.localPosition.x > 1.75f)) ObjectiveMet = true;

        return ObjectiveMet;
    } // BothOnUpperCheck
      // ==============================================================
    bool BothOnUpperFacingGateCheck()
    {
        bool ObjectiveMet = false;

        // Again Confirm Both priosners are on the Upper Level
        if ((Prisoner1Controller.IsOnTheUpperLevel()) && (Prisoner2Controller.IsOnTheUpperLevel()) && (TheCrateObject.transform.localPosition.x > 1.75f))
        {
            // And that Both Priosners are Facing Forward and gretaer than +5.0
            if ((Prisoner1Controller.IsFacingFoward()) && (Prisoner1.transform.localPosition.x > 4.5f) && (Prisoner2Controller.IsFacingFoward()) && (Prisoner2.transform.localPosition.x > 4.5f))
            {
                ObjectiveMet = true;
            }
        }
        return ObjectiveMet;
    } // BothOnUpperCheck
    // ==============================================================

    bool PrisonersEscapedCheck()
    {
        bool ObjectiveMet = false;

        if ((PrisonerAtEscapeGate(Prisoner1.transform.localPosition) && PrisonerAtEscapeGate(Prisoner2.transform.localPosition)) && (TheCrateObject.transform.localPosition.x > 1.75f))
        {
            ObjectiveMet = true;

            if (ManualTest) Debug.Log(" ** A Both Prisoners Have Escaped Has occured ! **");

            NarrativeDisplay.text = " Prisoners Have Escaped !!!";
            DisplayCountDown = 200; ; 
        }

        return ObjectiveMet;
    } // PrisonersEscapedCheck
    // ==============================================================

    // ===================================================================================
    // Final Escape Checks
    bool PrisonerAtEscapeGate(Vector3 PrisonerLocalPosition)
    {
        bool PrisonerAtGate = false;
        if ((PrisonerLocalPosition.y > 2.95f) && (PrisonerLocalPosition.x > 8.5f) && (PrisonerLocalPosition.z < 1.2f) && (PrisonerLocalPosition.z > -1.2f))
        {
            PrisonerAtGate = true;
        }

        return PrisonerAtGate;
    } // PrisonerAtEscapeGate
    // ===================================================================================
   


    // ===================================================================================
}
