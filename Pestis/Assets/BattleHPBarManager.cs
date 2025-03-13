using UnityEngine;
using UnityEngine.UI;

public class BattleHPBarManager : MonoBehaviour
{
    public Horde.HordeController horde;
    public Slider HPSlider;
    private float full;
    private float currentfill;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void OnAwake()
    {
        full = horde.TotalHealth;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        HPSlider.value = currentfill/full;
    }
}
