using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimulateurPliage
{
    // ================================================================
    //  Bibliotheque d'outils : poincons + matrices (mono-V ou multi-V)
    //  + embases (porte-poincon / semelle). Editable via outils.json.
    //  Cotes reelles connues pre-remplies ; le reste = A MESURER.
    // ================================================================

    public sealed class Poincon
    {
        public string Nom = "1012 / A / 35°";
        public double Hauteur = 120;      // 90 lame + 30 corps
        public double AngleDeg = 35;      // pointe
        public double R = 1.0;            // R1
        public double CorpsLg = 26;       // corps (fiche : 26)
        public double EpaulementDuHaut = 30; // epaulement a 30 mm du sommet
        // creux/degagement pour le retour de bord (cote gorge)
        public double ColRetrait = 4;     // A MESURER
        public double ColHauteur = 20;    // A MESURER
        // Profil ESTIME du 1012 : (hauteur depuis la pointe ; demi-largeur mm).
        // La lame s'evase, se CREUSE (galbe concave ~ milieu) puis ressort vers l'epaule.
        // -> c'est ce creux qui degage le retour de tole. A affiner au pied a coulisse.
        // Profil type PROMECAM/EURAM col-de-cygne : pointe fine, leger creux (gooseneck)
        // qui degage le retour, puis LONGUE partie quasi droite, epaulement, corps.
        public List<double[]> Profil = new()
        {
            new[] { 0.0,  1.0 },   // pointe R1
            new[] { 7.0,  2.5 },   // sortie de pointe (35deg)
            new[] { 16.0, 4.5 },   // montee
            new[] { 28.0, 6.2 },   // epaule basse
            new[] { 40.0, 5.6 },   // GOOSENECK : creux qui degage le retour
            new[] { 52.0, 6.2 },   // remontee
            new[] { 88.0, 6.6 },   // longue partie DROITE (plus haute)
            new[] { 90.0, 13.0 },  // EPAULEMENT : saut au corps (26/2)
            new[] { 120.0, 13.0 }, // corps jusqu'a la bride
        };

        // demi-largeur interpolee a la hauteur y (0 = pointe)
        public double DemiLargeur(double y)
        {
            if (Profil == null || Profil.Count == 0) return CorpsLg / 2.0;
            if (y <= Profil[0][0]) return Profil[0][1];
            for (int i = 1; i < Profil.Count; i++)
            {
                if (y <= Profil[i][0])
                {
                    double y0 = Profil[i - 1][0], y1 = Profil[i][0];
                    double x0 = Profil[i - 1][1], x1 = Profil[i][1];
                    double t = (y1 - y0) > 1e-6 ? (y - y0) / (y1 - y0) : 0;
                    return x0 + (x1 - x0) * t;
                }
            }
            return Profil[Profil.Count - 1][1];
        }

        // Contour ferme du poincon (pointe en bas a l'origine, monte en +Y).
        // Points en (x,y) : cote droit de bas en haut, puis cote gauche de haut en bas.
        public List<double[]> Contour()
        {
            double H = Hauteur;
            var right = new List<double[]>();
            for (double y = 0; y <= H + 0.001; y += 1.5) right.Add(new[] { DemiLargeur(y), y });
            var c = new List<double[]>();
            foreach (var p in right) c.Add(p);                       // droite, bas->haut
            for (int i = right.Count - 1; i >= 0; i--) c.Add(new[] { -right[i][0], right[i][1] }); // gauche, haut->bas
            return c;
        }

        public override string ToString() => Nom;
    }

    // Un V utilisable sur une matrice (une matrice multi-V en a plusieurs).
    public sealed class VForm
    {
        public double V = 12;        // ouverture (mm)
        public double AngleDeg = 45; // angle du V
        public double R = 1.5;       // rayon interne
        public double Profondeur = 0;// profondeur du V (0 = auto depuis V/angle)
        public override string ToString() => $"V{V:0.#} · {AngleDeg:0}° · R{R:0.#}";
    }

    public sealed class Matrice
    {
        public string Nom = "2045 / 45°";
        public double BlocLargeur = 60;  // largeur du bloc (mm)
        public double Hauteur = 120;     // H (mm)
        public bool   MultiV = false;    // matrice en croix / multi-vé
        public List<VForm> Vs = new();   // ouvertures dispo
        public override string ToString() => Nom;

        public VForm VProche(double v)
        {
            VForm best = null; double bd = double.MaxValue;
            foreach (var x in Vs) { double d = Math.Abs(x.V - v); if (d < bd) { bd = d; best = x; } }
            return best ?? new VForm { V = v };
        }
    }

    // Embase qui relie l'outil au tablier (peut entrer en collision aussi).
    public sealed class Embase
    {
        public double PortePoinconH = 60;   // A MESURER : hauteur porte-poincon
        public double PortePoinconLg = 40;   // A MESURER : largeur porte-poincon
        public double SemelleH = 60;         // A MESURER : hauteur semelle/porte-matrice
        public double SemelleLg = 90;        // A MESURER : largeur semelle
    }

    public sealed class ToolLib
    {
        public const int CURRENT_VERSION = 7;   // bump => regenere la biblio au prochain lancement
        public int Version { get; set; } = 0;
        public List<Poincon> Poincons { get; set; } = new();
        public List<Matrice> Matrices { get; set; } = new();
        public Embase Embase { get; set; } = new();

        static string PathFile()
        {
            string dir;
            try
            {
                dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TolTem", "SimulateurPliage");
                Directory.CreateDirectory(dir);
            }
            catch { dir = AppContext.BaseDirectory; }
            return Path.Combine(dir, "outils.json");
        }

        public static ToolLib Load()
        {
            try
            {
                string f = PathFile();
                if (File.Exists(f))
                {
                    var opt = new JsonSerializerOptions { IncludeFields = true };
                    var s = JsonSerializer.Deserialize<ToolLib>(File.ReadAllText(f), opt);
                    // on ne reutilise le fichier QUE s'il est a jour (evite les biblios perimees)
                    if (s != null && s.Version >= CURRENT_VERSION && s.Matrices.Count > 0 && s.Poincons.Count > 0)
                        return s;
                }
            }
            catch { }
            var def = Defaults(); def.Save(); return def;
        }

        public void Save()
        {
            try
            {
                var opt = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
                File.WriteAllText(PathFile(), JsonSerializer.Serialize(this, opt));
            }
            catch { }
        }

        public static ToolLib Defaults()
        {
            var lib = new ToolLib { Version = CURRENT_VERSION };

            lib.Poincons.Add(new Poincon());   // 1012 / A / 35° (Euram) par defaut

            // 2035 : mono-V 35°, bloc 60, H 80/120
            lib.Matrices.Add(new Matrice
            {
                Nom = "2035 / 35°", BlocLargeur = 60, Hauteur = 80, MultiV = false,
                Vs = { new VForm { V = 8, AngleDeg = 35, R = 1.5 }, new VForm { V = 12, AngleDeg = 35, R = 2.0 } }
            });

            // 2045 : mono-V 45°, bloc 60, H 80/120
            lib.Matrices.Add(new Matrice
            {
                Nom = "2045 / 45°", BlocLargeur = 60, Hauteur = 120, MultiV = false,
                Vs =
                {
                    new VForm { V = 10, AngleDeg = 45, R = 1.0 },
                    new VForm { V = 12, AngleDeg = 45, R = 1.5 },
                    new VForm { V = 16, AngleDeg = 45, R = 2.0 },
                    new VForm { V = 20, AngleDeg = 45, R = 2.5 },
                    new VForm { V = 25, AngleDeg = 45, R = 3.0 },
                }
            });

            // 2009 : multi-V en croix, bloc 60x60, 85/88°, R2 (grosses epaisseurs)
            lib.Matrices.Add(new Matrice
            {
                Nom = "2009 (multi-V 85/88°)", BlocLargeur = 60, Hauteur = 60, MultiV = true,
                Vs =
                {
                    new VForm { V = 50, AngleDeg = 85, R = 2.0, Profondeur = 22 },
                    new VForm { V = 35, AngleDeg = 85, R = 2.0, Profondeur = 22 },
                    new VForm { V = 16, AngleDeg = 88, R = 2.0, Profondeur = 16 },
                }
            });

            return lib;
        }
    }
}
