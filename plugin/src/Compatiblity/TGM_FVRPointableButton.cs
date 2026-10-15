using FistVR;
using UnityEngine;

public class TGM_FVRPointableButton : FVRPointableButton
{

    public void OnValidate()
    {
        return;
        BoxCollider box = GetComponent<BoxCollider>();

        if (box != null)
        {

            RectTransform rect = GetComponent<RectTransform>();
            box.size = new Vector3(rect.sizeDelta.x, rect.sizeDelta.y, 1);
        }
    }
}
