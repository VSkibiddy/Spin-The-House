using UnityEngine;

/// A pocket trigger. Created automatically by RouletteWheel; don't add it by hand.
public class Pocket : MonoBehaviour
{
    public int number;
    public bool cracked;
    [HideInInspector] public RouletteWheel wheel;
    [HideInInspector] public Renderer rend;
    [HideInInspector] public Material baseMat;

    void OnTriggerStay(Collider other)
    {
        if (other.attachedRigidbody == wheel.ball) wheel.lastPocket = this;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.attachedRigidbody == wheel.ball && wheel.lastPocket == this) wheel.lastPocket = null;
    }
}
