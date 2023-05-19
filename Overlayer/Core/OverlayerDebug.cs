﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

namespace Overlayer.Core
{
    public static class OverlayerDebug
    {
        struct ExecuteInfo
        {
            public Stopwatch timer;
            public string executor;
        }
        static readonly StringBuilder Buffer = new StringBuilder();
        static readonly Stack<ExecuteInfo> ExecutingStack = new Stack<ExecuteInfo>();
        static bool prevActiveStatus = true;
        public static void Init()
        {
            Application.quitting += SaveLog;
            Application.logMessageReceived += UnityLogCallback;
        }
        public static string CurrentExecutor => ExecutingStack.Count > 0 ? ExecutingStack.Peek().executor : null;
        public static void Term()
        {
            SaveLog();
            Application.quitting -= SaveLog;
            Application.logMessageReceived -= UnityLogCallback;
        }
        static void UnityLogCallback(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Log || type == LogType.Warning) return;
            if (string.IsNullOrEmpty(stackTrace))
                Log($"{condition} {type}");
            else Log($"{condition} {type}\n{stackTrace}");
        }
        static void SaveLog() => SaveLog(null);
        public static void SaveLog(string path = null)
        {
            if (!Main.Initialized || Main.Settings.DebugMode)
                File.WriteAllText(path ?? Path.Combine(Main.Mod.Path, "Debug.log"), Buffer.ToString());
        }
        public static T Log<T>(T obj, Func<T, string> toString = null)
        {
            if (Main.Initialized && !Main.Settings.DebugMode) return obj;
            Buffer.AppendLine(toString != null ? toString(obj) : obj?.ToString());
            return obj;
        }
        public static T Exception<T>(T ex, string message = null) where T : Exception
        {
            if (message != null) Log(message);
            Log($"Exception Has Occured.\n{ex}");
            return ex;
        }
        public static void Begin(string toExecute)
        {
            if (Main.Initialized && !Main.Settings.DebugMode) return;
            var timer = new Stopwatch();
            ExecuteInfo info;
            info.timer = timer;
            info.executor = toExecute ?? ExecutingStack.Count.ToString();
            ExecutingStack.Push(info);
            timer.Start();
        }
        public static string End(bool success = true)
        {
            if (Main.Initialized && !Main.Settings.DebugMode) return null;
            if (ExecutingStack.Count < 1) return null;
            var info = ExecutingStack.Pop();
            info.timer.Stop();
            return Log($"{(success ? "" : "Failed ")}{info.executor} For {info.timer.Elapsed}");
        }
        public static void OpenDebugLog()
        {
            SaveLog();
            Application.OpenURL(Path.Combine(Main.Mod.Path, "Debug.log"));
        }
        public static T ExecuteSafe<T>(Action action) where T : Exception
        {
            return ExecuteSafe<T>(null, action);
        }
        public static T ExecuteSafe<T>(string name, Action action) where T : Exception
        {
            T exception = null;
            Begin(name);
            try { action?.Invoke(); }
            catch (T ex) { exception = ex; }
            End(exception == null);
            return exception;
        }
        public static void Enable()
        {
            if (!Main.Initialized) return;
            Main.Settings.DebugMode = prevActiveStatus;
        }
        public static void Disable()
        {
            if (!Main.Initialized) return;
            prevActiveStatus = Main.Settings.DebugMode;
            Main.Settings.DebugMode = false;
        }
    }
}
