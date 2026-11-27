#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine;
using UnityEngine.Android;
#endif

internal static class MobileHaptics
{
    public static void PlayLight()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        PlayAndroid(CLOCK_TICK);
#endif
    }

    public static void PlayMedium()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        PlayAndroid(VIRTUAL_KEY);
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    // android.view.HapticFeedbackConstants
    private const int CLOCK_TICK = 4;
    private const int VIRTUAL_KEY = 1;

    private static void PlayAndroid(int feedbackConstant)
    {
        AndroidApplication.InvokeOnUIThread(() =>
        {
            using AndroidJavaObject window = AndroidApplication.currentActivity?.Call<AndroidJavaObject>("getWindow");
            using AndroidJavaObject view = window?.Call<AndroidJavaObject>("getDecorView");
            view?.Call<bool>("performHapticFeedback", feedbackConstant);
        });
    }
#endif
}
