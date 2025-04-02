#if UNITY_EDITOR
#define VERBOSE
#endif

#if UNITY_EDITOR
#define VERBOSE_EXTRA
#endif

using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Internal;
using Object = UnityEngine.Object;

public static class Debug
{
    //
    // Summary:
    //     Opens or closes developer console.
    public static bool developerConsoleVisible
    {
        get => UnityEngine.Debug.developerConsoleVisible;
        set => UnityEngine.Debug.developerConsoleVisible = value;
    }

    //
    // Summary:
    //     In the Build Settings dialog there is a check box called "Development Build".
    public static bool isDebugBuild => UnityEngine.Debug.isDebugBuild;

    //
    // Summary:
    //     Assert the condition.
    [Conditional("UNITY_ASSERTIONS")]
    public static void Assert(bool condition)
    {
        UnityEngine.Debug.Assert(condition);
    }

    //
    // Summary:
    //     Assert the condition.
    [Conditional("UNITY_ASSERTIONS")]
    public static void Assert(bool condition, string message)
    {
        UnityEngine.Debug.Assert(condition, message);
    }

    public static void Break()
    {
        UnityEngine.Debug.Break();
    }

    public static void ClearDeveloperConsole()
    {
        UnityEngine.Debug.ClearDeveloperConsole();
    }

    public static void DebugBreak()
    {
        UnityEngine.Debug.DebugBreak();
    }

    //
    // Summary:
    //     Draws a line between specified start and end points.
    public static void DrawLine(Vector3 start, Vector3 end)
    {
        UnityEngine.Debug.DrawLine(start, end);
    }

    //
    // Summary:
    //     Draws a line between specified start and end points.
    public static void DrawLine(Vector3 start, Vector3 end, Color color)
    {
        UnityEngine.Debug.DrawLine(start, end, color);
    }

    //
    // Summary:
    //     Draws a line between specified start and end points.
    public static void DrawLine(Vector3 start, Vector3 end, Color color, float duration)
    {
        UnityEngine.Debug.DrawLine(start, end, color, duration);
    }

    //
    // Summary:
    //     Draws a line between specified start and end points.
    public static void DrawLine(Vector3 start, Vector3 end, [DefaultValue("Color.white")] Color color,
        [DefaultValue("0.0f")] float duration, [DefaultValue("true")] bool depthTest)
    {
        UnityEngine.Debug.DrawLine(start, end, color, duration, depthTest);
    }

    //
    // Summary:
    //     Draws a line from start to start + dir in world coordinates.
    public static void DrawRay(Vector3 start, Vector3 dir)
    {
        UnityEngine.Debug.DrawRay(start, dir);
    }

    //
    // Summary:
    //     Draws a line from start to start + dir in world coordinates.
    public static void DrawRay(Vector3 start, Vector3 dir, Color color)
    {
        UnityEngine.Debug.DrawRay(start, dir, color);
    }

    //
    // Summary:
    //     Draws a line from start to start + dir in world coordinates.
    public static void DrawRay(Vector3 start, Vector3 dir, Color color, float duration)
    {
        UnityEngine.Debug.DrawRay(start, dir, color, duration);
    }

    //
    // Summary:
    //     Draws a line from start to start + dir in world coordinates.
    public static void DrawRay(Vector3 start, Vector3 dir, [DefaultValue("Color.white")] Color color,
        [DefaultValue("0.0f")] float duration, [DefaultValue("true")] bool depthTest)
    {
        UnityEngine.Debug.DrawRay(start, dir, color, duration, depthTest);
    }

    [Conditional("VERBOSE_EXTRA")]
    public static void LogInfo(object message, Object context = null)
    {
        UnityEngine.Debug.Log(message, context);
    }

    [Conditional("VERBOSE_EXTRA")]
    public static void LogInfoCat(params object[] args)
    {
        UnityEngine.Debug.Log(string.Concat(args));
    }

    //
    // Summary:
    //     Logs message to the Unity Console.
    [Conditional("UNITY_EDITOR")]
    public static void Log(object message, Object context = null)
    {
        UnityEngine.Debug.Log(message, context);
    }

    //
    // Summary:
    //     Logs message to the Unity Console.
    [Conditional("VERBOSE")]
    public static void LogCat(params object[] args)
    {
        UnityEngine.Debug.Log(string.Concat(args));
    }

    //
    // Summary:
    //     A variant of Debug.Log that logs an error message to the console.
    public static void LogError(object message, Object context = null)
    {
        UnityEngine.Debug.LogError(message, context);
    }

    //
    // Summary:
    //     A variant of Debug.Log that logs an error message to the console.
    public static void LogErrorCat(params object[] args)
    {
        UnityEngine.Debug.LogError(string.Concat(args));
    }

    //
    // Summary:
    //     Logs a formatted error message to the Unity console.
    public static void LogErrorFormat(string format, params object[] args)
    {
        UnityEngine.Debug.LogErrorFormat(format, args);
    }

    //
    // Summary:
    //     Logs a formatted error message to the Unity console.
    public static void LogErrorFormat(Object context, string format, params object[] args)
    {
        UnityEngine.Debug.LogErrorFormat(context, format, args);
    }

    //
    // Summary:
    //     A variant of Debug.Log that logs an error message to the console.
    public static void LogException(Exception exception)
    {
        UnityEngine.Debug.LogException(exception);
    }

    //
    // Summary:
    //     A variant of Debug.Log that logs an error message to the console.
    public static void LogException(Exception exception, Object context)
    {
        UnityEngine.Debug.LogException(exception, context);
    }

    //
    // Summary:
    //     Logs a formatted message to the Unity Console.
    [Conditional("VERBOSE")]
    public static void LogFormat(string format, params object[] args)
    {
        UnityEngine.Debug.LogFormat(format, args);
    }

    //
    // Summary:
    //     Logs a formatted message to the Unity Console.
    [Conditional("VERBOSE")]
    public static void LogFormat(Object context, string format, params object[] args)
    {
        UnityEngine.Debug.LogFormat(context, format, args);
    }

    //
    // Summary:
    //     A variant of Debug.Log that logs a warning message to the console.
    public static void LogWarning(object message, Object context = null)
    {
        UnityEngine.Debug.LogWarning(message, context);
    }

    //
    // Summary:
    //     A variant of Debug.Log that logs a warning message to the console.
    public static void LogWarningCat(params object[] args)
    {
        UnityEngine.Debug.LogWarning(string.Concat(args));
    }

    //
    // Summary:
    //     Logs a formatted warning message to the Unity Console.
    public static void LogWarningFormat(string format, params object[] args)
    {
        UnityEngine.Debug.LogWarningFormat(format, args);
    }

    //
    // Summary:
    //     Logs a formatted warning message to the Unity Console.
    public static void LogWarningFormat(Object context, string format, params object[] args)
    {
        UnityEngine.Debug.LogWarningFormat(context, format, args);
    }
}