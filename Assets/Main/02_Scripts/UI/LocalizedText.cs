using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LocalizedText : MonoBehaviour
{
    [SerializeField] private TMP_Text _targetTxt;
    [SerializeField, TextArea] private string _koreanText;
    [SerializeField, TextArea] private string _englishText;

    private void OnEnable()
    {
        GameLocalization.LanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        GameLocalization.LanguageChanged -= Refresh;
    }

    public void Refresh()
    {
        if (_targetTxt == null)
        {
            _targetTxt = GetComponent<TMP_Text>();
        }
        if (_targetTxt != null)
        {
            _targetTxt.text = GameLocalization.GetText(_koreanText, _englishText);
        }
    }
}
