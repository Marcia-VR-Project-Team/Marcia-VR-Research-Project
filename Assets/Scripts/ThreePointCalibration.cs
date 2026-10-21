using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using TMPro;

public class ThreePointRoomCalibration : MonoBehaviour
{
    [Header("assigned stuff")]
    public Transform roomRoot; // transforms entire room
    public Transform marker1; // bottom right
    public Transform marker2; // top right
    public Transform marker3; // bottom left

    [Header("Settings")]
    public float pinchThreshold = 0.02f; // amt needed to register a pinch
    public float captureCooldown = 0.75f; // time until next point can be activated
    private float lastCaptureTime = -1f;

    [Header("Text stuff")]
    public TMP_Text instructionText;   // main instruction in world space
    public TMP_Text statusText;        // confrimations
    public float statusFlashTime = 1.0f;

    public GameObject[] objectsToDestroy; //destroy objects

    private float statusUntil = 0f;

    private XRHandSubsystem handSubsystem;

    //fingertip positions
    private Vector3 realPoint1;
    private Vector3 realPoint2;
    private Vector3 realPoint3;

    //current used marker
    private int currentStep = 0;
    private bool calibrationComplete = false;

    // for color changing
    private Renderer marker1Renderer;
    private Renderer marker2Renderer;
    private Renderer marker3Renderer;

    private Color defaultColor = Color.red;
    private Color capturedColor = Color.green;

    void Start()
    {
        // Checks assignments
        if (!ValidateAssignments())
        {
            return;
        }

        marker1Renderer = marker1.GetComponent<Renderer>();
        marker2Renderer = marker2.GetComponent<Renderer>();
        marker3Renderer = marker3.GetComponent<Renderer>();

        ResetMarkers();
        RefreshInstruction();

        List<XRHandSubsystem> subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);

        if (subsystems.Count > 0)
        {
            handSubsystem = subsystems[0];
            Debug.Log("XR Hand Subsystem found.");
        }
        else
        {
            Debug.LogError("XR Hand Subsystem NOT found.");
        }

        Debug.Log("Calibration Ready. Pinch to capture Point 1.");
    }

    void Update()
    {
        if (calibrationComplete || handSubsystem == null)
        {
            return;
        }

        if (statusText != null && Time.time > statusUntil)
            statusText.text = "";

        // ----- Get RIGHT hand index tip (pointer hand) -----
        XRHand rightHand = handSubsystem.rightHand;

        if (!rightHand.isTracked)
        {
            return;
        }

        XRHandJoint rightIndexTip =
            rightHand.GetJoint(XRHandJointID.IndexTip);

        if (!rightIndexTip.TryGetPose(out Pose rightIndexPose))
        {
            return;
        }

        // ----- Get LEFT hand pinch (activation hand) -----
        XRHand leftHand = handSubsystem.leftHand;

        if (!leftHand.isTracked)
        {
            return;
        }

        XRHandJoint leftIndexTip =
            leftHand.GetJoint(XRHandJointID.IndexTip);

        XRHandJoint leftThumbTip =
            leftHand.GetJoint(XRHandJointID.ThumbTip);

        if (!leftIndexTip.TryGetPose(out Pose leftIndexPose) ||
            !leftThumbTip.TryGetPose(out Pose leftThumbPose))
        {
            return;
        }

        float pinchDistance =
            Vector3.Distance(leftIndexPose.position,
                             leftThumbPose.position);

        bool isLeftPinching = pinchDistance < pinchThreshold;

        if (isLeftPinching &&
            Time.time - lastCaptureTime > captureCooldown)
        {
            Debug.Log("Left pinch detected. Capturing RIGHT index tip.");

            CapturePoint(rightIndexPose.position);
            lastCaptureTime = Time.time;
        }
    }

    private void SetStatus(string msg)
    {
        if (statusText == null) return;
        statusText.text = msg;
        statusUntil = Time.time + statusFlashTime;
    }

    private void RefreshInstruction()
    {
        if (instructionText == null) return;

        if (calibrationComplete)
        {
            instructionText.text = "Calibration complete.";
            return;
        }

        // Instructions
        if (currentStep == 0)
        {
            instructionText.text =
                "Put both hands out in front of you with open palms.\n" +
                "Point with your RIGHT hand and place your fingertip on Corner 1.\n" +
                "Then pinch with your LEFT hand to confirm.";
        }
        else if (currentStep == 1)
        {
            instructionText.text =
                "Move to Corner 2.\n" +
                "Point with your RIGHT hand to the very tip.\n" +
                "Pinch with your LEFT hand to confirm.";
        }
        else // currentStep == 2
        {
            instructionText.text =
                "Move to Corner 3.\n" +
                "Point with your RIGHT hand to the very tip.\n" +
                "Pinch with your LEFT hand to confirm.";
        }
    }

    void CapturePoint(Vector3 capturePosition)
    {
        if (currentStep == 0)
        {
            realPoint1 = capturePosition;
            marker1Renderer.material.color = capturedColor;
            Debug.Log($"Point 1 captured: {realPoint1}");
            SetStatus("Corner 1 captured!");
        }
        else if (currentStep == 1)
        {
            realPoint2 = capturePosition;
            marker2Renderer.material.color = capturedColor;
            Debug.Log($"Point 2 captured: {realPoint2}");
            SetStatus("Corner 2 captured!");
        }
        else if (currentStep == 2)
        {
            realPoint3 = capturePosition;
            marker3Renderer.material.color = capturedColor;
            Debug.Log($"Point 3 captured: {realPoint3}");
            SetStatus("Corner 3 captured!");

            Calibrate();
            calibrationComplete = true;
            // GameManager.Instance.StartGame();
            return;
        }

        currentStep++;
        Debug.Log("Ready for Point " + (currentStep + 1));
        RefreshInstruction();
    }

    void Calibrate()
    {
        Debug.Log("Calibration started.");

 
        // 1) Get marker positions in RoomRoot's LOCAL space.
          
 
        Vector3 L1 = roomRoot.InverseTransformPoint(marker1.position);
        Vector3 L2 = roomRoot.InverseTransformPoint(marker2.position);
        Vector3 L3 = roomRoot.InverseTransformPoint(marker3.position);

        // Real-world captured positions (already world space)
        Vector3 R1 = realPoint1;
        Vector3 R2 = realPoint2;
        Vector3 R3 = realPoint3;

        // 2) Build orthonormal basis from the VIRTUAL triangle in RoomRoot local

        Vector3 vE1 = (L2 - L1).normalized;
        Vector3 vTemp = (L3 - L1).normalized;
        Vector3 vE3 = Vector3.Cross(vE1, vTemp).normalized;
        Vector3 vE2 = Vector3.Cross(vE3, vE1).normalized;


        // 3) Build orthonormal basis from the REAL triangle in world space

        Vector3 rE1 = (R2 - R1).normalized;
        Vector3 rTemp = (R3 - R1).normalized;
        Vector3 rE3 = Vector3.Cross(rE1, rTemp).normalized;
        Vector3 rE2 = Vector3.Cross(rE3, rE1).normalized;

        // -------------------------------------------------------
        // 4) Compute rotation from local basis to world basis.
        //
        //    Qv rotates standard axes -> virtual basis
        //    Qr rotates standard axes -> real basis
        //    Qnew = Qr * Inv(Qv) rotates virtual basis -> real basis
        //
        //    This is roomRoot.rotation (maps local -> world).
        // -------------------------------------------------------
        Quaternion Qv = Quaternion.LookRotation(vE1, vE3);
        Quaternion Qr = Quaternion.LookRotation(rE1, rE3);
        Quaternion Qnew = Qr * Quaternion.Inverse(Qv);

        // -------------------------------------------------------
        // 5) Apply rotation, then compute translation.
        //    After setting rotation, marker1.position changes,
        //    so we translate to pin marker1 onto R1.
        // -------------------------------------------------------
        roomRoot.rotation = Qnew;
        roomRoot.position = Vector3.zero; // reset before reading marker pos

        // Now marker1.position = Qnew * L1 (since roomRoot.position = 0)
        Vector3 translationOffset = R1 - marker1.position;
        roomRoot.position = translationOffset;

        // -------------------------------------------------------
        // 6) Verification logging
        // -------------------------------------------------------
        float err1 = Vector3.Distance(marker1.position, R1);
        float err2 = Vector3.Distance(marker2.position, R2);
        float err3 = Vector3.Distance(marker3.position, R3);

        Debug.Log("Calibration complete.");
        foreach (var obj in objectsToDestroy)
        {
            Debug.Log($"Destroying {obj.name}.");
            Destroy(obj);
        }
    }

    void ResetMarkers()
    {
        marker1Renderer.material.color = defaultColor;
        marker2Renderer.material.color = defaultColor;
        marker3Renderer.material.color = defaultColor;
    }

    bool ValidateAssignments()
    {
        if (
            roomRoot == null ||
            marker1 == null ||
            marker2 == null ||
            marker3 == null
        )
        {
            Debug.LogError("Assign RoomRoot and all markers in Inspector.");
            return false;
        }
        return true;
    }
}