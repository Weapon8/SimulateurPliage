using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimulateurPliage.Pliage
{
    /// <summary>Un profil de pliage nommé, rangé dans la bibliothèque.</summary>
    public sealed class Profil
    {
        public string Nom { get; set; } = "";
        public string Chantier { get; set; } = "";
        public string Date { get; set; } = "";
        public Piece Piece { get; set; }

        public string Libelle => string.IsNullOrWhiteSpace(Chantier) ? Nom : $"{Chantier} · {Nom}";
    }

    /// <summary>
    /// Bibliothèque de profils de pliage : les standards de l'atelier et les suites de
    /// chantier livrées par paquets. Persistée en JSON dans le dossier utilisateur,
    /// indépendamment de l'atelier (outillage) et des fichiers .plt.json d'échange.
    /// </summary>
    public sealed class Bibliotheque
    {
        public const int CURRENT_VERSION = 1;

        public int Version { get; set; } = CURRENT_VERSION;
        public List<Profil> Profils { get; set; } = new();

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
            return Path.Combine(dir, "profils.json");
        }

        static JsonSerializerOptions Opt() => new()
        {
            WriteIndented = true,
            IncludeFields = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static Bibliotheque Charger()
        {
            try
            {
                string f = Fichier();
                if (File.Exists(f))
                {
                    var b = JsonSerializer.Deserialize<Bibliotheque>(File.ReadAllText(f), Opt());
                    if (b != null) { b.Profils ??= new(); return b; }
                }
            }
            catch { }
            return new Bibliotheque();
        }

        public void Sauver()
        {
            try { File.WriteAllText(Fichier(), JsonSerializer.Serialize(this, Opt())); }
            catch { }
        }

        /// <summary>Copie profonde d'une pièce (fige l'état au moment de l'enregistrement).</summary>
        static Piece Copier(Piece p)
        {
            var c = JsonSerializer.Deserialize<Piece>(JsonSerializer.Serialize(p, Opt()), Opt());
            c.NormaliserReprises();
            return c;
        }

        /// <summary>Enregistre (ou remplace) le profil portant ce nom dans ce chantier.</summary>
        public void Enregistrer(Piece p, string nom, string chantier)
        {
            nom = (nom ?? "").Trim();
            chantier = (chantier ?? "").Trim();
            if (nom.Length == 0) nom = "Sans nom";

            var copie = Copier(p);
            copie.Nom = nom; copie.Chantier = chantier;

            Profils.RemoveAll(x =>
                string.Equals(x.Nom, nom, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Chantier ?? "", chantier, StringComparison.OrdinalIgnoreCase));

            Profils.Add(new Profil
            {
                Nom = nom, Chantier = chantier,
                Date = DateTime.Now.ToString("yyyy-MM-dd"),
                Piece = copie
            });

            // rangé par chantier puis par nom, pour lire les paquets d'un coup
            Profils.Sort((a, b) =>
            {
                int c = string.Compare(a.Chantier ?? "", b.Chantier ?? "", StringComparison.OrdinalIgnoreCase);
                return c != 0 ? c : string.Compare(a.Nom, b.Nom, StringComparison.OrdinalIgnoreCase);
            });
            Sauver();
        }

        public void Supprimer(Profil p)
        {
            if (p != null && Profils.Remove(p)) Sauver();
        }

        /// <summary>Copie détachée d'un profil, prête à charger dans l'éditeur.</summary>
        public Piece Instancier(Profil p) => p?.Piece != null ? Copier(p.Piece) : null;
    }
}
