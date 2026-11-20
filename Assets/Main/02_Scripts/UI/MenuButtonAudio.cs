using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MenuButtonAudio : MonoBehaviour
{
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private Button[] _menuBtns = System.Array.Empty<Button>();

    private void Awake()
    {
        if (_audioSource != null)
        {
            _audioSource.ignoreListenerPause = true;
        }
    }

    private void OnEnable()
    {
        foreach (Button menuBtn in _menuBtns)
        {
            if (menuBtn != null)
            {
                menuBtn.onClick.AddListener(OnMenuButtonClicked);
            }
        }
    }

    private void OnDisable()
    {
        foreach (Button menuBtn in _menuBtns)
        {
            if (menuBtn != null)
            {
                menuBtn.onClick.RemoveListener(OnMenuButtonClicked);
            }
        }
        if (_audioSource != null)
        {
            _audioSource.Stop();
        }
    }

    private void OnMenuButtonClicked()
    {
        if (_audioSource != null && _audioSource.clip != null)
        {
            _audioSource.Play();
        }
    }
}
