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
            Bibliotheque b = null;
            try
            {
                string f = Fichier();
                if (File.Exists(f))
                {
                    b = JsonSerializer.Deserialize<Bibliotheque>(File.ReadAllText(f), Opt());
                    if (b != null) b.Profils ??= new();
                }
            }
            catch { }
            b ??= new Bibliotheque();
            b.AssurerReferences();
            return b;
        }

        /// <summary>
        /// Les pièces de RÉFÉRENCE de l'atelier, rangées sous le chantier « Références » :
        /// le chevêtre et le Z laqué. Ce sont elles que l'autotest contrôle, et elles servent
        /// d'étalon quand on doute d'une modif — elles doivent donc rester chargeables d'un
        /// clic, toujours. On les réinjecte si elles manquent, sans rien toucher d'autre.
        /// </summary>
        public void AssurerReferences()
        {
            bool ajout = false;
            ajout |= Injecter(Piece.Demo(), "Chevêtre 20·40·100·40·20");
            ajout |= Injecter(Piece.DemoZLaque(), "Z laqué 30·25·25·10");
            ajout |= Injecter(Piece.DemoCouvertine(), "Couvertine 10·30·230·30·10");

            // Une boîte, c'est DEUX bandes — une par axe. On les range comme telles : c'est
            // ce que l'opérateur plie, quatre plis puis quatre plis. Sur une boîte carrée les
            // deux sont identiques ; sur une rectangulaire elles diffèrent.
            ajout |= Injecter(Boite.Demo().Piece(), "Pare-gravier 200×200×66");
            if (ajout)
            {
                Profils.Sort((a, c) =>
                {
                    int r = string.Compare(a.Chantier ?? "", c.Chantier ?? "", StringComparison.OrdinalIgnoreCase);
                    return r != 0 ? r : string.Compare(a.Nom, c.Nom, StringComparison.OrdinalIgnoreCase);
                });
                Sauver();
            }
        }

        const string CHANTIER_REF = "Références";

        bool Injecter(Piece p, string nom)
        {
            foreach (var x in Profils)
                if (string.Equals(x.Nom, nom, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.Chantier ?? "", CHANTIER_REF, StringComparison.OrdinalIgnoreCase))
                    return false;                       // déjà là : on ne l'écrase pas

            var copie = Copier(p);
            copie.Nom = nom; copie.Chantier = CHANTIER_REF;
            Profils.Add(new Profil
            {
                Nom = nom, Chantier = CHANTIER_REF,
                Date = DateTime.Now.ToString("yyyy-MM-dd"),
                Piece = copie
            });
            return true;
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
