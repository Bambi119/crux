#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class RunMenuOneShot
{
    public static void RunAllStatic()
    {
        bool ok = EditorApplication.ExecuteMenuItem("Crux/Test/Run All Static");
        Debug.Log($"[ONESHOT] ExecuteMenuItem 'Crux/Test/Run All Static' → {ok}");
    }

    public static void RunCounterFire()
    {
        bool ok = EditorApplication.ExecuteMenuItem("Crux/Test/PlaySmoke CounterFire (8s)");
        Debug.Log($"[ONESHOT] ExecuteMenuItem 'Crux/Test/PlaySmoke CounterFire (8s)' → {ok}");
    }

    public static void RunCounterFireDirect()
    {
        Debug.Log("[ONESHOT] direct call CruxCounterFireScenario.RunCounterFireScenario()");
        Crux.EditorTools.Tests.CruxCounterFireScenario.RunCounterFireScenario();
    }
}
#endif
