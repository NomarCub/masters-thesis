using System;
using System.Collections;
using UnityEngine;

public static class Coroutines {
    public static IEnumerator FromAction(Action action) {
        action();
        yield return null;
    }

    public static IEnumerator DoOnNextUpdate(Action action) {
        yield return null;
        action();
    }

    public static IEnumerator DoLater(float delayInSeconds, Action action) {
        yield return new WaitForSecondsRealtime(delayInSeconds);
        action();
    }
}