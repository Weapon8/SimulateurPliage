using System;
using System.Collections.Generic;

namespace SimulateurPliage
{
    // ================================================================
    //  Cotes machine — TOUTES ajustables dans l'UI.
    //  Valeurs de depart APPROXIMATIVES : a remplacer par les vraies
    //  mesures (voir la liste "A MESURER" fournie avec le proto).
    // ================================================================
    public sealed class MachineConfig
    {
        // --- Poincon (vu de cote, section) ---
        public double PoinconHauteur   = 120;   // A MESURER : hauteur totale du poincon
        public double PoinconAngleDeg  = 35;    // pointe du poincon (fourni)
        public double BecHauteur       = 15;    // hauteur du bec fin (35deg) avant le corps droit
        public double CorpsLg          = 12;    // largeur du corps droit du poincon
        public double PoinconPointeLg  = 2.0;   // A MESURER : largeur en pointe (mm)
        // Col de cygne (le creux qui laisse remonter le retour de bord) :
        public double ColRetrait       = 40;    // A MESURER : de combien le col rentre (mm horizontal)
        public double ColHauteur       = 25;    // A MESURER : hauteur au-dessus de la pointe ou le creux commence

        // --- Matrice (courante, alimentee par la biblio d'outils) ---
        public double[] Vs             = { 12, 16, 24 }; // ouvertures dispo (fourni)
        public double EpaulementFactor = 2.0;   // (obsolete : remplace par BlocLargeur)
        public double MatriceAngleDeg  = 45;    // angle du V (fourni)
        public double BlocLargeur      = 60;    // largeur reelle du bloc matrice (fiche)
        public Embase Embase           = new(); // embases porte-outil

        // --- Plieuse : Loire Safe (cotes relevees) ---
        public double TablierDeport    = 50;      // deport tablier bas
        public double HauteurLibre     = 120;     // garde verticale ouverte (repere visuel)

        // Butee arriere, en PROFONDEUR (perpendiculaire a la ligne de pli).
        // 10,2 mm mini : les doigts sont decroches, ils longent le bloc matrice.
        public double ButeeMin         = 10.2;
        public double ButeeMax         = 695;

        // Butee arriere, course LATERALE des doigts (le long de la ligne de pli).
        public double ButeeLatMin      = 100;
        public double ButeeLatMax      = 2900;

        // Longueur de pli admissible (longueur de la piece le long de la ligne de pli).
        public double LongPliMin       = 100;
        public double LongPliMax       = 4050;

        // Arcade : passage lateral entre les montants. Une piece deja fermee
        // (caisson, U profond) doit y entrer pour aller se poser sur la matrice.
        public double ColPassageLat    = 3000;

        public double DemiPointe => PoinconAngleDeg * Math.PI / 360.0; // demi-angle en rad
    }

    public enum Sens { Haut, Bas }   // sens du pli (retour vers le haut / vers le bas)

    // Une operation = une passe de pliage sur une ligne, dans l'ordre de la sequence.
    // Une meme ligne peut avoir plusieurs operations (marquage 130 puis fermeture 90) => Reprise=true.
    public sealed class Operation
    {
        public int    Bend;          // index de la ligne de pli (0 .. NbPlis-1)
        public double AngleCible = 90;   // angle interieur vise (180 = plat)
        public Sens   Sens = Sens.Haut;
        public double V = 16;        // ouverture matrice utilisee
        public bool   Reprise;       // true => pli en plusieurs passes (affiche en VERT), sinon BLEU (direct)

        // Cote butee. false (defaut) : la butee lit le pan AMONT (celui d'avant le pli).
        // true : l'operateur engage la piece dans l'autre sens, la butee lit le pan AVAL.
        // Geometriquement c'est un miroir gauche/droite : la piece est retournee bout pour
        // bout, la face reste la meme, l'angle et le sens ne changent pas.
        public bool   ButeeAval;
    }

    public sealed class Piece
    {
        public double Epaisseur = 1.0;
        public double LongueurPli = 500;            // longueur de la piece le long de la ligne de pli
        public double Rm = 450;                     // N/mm2 : acier 450, inox 600, alu 250, zinc 150
        public bool   CotesExterieures = false;     // false = cotes interieures, true = exterieures
        public List<double> Segments = new();       // longueurs des pans (NbPlis+1 segments)
        public List<Operation> Sequence = new();    // timeline ordonnee des operations

        public int NbPlis => Math.Max(0, Segments.Count - 1);

        // ---- cote butee = pan cote butee, TOUJOURS lue/ecrite en INTERIEUR ----
        // (c'est ce qu'affiche une CN Cybelec/Delem : R = cote interieure jusqu'a la ligne de pli)
        public double ButeeInt(int i)
        {
            if (i < 0 || i >= Segments.Count) return 0;
            double L = Segments[i];
            if (CotesExterieures) L -= Epaisseur;
            return Math.Max(0, L);
        }

        public void SetButeeInt(int i, double r)
        {
            if (i < 0 || i >= Segments.Count) return;
            r = Math.Max(0, r);
            Segments[i] = CotesExterieures ? r + Epaisseur : r;
        }

        public static Piece Demo()
        {
            // U simple : 3 pans, 2 plis. On part TOUJOURS de la tole a plat (180deg).
            var p = new Piece { Epaisseur = 1.0 };
            p.Segments.AddRange(new double[] { 40, 120, 40 });
            for (int b = 0; b < 2; b++)
                p.Sequence.Add(new Operation { Bend = b, AngleCible = 90, Sens = Sens.Haut, V = 16 });
            return p;
        }
    }

    // Resultat d'une verification de collision sur une etape.
    public sealed class Collision
    {
        public string Type;      // "col de cygne", "hauteur libre", "butee", "repli sur repli"
        public string Detail;    // texte lisible avec dispo/besoin
        public bool   Bloquant;  // true = tape, false = simple avertissement
        public Collision(string type, string detail, bool bloquant) { Type = type; Detail = detail; Bloquant = bloquant; }
    }
}
