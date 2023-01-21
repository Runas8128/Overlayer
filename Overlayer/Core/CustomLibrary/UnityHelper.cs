using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace JSEngine.CustomLibrary
{
    public class UnityHelper
    {
        public static Component getComponent(Component comp, Type compType) => comp?.GetComponent(compType);
        public static Component getComponentInChildren(Component comp, Type compType) => comp?.GetComponentInChildren(compType);
        public static Component addComponent(Component comp, Type compType) => comp?.gameObject?.AddComponent(compType);
        public static Type type(string clrType) => AccessTools.TypeByName(clrType);
    }
}
