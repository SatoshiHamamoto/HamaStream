using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class HamaStreamResyncCube : UdonSharpBehaviour
{
    public HamaStreamController Controller;

    public override void Interact()
    {
        if (Controller != null)
        {
            Controller.ResyncLocal();
        }
    }
}
