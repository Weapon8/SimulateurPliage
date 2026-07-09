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
    // Repere machine : origine = pointe du V (y=0 = face superieure de la matrice),
    // +Y vers le haut, axe du poincon = axe X=0.
    // Convention laterale : le pan BUTEE est a DROITE (+X), cote col de cygne, pour que
    // le retour deja plie remonte dans le creux. Le volet en formage monte a gauche (-X).
    public sealed class StepState
    {
        public int    Step;             // index de l'operation courante
        public int    ActiveBend;       // ligne de pli formee a cette etape
        public Operation Op;            // l'operation courante
        public List<Pt> BackChain = new();  // fibre neutre cote butee ; dernier point = sommet actif
        public List<Pt> Forming   = new();  // fibre neutre cote formage ; premier point = sommet actif
        public double ButeeDistance;    // longueur du pan cote butee
        public double Seat;             // Y du sommet de pli (fibre neutre) dans le repere machine
        public double AngleActif = 180; // angle interieur du pli en cours
        public List<Collision> Collisions = new();
    }

    public static class FoldEngine
    {
        const double D2R = Math.PI / 180.0;

        // Cote du col de cygne sur le contour du poincon : +1 = creux a droite (+X).
        // Le pan cote butee (retours deja plies) est amene de ce cote-la, pour qu'il
        // puisse remonter dans le creux. Passer a -1 pour un poincon mirroir.
        const int COL_SIGNE = +1;

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
            int vb = op.Bend + 1;                       // sommet de la ligne active
            if (vb < 1 || vb + 1 >= full.Count) return st;

            // --- 1) sommet actif a l'origine ---
            Pt o = full[vb];
            for (int i = 0; i < full.Count; i++) full[i] = new Pt(full[i].X - o.X, full[i].Y - o.Y);

            // --- 2) BISSECTRICE du pli actif -> +Y (axe du poincon) ---
            // L'axe du poincon est la bissectrice, PAS une des deux faces :
            // les deux pans remontent symetriquement a +/- alpha/2 de la verticale.
            Pt u = Norm(full[vb - 1]);                  // direction vers le pan butee
            Pt w = Norm(full[vb + 1]);                  // direction vers le pan en formage
            Pt bis = new Pt(u.X + w.X, u.Y + w.Y);
            double bl = Math.Sqrt(bis.X * bis.X + bis.Y * bis.Y);
            if (bl < 1e-6) bis = new Pt(-u.Y, u.X);     // pli encore a 180 : normale au pan
            else           bis = new Pt(bis.X / bl, bis.Y / bl);

            double rot = Math.PI / 2.0 - Math.Atan2(bis.Y, bis.X);
            double cs = Math.Cos(rot), sn = Math.Sin(rot);
            for (int i = 0; i < full.Count; i++)
                full[i] = new Pt(full[i].X * cs - full[i].Y * sn, full[i].X * sn + full[i].Y * cs);

            // --- 3) pan butee du COTE DU COL DE CYGNE (+X) ---
            // Le retour deja plie remonte dans le creux du poincon ; le volet en formage
            // part a l'oppose, le long de la face pleine du bec.
            if (full[vb - 1].X * COL_SIGNE < 0)
                for (int i = 0; i < full.Count; i++) full[i] = new Pt(-full[i].X, full[i].Y);

            // --- 4) assise : la FACE HAUTE du pli touche la pointe R (y = ep) ---
            //   seat(alpha) = ep - (ep/2) / sin(alpha/2)
            //   alpha=180 -> ep/2 (tole a plat sur la matrice)
            //   alpha=90  -> ~0.29*ep      alpha<~60 -> negatif : le pli plonge dans le V
            double ep = Math.Max(0.2, p.Epaisseur);
            double alpha = (op.Bend < ang.Length) ? ang[op.Bend] : 180.0;
            st.AngleActif = alpha;
            double s2 = Math.Sin(alpha * D2R / 2.0);
            double seat = s2 > 1e-6 ? ep - (ep / 2.0) / s2 : -ep;
            DiePoly(mat, op.V, out _, out _, out double vDepthA, out _);
            st.Seat = Math.Max(seat, -(vDepthA - 0.5));   // ne traverse pas le fond du V

            // --- split ---
            for (int i = 0; i <= vb; i++) st.BackChain.Add(full[i]);
            for (int i = vb; i < full.Count; i++) st.Forming.Add(full[i]);
            st.ButeeDistance = p.Segments.Count > op.Bend ? p.Segments[op.Bend] : 0;

            st.Collisions = Detect(st, cfg, p, poin, mat, emb);
            return st;
        }

        // Face droite du poincon a la hauteur y (fallback si pas de contour reel).
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
            double seat = st.Seat;

            // --- contours des outils dans le repere machine ---
            var punch = poin != null ? poin.Contour() : null;      // pointe a y=0 dans son repere
            if (punch != null) foreach (var q in punch) q[1] += ep; // pointe au contact face haute

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

            Pt Sh(Pt q) => new Pt(q.X, q.Y + seat);

            // adj = pan directement forme sur la pointe -> il EPOUSE l'outil, jamais une collision.
            var segs = new List<(Pt a, Pt b, bool adj)>();
            int nbk = st.BackChain.Count;
            for (int i = 0; i + 1 < nbk; i++)
                segs.Add((Sh(st.BackChain[i]), Sh(st.BackChain[i + 1]), i == nbk - 2));
            for (int i = 0; i + 1 < st.Forming.Count; i++)
                segs.Add((Sh(st.Forming[i]), Sh(st.Forming[i + 1]), i == 0));

            bool hitP = false, hitM = false, hitPP = false, hitSem = false;
            foreach (var s in segs)
            {
                if (!s.adj)
                {
                    if (!hitP && punch != null && SegCrossesPoly(s.a, s.b, punch)) hitP = true;
                    if (!hitM && die != null && (s.a.Y < -0.6 || s.b.Y < -0.6) && SegCrossesPoly(s.a, s.b, die)) hitM = true;
                }
                if (!hitPP && porteP != null && SegCrossesPoly(s.a, s.b, porteP)) hitPP = true;
                if (!hitSem && semelle != null && SegCrossesPoly(s.a, s.b, semelle)) hitSem = true;
            }
            if (hitP)   res.Add(new Collision("poinçon", "un retour de tôle tape le poinçon", true));
            if (hitM)   res.Add(new Collision("matrice", "la tôle tape la matrice", true));
            if (hitPP)  res.Add(new Collision("porte-poinçon", "un retour touche l'embase du poinçon", true));
            if (hitSem) res.Add(new Collision("semelle", "la pièce touche la semelle/porte-matrice", true));

            // bec du poincon plus ouvert que le pli demande
            double bec = poin != null ? poin.AngleDeg : cfg.PoinconAngleDeg;
            if (st.AngleActif < bec - 0.5)
                res.Add(new Collision("bec poinçon", $"pli {st.AngleActif:0}° < bec {bec:0}° — poinçon trop épais", true));

            // hauteur libre
            double top = double.MinValue;
            foreach (var q in st.BackChain) top = Math.Max(top, q.Y + seat);
            foreach (var q in st.Forming)   top = Math.Max(top, q.Y + seat);
            if (top > cfg.HauteurLibre)
                res.Add(new Collision("hauteur libre", $"la pièce monte à {top:0} mm > {cfg.HauteurLibre:0} mm", false));

            // repli sur repli (la piece se referme sur elle-meme)
            var poly = new List<Pt>();
            for (int i = 0; i < st.BackChain.Count; i++) poly.Add(st.BackChain[i]);
            for (int i = 1; i < st.Forming.Count; i++)   poly.Add(st.Forming[i]);
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

        // point a l'interieur d'un polygone (ray casting) — utilitaire.
        public static bool PointInPoly(Pt p, List<double[]> poly)
        {
            bool inside = false;
            int n = poly.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = poly[i][0], yi = poly[i][1], xj = poly[j][0], yj = poly[j][1];
                if (((yi > p.Y) != (yj > p.Y)) &&
                    (p.X < (xj - xi) * (p.Y - yi) / (yj - yi + 1e-12) + xi))
                    inside = !inside;
            }
            return inside;
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
            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
                   ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }
        static double Cross(Pt a, Pt b, Pt c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        static Pt Norm(Pt v) { double m = Math.Sqrt(v.X * v.X + v.Y * v.Y); return m > 1e-9 ? new Pt(v.X / m, v.Y / m) : new Pt(0, 0); }
    }
}
