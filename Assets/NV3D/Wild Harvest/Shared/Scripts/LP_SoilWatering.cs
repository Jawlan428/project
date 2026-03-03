using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace WildHarvest

{

public class LP_SoilWatering : MonoBehaviour
{

  private bool soilBool = false;
  private Color dryColor;
  private Color wetColor;


  private void Start()
      {
          Renderer renderer = GetComponent<Renderer>();
          if (renderer != null && renderer.material != null)
          {
              Material material = renderer.material;
              dryColor = material.GetColor("_BaseColor"); // Get the current color
              wetColor = dryColor * 0.8f; // Slightly darker version of dryColor
          }
          else
          {
              Debug.LogError("Renderer or material not found on the object!");
          }
      }


  private void OnMouseDown()
  {
    WaterSoil();
  }

  public void WaterSoil()
  {
    // Wet/Dry toggle function.
    soilBool = !soilBool;
    Renderer renderer = GetComponent<Renderer>();
    Material material = renderer.material;

    if (soilBool==true)
    {
      material.SetColor("_BaseColor", wetColor);
    }
    else
    {
      material.SetColor("_BaseColor", dryColor);
    }
  }

  private void OnMouseOver()
  {

    Renderer renderer = GetComponent<Renderer>();
    Material material = renderer.material;
    material.EnableKeyword("_EMISSION");
    material.SetColor("_EmissionColor", Color.white * 0.05f);
  }

  private void OnMouseExit()
  {
    Renderer renderer = GetComponent<Renderer>();
    Material material = renderer.material;
    material.SetColor("_EmissionColor", Color.black);
    material.DisableKeyword("_EMISSION");
  }


}
}
