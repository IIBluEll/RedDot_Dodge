using System;
using UnityEngine;

public enum GAME_LANGUAGE { KOREAN, ENGLISH }

public static class GameLocalization
{
    public const string LANGUAGE_KEY = "RedDotDodge.Settings.Language.v1";

    private static GAME_LANGUAGE s_language;

    public static GAME_LANGUAGE Language => s_language;
    public static event Action LanguageChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Initialize()
    {
        LanguageChanged = null;
        s_language = ReadSavedLanguage();
    }

    public static GAME_LANGUAGE ReadSavedLanguage()
    {
        return PlayerPrefs.GetInt(LANGUAGE_KEY, -1) switch
        {
            (int)GAME_LANGUAGE.KOREAN => GAME_LANGUAGE.KOREAN,
            (int)GAME_LANGUAGE.ENGLISH => GAME_LANGUAGE.ENGLISH,
            _ => GetDefaultLanguage()
        };
    }

    private static GAME_LANGUAGE GetDefaultLanguage()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return GAME_LANGUAGE.ENGLISH;
#else
        return Application.systemLanguage == SystemLanguage.Korean ? GAME_LANGUAGE.KOREAN : GAME_LANGUAGE.ENGLISH;
#endif
    }

    public static void SetLanguage(GAME_LANGUAGE language)
    {
        GAME_LANGUAGE next = language == GAME_LANGUAGE.ENGLISH ? GAME_LANGUAGE.ENGLISH : GAME_LANGUAGE.KOREAN;
        if (s_language == next)
        {
            return;
        }
        s_language = next;
        LanguageChanged?.Invoke();
    }

    public static string GetText(string korean, string english)
    {
        return s_language == GAME_LANGUAGE.ENGLISH ? english : korean;
    }
}
