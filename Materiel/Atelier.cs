using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SimulateurPliage.Materiel
{
    /// <summary>
    /// Bibliothèque du parc : plieuses, poinçons, matrices, embases.
    /// L'outillage est COMMUN aux machines — seules les cotes bâti diffèrent.
    /// Persistée en JSON ; un changement de Version régénère les valeurs par défaut.
    /// </summary>
    public sealed class Atelier
    {
        public const int CURRENT_VERSION = 12;

        public int Version { get; set; }
        public List<Plieuse> Plieuses { get; set; } = new();
        public List<Poincon> Poincons { get; set; } = new();
        public List<Matrice> Matrices { get; set; } = new();
        public Embase Embase { get; set; } = new();

        static string Fichier()
        {
            string dir;
            try
            {
                dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TolTem", "SimulateurPliage");
                Directory.CreateDirectory(dir);
            }
            catch { dir = AppContext.BaseDirectory; }
            return Path.Combine(dir, "atelier.json");
        }

        public static Atelier Charger()
        {
            try
            {
                string f = Fichier();
                if (File.Exists(f))
                {
                    var opt = new JsonSerializerOptions { IncludeFields = true };
                    var a = JsonSerializer.Deserialize<Atelier>(File.ReadAllText(f), opt);
                    if (a != null && a.Version >= CURRENT_VERSION
                        && a.Plieuses.Count > 0 && a.Poincons.Count > 0 && a.Matrices.Count > 0)
                        return a;
                }
            }
            catch { }

            var def = Defaut();
            def.Sauver();
            return def;
        }

        public void Sauver()
        {
            try
            {
                var opt = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
                File.WriteAllText(Fichier(), JsonSerializer.Serialize(this, opt));
            }
            catch { }
        }

        public static Atelier Defaut() => new()
        {
            Version = CURRENT_VERSION,
            Plieuses = Plieuse.Presets(),
            Poincons = new List<Poincon> { new Poincon() },
            Matrices = Matrice.Presets(),
            Embase = new Embase(),
        };
    }
}
