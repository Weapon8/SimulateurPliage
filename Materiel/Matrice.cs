using System;
using System.Collections.Generic;

namespace SimulateurPliage.Materiel
{
    /// <summary>Un vé utilisable sur une matrice (une matrice multi-V en a plusieurs).</summary>
    public sealed class VForm
    {
        public double V = 12;          // ouverture
        public double AngleDeg = 45;
        public double R = 1.5;
        public double Profondeur = 0;  // 0 = déduite de V et de l'angle

        public double ProfondeurReelle =>
            Profondeur > 0 ? Profondeur : (V / 2.0) / Math.Tan(AngleDeg * Math.PI / 360.0);

        public override string ToString() => $"V{V:0.#} · {AngleDeg:0}° · R{R:0.#}";
    }

    public sealed class Matrice
    {
        public string Nom = "2045 / 45°";
        public double BlocLargeur = 60;   // largeur du bloc (mesure photo ~44, corrigée perspective)
        public double Hauteur = 48;       // hauteur du bloc sous la face — estimée sur photo, à ajuster
        public bool MultiV = false;
        public List<VForm> Vs = new();

        public VForm VProche(double v)
        {
            VForm best = null; double bd = double.MaxValue;
            foreach (var x in Vs) { double d = Math.Abs(x.V - v); if (d < bd) { bd = d; best = x; } }
            return best ?? new VForm { V = v };
        }

        /// <summary>Contour du bloc matrice avec son vé, face supérieure à y = 0.</summary>
        public List<double[]> Contour(double v)
        {
            var vf = VProche(v);
            double half = BlocLargeur / 2.0;
            double prof = vf.ProfondeurReelle;
            double bas = -(prof + 18);
            return new List<double[]>
            {
                new[] { -half, 0.0 },
                new[] { -vf.V / 2, 0.0 },
                new[] { 0.0, -prof },
                new[] { vf.V / 2, 0.0 },
                new[] { half, 0.0 },
                new[] { half, bas },
                new[] { -half, bas },
            };
        }

        public override string ToString() => Nom;

        public static List<Matrice> Presets() => new()
        {
            new Matrice
            {
                Nom = "2035 / 35°", BlocLargeur = 60, Hauteur = 80,
                Vs = { new VForm { V = 8, AngleDeg = 35, R = 1.5 }, new VForm { V = 12, AngleDeg = 35, R = 2.0 } }
            },
            new Matrice
            {
                Nom = "2045 / 45°", BlocLargeur = 60, Hauteur = 120,
                Vs =
                {
                    new VForm { V = 10, AngleDeg = 45, R = 1.0 },
                    new VForm { V = 12, AngleDeg = 45, R = 1.5 },
                    new VForm { V = 16, AngleDeg = 45, R = 2.0 },
                    new VForm { V = 20, AngleDeg = 45, R = 2.5 },
                    new VForm { V = 25, AngleDeg = 45, R = 3.0 },
                }
            },
            new Matrice
            {
                Nom = "2009 (multi-V 85/88°)", BlocLargeur = 60, Hauteur = 60, MultiV = true,
                Vs =
                {
                    new VForm { V = 50, AngleDeg = 85, R = 2.0, Profondeur = 22 },
                    new VForm { V = 35, AngleDeg = 85, R = 2.0, Profondeur = 22 },
                    new VForm { V = 16, AngleDeg = 88, R = 2.0, Profondeur = 16 },
                }
            },
        };
    }

    /// <summary>Embases porte-outil : elles peuvent entrer en collision avec la pièce.</summary>
    public sealed class Embase
    {
        public double PortePoinconH = 60;
        public double PortePoinconLg = 40;
        public double SemelleH = 25;      // porte-matrice sous le bloc — estimé sur photo (~22)
        public double SemelleLg = 90;
    }
}
