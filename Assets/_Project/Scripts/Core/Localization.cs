using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace AntColony
{
    public enum Language
    {
        English,
        Turkish,
    }

    /// <summary>
    /// The game's languages. Text is written in English in the code; <see cref="T"/> swaps in the translation of the
    /// whole English text (the tables are in Localization.Turkish.cs), and English stays when a text has none.
    /// The pixel font translates every text it draws, so plain texts need nothing; texts with numbers or names in
    /// them use <see cref="F"/> with a {0} {1}... format, whose translation can put the values in another order.
    /// The choice is remembered between sessions; the first time, it follows the computer's language.
    /// </summary>
    public static partial class Loc
    {
        const string PrefKey = "language";
        static Language? current;

        public static Language Current
        {
            get
            {
                if (current == null)
                {
                    int fallback = Application.systemLanguage == SystemLanguage.Turkish ? (int)Language.Turkish : (int)Language.English;
                    current = (Language)Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, fallback), 0, (int)Language.Turkish);
                }
                return current.Value;
            }
            set
            {
                current = value;
                PlayerPrefs.SetInt(PrefKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static bool IsTurkish => Current == Language.Turkish;

        /// <summary>The language after the current one (the main menu's language button steps through them).</summary>
        public static void Next() => Current = Current == Language.English ? Language.Turkish : Language.English;

        /// <summary>Each language's name in itself.</summary>
        public static string NameOf(Language language) => language == Language.Turkish ? "Türkçe" : "English";

        /// <summary>The current language's text for an English text (the English itself if there's no translation).</summary>
        public static string T(string english)
        {
            if (string.IsNullOrEmpty(english) || !IsTurkish) return english;
            return Turkish.TryGetValue(english, out string translated) ? translated : english;
        }

        /// <summary>Translates an English format ("Wave {0} is coming!") and fills in the values.</summary>
        public static string F(string englishFormat, params object[] values)
        {
            return string.Format(CultureInfo.InvariantCulture, T(englishFormat), values);
        }
    }
}
