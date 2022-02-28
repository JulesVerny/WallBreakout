using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CrateControl : MonoBehaviour
{
   
    public GameObject TheBOEnvronment;
    private float CrateSpeed = 0.25f;

    private Vector3 Prisoner1PushVector;
    private Vector3 Prisoner2PushVector;
    public Vector3 TotalPushDirection;
    public GameObject Prisoner1GO;
    public GameObject Prisoner2GO;

    private MLPrisonerControlScript Prisoner1Controller;
    private MLPrisonerControlScript Prisoner2Controller;
    // ==============================================================================
    void Awake()
    {
        Prisoner1Controller = Prisoner1GO.GetComponent<MLPrisonerControlScript>();
        Prisoner2Controller = Prisoner2GO.GetComponent<MLPrisonerControlScript>();

    } // Awake

    // ==============================================================================
    void Start()
    {
        // None
    }
    // ==============================================================================
    public void ResetScenario()
    {
        float RandomZ = Random.Range(-2.0f, 2.0f);
        transform.localPosition = new Vector3(0.0f, 2.0f, RandomZ);     // The Initial Position Relative to the Parent Experiment Environment 
        transform.localRotation = Quaternion.Euler(-90.0f, 0.0f, 90.0f);
        CrateSpeed = 0.5f;
        Prisoner1PushVector = Vector3.zero;
        Prisoner2PushVector = Vector3.zero;
        TotalPushDirection = Vector3.zero;

    } // ResetScenario

    // ==============================================================================
    // Update is called once per frame
    void Update()
    {
        
    } // Update

    // ==============================================================================
    // Fixed Update Physics
    void FixedUpdate()
    {

        // Limit the Crate Movement Distances
        if ((transform.localPosition.x > 2.5f) || (transform.localPosition.x < -4.0f) || (transform.localPosition.z < -3.5f) || (transform.localPosition.z > 3.5f)) CrateSpeed = 0.0f;
        else CrateSpeed = 0.25f;

        // Need to ensure that the Kinematic Crate is Grounded
        if (transform.position.y > 0.75f)
        {
            transform.position = transform.position + new Vector3(0.0f, -2.0f * Time.deltaTime, 0.0f);
        }

        // Vector Sum the Two Prisoner Push Vectors and Check the Resulting Maginitude is Greater than a Push Threshold,
        TotalPushDirection = Prisoner1PushVector + Prisoner2PushVector; 
        TotalPushDirection.y = 0.0f;

        // Can Only Keep Mopving Crate if Both priosners are in the correct Postion 
        if (Prisoner1Controller.IsWithinCratePushingZone() && Prisoner2Controller.IsWithinCratePushingZone())
        {
            // Can Only Move the Crate if the Magnitude is more than 1.8 (ie Both moving in the same direction Magnitude ~2.0)
            if (TotalPushDirection.magnitude > 1.8f)
            {
                // Move the Crate
                transform.position = transform.position + new Vector3(CrateSpeed * Time.deltaTime * TotalPushDirection.x, 0.0f, CrateSpeed * Time.deltaTime * TotalPushDirection.z);
            }
        }
    } // FixedUpdate
    // ==============================================================================
    public void ApplyPush(Vector3 PushDirection, int PrisonerIdentity)
    {
        // Need to Maintain Two Push vectors   - One for Each Player
        
        if (PrisonerIdentity == 1) Prisoner1PushVector = PushDirection;
        if (PrisonerIdentity == 2) Prisoner2PushVector = PushDirection;

    } // PushApplied
  
    // ==============================================================================
 
   
    // ==============================================================================
}
