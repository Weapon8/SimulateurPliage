using System;
using System.Collections.Generic;

namespace SimulateurPliage.Pliage
{
    /// <summary>
    /// Géométrie du pliage. Repère de travail : le sommet du pli actif est à l'origine,
    /// le pan côté butée est couché le long de -X, le volet en formage monte vers +Y.
    /// Ne dépend d'aucune UI.
    /// </summary>
    public static class Moteur
    {
        const double D2R = Math.PI / 180.0;

        /// <summary>Angles intérieurs de chaque ligne après les étapes 0..etape.</summary>
        public static double[] AnglesA(Piece p, int etape, out Sens[] sens)
        {
            int nb = p.NbPlis;
            var ang = new double[Math.Max(0, nb)];
            sens = new Sens[Math.Max(0, nb)];
            for (int i = 0; i < nb; i++) { ang[i] = 180.0; sens[i] = Sens.Haut; }

            for (int i = 0; i <= etape && i < p.Sequence.Count; i++)
            {
                var op = p.Sequence[i];
                if (op.Bend >= 0 && op.Bend < nb) { ang[op.Bend] = op.AngleCible; sens[op.Bend] = op.Sens; }
            }
            return ang;
        }

        /// <summary>Chaîne de la fibre neutre, pliée selon les angles donnés.</summary>
        public static List<Pt> Chaine(List<double> segs, double[] ang, Sens[] sens)
        {
            var pts = new List<Pt> { new Pt(0, 0) };
            double dir = 0, x = 0, y = 0;

            for (int i = 0; i < segs.Count; i++)
            {
                x += segs[i] * Math.Cos(dir);
                y += segs[i] * Math.Sin(dir);
                pts.Add(new Pt(x, y));

                if (i < segs.Count - 1 && i < ang.Length)
                    dir += (sens[i] == Sens.Haut ? +1 : -1) * (180.0 - ang[i]) * D2R;
            }
            return pts;
        }

        /// <summary>Construit l'état géométrique d'une étape, collisions comprises.</summary>
        public static EtatEtape Construire(Piece p, int etape, Materiel.Plieuse plieuse,
                                           Materiel.Poincon poincon, Materiel.Matrice matrice, Materiel.Embase embase)
        {
            var st = new EtatEtape { Etape = etape };
            if (p.Sequence.Count == 0 || etape < 0 || etape >= p.Sequence.Count) return st;

            st.Op = p.Sequence[etape];
            var ang = AnglesA(p, etape, out var sens);
            var chaine = Chaine(p.Segments, ang, sens);

            int sommet = st.Op.Bend + 1;
            if (sommet < 1 || sommet >= chaine.Count) return st;

            Ancrer(chaine, sommet);

            for (int i = 0; i <= sommet; i++) st.PanArriere.Add(chaine[i]);
            for (int i = sommet; i < chaine.Count; i++) st.Formage.Add(chaine[i]);

            st.ButeeDistance = st.Op.Bend < p.Segments.Count ? p.Segments[st.Op.Bend] : 0;
            st.Collisions = Detecteur.Analyser(st, p, plieuse, poincon, matrice, embase);
            return st;
        }

        /// <summary>
        /// Place le sommet actif a l'origine et aligne la BISSECTRICE du pli sur +Y,
        /// c'est-a-dire sur l'axe du poincon : les deux ailes s'ecartent symetriquement
        /// de part et d'autre du bec, comme sur la machine. A 180° (tole a plat) la
        /// bissectrice est indefinie : on couche le pan a l'horizontale.
        /// </summary>
        static void Ancrer(List<Pt> chaine, int sommet)
        {
            Pt o = chaine[sommet];
            for (int i = 0; i < chaine.Count; i++)
                chaine[i] = new Pt(chaine[i].X - o.X, chaine[i].Y - o.Y);

            Pt u1 = Unitaire(chaine[sommet - 1]);
            Pt u2 = sommet + 1 < chaine.Count ? Unitaire(chaine[sommet + 1]) : new Pt(-u1.X, -u1.Y);

            double bx = u1.X + u2.X, by = u1.Y + u2.Y;
            if (Math.Sqrt(bx * bx + by * by) < 1e-6) { bx = -u1.Y; by = u1.X; }   // pli a plat
            Pt b = Unitaire(new Pt(bx, by));

            double rot = Math.PI / 2 - Math.Atan2(b.Y, b.X);
            double cs = Math.Cos(rot), sn = Math.Sin(rot);
            for (int i = 0; i < chaine.Count; i++)
                chaine[i] = new Pt(chaine[i].X * cs - chaine[i].Y * sn,
                                   chaine[i].X * sn + chaine[i].Y * cs);

            // le pan cote butee se lit a gauche (arriere machine)
            if (chaine[sommet - 1].X > 0)
                for (int i = 0; i < chaine.Count; i++)
                    chaine[i] = new Pt(-chaine[i].X, chaine[i].Y);
        }

        static Pt Unitaire(Pt p)
        {
            double m = Math.Sqrt(p.X * p.X + p.Y * p.Y);
            return m > 1e-9 ? new Pt(p.X / m, p.Y / m) : new Pt(0, 0);
        }
    }
}
