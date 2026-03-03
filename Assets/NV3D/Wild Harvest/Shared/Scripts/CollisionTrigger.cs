using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace WildHarvest

{

public class CollisionTrigger : MonoBehaviour
{

bool hasEntered = false;

    private void OnTriggerEnter(Collider other)
    {

        if (other.CompareTag("Crop") && !hasEntered) //Checks Tag of collided object.
        {
            other.GetComponent<PatchController>()?.PatchGrowth(); //Triggers patch growth.
            hasEntered = true;
        }
    }

    private void OnTriggerExit(Collider other) //Resets collision event after it has left.
    {
        hasEntered = false;
    }
}
}
