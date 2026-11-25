using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class HowToPanel_view : MonoBehaviour
{
    [SerializeField] private Button _closeBtn;

    public void Show()
    {
        gameObject.SetActive(true);
        _closeBtn.Select();
    }

    public void Hide()
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
            && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
        gameObject.SetActive(false);
    }
}
