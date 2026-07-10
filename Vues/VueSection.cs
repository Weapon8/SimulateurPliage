using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SimulateurPliage.Materiel;
using SimulateurPliage.Pliage;

namespace SimulateurPliage.Vues
{
    /// <summary>Vue de section : outillage + tôle, dans le repère ancré sur le pli actif.</summary>
    public class VueSection : Panel
    {
        EtatEtape etat;
        Piece piece;
        Plieuse plieuse;
        Poincon poincon;
        Matrice matrice;
        Embase embase;

        double sc = 1, ox, oy;

        public VueSection()
        {
            DoubleBuffered = true;
            BackColor = Theme.Fond;
        }

        public void Afficher(EtatEtape e, Piece p) { etat = e; piece = p; Invalidate(); }

        public void Outillage(Plieuse pl, Poincon po, Matrice ma, Embase em)
        { plieuse = pl; poincon = po; matrice = ma; embase = em; Invalidate(); }

        PointF T(double x, double y) => new((float)(ox + x * sc), (float)(oy - y * sc));
        PointF T(Pt p) => T(p.X, p.Y);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Fond);

            if (etat?.Op == null) { Centre(g, "Ajoute des plis et une séquence"); return; }

            double ep = Math.Max(0.2, piece.Epaisseur);
            // Ancrage bissectrice : le sommet du pli est a l'origine, sous la pointe du
            // poincon. La face haute de la tole est donc a y = +ep/2 au sommet.
            double assise = 0;

            var matriceC = matrice?.Contour(etat.Op.V);
            var poinconC = poincon?.Contour();
            double hLibre = plieuse?.HauteurLibre ?? 120;

            if (!Cadrer(g, ep, assise, matriceC, poinconC, hLibre)) return;

            Grille(g);
            DessinerEmbases(g, ep, matriceC);
            if (matriceC != null) Polygone(g, matriceC, 0, Theme.Matrice, Color.FromArgb(110, 120, 132));
            if (poinconC != null) Polygone(g, poinconC, ep / 2.0, Theme.Outil, Color.FromArgb(130, 138, 150));
            HauteurLibre(g, hLibre);
            DessinerTole(g, ep, assise);
            Legende(g);
        }

        bool Cadrer(Graphics g, double ep, double assise,
                    List<double[]> matriceC, List<double[]> poinconC, double hLibre)
        {
            double minX = -40, maxX = 40, minY = -40, maxY = hLibre;
            void Acc(double x, double y)
            {
                if (x < minX) minX = x; if (x > maxX) maxX = x;
                if (y < minY) minY = y; if (y > maxY) maxY = y;
            }

            foreach (var q in etat.PanArriere) Acc(q.X, q.Y + assise);
            foreach (var q in etat.Formage) Acc(q.X, q.Y + assise);
            if (matriceC != null) foreach (var p in matriceC) Acc(p[0], p[1]);
            if (poinconC != null) foreach (var p in poinconC) Acc(p[0], p[1] + ep / 2.0);

            double dx = Math.Max(1, maxX - minX), dy = Math.Max(1, maxY - minY);
            const double m = 40;
            sc = Math.Min((Width - 2 * m) / dx, (Height - 2 * m) / dy);
            if (sc <= 0 || double.IsInfinity(sc)) return false;

            ox = m - minX * sc + (Width - 2 * m - dx * sc) / 2;
            oy = Height - m + minY * sc - (Height - 2 * m - dy * sc) / 2;
            return true;
        }

        void DessinerEmbases(Graphics g, double ep, List<double[]> matriceC)
        {
            if (embase == null) return;
            using var b = new SolidBrush(Theme.Embase);

            if (embase.PortePoinconLg > 0 && embase.PortePoinconH > 0)
            {
                double haut = (poincon?.Hauteur ?? 120) + ep / 2.0;
                double w = embase.PortePoinconLg / 2;
                g.FillPolygon(b, new[]
                {
                    T(-w, haut), T(w, haut),
                    T(w, haut + embase.PortePoinconH), T(-w, haut + embase.PortePoinconH)
                });
            }

            if (matriceC != null && embase.SemelleLg > 0 && embase.SemelleH > 0)
            {
                double bas = double.MaxValue;
                foreach (var p in matriceC) bas = Math.Min(bas, p[1]);
                double w = embase.SemelleLg / 2;
                g.FillPolygon(b, new[]
                {
                    T(-w, bas), T(w, bas),
                    T(w, bas - embase.SemelleH), T(-w, bas - embase.SemelleH)
                });
            }
        }

        void Polygone(Graphics g, List<double[]> pts, double decalageY, Color fond, Color bord)
        {
            var poly = new PointF[pts.Count];
            for (int i = 0; i < pts.Count; i++) poly[i] = T(pts[i][0], pts[i][1] + decalageY);
            using var b = new SolidBrush(fond);
            using var pn = new Pen(bord, 1.3f);
            g.FillPolygon(b, poly);
            g.DrawPolygon(pn, poly);
        }

        void HauteurLibre(Graphics g, double h)
        {
            using var pn = new Pen(Color.FromArgb(90, 98, 110), 1f) { DashStyle = DashStyle.Dash };
            using var f = new Font("Segoe UI", 8);
            var a = T(-1000, h);
            g.DrawLine(pn, 0, a.Y, Width, a.Y);
            g.DrawString($"hauteur libre {h:0}", f, new SolidBrush(Theme.Discret), 14, a.Y - 16);
        }

        void DessinerTole(Graphics g, double ep, double assise)
        {
            var chaine = new List<Pt>();
            foreach (var p in etat.PanArriere) chaine.Add(new Pt(p.X, p.Y + assise));
            for (int i = 1; i < etat.Formage.Count; i++)
                chaine.Add(new Pt(etat.Formage[i].X, etat.Formage[i].Y + assise));
            if (chaine.Count < 2) return;

            Color col = etat.Bloque ? Theme.Alerte
                      : etat.Op.Reprise ? Theme.Reprise : Theme.Tole;

            // épaisseur réelle en fond (souvent fine), trait porteur par-dessus
            var ext = Offset(chaine, +ep / 2);
            var inte = Offset(chaine, -ep / 2);
            var ruban = new List<PointF>(ext.Count + inte.Count);
            foreach (var p in ext) ruban.Add(T(p));
            for (int i = inte.Count - 1; i >= 0; i--) ruban.Add(T(inte[i]));
            using (var b = new SolidBrush(Color.FromArgb(150, col)))
                g.FillPolygon(b, ruban.ToArray());

            var ligne = new PointF[chaine.Count];
            for (int i = 0; i < chaine.Count; i++) ligne[i] = T(chaine[i]);
            using (var pn = new Pen(col, 3f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawLines(pn, ligne);

            using (var b = new SolidBrush(col))
            {
                foreach (var p in ligne) g.FillEllipse(b, p.X - 2.4f, p.Y - 2.4f, 4.8f, 4.8f);
                var o = T(0, assise);
                g.FillEllipse(b, o.X - 4, o.Y - 4, 8, 8);
            }
        }

        /// <summary>Offset d'une polyligne, jointures mitrées (plis francs, sans rayon).</summary>
        static List<Pt> Offset(List<Pt> pts, double d)
        {
            var res = new List<Pt>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
            {
                Pt p = pts[i];
                Pt n0 = default, n1 = default;
                bool a = i > 0, b = i < pts.Count - 1;

                if (a) { var t = Unit(p.X - pts[i - 1].X, p.Y - pts[i - 1].Y); n0 = new Pt(t.Y, -t.X); }
                if (b) { var t = Unit(pts[i + 1].X - p.X, pts[i + 1].Y - p.Y); n1 = new Pt(t.Y, -t.X); }

                Pt m;
                if (!a) m = n1;
                else if (!b) m = n0;
                else
                {
                    var mm = Unit(n0.X + n1.X, n0.Y + n1.Y);
                    double c = Math.Max(0.25, mm.X * n0.X + mm.Y * n0.Y);  // anti-pic sur plis serrés
                    m = new Pt(mm.X / c, mm.Y / c);
                }
                res.Add(new Pt(p.X + m.X * d, p.Y + m.Y * d));
            }
            return res;
        }

        static Pt Unit(double x, double y)
        {
            double m = Math.Sqrt(x * x + y * y);
            return m > 1e-9 ? new Pt(x / m, y / m) : new Pt(0, 0);
        }

        void Grille(Graphics g)
        {
            using var pn = new Pen(Theme.Grille, 1f);
            for (int x = 0; x < Width; x += 40) g.DrawLine(pn, x, 0, x, Height);
            for (int y = 0; y < Height; y += 40) g.DrawLine(pn, 0, y, Width, y);
        }

        void Legende(Graphics g)
        {
            using var f = new Font("Segoe UI", 8.5f);
            g.DrawString("bleu = tôle · vert = reprise · rouge = collision outillage",
                f, new SolidBrush(Theme.Discret), 12, Height - 22);
        }

        void Centre(Graphics g, string t)
        {
            using var f = new Font("Segoe UI", 11);
            var sz = g.MeasureString(t, f);
            g.DrawString(t, f, new SolidBrush(Theme.Discret), (Width - sz.Width) / 2, (Height - sz.Height) / 2);
        }
    }
}
