using System;
using System.Collections.Generic;
using System.Text;

namespace GrandStrategy.Map.Generation
{
    /// <summary>Genera nombres ficticios para países y provincias a partir de sílabas.</summary>
    public sealed class NameGenerator
    {
        private static readonly string[] Onsets =
        {
            "b", "br", "c", "d", "dr", "f", "g", "gr", "h", "k", "kr", "l", "m", "n", "p", "r", "s", "st",
            "t", "tr", "v", "vl", "z", "al", "or", "es", "an", "ul", "ar", "el", "th", "w", "y", "ch", "sv",
        };

        private static readonly string[] Vowels = { "a", "e", "i", "o", "u", "a", "e", "o", "ia", "ei", "au" };

        private static readonly string[] Codas = { "", "", "", "n", "r", "s", "l", "nd", "rk", "st", "m", "th", "v" };

        private static readonly string[] CountrySuffixes =
        {
            "ia", "ania", "land", "istan", "or", "ena", "aria", "heim", "ovia", "esia", "onia", "ar", "un", "ad",
        };

        private static readonly string[] ProvinceSuffixes =
        {
            "", "", "", "burg", "grad", "ville", "polis", "ton", "a", "ia", "ford", "mar", "dor", "ica", "stad",
        };

        private readonly Random _rng;
        private readonly HashSet<string> _used = new HashSet<string>();
        private readonly HashSet<string> _usedTags = new HashSet<string>();

        public NameGenerator(int seed)
        {
            _rng = new Random(seed);
        }

        public string CountryRoot()
        {
            for (int attempt = 0; attempt < 50; attempt++)
            {
                string root = Capitalize(Syllables(_rng.Next(1, 3)) + CountrySuffixes[_rng.Next(CountrySuffixes.Length)]);
                if (root.Length >= 4 && root.Length <= 12 && _used.Add(root)) return root;
            }
            return "Nación " + _used.Count;
        }

        public string ProvinceName()
        {
            for (int attempt = 0; attempt < 50; attempt++)
            {
                string name = Capitalize(Syllables(_rng.Next(2, 4)) + ProvinceSuffixes[_rng.Next(ProvinceSuffixes.Length)]);
                if (name.Length >= 4 && name.Length <= 13 && _used.Add(name)) return name;
            }
            return "Provincia " + _used.Count;
        }

        /// <summary>Nombre oficial según el gobierno (p. ej. "Reino de Valdoria").</summary>
        public string OfficialName(string root, GovernmentType government)
        {
            int v = _rng.Next(3);
            switch (government)
            {
                case GovernmentType.Monarchy:
                    return v == 0 ? "Imperio de " + root : v == 1 ? "Reino de " + root : "Gran Ducado de " + root;
                case GovernmentType.Democracy:
                    return v == 0 ? "República de " + root : v == 1 ? root : "Federación de " + root;
                case GovernmentType.Communism:
                    return v == 0 ? "República Popular de " + root : "Unión Socialista de " + root;
                case GovernmentType.Fascism:
                    return v == 0 ? "Estado de " + root : "Imperio de " + root;
                default:
                    return v == 0 ? root : "Principado de " + root;
            }
        }

        /// <summary>TAG de 3 letras único derivado del nombre.</summary>
        public string MakeTag(string root)
        {
            string letters = root.ToUpperInvariant().Replace(" ", "");
            var candidates = new List<string>();
            if (letters.Length >= 3)
            {
                candidates.Add(letters.Substring(0, 3));
                candidates.Add("" + letters[0] + letters[1] + letters[letters.Length - 1]);
                candidates.Add("" + letters[0] + letters[2] + letters[letters.Length - 1]);
            }
            foreach (var c in candidates)
                if (IsAsciiTag(c) && _usedTags.Add(c)) return c;

            while (true)
            {
                var sb = new StringBuilder(3);
                sb.Append(letters.Length > 0 && letters[0] >= 'A' && letters[0] <= 'Z' ? letters[0] : 'X');
                sb.Append((char)('A' + _rng.Next(26)));
                sb.Append((char)('A' + _rng.Next(26)));
                string tag = sb.ToString();
                if (_usedTags.Add(tag)) return tag;
            }
        }

        private static bool IsAsciiTag(string s)
        {
            foreach (char ch in s)
                if (ch < 'A' || ch > 'Z') return false;
            return true;
        }

        private string Syllables(int count)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                sb.Append(Onsets[_rng.Next(Onsets.Length)]);
                sb.Append(Vowels[_rng.Next(Vowels.Length)]);
                if (i == count - 1 || _rng.NextDouble() < 0.3) sb.Append(Codas[_rng.Next(Codas.Length)]);
            }
            return sb.ToString();
        }

        private static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
