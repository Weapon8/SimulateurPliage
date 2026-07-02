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

        // Demi-largeur du poincon a la hauteur y (0 = pointe) — profil reel du 1012 (galbe concave).
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

            var vf = mat != null ? mat.VProche(st.Op.V) : new VForm { V = st.Op.V, AngleDeg = cfg.MatriceAngleDeg };
            double Vopen = vf.V;
            double half = (mat != null ? mat.BlocLargeur : cfg.BlocLargeur) / 2.0;
            double vDepth = vf.Profondeur > 0 ? vf.Profondeur : (Vopen / 2.0) / Math.Tan(vf.AngleDeg * Math.PI / 360.0);
            double dieH = vDepth + 18;
            double hLibre = cfg.HauteurLibre;
            double punchTop = poin != null ? poin.Hauteur : Math.Max(cfg.PoinconHauteur, cfg.BecHauteur + 10);
            double semH = emb != null ? emb.SemelleH : 0, semW = emb != null ? emb.SemelleLg : 0;
            double ppH = emb != null ? emb.PortePoinconH : 0, ppW = emb != null ? emb.PortePoinconLg : 0;

            // --- bornes monde pour cadrer ---
            double minX = -half, maxX = half, minY = -dieH, maxY = hLibre;
            void Acc(double x, double y) { if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y; }
            foreach (var q in st.BackChain) Acc(q.X, q.Y);
            foreach (var q in st.Forming) Acc(q.X, q.Y);
            for (double y = 0; y <= punchTop; y += 10) { double f = PunchFaceX(y); Acc(f, y); Acc(-f, y); }
            Acc(-semW / 2, -dieH - semH); Acc(semW / 2, -dieH - semH);
            Acc(-ppW / 2, punchTop + ppH); Acc(ppW / 2, punchTop + ppH);

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
                var s = new[] { T(-ppW / 2, punchTop), T(ppW / 2, punchTop), T(ppW / 2, punchTop + ppH), T(-ppW / 2, punchTop + ppH) };
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

            // --- poincon (avec creux du col de cygne) ---
            var pun = new List<PointF>();
            for (double y = 0; y <= punchTop; y += 1.5) pun.Add(T(PunchFaceX(y), y));
            for (double y = punchTop; y >= 0; y -= 1.5) pun.Add(T(-PunchFaceX(y), y));
            using (var b = new SolidBrush(CTool)) g.FillPolygon(b, pun.ToArray());
            using (var pn = new Pen(Color.FromArgb(130, 138, 150), 1.2f)) g.DrawPolygon(pn, pun.ToArray());

            // --- hauteur libre ---
            using (var pn = new Pen(Color.FromArgb(90, 98, 110), 1f) { DashStyle = DashStyle.Dash })
            {
                g.DrawLine(pn, T(minX, hLibre), T(maxX, hLibre));
                using var f = new Font("Segoe UI", 8);
                g.DrawString($"hauteur libre {hLibre:0}", f, new SolidBrush(CMuted), T(minX, hLibre).X + 4, T(minX, hLibre).Y - 16);
            }

            // --- piece : pan arriere (neutre) + volet en formage (couleur/rouge) ---
            bool hit = st.Collisions.Count > 0;
            float pw = (float)Math.Max(2.5, piece.Epaisseur * sc);
            DrawChain(g, st.BackChain, CMuted, pw);
            DrawChain(g, st.Forming, hit ? CRouge : activeCol, pw);

            // sommet de pli actif
            var o = T(0, 0);
            using (var b = new SolidBrush(activeCol)) g.FillEllipse(b, o.X - 4, o.Y - 4, 8, 8);

            if (hit)
            {
                double topY = 0; foreach (var q in st.Forming) if (q.Y > topY) topY = q.Y;
                var pth = T(0, topY);
                using var pn = new Pen(CRouge, 2f);
                g.DrawEllipse(pn, pth.X - 9, pth.Y - 9, 18, 18);
            }

            DrawLegend(g);
        }

        void DrawChain(Graphics g, List<Pt> chain, Color col, float w)
        {
            if (chain == null || chain.Count < 2) return;
            var pts = new PointF[chain.Count];
            for (int i = 0; i < chain.Count; i++) pts[i] = T(chain[i]);
            using var pn = new Pen(col, w) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            g.DrawLines(pn, pts);
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
            string s = "gris = pan sur matrice   ·   bleu = pli direct   ·   vert = reprise   ·   rouge = collision";
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
