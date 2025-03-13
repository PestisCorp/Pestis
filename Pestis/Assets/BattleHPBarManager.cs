using UnityEngine;
using UnityEngine.UI;

public class BattleHPBarManager : MonoBehaviour
{
    public Horde.HordeController horde;
    public Slider HPSlider;
    //private float full;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void OnEnable()
    {
        HPSlider.maxValue = horde.TotalHealth;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        //Debug.Log(horde.name+" " + HPSlider.maxValue + "  "+ horde.TotalHealth);
        HPSlider.value = horde.TotalHealth;
    }
}
