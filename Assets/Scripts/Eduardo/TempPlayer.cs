using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TempPlayer : MonoBehaviour
{
    public Camera playerCamera; // The camera attached to the player
    public float speed = 6f; // Movement speed of the player
    public float lookSpeed = 2f; // Speed at which the player can look around
    public float lookXLimit = 45f; // Limit for looking up and down

    public Furniture furniture;
    
    private Vector3 moveDirection = Vector3.zero; // Direction the player is moving in
    private float rotationX = 0f; // Current rotation around the X-axis (looking up and down)
    private Rigidbody rb; // Reference to the Rigidbody component of the player
    

    private bool canMove = true; // Flag to determine if the player can move

    void Start()
    {
        rb = this.GetComponent<Rigidbody>();
        Cursor.lockState = CursorLockMode.Locked; // Lock the cursor to the center of the screen
        Cursor.visible = false; // Hide the cursor
    }

    void Update()
    {
        Vector3 forward = transform.TransformDirection(Vector3.forward); // Get the forward direction relative to the player's orientation
        Vector3 right = transform.TransformDirection(Vector3.right); // Get the right direction relative to the player's orientation

        float curSpeedX = speed * Input.GetAxis("Vertical"); // Calculate the current speed in the forward/backward direction based on input
        float curSpeedY = speed * Input.GetAxis("Horizontal"); // Calculate the current speed in the left/right direction based on input
        float movementDirectionY = moveDirection.y; // Preserve the current vertical movement (e.g., gravity)
        moveDirection = (forward * curSpeedX) + (right * curSpeedY); // Combine the forward and right movement directions to get the overall movement direction

        moveDirection.y = movementDirectionY; // Restore the vertical movement component to the moveDirection vector

        rb.MovePosition(rb.position + moveDirection * Time.deltaTime); // Move the player to the new position based on the calculated movement direction and speed

        if(canMove){
            rotationX += -Input.GetAxis("Mouse Y") * lookSpeed; // Update the rotation around the X-axis based on mouse input and look speed
            rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit); // Clamp the rotation to prevent looking too far up or down
            playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0); // Apply the rotation to the camera's local rotation to look up and down
            transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeed, 0); // Rotate the player around the Y-axis based on mouse input and look speed
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            int layerMask = ~(1 << LayerMask.NameToLayer("Player"));

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            
            if (Physics.Raycast(ray, out RaycastHit hit, 10f, layerMask))
            {
                Debug.Log("Ray hit: " + hit.collider.gameObject.name);

                if (hit.collider.TryGetComponent<Furniture>(out var f))
                {
                    Debug.Log("Found Furniture component!");
                    if (f.CanInteract())
                    {
                        Debug.Log("Can interact! Opening dialogue.");
                        f.Interact();
                        Cursor.lockState = CursorLockMode.None; 
                        Cursor.visible = true;
                    }
                    else
                    {
                        Debug.Log("Furniture is done, or dialogue is already active. CanInteract() returned false.");
                    }
                }
                else
                {
                    Debug.Log("Hit an object, but it has no 'Furniture' script attached.");
                }
            }
            else
            {
                Debug.Log("Raycast hit NOTHING. Are you looking at the cube? Are you within 10 meters?");
            }
        }
    }
}
