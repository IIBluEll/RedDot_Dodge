using UnityEngine;

[DisallowMultipleComponent]
public sealed class HowToPresenter_presenter : MonoBehaviour
{
    [SerializeField] private HowToPanel_view _view;

    public void Show()
    {
        _view.Show(Application.isMobilePlatform);
    }

    public void Hide()
    {
        if (_view != null)
            _view.Hide();
    }
}
