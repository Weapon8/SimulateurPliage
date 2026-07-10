using System;
using System.Collections.Generic;

namespace SimulateurPliage.Materiel
{
    /// <summary>Cotes physiques d'une plieuse. Un preset = une machine de l'atelier.</summary>
    public sealed class Plieuse
    {
        public string Nom = "Loire Safe 4 m";

        // Butée arrière : profondeur (perpendiculaire à la ligne de pli).
        public double ButeeMin = 10.2;
        public double ButeeMax = 695;

        // Butée arrière : course latérale des doigts (le long de la ligne de pli).
        public double ButeeLatMin = 100;
        public double ButeeLatMax = 2900;

        // Longueur de pli admissible.
        public double LongPliMin = 100;
        public double LongPliMax = 4050;

        // Bâti.
        public double Arcade = 3000;        // passage latéral entre montants
        public double HauteurLibre = 120;   // garde verticale ouverte
        public double TablierDeport = 50;

        // À mesurer sur machine.
        public double TonnageMax = 0;       // tonnage machine (t)
        public double DoigtHauteur = 0;     // hauteur des doigts au-dessus de la face matrice

        public override string ToString() => Nom;

        public static List<Plieuse> Presets() => new()
        {
            new Plieuse
            {
                Nom = "Loire Safe 4 m",
                ButeeMin = 10.2, ButeeMax = 695,
                ButeeLatMin = 100, ButeeLatMax = 2900,
                LongPliMin = 100, LongPliMax = 4050,
                Arcade = 3000, HauteurLibre = 120, TablierDeport = 50,
            },
            new Plieuse
            {
                Nom = "Amada 2 m",
                // Outillage identique à la Loire Safe : seules les cotes bâti changent.
                // TODO : relever ces valeurs sur machine.
                ButeeMin = 10.2, ButeeMax = 500,
                ButeeLatMin = 100, ButeeLatMax = 1500,
                LongPliMin = 100, LongPliMax = 2000,
                Arcade = 1600, HauteurLibre = 120, TablierDeport = 50,
            },
        };
    }
}
