using UnityEngine;

public class DoorPopupController : MonoBehaviour
{
    public GameObject popup;

    public void ShowPopup() => popup.SetActive(true);
}
