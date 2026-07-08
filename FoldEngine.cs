using System;
using System.Collections.Generic;

namespace SimulateurPliage
{
    public struct Pt
    {
        public double X, Y;
        public Pt(double x, double y) { X = x; Y = y; }
    }

    // Etat geometrique d'une etape (apres execution des operations 0..step).
    public sealed class StepState
    {
        public int    Step;             // index de l'operation courante
        public int    ActiveBend;       // ligne de pli formee a cette etape
        public Operation Op;            // l'operation courante
        public List<Pt> BackChain = new();    // cote butee (pan pose sur la matrice), repere ancre
        public List<Pt> Forming   = new();    // volet en cours de formage (monte vers le poincon)
        public double ButeeDistance;    // longueur du pan cote butee
        public List<Collision> Collisions = new();
    }

    public static class FoldEngine
    {
        const double D2R = Math.PI / 180.0;

        // Angles interieurs courants de chaque ligne apres les operations 0..step.
        public static double[] AnglesAtStep(Piece p, int step, out Sens[] senses)
        {
            int nb = p.NbPlis;
            var ang = new double[Math.Max(0, nb)];
            senses = new Sens[Math.Max(0, nb)];
            for (int i = 0; i < nb; i++) { ang[i] = 180.0; senses[i] = Sens.Haut; }
            for (int i = 0; i <= step && i < p.Sequence.Count; i++)
            {
                var op = p.Sequence[i];
                if (op.Bend >= 0 && op.Bend < nb) { ang[op.Bend] = op.AngleCible; senses[op.Bend] = op.Sens; }
            }
            return ang;
        }

        // Chaine repliee (fibre centrale) : P[0..n].
        public static List<Pt> FullFold(List<double> segs, double[] ang, Sens[] senses)
        {
            var pts = new List<Pt>();
            double dir = 0, x = 0, y = 0;
            pts.Add(new Pt(0, 0));
            int n = segs.Count;
            for (int i = 0; i < n; i++)
            {
                x += segs[i] * Math.Cos(dir);
                y += segs[i] * Math.Sin(dir);
                pts.Add(new Pt(x, y));
                if (i < n - 1 && i < ang.Length)
                {
                    double delta = (180.0 - ang[i]) * D2R;
                    dir += (senses[i] == Sens.Haut ? +1 : -1) * delta;
                }
            }
            return pts;
        }

        public static StepState Build(Piece p, int step, MachineConfig cfg, Poincon poin, Matrice mat, Embase emb)
        {
            var st = new StepState { Step = step };
            if (p.Sequence.Count == 0 || step < 0 || step >= p.Sequence.Count) return st;
            var op = p.Sequence[step];
            st.Op = op; st.ActiveBend = op.Bend;

            var ang = AnglesAtStep(p, step, out var senses);
            var full = FullFold(p.Segments, ang, senses);
            int vb = op.Bend + 1;   // sommet de la ligne active
            if (vb < 1 || vb >= full.Count) return st;

            // --- ancrage : sommet actif a l'origine, pan arriere le long de -X ---
            Pt o = full[vb];
            for (int i = 0; i < full.Count; i++) full[i] = new Pt(full[i].X - o.X, full[i].Y - o.Y);
            Pt back = full[vb - 1];
            double aBack = Math.Atan2(back.Y, back.X);
            double rot = Math.PI - aBack;   // amener le pan arriere sur 180deg
            double cs = Math.Cos(rot), sn = Math.Sin(rot);
            for (int i = 0; i < full.Count; i++)
            {
                double nx = full[i].X * cs - full[i].Y * sn;
                double ny = full[i].X * sn + full[i].Y * cs;
                full[i] = new Pt(nx, ny);
            }

            // le volet en formage doit MONTER (+Y) : si la moyenne est negative, on mirror en Y
            double sy = 0; int cnt = 0;
            for (int i = vb + 1; i < full.Count; i++) { sy += full[i].Y; cnt++; }
            if (cnt > 0 && sy < 0)
                for (int i = 0; i < full.Count; i++) full[i] = new Pt(full[i].X, -full[i].Y);

            // split
            for (int i = 0; i <= vb; i++) st.BackChain.Add(full[i]);
            for (int i = vb; i < full.Count; i++) st.Forming.Add(full[i]);
            st.ButeeDistance = p.Segments.Count > op.Bend ? p.Segments[op.Bend] : 0;

            st.Collisions = Detect(st, cfg, p, poin, mat, emb);
            return st;
        }

        // Face droite du poincon a la hauteur y (fallback si pas de Poincon.Profil).
        public static double PoinconFaceX(double y, MachineConfig cfg)
        {
            if (y <= 0) return cfg.PoinconPointeLg / 2.0;
            double half = cfg.PoinconPointeLg / 2.0;
            double becTop = half + cfg.BecHauteur * Math.Tan(cfg.DemiPointe);
            double x;
            if (y <= cfg.BecHauteur) x = half + y * Math.Tan(cfg.DemiPointe);
            else x = Math.Max(becTop, cfg.CorpsLg / 2.0);
            if (y > cfg.ColHauteur) x -= cfg.ColRetrait;
            return Math.Max(0, x);
        }

        static List<Collision> Detect(StepState st, MachineConfig cfg, Piece p, Poincon poin, Matrice mat, Embase emb)
        {
            var res = new List<Collision>();
            double ep = Math.Max(0.2, p.Epaisseur);

            // --- contours des outils dans le repere ancre (pointe de pli a l'origine) ---
            var punch = poin != null ? poin.Contour() : null;         // pointe a y=0, +Y
            // on remonte le poincon : la pointe touche la FACE HAUTE de la tole (y = ep),
            // sinon le pan a plat sur la matrice (y=0) rase la pointe -> faux positif.
            if (punch != null) foreach (var q in punch) q[1] += ep;

            var die = DiePoly(mat, st.Op != null ? st.Op.V : 16, out _, out _, out _, out double dieH);
            double punchTop = poin != null ? poin.Hauteur : cfg.PoinconHauteur;
            List<double[]> porteP = null, semelle = null;
            if (emb != null)
            {
                if (emb.PortePoinconLg > 0 && emb.PortePoinconH > 0)
                    porteP = Rect(-emb.PortePoinconLg / 2, punchTop + ep, emb.PortePoinconLg / 2, punchTop + ep + emb.PortePoinconH);
                if (emb.SemelleLg > 0 && emb.SemelleH > 0)
                    semelle = Rect(-emb.SemelleLg / 2, -dieH - emb.SemelleH, emb.SemelleLg / 2, -dieH);
            }

            // largeur du poincon a la hauteur y (repere tole). En dessous de la pointe (y<ep) => 0.
            double PunchW(double y)
            {
                double yy = y - ep;
                if (yy < 0) return 0;
                return poin != null ? poin.DemiLargeur(yy) : PoinconFaceX(yy, cfg);
            }
            // tole DANS l'ame du poincon : elle est formee autour de l'outil, pas une collision.
            bool InAme(Pt q) => Math.Abs(q.X) <= PunchW(q.Y) + ep + 0.5;

            // --- zone morte de formage autour de la pointe (dans le V) ---
            double Vopen0 = mat != null ? mat.VProche(st.Op != null ? st.Op.V : 16).V : 16;
            double zoneY = ep + 6;
            double zoneX = Vopen0 / 2.0 + ep + 4;
            bool InFormage(Pt q) => q.Y <= zoneY && q.Y >= -2 && Math.Abs(q.X) <= zoneX;

            var segs = new List<Pt[]>();
            // pans deja plies cote insertion (back chain) : tous
            for (int i = 0; i + 1 < st.BackChain.Count; i++) segs.Add(new[] { st.BackChain[i], st.BackChain[i + 1] });
            // volet en formage : on saute le riser droit initial (collineaire), on garde les retours
            if (st.Forming.Count >= 2)
            {
                Pt d0 = Norm(new Pt(st.Forming[1].X - st.Forming[0].X, st.Forming[1].Y - st.Forming[0].Y));
                bool riser = true;
                for (int i = 0; i + 1 < st.Forming.Count; i++)
                {
                    Pt dir = Norm(new Pt(st.Forming[i + 1].X - st.Forming[i].X, st.Forming[i + 1].Y - st.Forming[i].Y));
                    if (riser && (dir.X * d0.X + dir.Y * d0.Y) > 0.985) continue;  // encore droit -> ignore
                    riser = false;
                    segs.Add(new[] { st.Forming[i], st.Forming[i + 1] });
                }
            }

            bool hitP = false, hitM = false, hitPP = false, hitSem = false;
            foreach (var s in segs)
            {
                Pt a = s[0], b = s[1];
                if (InFormage(a) && InFormage(b)) continue;
                // POINCON : on ignore la tole qui longe/traverse l'ame (formage autour de l'outil).
                //  Une vraie collision = un retour qui revient PAR LE FLANC -> au moins un bout hors de l'ame.
                if (!hitP && punch != null && !(InAme(a) && InAme(b)) && SegCrossesPoly(a, b, punch)) hitP = true;
                if (!hitM && die != null && (a.Y < -0.6 || b.Y < -0.6) && SegCrossesPoly(a, b, die)) hitM = true;
                if (!hitPP && porteP != null && SegCrossesPoly(a, b, porteP)) hitPP = true;
                if (!hitSem && semelle != null && SegCrossesPoly(a, b, semelle)) hitSem = true;
            }
            if (hitP) res.Add(new Collision("poinçon", "un retour de tôle tape le poinçon", true));
            if (hitM) res.Add(new Collision("matrice", "la tôle tape la matrice", true));
            if (hitPP) res.Add(new Collision("porte-poinçon", "un retour touche l'embase du poinçon", true));
            if (hitSem) res.Add(new Collision("semelle", "la pièce touche la semelle/porte-matrice", true));

            // repli sur repli (la piece se referme sur elle-meme)
            var poly = new List<Pt>();
            for (int i = 0; i < st.BackChain.Count; i++) poly.Add(st.BackChain[i]);
            for (int i = 1; i < st.Forming.Count; i++) poly.Add(st.Forming[i]);
            if (AutoCroise(poly))
                res.Add(new Collision("repli sur repli", "la pièce se referme sur elle-même", true));

            // butee
            if (st.ButeeDistance > cfg.ButeeMax)
                res.Add(new Collision("butée arrière", $"pan de {st.ButeeDistance:0} mm > course butée {cfg.ButeeMax:0} mm", false));

            return res;
        }

        static List<double[]> Rect(double x0, double y0, double x1, double y1)
            => new() { new[] { x0, y0 }, new[] { x1, y0 }, new[] { x1, y1 }, new[] { x0, y1 } };

        static List<double[]> DiePoly(Matrice mat, double v, out double half, out double Vopen, out double vDepth, out double dieH)
        {
            var vf = mat != null ? mat.VProche(v) : new VForm { V = v, AngleDeg = 45 };
            Vopen = vf.V;
            half = (mat != null ? mat.BlocLargeur : 60) / 2.0;
            vDepth = vf.Profondeur > 0 ? vf.Profondeur : (Vopen / 2.0) / Math.Tan(vf.AngleDeg * Math.PI / 360.0);
            dieH = vDepth + 18;
            return new()
            {
                new[] { -half, 0.0 }, new[] { -Vopen / 2, 0.0 }, new[] { 0.0, -vDepth }, new[] { Vopen / 2, 0.0 }, new[] { half, 0.0 },
                new[] { half, -dieH }, new[] { -half, -dieH }
            };
        }

        static bool SegCrossesPoly(Pt a, Pt b, List<double[]> poly)
        {
            int n = poly.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var p3 = new Pt(poly[j][0], poly[j][1]);
                var p4 = new Pt(poly[i][0], poly[i][1]);
                if (Inter(a, b, p3, p4)) return true;
            }
            return false;
        }

        static bool AutoCroise(List<Pt> a)
        {
            for (int i = 0; i + 1 < a.Count; i++)
                for (int j = i + 2; j + 1 < a.Count; j++)
                {
                    if (i == 0 && j == a.Count - 2) continue;
                    if (Inter(a[i], a[i + 1], a[j], a[j + 1])) return true;
                }
            return false;
        }

        static bool Inter(Pt p1, Pt p2, Pt p3, Pt p4)
        {
            double d1 = Cross(p3, p4, p1), d2 = Cross(p3, p4, p2);
            double d3 = Cross(p1, p2, p3), d4 = Cross(p1, p2, p4);
            if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
                ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0))) return true;
            return false;
        }
        static double Cross(Pt a, Pt b, Pt c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        static Pt Norm(Pt v) { double m = Math.Sqrt(v.X * v.X + v.Y * v.Y); return m > 1e-9 ? new Pt(v.X / m, v.Y / m) : new Pt(0, 0); }
    }
}
