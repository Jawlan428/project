using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WildHarvest

{

public class PatchController : MonoBehaviour
{
    [SerializeField] private List<GameObject> growthStages;
    [SerializeField] private GameObject bunch;
    [SerializeField] private GameObject leafParticles;
    [SerializeField] private int harvestStage = 4;
    [SerializeField] private int harvestAmount = 5;
    [SerializeField] private bool mouseDownTrigger = true;
    [SerializeField] private bool timerTrigger = true;
    [SerializeField] private float growthTimerInterval = 5f;
    [SerializeField] private float initialDelay = 0f;


    private float t = 0.0f;
    private int listIndex;
    private Animator plantAnimator;


    // On click trigger.
    private void OnMouseDown()
    {
      if (mouseDownTrigger)
      {
      PatchGrowth();
      }
    }


    private void Start()
    {
      StartCoroutine(GrowthTimer());
      plantAnimator = GetComponentInChildren<Animator>();
    }



    private IEnumerator GrowthTimer()
    {

      yield return new WaitForSeconds(initialDelay);

      while (true)
          {
            if (timerTrigger)
            {
              t += Time.deltaTime;
              if (t >= growthTimerInterval)
              {
                PatchGrowth();
                t = 0.0f;
              }
            }
              yield return null;
          }
    }


    public void PatchGrowth()
    {
      // Play plant reaction animation
      plantAnimator.Play("React");

      // Swaps out to new mesh
      growthStages[listIndex].SetActive(false);
      listIndex = (listIndex + 1) % growthStages.Count;
      growthStages[listIndex].SetActive(true);

      // Call harvest function
      if (listIndex == harvestStage)
      {
        Harvest();
      }

    }


    public void Harvest()
    {
      //Reset Leaf Leaf Particles.
      leafParticles.SetActive(false);

      //Spawn bunches
      for (int i = 0; i < harvestAmount; i++)
      {
          Vector3 objectPosition = transform.position;

          Instantiate(bunch, objectPosition, Quaternion.Euler(0, Random.Range(0, 360), 0));
      }

      //Spawn Leaf Particles
      leafParticles.SetActive(true);

    }

    // Mouse over highlighting
    private void OnMouseOver()
    {
      Renderer renderer = GetComponentInChildren<Renderer>();
      Material material = renderer.material;
      material.SetColor("_Emission", Color.white * 0.2f);
    }

    private void OnMouseExit()
    {
      Renderer renderer = GetComponentInChildren<Renderer>();
      Material material = renderer.material;
      material.SetColor("_Emission", Color.black);
    }

}
}
