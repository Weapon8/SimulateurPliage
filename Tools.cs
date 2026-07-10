using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimulateurPliage
{
    // ================================================================
    //  Bibliotheque d'outils. Le poincon est un CONTOUR EXACT (col de
    //  cygne asymetrique, bec 10°/25°) en repere pointe : pointe=(0,0),
    //  +Y vers le haut. Hauteur reglable : seul le fut droit s'etire.
    // ================================================================

    public sealed class Poincon
    {
        public string Nom = "Rolleri P.120.35.R3";
        public double Hauteur  = 120;   // hauteur totale REGLABLE (etire le fut droit)
        public double AngleDeg = 35;    // bec total (avant 10° + arriere 25°)
        public double R        = 1.0;   // pointe R1 (celui monte sur la Loire Safe 4 m)
        public double CorpsLg  = 26;    // corps

        const double HRef     = 120.0;  // hauteur de reference du contour ci-dessous
        const double UtileRef = 90.0;   // hauteur utile de reference (pointe -> epaulement)
        const double YStretch = 60.0;   // au-dessus : fut droit etire ; en-dessous : bec+col figes

        // hauteur utile (pointe -> epaulement) : suit l'etirement du fut.
        public double HauteurUtile => UtileRef + (Hauteur - HRef);

        // Contour EXACT (repere pointe, +Y haut) — parse du trace vectoriel Rolleri :
        //  bec avant 10° (droite) / arriere 25° (gauche), pointe R3 sur l'axe,
        //  col de cygne + contre-pli echantillonnes, corps 26, queue 18.
        public List<double[]> ContourPts = new()
        {
            new[] {   -7.000,  120.000 },
            new[] {   11.000,  120.000 },
            new[] {   11.000,  105.000 },
            new[] {    7.000,  105.000 },
            new[] {    7.000,   94.000 },
            new[] {   11.000,   94.000 },
            new[] {   11.000,   90.000 },
            new[] {   11.000,   60.000 },
            new[] {    9.464,   56.786 },
            new[] {    8.079,   53.571 },
            new[] {    6.843,   50.357 },
            new[] {    5.757,   47.143 },
            new[] {    4.821,   43.929 },
            new[] {    4.036,   40.714 },
            new[] {    3.400,   37.500 },
            new[] {    2.914,   34.286 },
            new[] {    2.579,   31.071 },
            new[] {    2.393,   27.857 },
            new[] {    2.357,   24.643 },
            new[] {    2.471,   21.429 },
            new[] {    2.736,   18.214 },
            new[] {    3.150,   15.000 },
            new[] {    0.521,    0.046 },
            new[] {    0.435,    0.021 },
            new[] {    0.347,    0.005 },
            new[] {    0.259,   -0.002 },
            new[] {    0.170,   -0.001 },
            new[] {    0.081,    0.007 },
            new[] {   -0.008,    0.023 },
            new[] {   -0.096,    0.047 },
            new[] {   -0.183,    0.078 },
            new[] {   -0.267,    0.117 },
            new[] {   -0.349,    0.163 },
            new[] {   -0.427,    0.216 },
            new[] {   -0.501,    0.276 },
            new[] {   -1.268,    0.281 },
            new[] {   -8.127,   15.000 },
            new[] {   -8.356,   15.000 },
            new[] {   -8.814,   18.214 },
            new[] {   -9.273,   21.429 },
            new[] {   -9.731,   24.643 },
            new[] {  -10.189,   27.857 },
            new[] {  -10.647,   31.071 },
            new[] {  -11.105,   34.286 },
            new[] {  -11.564,   37.500 },
            new[] {  -12.022,   40.714 },
            new[] {  -12.480,   43.929 },
            new[] {  -12.938,   47.143 },
            new[] {  -13.396,   50.357 },
            new[] {  -13.854,   53.571 },
            new[] {  -14.313,   56.786 },
            new[] {  -14.771,   60.000 },
            new[] {  -15.000,   90.000 },
            new[] {   -7.000,   90.000 },
        };

        // Contour ferme, ETIRE a la hauteur courante (copie) — rendu ET collision.
        public List<double[]> Contour()
        {
            double d = Hauteur - HRef;
            var c = new List<double[]>(ContourPts.Count);
            foreach (var p in ContourPts)
                c.Add(new[] { p[0], p[1] >= YStretch ? p[1] + d : p[1] });
            return c;
        }

        // demi-largeur = max |x| a la hauteur y (sur le contour etire) — bornes / filtres.
        public double DemiLargeur(double y)
        {
            var c = Contour(); int n = c.Count;
            double best = 0.2; bool found = false;
            for (int i = 0; i < n; i++)
            {
                var p1 = c[i]; var p2 = c[(i + 1) % n];
                double y1 = p1[1], y2 = p2[1];
                if ((y1 <= y && y <= y2) || (y2 <= y && y <= y1))
                {
                    double t = Math.Abs(y2 - y1) > 1e-9 ? (y - y1) / (y2 - y1) : 0;
                    double x = p1[0] + (p2[0] - p1[0]) * t;
                    best = Math.Max(best, Math.Abs(x)); found = true;
                }
            }
            return found ? best : 0.2;
        }

        public override string ToString() => Nom;
    }

    public sealed class VForm
    {
        public double V = 12;
        public double AngleDeg = 45;
        public double R = 1.5;
        public double Profondeur = 0;
        public override string ToString() => $"V{V:0.#} · {AngleDeg:0}° · R{R:0.#}";
    }

    public sealed class Matrice
    {
        public string Nom = "2045 / 45°";
        public double BlocLargeur = 60;
        public double Hauteur = 120;
        public bool   MultiV = false;
        public List<VForm> Vs = new();
        public override string ToString() => Nom;

        public VForm VProche(double v)
        {
            VForm best = null; double bd = double.MaxValue;
            foreach (var x in Vs) { double d = Math.Abs(x.V - v); if (d < bd) { bd = d; best = x; } }
            return best ?? new VForm { V = v };
        }
    }

    public sealed class Embase
    {
        public double PortePoinconH = 60;
        public double PortePoinconLg = 40;
        public double SemelleH = 60;
        public double SemelleLg = 90;
    }

    public sealed class ToolLib
    {
        public const int CURRENT_VERSION = 11;   // bump => regenere (contour exact + hauteur)
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
            lib.Poincons.Add(new Poincon());   // Rolleri P.120.35.R3 (contour exact)

            lib.Matrices.Add(new Matrice
            {
                Nom = "2035 / 35°", BlocLargeur = 60, Hauteur = 80, MultiV = false,
                Vs = { new VForm { V = 8, AngleDeg = 35, R = 1.5 }, new VForm { V = 12, AngleDeg = 35, R = 2.0 } }
            });
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
