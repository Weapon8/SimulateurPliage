using System;
using System.Collections.Generic;

namespace SimulateurPliage.Pliage
{
    public enum Sens { Haut, Bas }

    /// <summary>Un point 2D dans le plan de section.</summary>
    public readonly struct Pt
    {
        public readonly double X, Y;
        public Pt(double x, double y) { X = x; Y = y; }
    }

    /// <summary>Une passe de pliage sur une ligne. Plusieurs passes sur la même ligne = reprise.</summary>
    public sealed class Operation
    {
        public int Bend;                  // index de la ligne de pli
        public double AngleCible = 90;    // angle intérieur visé (180 = plat)
        public Sens Sens = Sens.Haut;
        public double V = 16;             // ouverture matrice
        public bool Reprise;              // pli en plusieurs passes
        public bool ButeeAval;            // rotation 180° À PLAT (bout pour bout) : la butée lit le pan aval
        public bool Retournee;            // retournement DESSUS/DESSOUS : les plis déjà faits pointent en bas
    }

    /// <summary>La tôle : ses pans, son épaisseur, sa séquence de pliage.</summary>
    public sealed class Piece
    {
        public double Epaisseur = 1.0;
        public double LongueurPli = 500;
        public double Rm = 450;                  // N/mm² : acier 450, inox 600, alu 250, zinc 150
        public bool CotesExterieures = false;
        public List<double> Segments = new();    // NbPlis + 1 pans
        public List<Operation> Sequence = new();

        public int NbPlis => Math.Max(0, Segments.Count - 1);
        public double Developpe { get { double t = 0; foreach (var s in Segments) t += s; return t; } }

        /// <summary>Cote butée, toujours exprimée en intérieur (comme une CN Cybelec/Delem).</summary>
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
            Segments[i] = CotesExterieures ? Math.Max(0, r) + Epaisseur : Math.Max(0, r);
        }

        /// <summary>Garantit au moins nb lignes de pli (donc nb+1 pans).</summary>
        public void AssurerPlis(int nb)
        {
            int segs = Math.Max(2, nb + 1);
            while (Segments.Count < segs) Segments.Add(100);
        }

        /// <summary>
        /// Retournements de la pièce. Le premier pli fixe la face de référence ;
        /// tout changement de sens impose de retourner la pièce.
        /// </summary>
        public bool[] Retournements()
        {
            var f = new bool[Sequence.Count];
            if (Sequence.Count == 0) return f;
            Sens courant = Sequence[0].Sens;
            for (int i = 1; i < Sequence.Count; i++)
            {
                if (Sequence[i].Sens != courant) { f[i] = true; courant = Sequence[i].Sens; }
            }
            return f;
        }

        /// <summary>Angle intérieur d'une ligne juste avant l'étape s.</summary>
        public double AngleAvant(int s)
        {
            if (s < 0 || s >= Sequence.Count) return 180;
            int bend = Sequence[s].Bend;
            double a = 180;
            for (int i = 0; i < s; i++)
                if (Sequence[i].Bend == bend) a = Sequence[i].AngleCible;
            return a;
        }

        /// <summary>
        /// Chevetre de reference : aile 20 · joue 60 · fond 100 · joue 60 · aile 20.
        /// Les plis ALTERNENT : Bas, Haut, Haut, Bas — les joues descendent, le fond
        /// remonte, les ailes repartent a plat. Encombrement fini 140 x 60 mm.
        /// (Quatre plis dans le meme sens replieraient la tole sur elle-meme.)
        /// </summary>
        public static Piece Demo()
        {
            var p = new Piece { Epaisseur = 1.0 };
            p.Segments.AddRange(new double[] { 20, 60, 100, 60, 20 });
            var sens = new[] { Sens.Bas, Sens.Haut, Sens.Haut, Sens.Bas };
            for (int b = 0; b < 4; b++)
                p.Sequence.Add(new Operation { Bend = b, AngleCible = 90, Sens = sens[b], V = 16 });
            return p;
        }
    }

    /// <summary>Une collision détectée à une étape.</summary>
    public sealed class Collision
    {
        public string Type;
        public string Detail;
        public bool Bloquant;
        public Collision(string type, string detail, bool bloquant)
        { Type = type; Detail = detail; Bloquant = bloquant; }
    }

    /// <summary>Géométrie de la pièce à une étape donnée, dans le repère ancré sur le pli actif.</summary>
    public sealed class EtatEtape
    {
        public int Etape;
        public Operation Op;
        public List<Pt> PanArriere = new();   // côté butée, posé sur la matrice
        public List<Pt> Formage = new();       // volet en cours de formage
        public double ButeeDistance;
        public List<Collision> Collisions = new();

        public bool Bloque => Collisions.Exists(c => c.Bloquant);
    }
}
