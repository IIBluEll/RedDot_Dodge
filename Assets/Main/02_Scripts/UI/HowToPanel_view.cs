using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class HowToPanel_view : MonoBehaviour
{
    [SerializeField] private TMP_Text _platformTxt;
    [SerializeField] private TMP_Text _controlsTxt;
    [SerializeField] private Button _closeBtn;

    public void Show(bool useTilt)
    {
        _platformTxt.text = useTilt ? "Android 조작" : "PC 조작";
        _controlsTxt.text = useTilt
            ? "기기를 기울여 이동합니다. 시작할 때 편한 자세를 잡아 주세요.\n화면의 일시정지 버튼에서 설정과 자세 재보정을 사용할 수 있습니다."
            : "마우스 커서를 향해 이동합니다. 적을 피해 아이템에 닿아 공격하세요.\nEsc 또는 화면의 일시정지 버튼으로 게임을 멈출 수 있습니다.";
        gameObject.SetActive(true);
        _closeBtn.Select();
    }

    public void Hide()
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
            && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
            EventSystem.current.SetSelectedGameObject(null);
        gameObject.SetActive(false);
    }
}
