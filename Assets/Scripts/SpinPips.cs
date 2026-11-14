using UnityEngine;
using UnityEngine.UI;

/// Row of small bars under "SPINS LEFT": lit = spins remaining, dim = used, hidden = not available this deadline.
public class SpinPips : MonoBehaviour
{
    public Image[] pips;                                  // drag the 6 bar Images in, left to right
    public Color lit = new Color(1f, 0.23f, 0.14f);       // LED red
    public Color unlit = new Color(0.18f, 0.05f, 0.04f);  // dark, "switched off"

    public void Show(int left, int max)
    {
        for (int i = 0; i < pips.Length; i++)
        {
            pips[i].gameObject.SetActive(i < max);
            pips[i].color = i < left ? lit : unlit;
        }
    }
}
