using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ObjectPickup : MonoBehaviour
{
    [Header("Pickup Settings")]
    public float pickupRange = 5f;
    public float holdDistance = 2f;
    public LayerMask pickupLayer = -1; 
    public Transform holdPosition; 

    private Camera playerCamera;
    private GameObject heldObject;
    private Rigidbody heldObjectRb;
    private Vector3 originalHoldPosition;

    private PlayerInput playerInput;
    private InputAction pickupAction;

    void Awake()
    {
     
        playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera == null)
        {
           
        }

        
        playerInput = GetComponent<PlayerInput>();
        pickupAction = playerInput.actions["Pickup"]; 

        
        if (holdPosition == null)
        {
            GameObject holdPoint = new GameObject("HoldPosition");
            holdPoint.transform.SetParent(playerCamera.transform);
            holdPoint.transform.localPosition = Vector3.forward * holdDistance;
            holdPosition = holdPoint.transform;
        }

        originalHoldPosition = holdPosition.localPosition;
    }

    void Update()
    {
        if (pickupAction.WasPressedThisFrame())
        {
            if (heldObject == null)
            {
                TryPickupObject();
            }
            else
            {
                DropObject();
            }
        }

     
        if (heldObject != null)
        {
            MoveHeldObject();
        }
    }

    void TryPickupObject()
    {
        Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, pickupRange, pickupLayer))
        {
            GameObject hitObject = hit.collider.gameObject;

           
            Rigidbody rb = hitObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                PickupObject(hitObject, rb);
            }
        }
    }

    void PickupObject(GameObject obj, Rigidbody rb)
    {
        heldObject = obj;
        heldObjectRb = rb;

        // Kinematic to make object not fall
        heldObjectRb.isKinematic = true;

   
        Collider objCollider = obj.GetComponent<Collider>();
        if (objCollider != null)
        {
            objCollider.enabled = false;
        }
    }

    void DropObject()
    {
        if (heldObject != null)
        {
            // Re-enable physics
            heldObjectRb.isKinematic = false;

            // Re-enable the collider
            Collider objCollider = heldObject.GetComponent<Collider>();
            if (objCollider != null)
            {
                objCollider.enabled = true;
            }

         

            heldObject = null;
            heldObjectRb = null;
        }
    }

    void MoveHeldObject()
    {
        if (heldObject != null)
        {
           
            Vector3 targetPosition = holdPosition.position;
            heldObject.transform.position = Vector3.Lerp(heldObject.transform.position, targetPosition, Time.deltaTime * 10f);

          
            heldObject.transform.rotation = Quaternion.Lerp(heldObject.transform.rotation, holdPosition.rotation, Time.deltaTime * 5f);
        }
    }

 
}