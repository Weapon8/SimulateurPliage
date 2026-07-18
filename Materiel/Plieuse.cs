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
        // TABLIER (le HAUT mobile : coulisseau + inter + poinçon). Le bas (bâti + support
        // matrice) est fixe = référence y=0 sur la face matrice.
        // Garde utile mesurée sur la Loire Safe : du BOUT DU BEC au bas du tablier = 280 mm.
        // Une aile formée qui remonte au-delà tape le tablier -> tôle + machine bousillées.
        // (Développés d'1 m, ailes de 300 : ça arrive en prod, ce n'est pas un cas d'école.)
        public double TablierHauteur = 280;   // bas du tablier au-dessus de la face matrice (mm)
        // Le bâti/tablier déborde de 66 mm de PART ET D'AUTRE du bloc matrice (Loire Safe).
        public double TablierDebord = 66;     // débord latéral au-delà du bloc matrice (mm/côté)
        public double HauteurLibre = 120;   // garde verticale ouverte (héritée, non utilisée)
        public double TablierDeport = 50;

        // À mesurer sur machine.
        public double TonnageMax = 180;     // tonnage machine (t) — Loire Safe : 180 t DE TÊTE, à confirmer sur la plaque
        public double DoigtHauteur = 0;     // hauteur totale du doigt au-dessus de la face matrice
        public double DoigtContact = 10;    // hauteur de la FACE D'APPUI tôle (le reste = support)

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
                DoigtHauteur = 35, DoigtContact = 10,
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
                DoigtHauteur = 35, DoigtContact = 10,
            },
        };
    }
}
