using UnityEngine;
using UnityEngine.UI;

public class BattleHPBarManager : MonoBehaviour
{
    public Horde.HordeController horde;
    public Slider HPSlider;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void OnAwake()
    {
        HPSlider.maxValue= horde.TotalHealth;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        HPSlider.value = horde.TotalHealth;
    }
}
