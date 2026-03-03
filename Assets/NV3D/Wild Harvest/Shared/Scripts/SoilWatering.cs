using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace WildHarvest

{

public class SoilWatering : MonoBehaviour
{

  private bool soilBool = false;

  private void OnMouseDown()
  {
    WaterSoil();
  }

  public void WaterSoil()
  {
    soilBool = !soilBool;
    Renderer renderer = GetComponent<Renderer>();
    Material material = renderer.material;
    material.SetInt("_Wet", soilBool ? 1 : 0);
  }

  private void OnMouseOver()
  {
    Renderer renderer = GetComponent<Renderer>();
    Material material = renderer.material;
    material.SetColor("_Emission", Color.white * 0.1f);
  }

  private void OnMouseExit()
  {
    Renderer renderer = GetComponent<Renderer>();
    Material material = renderer.material;
    material.SetColor("_Emission", Color.black);
  }


}
}
