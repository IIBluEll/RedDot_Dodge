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
        return PlayerPrefs.GetInt(LANGUAGE_KEY, 0) == (int)GAME_LANGUAGE.ENGLISH ? GAME_LANGUAGE.ENGLISH : GAME_LANGUAGE.KOREAN;
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
