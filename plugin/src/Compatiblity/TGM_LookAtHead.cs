
using FistVR;
using UnityEngine;

namespace TeamsGameMode;

public class TGM_LookAtHead : MonoBehaviour
{

    void Update()
    {
        if(GM.CurrentPlayerBody != null)
            transform.LookAt(GM.CurrentPlayerBody.Head.position);
    }
}

