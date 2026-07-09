using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SimulateurPliage
{
    // Vue en coupe. Repere machine : origine = pointe du V, +Y vers le haut,
    // axe du poincon = X=0. Le pan cote BUTEE est a droite (col de cygne),
    // le cote OPERATEUR est a gauche.
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
        static readonly Color CDim   = Color.FromArgb(120, 130, 145);
        static readonly Color CRouge = Color.FromArgb(229, 83, 75);
        static readonly Color CAccent= Color.FromArgb(255, 122, 26);
        static readonly Color CGrid  = Color.FromArgb(34, 40, 49);
        static readonly Color CTxt   = Color.FromArgb(200, 208, 218);
        static readonly Color CFutur = Color.FromArgb(110, 122, 140);   // pli pas encore forme

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

        double PunchFaceX(double y)
        {
            if (poin == null) return FoldEngine.PoinconFaceX(y, cfg);
            return Math.Max(0.2, poin.DemiLargeur(y));
        }

        PointF T(double x, double y) => new PointF((float)(ox + x * sc), (float)(oy - y * sc));
        PointF T(Pt p) => T(p.X, p.Y);

        // chaine complete de la fibre neutre, assise sur la matrice.
        // chain[k] = sommet k ; le pan k relie chain[k] a chain[k+1] et vaut Segments[k].
        // Le pli k-1 est situe au sommet k.
        List<Pt> Chain(double seat)
        {
            var c = new List<Pt>();
            foreach (var p in st.BackChain) c.Add(new Pt(p.X, p.Y + seat));
            for (int i = 1; i < st.Forming.Count; i++) c.Add(new Pt(st.Forming[i].X, st.Forming[i].Y + seat));
            return c;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            if (st == null || st.Op == null) { Center(g, "Ajoute des plis et une séquence"); return; }

            double ep   = Math.Max(0.2, piece.Epaisseur);
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

            var chain = Chain(seat);

            // --- bornes monde ---
            double minX = -half, maxX = half, minY = -dieH, maxY = hLibre;
            void Acc(double x, double y) { if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y; }
            foreach (var q in chain) Acc(q.X, q.Y);
            if (poin != null) foreach (var p in poin.Contour()) Acc(p[0], p[1] + ep);
            else for (double y = 0; y <= punchTop; y += 10) { double f = PunchFaceX(y); Acc(f, y + ep); Acc(-f, y + ep); }
            Acc(-semW / 2, -dieH - semH); Acc(semW / 2, -dieH - semH);
            Acc(-ppW / 2, punchTop + ep + ppH); Acc(ppW / 2, punchTop + ep + ppH);

            double dx = Math.Max(1, maxX - minX), dy = Math.Max(1, maxY - minY);
            double m = 56;
            sc = Math.Min((Width - 2 * m) / dx, (Height - 2 * m) / dy);
            if (sc <= 0 || double.IsInfinity(sc)) sc = 1;
            ox = m - minX * sc + (Width - 2 * m - dx * sc) / 2;
            oy = Height - m + minY * sc - (Height - 2 * m - dy * sc) / 2;

            DrawGrid(g);

            var cEmb = Color.FromArgb(58, 64, 74);
            if (semW > 0 && semH > 0)
            {
                var s = new[] { T(-semW / 2, -dieH), T(semW / 2, -dieH), T(semW / 2, -dieH - semH), T(-semW / 2, -dieH - semH) };
                using var b = new SolidBrush(cEmb); g.FillPolygon(b, s);
            }
            if (ppW > 0 && ppH > 0)
            {
                var s = new[] { T(-ppW / 2, punchTop + ep), T(ppW / 2, punchTop + ep), T(ppW / 2, punchTop + ep + ppH), T(-ppW / 2, punchTop + ep + ppH) };
                using var b = new SolidBrush(cEmb); g.FillPolygon(b, s);
            }

            // --- matrice ---
            var die = new List<PointF>
            {
                T(-half, 0), T(-Vopen / 2, 0), T(0, -vDepth), T(Vopen / 2, 0), T(half, 0),
                T(half, -dieH), T(-half, -dieH)
            };
            using (var b = new SolidBrush(CDie)) g.FillPolygon(b, die.ToArray());
            using (var pn = new Pen(Color.FromArgb(110, 120, 132), 1.4f)) g.DrawPolygon(pn, die.ToArray());

            // --- axe du poincon (bissectrice du pli actif) ---
            using (var pn = new Pen(Color.FromArgb(60, 70, 84), 1f) { DashStyle = DashStyle.DashDot })
                g.DrawLine(pn, T(0, -vDepth), T(0, maxY));

            // --- poincon ---
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

            if (utile < punchTop)
                using (var pn = new Pen(Color.FromArgb(120, 128, 140), 1f) { DashStyle = DashStyle.Dot })
                    g.DrawLine(pn, T(-PunchFaceX(utile) - 3, utile + ep), T(PunchFaceX(utile) + 3, utile + ep));

            using (var pn = new Pen(Color.FromArgb(90, 98, 110), 1f) { DashStyle = DashStyle.Dash })
            {
                g.DrawLine(pn, T(minX, hLibre), T(maxX, hLibre));
                using var f = new Font("Segoe UI", 8);
                g.DrawString($"hauteur libre {hLibre:0}", f, new SolidBrush(CMuted), T(minX, hLibre).X + 4, T(minX, hLibre).Y - 16);
            }

            // --- tole ---
            bool hit = st.Collisions.Count > 0;
            DrawSheet(g, chain, hit ? CRouge : activeCol, ep);
            DrawPansEtPlis(g, chain, hit ? CRouge : activeCol);

            DrawCotesLaterales(g, minX, maxX, minY);
            DrawInfoBox(g, ep, seat, Vopen);
            DrawLegend(g);
        }

        // ---------- tole en ruban ----------
        void DrawSheet(Graphics g, List<Pt> chain, Color col, double ep)
        {
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
        }

        // ---------- cotes de pans + reperes de plis ----------
        void DrawPansEtPlis(Graphics g, List<Pt> chain, Color col)
        {
            int n = piece.Segments.Count;                 // nb de pans
            if (chain.Count < n + 1) return;

            var ang = FoldEngine.AnglesAtStep(piece, st.Step, out _);
            using var fPan  = new Font("Consolas", 9.5f, FontStyle.Bold);
            using var fPli  = new Font("Consolas", 8.5f, FontStyle.Bold);
            using var sfC   = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            // --- longueur de chaque pan, posee du cote exterieur du pli ---
            for (int k = 0; k < n; k++)
            {
                Pt a = chain[k], b = chain[k + 1];
                double mx = (a.X + b.X) / 2, my = (a.Y + b.Y) / 2;
                double tx = b.X - a.X, ty = b.Y - a.Y;
                double L = Math.Sqrt(tx * tx + ty * ty);
                if (L < 1e-6) continue;
                // normale : on decale vers l'exterieur (loin de l'axe du poincon)
                double nx = -ty / L, ny = tx / L;
                if (mx * nx + my * ny < 0) { nx = -nx; ny = -ny; }
                double off = 13 / sc;

                var p = T(mx + nx * off, my + ny * off);
                string t = piece.Segments[k].ToString("0.#");
                var sz = g.MeasureString(t, fPan);
                using (var bg = new SolidBrush(Color.FromArgb(190, 20, 24, 31)))
                    g.FillRectangle(bg, p.X - sz.Width / 2 - 3, p.Y - sz.Height / 2 - 1, sz.Width + 6, sz.Height + 2);
                g.DrawString(t, fPan, new SolidBrush(CDim), p, sfC);
            }

            // --- sommets : pli actif, plis deja faits, plis a venir ---
            for (int k = 1; k < n; k++)
            {
                int bend = k - 1;
                var p = T(chain[k]);
                bool actif = bend == st.ActiveBend;
                bool fait  = bend < ang.Length && Math.Abs(ang[bend] - 180.0) > 0.01;

                if (actif)
                {
                    using var b = new SolidBrush(CAccent);
                    g.FillEllipse(b, p.X - 5, p.Y - 5, 10, 10);
                }
                else if (fait)
                {
                    using var b = new SolidBrush(col);
                    g.FillEllipse(b, p.X - 3.2f, p.Y - 3.2f, 6.4f, 6.4f);
                }
                else
                {
                    // pli PAS ENCORE forme : cercle creux + etiquette.
                    // C'est lui qui explique pourquoi deux pans consecutifs sont alignes.
                    using (var pn = new Pen(CFutur, 1.6f) { DashStyle = DashStyle.Dot })
                        g.DrawEllipse(pn, p.X - 5, p.Y - 5, 10, 10);
                    string lab = "P" + (bend + 1);
                    var sz = g.MeasureString(lab, fPli);
                    g.DrawString(lab, fPli, new SolidBrush(CFutur), p.X - sz.Width / 2, p.Y - sz.Height - 8);
                }
            }
        }

        // ---------- reperes de cote : OPERATEUR / BUTEE ----------
        void DrawCotesLaterales(Graphics g, double minX, double maxX, double minY)
        {
            using var f = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            float y = (float)(oy - minY * sc) + 6;
            if (y > Height - 40) y = Height - 40;

            using var bl = new SolidBrush(Color.FromArgb(120, 132, 148));
            g.DrawString("◀  OPÉRATEUR", f, bl, 14, y);
            var t = "BUTÉE ARRIÈRE  ▶";
            var sz = g.MeasureString(t, f);
            g.DrawString(t, f, bl, Width - sz.Width - 14, y);
        }

        // ---------- encart d'infos, hors du dessin ----------
        void DrawInfoBox(Graphics g, double ep, double seat, double Vopen)
        {
            using var f = new Font("Consolas", 9f);
            using var fb = new Font("Consolas", 9f, FontStyle.Bold);
            var lines = new[]
            {
                $"pli actif   P{st.ActiveBend + 1}",
                $"angle       {st.AngleActif:0}°",
                $"V           {(int)st.Op.V}",
                $"butée       {st.ButeeDistance:0.#} mm",
                $"épaisseur   {ep:0.##} mm",
                $"assise      {seat:0.00} mm",
            };
            float w = 0, h = 0;
            foreach (var l in lines) { var s = g.MeasureString(l, f); w = Math.Max(w, s.Width); h += s.Height; }
            var r = new RectangleF(14, 14, w + 20, h + 14);
            using (var b = new SolidBrush(Color.FromArgb(200, 24, 29, 37))) g.FillRectangle(b, r);
            using (var pn = new Pen(Color.FromArgb(48, 55, 66), 1f)) g.DrawRectangle(pn, r.X, r.Y, r.Width, r.Height);
            float yy = r.Y + 7;
            foreach (var l in lines)
            {
                bool head = l.StartsWith("pli actif");
                g.DrawString(l, head ? fb : f, new SolidBrush(head ? CAccent : CMuted), r.X + 10, yy);
                yy += g.MeasureString(l, f).Height;
            }
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
            string s = "bleu = tôle   ·   vert = reprise   ·   rouge = collision   ·   cercle pointillé = pli pas encore formé";
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
