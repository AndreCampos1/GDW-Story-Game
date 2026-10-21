using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class PlayerMovement : MonoBehaviour
{
    private Rigidbody rb;

    public int speed;

    public Vector3 Wdir = new Vector3(1,0,0);
    public Vector3 Adir = new Vector3(0,1,0);
    public Vector3 Sdir = new Vector3(0,0,-1);
    public Vector3 Ddir = new Vector3(0,-1,0);

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(KeyCode.W)){
            rb.AddForce(Wdir * speed, ForceMode.Force);
        }

        if (Input.GetKey(KeyCode.A)){
            rb.AddForce(Adir * speed, ForceMode.Force);
        }

        if (Input.GetKey(KeyCode.S)){
            rb.AddForce(Sdir * speed, ForceMode.Force);
        }

        if (Input.GetKey(KeyCode.D)){
            rb.AddForce(Ddir * speed, ForceMode.Force);
        }
    }
}
