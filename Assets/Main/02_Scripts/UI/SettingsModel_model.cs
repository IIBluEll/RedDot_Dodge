using UnityEngine;

public sealed class SettingsModel_model
{
    private const string VOLUME_KEY = "RedDotDodge.Settings.Volume.v1";
    private const string MUTE_KEY = "RedDotDodge.Settings.Muted.v1";
    private const string TILT_KEY = "RedDotDodge.Settings.TiltSensitivity.v1";

    public float Volume { get; private set; }
    public bool IsMuted { get; private set; }
    public float TiltSensitivity { get; private set; }

    public SettingsModel_model()
    {
        Volume = ReadFloat(VOLUME_KEY, 1f, 0f, 1f);
        IsMuted = PlayerPrefs.GetInt(MUTE_KEY, 0) == 1;
        TiltSensitivity = ReadFloat(TILT_KEY, 1f, 0.5f, 2f);
    }

    public void SetVolume(float volume)
    {
        Volume = Mathf.Clamp01(volume);
    }

    public void ToggleMute()
    {
        IsMuted = !IsMuted;
    }

    public void SetTiltSensitivity(float sensitivity)
    {
        TiltSensitivity = Mathf.Clamp(sensitivity, 0.5f, 2f);
    }

    public void Save()
    {
        PlayerPrefs.SetFloat(VOLUME_KEY, Volume);
        PlayerPrefs.SetInt(MUTE_KEY, IsMuted ? 1 : 0);
        PlayerPrefs.SetFloat(TILT_KEY, TiltSensitivity);
        PlayerPrefs.Save();
    }

    private static float ReadFloat(string key, float defaultValue, float minimum, float maximum)
    {
        float value = PlayerPrefs.GetFloat(key, defaultValue);
        return float.IsNaN(value) || float.IsInfinity(value) ? defaultValue : Mathf.Clamp(value, minimum, maximum);
    }
}
