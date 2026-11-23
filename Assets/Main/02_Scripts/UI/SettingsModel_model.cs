using UnityEngine;

public sealed class SettingsModel_model
{
    private const string LEGACY_VOLUME_KEY = "RedDotDodge.Settings.Volume.v1";
    private const string LEGACY_MUTE_KEY = "RedDotDodge.Settings.Muted.v1";
    private const string TILT_KEY = "RedDotDodge.Settings.TiltSensitivity.v1";
    private const string JOYSTICK_KEY = "RedDotDodge.Settings.UseJoystick.v1";
    private const string JOYSTICK_SENSITIVITY_KEY = "RedDotDodge.Settings.JoystickSensitivity.v1";
    private const string BGM_KEY = "RedDotDodge.Settings.BgmVolume.v1";
    private const string SFX_KEY = "RedDotDodge.Settings.SfxVolume.v1";

    public float BgmVolume { get; private set; }
    public float SfxVolume { get; private set; }
    public float TiltSensitivity { get; private set; }
    public float JoystickSensitivity { get; private set; }
    public bool UseJoystick { get; private set; }
    public GAME_LANGUAGE Language { get; private set; }

    public SettingsModel_model()
    {
        float previousVolume = PlayerPrefs.GetInt(LEGACY_MUTE_KEY, 0) == 1 ? 0f : ReadFloat(LEGACY_VOLUME_KEY, 1f, 0f, 1f);
        BgmVolume = ReadFloat(BGM_KEY, previousVolume, 0f, 1f);
        SfxVolume = ReadFloat(SFX_KEY, previousVolume, 0f, 1f);
        TiltSensitivity = ReadFloat(TILT_KEY, 1f, 0.5f, 2f);
        JoystickSensitivity = ReadFloat(JOYSTICK_SENSITIVITY_KEY, 1f, 0.5f, 2f);
        UseJoystick = PlayerPrefs.GetInt(JOYSTICK_KEY, 0) == 1;
        Language = GameLocalization.ReadSavedLanguage();
    }

    public void SetBgmVolume(float volume)
    {
        BgmVolume = Mathf.Clamp01(volume);
    }

    public void SetSfxVolume(float volume)
    {
        SfxVolume = Mathf.Clamp01(volume);
    }

    public void SetTiltSensitivity(float sensitivity)
    {
        TiltSensitivity = Mathf.Clamp(sensitivity, 0.5f, 2f);
    }

    public void SetJoystickSensitivity(float sensitivity)
    {
        JoystickSensitivity = Mathf.Clamp(sensitivity, 0.5f, 2f);
    }

    public void ToggleLanguage()
    {
        Language = Language == GAME_LANGUAGE.KOREAN ? GAME_LANGUAGE.ENGLISH : GAME_LANGUAGE.KOREAN;
    }

    public void ToggleMobileControls()
    {
        UseJoystick = !UseJoystick;
    }

    public void Save()
    {
        PlayerPrefs.SetFloat(BGM_KEY, BgmVolume);
        PlayerPrefs.SetFloat(SFX_KEY, SfxVolume);
        PlayerPrefs.SetFloat(TILT_KEY, TiltSensitivity);
        PlayerPrefs.SetFloat(JOYSTICK_SENSITIVITY_KEY, JoystickSensitivity);
        PlayerPrefs.SetInt(JOYSTICK_KEY, UseJoystick ? 1 : 0);
        PlayerPrefs.SetInt(GameLocalization.LANGUAGE_KEY, (int)Language);
        PlayerPrefs.Save();
    }

    private static float ReadFloat(string key, float defaultValue, float minimum, float maximum)
    {
        float value = PlayerPrefs.GetFloat(key, defaultValue);
        return float.IsNaN(value) || float.IsInfinity(value) ? defaultValue : Mathf.Clamp(value, minimum, maximum);
    }
}
