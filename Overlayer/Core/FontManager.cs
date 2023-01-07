using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using TMPro;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore;
using HarmonyLib;
using Overlayer.Patches;

namespace Overlayer.Core
{
    public static class FontManager
    {
        static TMP_FontAsset DefaultTMPFont;
        static Font DefaultFont;
        static bool initialized;
        static string[] fontNames;
        static FontData defaultFont;
        static List<TMP_FontAsset> FallbackFonts;
        static Dictionary<string, FontData> Fonts = new Dictionary<string, FontData>();
        public static FontData GetFont(string name) => TryGetFont(name, out FontData font) ? font : defaultFont;
        public static bool TryGetFont(string name, out FontData font)
        {
            if (!initialized)
            {
                DefaultFont = RDString.GetFontDataForLanguage(SystemLanguage.English).font;
                DefaultTMPFont = TMP_FontAsset.CreateFontAsset(DefaultFont, 100, 10, GlyphRenderMode.SDFAA, 1024, 1024);
                DefaultTMPFont.fallbackFontAssetTable = FallbackFonts = RDString.AvailableLanguages.Select(s => RDString.GetFontDataForLanguage(s).fontTMP).ToList();
                defaultFont = RDString.fontData;
                defaultFont.font = DefaultFont;
                defaultFont.fontTMP = DefaultTMPFont;
                fontNames = Font.GetOSInstalledFontNames();
                Fonts = new Dictionary<string, FontData>();
                initialized = true;
            }
            if (string.IsNullOrEmpty(name))
            {
                font = defaultFont;
                return false;
            }
            if (name == "Default")
            {
                font = defaultFont;
                return true;
            }
            if (Fonts.TryGetValue(name, out FontData data))
            {
                font = data;
                return true;
            }
            else
            {
                if (File.Exists(name))
                {
                    FontData newData = defaultFont;
                    Font newFont = new Font(name);
                    TMP_FontAsset newTMPFont = TMP_FontAsset.CreateFontAsset(newFont);
                    newTMPFont.fallbackFontAssetTable = FallbackFonts.ToList();
                    newData.font = newFont;
                    newData.fontTMP = newTMPFont;
                    Fonts.Add(name, newData);
                    font = newData;
                    return true;
                }
                else
                {
                    int index = Array.IndexOf(fontNames, name);
                    if (index != -1)
                    {
                        FontData newData = defaultFont;
                        Font newFont = Font.CreateDynamicFontFromOSFont(name, 1);
                        TMP_FontAsset newTMPFont = TMP_FontAsset.CreateFontAsset(newFont);
                        newTMPFont.fallbackFontAssetTable = FallbackFonts.ToList();
                        newData.font = newFont;
                        newData.fontTMP = newTMPFont;
                        Fonts.Add(name, newData);
                        font = newData;
                        return true;
                    }
                }
                font = defaultFont;
                return false;
            }
        }
    }
}
