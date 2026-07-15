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
        public string Nom = "Euram / V16";
        public double TeteLargeur = 30;   // largeur de la TÊTE : le vé y est usiné
        public double PiedLargeur = 60;   // largeur du PIED d'appui (talon Promecam, s'aligne sur la table)
        public double PiedHauteur = 20;   // hauteur du pied
        public double Hauteur     = 80;   // hauteur totale sous la face (Euram série 2009 : 80 ou 100)
        public bool MultiV = false;
        public List<VForm> Vs = new();

        public VForm VProche(double v)
        {
            VForm best = null; double bd = double.MaxValue;
            foreach (var x in Vs) { double d = Math.Abs(x.V - v); if (d < bd) { bd = d; best = x; } }
            return best ?? new VForm { V = v };
        }

        /// <summary>
        /// Contour de la matrice, face supérieure à y = 0. Profil en T INVERSÉ (⊥) Euro-style
        /// Amada/Promecam : tête étroite en haut où le vé est usiné, pied large en bas — le
        /// talon d'appui, qui cale la matrice et l'allège (Weapon). Purement visuel : la tôle
        /// ne touche que le vé, le corps n'entre dans aucun calcul de collision.
        /// </summary>
        public List<double[]> Contour(double v)
        {
            var vf = VProche(v);
            double prof = vf.ProfondeurReelle;
            double demiT = Math.Max(TeteLargeur, vf.V + 8) / 2.0;          // la tête doit border le vé
            double demiP = Math.Max(PiedLargeur, demiT * 2) / 2.0;         // le pied déborde la tête
            double h = Math.Max(Hauteur, prof + PiedHauteur + 8);
            double yPied = -(h - PiedHauteur);
            return new List<double[]>
            {
                new[] { -demiT, 0.0 },       // coin haut gauche de la tête
                new[] { -vf.V / 2, 0.0 },    // bord gauche du vé
                new[] { 0.0, -prof },        // fond du vé
                new[] {  vf.V / 2, 0.0 },    // bord droit du vé
                new[] {  demiT, 0.0 },       // coin haut droit
                new[] {  demiT, yPied },     // descente de la tête (droite)
                new[] {  demiP, yPied },     // épaulement du pied (droite)
                new[] {  demiP, -h },        // pied droit
                new[] { -demiP, -h },        // pied gauche
                new[] { -demiP, yPied },     // épaulement du pied (gauche)
                new[] { -demiT, yPied },     // remontée de la tête (gauche)
            };
        }

        public override string ToString() => Nom;

        public static List<Matrice> Presets() => new()
        {
            new Matrice
            {
                Nom = "2035 / 35°", TeteLargeur = 26, PiedLargeur = 60, Hauteur = 80,
                Vs = { new VForm { V = 8, AngleDeg = 35, R = 1.5 }, new VForm { V = 12, AngleDeg = 35, R = 2.0 } }
            },
            new Matrice
            {
                Nom = "2045 / 45°", TeteLargeur = 30, PiedLargeur = 60, Hauteur = 80,
                Vs =
                {
                    new VForm { V = 10, AngleDeg = 45, R = 1.0 },
                    new VForm { V = 12, AngleDeg = 45, R = 1.5 },
                    new VForm { V = 16, AngleDeg = 45, R = 2.0 },
                    new VForm { V = 20, AngleDeg = 45, R = 2.5 },
                    new VForm { V = 25, AngleDeg = 45, R = 3.0 },
                }
            },
            // Matrice 4 VOIES : bloc carré 60×60, un vé usiné sur chacune des quatre faces,
            // R2, 50-60 HRC. On la tourne pour changer de vé — d'où le bloc carré et pas un ⊥.
            // Cotes relevées sur le plan constructeur (Weapon) : V35 et V50 à 85°, V22 et V16
            // à 88°. Les profondeurs se déduisent de l'ouverture et de l'angle, on ne les force
            // pas : les anciennes valeurs (22/22/16) étaient en fait les OUVERTURES des vés
            // voisins, prises pour des profondeurs. Et le V22 manquait.
            new Matrice
            {
                Nom = "Euram 2009 · 4 voies 60×60", TeteLargeur = 60, PiedLargeur = 60, Hauteur = 60, MultiV = true,
                Vs =
                {
                    new VForm { V = 50, AngleDeg = 85, R = 2.0 },
                    new VForm { V = 35, AngleDeg = 85, R = 2.0 },
                    new VForm { V = 22, AngleDeg = 88, R = 2.0 },
                    new VForm { V = 16, AngleDeg = 88, R = 2.0 },
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
