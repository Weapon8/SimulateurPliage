using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SimulateurPliage
{
    public class SectionPanel : Panel
    {
        readonly MachineConfig cfg;
        StepState st;
        Piece piece;
        Color activeCol = Color.SteelBlue;
        Matrice mat;
        Poincon poin;
        Embase emb;

        static readonly Color CTool  = Color.FromArgb(96, 104, 116);
        static readonly Color CDie   = Color.FromArgb(74, 82, 94);
        static readonly Color CMuted = Color.FromArgb(150, 160, 172);
        static readonly Color CRouge = Color.FromArgb(229, 83, 75);
        static readonly Color CGrid  = Color.FromArgb(34, 40, 49);
        static readonly Color CTxt   = Color.FromArgb(200, 208, 218);

        double sc = 1, ox, oy;

        public SectionPanel(MachineConfig c)
        {
            cfg = c;
            DoubleBuffered = true;
        }

        public void SetState(StepState state, Piece p, Color active)
        {
            st = state; piece = p; activeCol = active; Invalidate();
        }

        public void SetTools(Matrice m, Poincon p, Embase e) { mat = m; poin = p; emb = e; Invalidate(); }

        // Demi-largeur du poincon a la hauteur y (0 = pointe) — profil reel.
        double PunchFaceX(double y)
        {
            if (poin == null) return FoldEngine.PoinconFaceX(y, cfg);
            return Math.Max(0.2, poin.DemiLargeur(y));
        }

        PointF T(double x, double y) => new PointF((float)(ox + x * sc), (float)(oy - y * sc));
        PointF T(Pt p) => T(p.X, p.Y);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            if (st == null || st.Op == null) { Center(g, "Ajoute des plis et une séquence"); return; }

            double ep = Math.Max(0.2, piece.Epaisseur);

            // assise calculee par FoldEngine : depend de l'angle du pli en cours.
            // (a 180° -> ep/2 : la tole pose a plat ; a 90° -> ~0.29*ep ; en dessous, le pli plonge dans le V)
            double seat = st.Seat;

            var vf = mat != null ? mat.VProche(st.Op.V) : new VForm { V = st.Op.V, AngleDeg = cfg.MatriceAngleDeg };
            double Vopen = vf.V;
            double half = (mat != null ? mat.BlocLargeur : cfg.BlocLargeur) / 2.0;
            double vDepth = vf.Profondeur > 0 ? vf.Profondeur : (Vopen / 2.0) / Math.Tan(vf.AngleDeg * Math.PI / 360.0);
            double dieH = vDepth + 18;
            double hLibre = cfg.HauteurLibre;
            double punchTop = poin != null ? poin.Hauteur : Math.Max(cfg.PoinconHauteur, cfg.BecHauteur + 10);
            double utile = poin != null && poin.HauteurUtile > 0 ? poin.HauteurUtile : punchTop;
            double semH = emb != null ? emb.SemelleH : 0, semW = emb != null ? emb.SemelleLg : 0;
            double ppH = emb != null ? emb.PortePoinconH : 0, ppW = emb != null ? emb.PortePoinconLg : 0;

            // --- bornes monde pour cadrer ---
            double minX = -half, maxX = half, minY = -dieH, maxY = hLibre;
            void Acc(double x, double y) { if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y; }
            foreach (var q in st.BackChain) Acc(q.X, q.Y + seat);
            foreach (var q in st.Forming) Acc(q.X, q.Y + seat);
            if (poin != null) foreach (var p in poin.Contour()) Acc(p[0], p[1] + ep);
            else for (double y = 0; y <= punchTop; y += 10) { double f = PunchFaceX(y); Acc(f, y + ep); Acc(-f, y + ep); }
            Acc(-semW / 2, -dieH - semH); Acc(semW / 2, -dieH - semH);
            Acc(-ppW / 2, punchTop + ep + ppH); Acc(ppW / 2, punchTop + ep + ppH);

            double dx = Math.Max(1, maxX - minX), dy = Math.Max(1, maxY - minY);
            double m = 40;
            sc = Math.Min((Width - 2 * m) / dx, (Height - 2 * m) / dy);
            if (sc <= 0 || double.IsInfinity(sc)) sc = 1;
            ox = m - minX * sc + (Width - 2 * m - dx * sc) / 2;
            oy = Height - m + minY * sc - (Height - 2 * m - dy * sc) / 2;

            DrawGrid(g);

            var cEmb = Color.FromArgb(58, 64, 74);
            // --- semelle / porte-matrice ---
            if (semW > 0 && semH > 0)
            {
                var s = new[] { T(-semW / 2, -dieH), T(semW / 2, -dieH), T(semW / 2, -dieH - semH), T(-semW / 2, -dieH - semH) };
                using var b = new SolidBrush(cEmb); g.FillPolygon(b, s);
            }
            // --- porte-poincon ---
            if (ppW > 0 && ppH > 0)
            {
                var s = new[] { T(-ppW / 2, punchTop + ep), T(ppW / 2, punchTop + ep), T(ppW / 2, punchTop + ep + ppH), T(-ppW / 2, punchTop + ep + ppH) };
                using var b = new SolidBrush(cEmb); g.FillPolygon(b, s);
            }

            // --- matrice (die + V) ---
            var die = new List<PointF>
            {
                T(-half, 0), T(-Vopen / 2, 0), T(0, -vDepth), T(Vopen / 2, 0), T(half, 0),
                T(half, -dieH), T(-half, -dieH)
            };
            using (var b = new SolidBrush(CDie)) g.FillPolygon(b, die.ToArray());
            using (var pn = new Pen(Color.FromArgb(110, 120, 132), 1.4f)) g.DrawPolygon(pn, die.ToArray());

            // --- axe du poincon (= bissectrice du pli actif) ---
            using (var pn = new Pen(Color.FromArgb(60, 70, 84), 1f) { DashStyle = DashStyle.DashDot })
                g.DrawLine(pn, T(0, -vDepth), T(0, maxY));

            // --- poincon : CONTOUR REEL, pointe posee a y=ep ---
            var punchC = poin != null ? poin.Contour() : null;
            if (punchC != null && punchC.Count >= 3)
            {
                var pun = new PointF[punchC.Count];
                for (int i = 0; i < punchC.Count; i++) pun[i] = T(punchC[i][0], punchC[i][1] + ep);
                using (var b = new SolidBrush(CTool)) g.FillPolygon(b, pun);
                using (var pn = new Pen(Color.FromArgb(130, 138, 150), 1.2f)) g.DrawPolygon(pn, pun);
            }
            else
            {
                var pun = new List<PointF>();
                for (double y = 0; y <= punchTop; y += 1.5) pun.Add(T(PunchFaceX(y), y + ep));
                for (double y = punchTop; y >= 0; y -= 1.5) pun.Add(T(-PunchFaceX(y), y + ep));
                using (var b = new SolidBrush(CTool)) g.FillPolygon(b, pun.ToArray());
                using (var pn = new Pen(Color.FromArgb(130, 138, 150), 1.2f)) g.DrawPolygon(pn, pun.ToArray());
            }

            // --- repere hauteur utile du poincon ---
            if (utile < punchTop)
                using (var pn = new Pen(Color.FromArgb(120, 128, 140), 1f) { DashStyle = DashStyle.Dot })
                    g.DrawLine(pn, T(-PunchFaceX(utile) - 3, utile + ep), T(PunchFaceX(utile) + 3, utile + ep));

            // --- hauteur libre ---
            using (var pn = new Pen(Color.FromArgb(90, 98, 110), 1f) { DashStyle = DashStyle.Dash })
            {
                g.DrawLine(pn, T(minX, hLibre), T(maxX, hLibre));
                using var f = new Font("Segoe UI", 8);
                g.DrawString($"hauteur libre {hLibre:0}", f, new SolidBrush(CMuted), T(minX, hLibre).X + 4, T(minX, hLibre).Y - 16);
            }

            // --- tole ---
            bool hit = st.Collisions.Count > 0;
            DrawSheet(g, st, hit ? CRouge : activeCol, ep, seat);

            // sommet de pli actif
            var o = T(0, seat);
            using (var b = new SolidBrush(activeCol)) g.FillEllipse(b, o.X - 4, o.Y - 4, 8, 8);

            // --- cotation angle du pli en cours ---
            using (var f = new Font("Consolas", 9, FontStyle.Bold))
                g.DrawString($"α = {st.AngleActif:0}°   ·   assise {seat:0.00} mm",
                    f, new SolidBrush(hit ? CRouge : CMuted), o.X + 12, o.Y + 6);

            if (hit)
            {
                double topY = 0; foreach (var q in st.Forming) if (q.Y > topY) topY = q.Y;
                var pth = T(0, topY + seat);
                using var pn = new Pen(CRouge, 2f);
                g.DrawEllipse(pn, pth.X - 9, pth.Y - 9, 18, 18);
            }

            DrawLegend(g);
        }

        // ---------- tole en ruban (fibre neutre offsetee +/- ep/2, jointures mitrees) ----------
        void DrawSheet(Graphics g, StepState st, Color col, double ep, double seat)
        {
            var chain = new List<Pt>();
            foreach (var p in st.BackChain) chain.Add(new Pt(p.X, p.Y + seat));
            for (int i = 1; i < st.Forming.Count; i++) chain.Add(new Pt(st.Forming[i].X, st.Forming[i].Y + seat));
            if (chain.Count < 2) return;

            var outer = OffsetMiter(chain, +ep / 2.0);
            var inner = OffsetMiter(chain, -ep / 2.0);
            var poly = new List<PointF>(outer.Count + inner.Count);
            foreach (var p in outer) poly.Add(T(p));
            for (int i = inner.Count - 1; i >= 0; i--) poly.Add(T(inner[i]));
            using (var b = new SolidBrush(Color.FromArgb(150, col))) g.FillPolygon(b, poly.ToArray());

            var line = new PointF[chain.Count];
            for (int i = 0; i < chain.Count; i++) line[i] = T(chain[i]);
            using (var pn = new Pen(col, 3.0f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawLines(pn, line);
            using (var b = new SolidBrush(col))
                foreach (var p in line) g.FillEllipse(b, p.X - 2.4f, p.Y - 2.4f, 4.8f, 4.8f);
        }

        static List<Pt> OffsetMiter(List<Pt> pts, double d)
        {
            int n = pts.Count;
            var outp = new List<Pt>(n);
            for (int i = 0; i < n; i++)
            {
                Pt p = pts[i];
                double n0x = 0, n0y = 0, n1x = 0, n1y = 0;
                bool has0 = false, has1 = false;
                if (i > 0) { var t = NormV(p.X - pts[i - 1].X, p.Y - pts[i - 1].Y); n0x = -t.Y; n0y = t.X; has0 = true; }
                if (i < n - 1) { var t = NormV(pts[i + 1].X - p.X, pts[i + 1].Y - p.Y); n1x = -t.Y; n1y = t.X; has1 = true; }
                double mx, my;
                if (!has0) { mx = n1x; my = n1y; }
                else if (!has1) { mx = n0x; my = n0y; }
                else
                {
                    var mm = NormV(n0x + n1x, n0y + n1y);
                    double c = Math.Max(0.25, mm.X * n0x + mm.Y * n0y);
                    mx = mm.X / c; my = mm.Y / c;
                }
                outp.Add(new Pt(p.X + mx * d, p.Y + my * d));
            }
            return outp;
        }

        static Pt NormV(double x, double y)
        {
            double m = Math.Sqrt(x * x + y * y);
            return m > 1e-9 ? new Pt(x / m, y / m) : new Pt(0, 0);
        }

        void DrawGrid(Graphics g)
        {
            using var pn = new Pen(CGrid, 1f);
            for (int gx = 0; gx < Width; gx += 40) g.DrawLine(pn, gx, 0, gx, Height);
            for (int gy = 0; gy < Height; gy += 40) g.DrawLine(pn, 0, gy, Width, gy);
        }

        void DrawLegend(Graphics g)
        {
            using var f = new Font("Segoe UI", 8.5f);
            string s = "bleu = tôle   ·   vert = reprise   ·   rouge = collision outillage";
            g.DrawString(s, f, new SolidBrush(CMuted), 12, Height - 22);
        }

        void Center(Graphics g, string t)
        {
            using var f = new Font("Segoe UI", 11);
            var sz = g.MeasureString(t, f);
            g.DrawString(t, f, new SolidBrush(CTxt), (Width - sz.Width) / 2, (Height - sz.Height) / 2);
        }
    }
}
