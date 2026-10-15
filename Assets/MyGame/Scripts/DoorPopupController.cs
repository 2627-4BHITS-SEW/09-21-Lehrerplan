using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hängt an einer Tür im Gangplan. Klick auf die Tür (Button -> ShowPopup) öffnet das Popup
/// und lädt per HtlTeacherService die Lehrer:innen und die Telefonnummer des Raums.
/// </summary>
public class DoorPopupController : MonoBehaviour
{
    public GameObject popup;

    public string roomNumber;

    public float popupWidth = 380f;

    static DoorPopupController openController;

    RectTransform popupRect;
    RectTransform canvasRect;
    Text popupText;
    bool loading;

    void Awake()
    {
        if (string.IsNullOrEmpty(roomNumber))
            roomNumber = HtlTeacherService.NormalizeRoom(name) ?? name;

        popupRect = (RectTransform)popup.transform;
        popupText = popup.GetComponentInChildren<Text>(true);
        popupText.supportRichText = true;
        popupText.alignment = TextAnchor.UpperLeft;
        popupText.horizontalOverflow = HorizontalWrapMode.Wrap;
        popupText.verticalOverflow = VerticalWrapMode.Overflow;
        popupText.rectTransform.offsetMin = new Vector2(12, 10);
        popupText.rectTransform.offsetMax = new Vector2(-12, -10);

        // Popup direkt unter den Canvas hängen, damit es über allen Räumen gezeichnet wird
        canvasRect = (RectTransform)GetComponentInParent<Canvas>().rootCanvas.transform;
        popupRect.SetParent(canvasRect, true);
        popupRect.anchorMin = popupRect.anchorMax = popupRect.pivot = new Vector2(0.5f, 0.5f);

        // Klick auf das Popup schließt es
        if (!popup.TryGetComponent(out Button closeButton))
            closeButton = popup.AddComponent<Button>();
        closeButton.transition = Selectable.Transition.None;
        closeButton.onClick.AddListener(HidePopup);

        popup.SetActive(false);
    }

    public void ShowPopup()
    {
        // Zweiter Klick auf dieselbe Tür schließt das Popup wieder
        if (openController == this)
        {
            HidePopup();
            return;
        }
        if (openController != null) openController.HidePopup();
        openController = this;

        popup.SetActive(true);
        popupRect.SetAsLastSibling();
        SetText($"<b>Raum {roomNumber}</b>\n\nLade Daten von htl-salzburg.ac.at ...");

        if (!loading)
        {
            loading = true;
            StartCoroutine(HtlTeacherService.LoadTeachersInRoom(roomNumber, ShowTeachers, ShowError));
        }
    }

    public void HidePopup()
    {
        popup.SetActive(false);
        if (openController == this) openController = null;
    }

    void ShowTeachers(List<TeacherInfo> teachers)
    {
        loading = false;
        var sb = new StringBuilder();
        sb.Append("<b>Raum ").Append(roomNumber).Append("</b>\n");

        if (teachers.Count == 0)
        {
            sb.Append("\nKeine Lehrer:innen in diesem Raum gefunden.");
            SetText(sb.ToString());
            return;
        }

        List<string> extensions = teachers.Select(t => t.PhoneExtension).Where(e => e != null).Distinct().ToList();
        sb.Append("Tel: ");
        sb.Append(extensions.Count > 0 ? string.Join(", ", extensions.Select(HtlTeacherService.FormatPhone)) : "unbekannt");
        sb.Append("\n\n<b>Lehrer:innen:</b>");

        foreach (TeacherInfo teacher in teachers)
        {
            sb.Append("\n• ").Append(teacher.Name);
            // Nur wenn im Raum verschiedene Durchwahlen existieren, pro Person anzeigen
            if (extensions.Count > 1 && teacher.PhoneExtension != null)
                sb.Append("  (DW ").Append(teacher.PhoneExtension).Append(')');
        }
        SetText(sb.ToString());
    }

    void ShowError(string message)
    {
        loading = false;
        SetText($"<b>Raum {roomNumber}</b>\n\n<color=#B00000>{message}</color>");
    }

    void SetText(string text)
    {
        popupText.text = text;

        // Popup-Höhe an den Text anpassen
        popupRect.sizeDelta = new Vector2(popupWidth, popupRect.sizeDelta.y);
        float padding = -popupText.rectTransform.sizeDelta.y;
        popupRect.sizeDelta = new Vector2(popupWidth, popupText.preferredHeight + padding);

        PositionNextToDoor();
    }

    /// <summary>Popup über der Tür platzieren (oder darunter, falls oben kein Platz) und im Bild halten.</summary>
    void PositionNextToDoor()
    {
        const float gap = 10f;
        var door = (RectTransform)transform;
        Rect canvas = canvasRect.rect;
        Vector2 size = popupRect.rect.size;
        Vector2 doorPos = canvasRect.InverseTransformPoint(door.position);
        float doorHalfHeight = door.rect.height / 2;

        float y = doorPos.y + doorHalfHeight + gap + size.y / 2;
        if (y + size.y / 2 > canvas.yMax)
            y = doorPos.y - doorHalfHeight - gap - size.y / 2;

        float x = Mathf.Clamp(doorPos.x, canvas.xMin + size.x / 2, canvas.xMax - size.x / 2);
        y = Mathf.Clamp(y, canvas.yMin + size.y / 2, canvas.yMax - size.y / 2);
        popupRect.localPosition = new Vector3(x, y, 0);
    }
}
